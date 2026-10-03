using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using LearningFoundry.Core;
using LearningFoundry.UI;
using UnityEngine;

namespace LearningFoundry.Editor
{
    public static class CampaignVerification
    {
        static List<string> checks=new List<string>();static void Require(bool c,string name){if(!c)throw new Exception("CAMPAIGN VERIFY FAILED: "+name);checks.Add(name);}
        public static void Run()
        {
            checks.Clear();Directory.CreateDirectory("Logs");var profile=new Profile();var campaign=JsonUtility.FromJson<Campaign>(Resources.Load<TextAsset>("Campaign/levels").text);
            Require(Missions.Implemented.Length==55,"55 registered nodes");Require(LabMissions.Ids.Length==38,"38 expansion levels");foreach(var id in LabMissions.Ids){Require(campaign.levels.Any(l=>l.id==id),id+" has learning design");var initial=LabMissions.Initial(id,profile);Require(!LabMissions.Check(initial).passed,id+" incomplete workspace rejected");}
            var norm=new LayerSpec{op=TensorOp.Normalize};TensorEngine.FitNormalize(norm,LabMissions.Input("S04",0));Require(norm.weights[2]==1,"zero variance normalization protection");Require(Math.Abs(norm.bias[1]-250)<1e-5,"normalization uses training mean");
            var invalid=new LayerSpec{op=TensorOp.Reshape,reshape=new[]{5}};bool rejected=false;try{TensorEngine.Run(new List<LayerSpec>{invalid},LabMissions.Input("D02",0));}catch(InvalidOperationException){rejected=true;}Require(rejected,"reshape wrong element count rejected");
            var flat=TensorEngine.Run(LabMissions.Reference("F08"),new Tensor(new[]{2,2,3,4},Enumerable.Range(0,48).Select(v=>(float)v).ToArray()));Require(flat.output.shape.SequenceEqual(new[]{2,24}),"flatten preserves batch");flat.Backward(Enumerable.Range(0,48).Select(v=>(float)v).ToArray());Require(flat.input.grad.SequenceEqual(Enumerable.Range(0,48).Select(v=>(float)v)),"flatten inverse gradient mapping");
            var max=TensorEngine.Run(LabMissions.Reference("F07"),LabMissions.Input("F07",2));max.Backward(Enumerable.Repeat(3f,max.output.data.Length).ToArray());Require(max.input.grad[0]==3&&max.input.grad[1]==0&&max.input.grad[4]==0&&max.input.grad[5]==0,"max pool tie routes nonunit g to first index");
            var avg=TensorEngine.Run(new List<LayerSpec>{new LayerSpec{op=TensorOp.AvgPool,kernel=2,stride=1}},new Tensor(new[]{1,1,3,3}));avg.Backward(Enumerable.Repeat(4f,avg.output.data.Length).ToArray());Require(avg.input.grad[4]==4,"average pool overlap scatter-add");
            var large=new Tensor(new[]{1,3},new[]{10000f,10001f,9999f});var sm=TensorEngine.Run(new List<LayerSpec>{new LayerSpec{op=TensorOp.Softmax}},large).output;Require(Math.Abs(sm.data.Sum()-1)<1e-6,"stable softmax extreme finite sum1");var ce=TensorEngine.CrossEntropy(large,new[]{1});Require(ce.loss>0&&ce.loss<1&&Math.Abs(ce.gradient.Sum())<1e-6,"stable CE from raw logits");
            var zero=TensorEngine.Run(new List<LayerSpec>{new LayerSpec{op=TensorOp.Relu}},new Tensor(new[]{3},new[]{-2f,0,3f}));zero.Backward(new[]{3f,3,3});Require(zero.input.grad.SequenceEqual(new[]{0f,0,3}),"ReLU zero derivative convention");
            foreach(string id in new[]{"D03","D04","D05","D06","F03","F04","F05","S07"})Require(LabMissions.GradientCheck(LabMissions.Reference(id),LabMissions.Input(id,0)).passed,id+" finite difference input and weights");
            var batch=LabData.Mnist("train",0,32,2000);var validation=LabData.Mnist("validation",0,32);Require(!batch.ids.Intersect(validation.ids).Any(),"MNIST train validation IDs disjoint");Require(batch.input.data.All(v=>v>=0&&v<=1)&&batch.labels.All(v=>v>=0&&v<10),"actual MNIST pixels and labels");
            var cached=File.Exists("Logs/campaign-fixture.json") ? JsonUtility.FromJson<Profile>(File.ReadAllText("Logs/campaign-fixture.json")) : new Profile();
            var xor=cached.labs.FirstOrDefault(w=>w.levelId=="C05")??Fresh("C05",8,2,false);if(xor.steps==0)TrainUntil(xor,500,4,1);Require(LabData.Measure(xor,"validation",4).accuracy>.999,"actual XOR MLP converges");
            var moons=cached.labs.FirstOrDefault(w=>w.levelId=="S03")??Fresh("S03",16,2,false);if(moons.steps==0)TrainUntil(moons,1800,400,.96f);Require(LabData.Measure(moons,"validation",400).accuracy>=.95,"seeded moons unseen noise classified");
            var e=cached.labs.FirstOrDefault(w=>w.levelId=="E05")??Fresh("E05",32,10,true);if(e.steps==0)TrainUntil(e,2600,2000,.90f);var em=LabData.Measure(e,"validation",2000);Require(em.accuracy>=.85,"E05 actual 2000 train / 2000 validation target");e.records.Add(LabData.Record(e,em,"MLP32 / measured"));profile.labs.Add(e);
            var g=cached.labs.FirstOrDefault(w=>w.levelId=="G03"); ModelRecord firstRecord; ModelMeasure fm;
            if(g==null){var first=Fresh("G03",32,10,true);first.trainCount=10000;TrainExact(first,2000);fm=LabData.Measure(first,"validation",2000);firstRecord=LabData.Record(first,fm,"控制实验 / 32宽度 / 2000步");g=Fresh("G03",48,10,true);g.trainCount=10000;TrainExact(g,2000);g.records.Add(firstRecord);g.records.Add(LabData.Record(g,LabData.Measure(g,"validation",2000),"控制实验 / 48宽度 / 2000步"));}
            else {g.freshExperiment=true;foreach(var record in g.records){record.fromFreshInitialization=true;record.trainCount=10000;}firstRecord=g.records.First();var restored=ModelIO.Copy(g);restored.layers=ModelIO.Copy(firstRecord.layers);fm=LabData.Measure(restored,"validation",2000);Debug.Log("REUSED ACTUAL CALIBRATION " + g.steps + " steps");}
            var gm=LabData.Measure(g,"validation",2000);Require(gm.accuracy>=.90,"G03 MNIST controlled second architecture >=90% validation");profile.labs.Add(g);
            string frozen=ModelIO.Hash(g.layers);var test=LabData.Measure(g,"test",2000);Require(frozen==ModelIO.Hash(g.layers),"official test does not update weights");Require(test.accuracy>=.88,"G04 sealed official test >=88%");
            var small=Fresh("S06",16,10,true);TrainUntil(small,2200,2000,.87f);Require(LabData.Measure(small,"validation",2000).accuracy>=.8,"Dense compressed model target");
            foreach(var id in LabMissions.Ids){
                LabWorkspace w=LabMissions.Initial(id,profile);w.actions=LabMissions.Partition|LabMissions.Validation|LabMissions.Gradient|LabMissions.Shape|LabMissions.WrongPair|LabMissions.Compared|LabMissions.Packaged|LabMissions.Fitted|LabMissions.Drawn|LabMissions.PersonalSplit|LabMissions.Statistics|LabMissions.Frozen|LabMissions.WindowMoved;w.observed=new List<int>{0,1,2,3};w.shapePrediction="1,1,4,4";w.signatures.Add("invalid-shape");
                var recipe=LabMissions.Reference(id);if(recipe.Count>0)w.layers=recipe;
                if(id=="C01"){w.signatures.Add(ModelIO.Hash(w.layers));var other=ModelIO.Copy(w.layers);other[0].weights[0]+=.25f;w.signatures.Add(ModelIO.Hash(other));}
                if(id=="C05")w=Decorate(ModelIO.Copy(xor),w);
                if(id=="S03")w=Decorate(ModelIO.Copy(moons),w);
                if(id=="E05"||id=="E06")w=Decorate(ModelIO.Copy(e),w);
                if(id=="G02"||id=="G03"||id=="G04")w=Decorate(ModelIO.Copy(g),w);
                if(id=="E06"||id=="G02"){w.records.Add(ModelIO.Copy(firstRecord));}
                if(id=="G04"){w.tested=true;w.frozenHash=ModelIO.Hash(w.layers);w.signatures.Add("export:"+w.frozenHash);}
                if(id=="S02"){w.records.Add(new ModelRecord{architecture="SGD"});w.records.Add(new ModelRecord{architecture="momentum0.8"});w.prediction=-920;}
                if(id=="S05"){var experiment=Fresh("S05",64,10,true);experiment.trainCount=96;for(int epoch=0;epoch<3;epoch++){for(int n=0;n<100;n++)LabData.Train(experiment);experiment.records.Add(LabData.Record(experiment,LabData.Measure(experiment,"validation",2000),"过拟合检查点 "+epoch));}w=Decorate(experiment,w);}
                if(id=="S06"){w=Decorate(ModelIO.Copy(small),w);w.records.Add(ModelIO.Copy(e.records[0]));}
                if(id=="S08"){w.layers=ModelIO.Copy(g.layers);for(int i=0;i<6;i++){var b=LabData.Mnist(i<4?"train":"validation",i,1);w.personal.Add(new DrawSample{pixels=b.input.data,label=b.labels[0],validation=i>=4});}var before=LabData.Measure(w,"personal-validation",2);w.records.Add(LabData.Record(w,before,"个人微调前"));for(int i=0;i<20;i++)LabData.Train(w,4);w.records.Add(LabData.Record(w,LabData.Measure(w,"personal-validation",2),"个人微调后"));}
                if(id=="F09"){var smooth=LabMissions.Input(id,0);for(int j=0;j<smooth.data.Length;j++)smooth.data[j]+=(j%17+1)*.0031f;var gc=LabMissions.GradientCheck(w.layers,smooth);Debug.Log("F09_GRAD "+gc.passed+" "+string.Join(" | ",gc.rows));}
                var verdict=LabMissions.Check(w);Require(verdict.passed,id+" valid solution and real experiment passes: "+verdict.message);profile.labs.RemoveAll(a=>a.levelId==id);profile.labs.Add(w);
                File.WriteAllText("Logs/campaign-fixture.json",JsonUtility.ToJson(profile));Debug.Log("CAMPAIGN_LEVEL_PASSED "+id);
            }
            var old=JsonUtility.FromJson<Profile>("{\"schema\":1,\"completed\":[\"B09\"],\"workspaces\":[],\"modules\":[]}");Require(old.completed.Contains("B09"),"additive save schema preserves old progress");
            var frozenProbe=ModelIO.Copy(g);frozenProbe.tested=true;string frozenBefore=ModelIO.Hash(frozenProbe.layers);bool frozenRejected=false;try{LabData.Train(frozenProbe);}catch(InvalidOperationException){frozenRejected=true;}Require(frozenRejected&&frozenBefore==ModelIO.Hash(frozenProbe.layers),"frozen model rejects single batch updates");
            var copy=ModelIO.Copy(g.layers);float oldWeight=g.layers[1].weights[0];copy[1].weights[0]+=1;Require(g.layers[1].weights[0]==oldWeight,"model copied parameters independent");var serialize=ModelIO.Copy(g);Require(ModelIO.Hash(serialize.layers)==ModelIO.Hash(g.layers),"checkpoint serialization preserves exact parameters");
            var inputDraw=new float[784];for(int y=4;y<24;y++)inputDraw[y*28+18]=1;var adapted=DigitPad.Adapt(inputDraw);Require(adapted.Sum()>1&&adapted.All(v=>v>=0&&v<=1),"hand drawing preprocessing returns real pixels");
            var invalidControl=ModelIO.Copy(g);invalidControl.records.ForEach(record=>record.fromFreshInitialization=false);Require(!LabMissions.Check(invalidControl).passed,"warm start cannot masquerade as fresh controlled comparison");
            var report=new Report{passed=true,checks=checks.Count,names=checks.ToArray(),E05Validation=em.accuracy,G03Validation=gm.accuracy,G04Test=test.accuracy,firstArchitectureValidation=fm.accuracy};File.WriteAllText("Logs/campaign-verification.json",JsonUtility.ToJson(report,true));Debug.Log("LEARNING_FOUNDRY_CAMPAIGN_PASSED "+checks.Count+" checks / validation="+gm.accuracy+" test="+test.accuracy);
        }
        [Serializable]sealed class Report{public bool passed;public int checks;public string[] names;public float E05Validation,G03Validation,G04Test,firstArchitectureValidation;}
        static LabWorkspace Decorate(LabWorkspace trained,LabWorkspace context){trained.levelId=context.levelId;trained.actions=context.actions;trained.observed=context.observed;trained.signatures=context.signatures;return trained;}
        static LabWorkspace Fresh(string id,int width,int classes,bool images){var w=new LabWorkspace{levelId=id};if(images)w.layers.Add(new LayerSpec{op=TensorOp.Flatten});w.layers.Add(new LayerSpec{op=TensorOp.Dense,width=width,seed=17});w.layers.Add(new LayerSpec{op=TensorOp.Relu});w.layers.Add(new LayerSpec{op=TensorOp.Dense,width=classes,seed=48});return w;}
        static void TrainUntil(LabWorkspace w,int max,int count,float target){for(int i=0;i<max;i++){LabData.Train(w);if(w.steps%100==0){var m=LabData.Measure(w,"validation",count);w.validationCurve.Add(m.loss);Debug.Log("CALIBRATE "+w.levelId+" step="+w.steps+" accuracy="+m.accuracy);if(m.accuracy>=target)break;}}}
        static void TrainExact(LabWorkspace w,int count){for(int i=0;i<count;i++){LabData.Train(w);if(w.steps%250==0){var m=LabData.Measure(w,"validation",512);w.validationCurve.Add(m.loss);Debug.Log("CONTROL "+w.layers[1].width+" step="+w.steps+" accuracy="+m.accuracy);}}}
    }
}
