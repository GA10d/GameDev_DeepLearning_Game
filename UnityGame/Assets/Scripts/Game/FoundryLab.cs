using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Globalization;
using LearningFoundry.Core;
using LearningFoundry.UI;
using UnityEngine;
using UnityEngine.UI;

namespace LearningFoundry.Game
{
    public sealed partial class FoundryGame
    {
        LabWorkspace lab;bool labRunning,labBackward;int labSelection=-1,labSample,labChannel;TensorRun labRun;ModelMeasure labMeasure;
        RectTransform labParts,labCabinet,labReadouts,labTask,labConsole;Text labSummary,labProbe;TracePlot labTrainPlot,labValPlot;
        readonly List<Text> labNodeReadouts=new List<Text>();readonly List<string> labUndo=new List<string>();string labReport="";
        void OpenLab(Level target)
        {
            level=target;profile.currentLevel=target.id;graph=null;averageTab=false;ResetGuideSession();mapChapter=level.chapter;
            if(profile.labs==null)profile.labs=new List<LabWorkspace>();lab=profile.labs.Find(w=>w.levelId==level.id);
            if(lab==null){lab=LabMissions.Initial(level.id,profile);profile.labs.Add(lab);}labSelection=-1;labSample=0;labChannel=0;labRun=null;labMeasure=null;labBackward=false;labUndo.Clear();labReport="";
            BuildLab();Save();
        }
        void BuildLab()
        {
            BaseScreen();Header(level.id+"   "+level.name,"知识点 · "+level.concept);
            labParts=Hardware.Plate(screen,"Parts Drawer",24,113,240,499,Hardware.Metal).rectTransform;
            Style.Number(labParts,"PARTS / 备件抽屉",16,13,212,28,16);
            int i=0;foreach(var op in LabMissions.Palette(level.id)){var item=op;Style.Button(labParts,LabMissions.Name(item)+"  +",16,54+i++*47,208,38,()=>LabAdd(item));}
            Style.Text(labParts,"按顺序安装，线路自动接通。\n选模块可调参数、换序和拆除。",16,417,210,71,16,Style.Muted);
            Hardware.Plate(screen,"Tensor Bench",280,113,970,487,Hardware.Metal);
            var scroll=Style.Scroll(screen,292,125,946,463,2400,1600,out boardRoot);boardViewport=(RectTransform)scroll.transform;scroll.movementType=ScrollRect.MovementType.Unrestricted;scroll.inertia=false;var bar=scroll.verticalScrollbar;scroll.verticalScrollbar=null;if(bar)bar.gameObject.SetActive(false);scroll.horizontal=true;scroll.vertical=true;scroll.horizontalNormalizedPosition=0;scroll.verticalNormalizedPosition=1;
            boardPan=scroll.gameObject.AddComponent<WorkbenchPan>();boardPan.scroll=scroll;boardPan.rightClick=()=>{labSelection=-1;LabInspector();};
            DrawLabBench();
            labCabinet=Hardware.Plate(screen,"Tensor Control Cabinet",1264,113,312,487,Hardware.Metal).rectTransform;LabInspector();
            labTask=Hardware.Plate(screen,"Repair Ticket",24,630,240,226,Style.Paper).rectTransform;
            Hardware.Shape(screen,"Ticket Clip",83,620,115,20,HardwareShape.Chip,Hardware.Metal);
            Style.Text(labTask,"当前委托",15,14,210,28,18,Style.Brass);
            Style.Scroll(labTask,12,50,216,164,204,Math.Max(164,LabMissions.Objective(level.id).Length/11*25+50),out var paper);
            // The cutting-mat scroll is covered with the same paper stock as the ticket.
            var bg=Style.Box(paper,"Ticket Paper",0,0,204,paper.rect.height,Style.Paper);bg.raycastTarget=false;
            Style.Text(paper,LabMissions.Objective(level.id),9,8,187,paper.rect.height-10,16);
            labReadouts=Hardware.Plate(screen,"Tensor Oscilloscope",280,612,970,244,Hardware.Metal).rectTransform;
            labConsole=Hardware.Plate(screen,"Lab Console",1264,612,312,244,Hardware.Metal).rectTransform;
            Style.Button(labConsole,"送入 / 下一样本",14,12,284,34,()=>{labSample++;LabObserve();});
            if(LabData.IsTrainable(level.id)){Style.Button(labConsole,"启动 / 暂停训练",14,56,176,34,()=>{labRunning=!labRunning;LabInspector();SetStatus(labRunning?"训练已启动，可随时暂停与保存检查点。":"训练已暂停。");});Style.Button(labConsole,"单步",202,56,96,34,()=>{try{labRunning=false;LabData.Train(lab);LabVisualize();Save();}catch(Exception e){SetStatus(e.Message);}});}
            Style.Button(labConsole,"验收设备",14,LabData.IsTrainable(level.id)?100:68,284,LabData.IsTrainable(level.id)?38:42,LabCheck,Style.Brass);
            Style.Button(labConsole,"记录 / 检查点",14,LabData.IsTrainable(level.id)?148:124,135,34,LabRecord);
            Style.Button(labConsole,"比较记录",161,LabData.IsTrainable(level.id)?148:124,137,34,LabCompare);
            Style.Button(labConsole,"撤销",14,LabData.IsTrainable(level.id)?197:180,86,34,LabUndo);
            Style.Button(labConsole,"读手册",112,LabData.IsTrainable(level.id)?197:180,86,34,Notebook);
            Style.Button(labConsole,"重置",210,LabData.IsTrainable(level.id)?197:180,88,34,()=>Modal("重新装配", "重置本关的线路和实验记录。已完成的委托保留。", "重置本关",()=>{profile.labs.Remove(lab);lab=LabMissions.Initial(level.id,profile);profile.labs.Add(lab);labSelection=-1;labRun=null;BuildLab();Save();}));
            status=Style.Text(screen,"右键按住拖动画布；选模块调参。新部件说明在控制柜与 F1 手册中。",27,876,1546,25,14,Style.Paper);
            LabVisualize();
        }
        void LabBefore(){labRunning=false;labUndo.Add(JsonUtility.ToJson(lab));if(labUndo.Count>24)labUndo.RemoveAt(0);}
        void LabChanged(){if(LabData.IsClassifier(level.id))lab.freshExperiment=false;labRun=null;labMeasure=null;lab.tested=false;labBackward=false;lab.steps=lab.cursor=0;lab.trainCurve.Clear();lab.validationCurve.Clear();DrawLabBench();LabInspector();LabVisualize();Save();}
        void LabUndo(){if(labUndo.Count==0){SetStatus("没有可撤销的改动。");return;}labRunning=false;var index=profile.labs.IndexOf(lab);lab=JsonUtility.FromJson<LabWorkspace>(labUndo.Last());labUndo.RemoveAt(labUndo.Count-1);profile.labs[index]=lab;labSelection=-1;labRun=null;BuildLab();Save();SetStatus("已撤销上一次装配改动。");}
        void LabAdd(TensorOp op)
        {
            try{if(lab.layers.Count>=20){SetStatus("备件预算20个。可封装视觉积木再继续。");return;}LabBefore();var l=new LayerSpec{op=op,title=LabMissions.Name(op),seed=lab.seed+lab.layers.Count*31};
                if(op==TensorOp.Conv2D||op==TensorOp.Window||op==TensorOp.Scan)l.width=1;
                if(op==TensorOp.MaxPool||op==TensorOp.AvgPool){l.kernel=2;l.stride=2;l.width=1;}
                if(op==TensorOp.Window||op==TensorOp.Scan){l.weights=new[]{-1f,0,1,-1f,0,1,-1f,0,1};l.bias=new float[1];l.learnable=false;}
                if(op==TensorOp.GateBackward)l.width=3;
                if(op==TensorOp.Vision){var template=profile.labs.Find(w=>w.levelId=="F09")?.layers.FirstOrDefault(x=>x.op==TensorOp.Vision);if(template==null){SetStatus("先在F09封装视觉积木。");return;}l=ModelIO.Copy(template);l.id=Guid.NewGuid().ToString("N");}
                lab.layers.Add(l);labSelection=lab.layers.Count-1;LabChanged();SetStatus("已安装 "+LabMissions.Name(op)+"。点击‘送入样本’检查线路。");
            }catch(Exception e){SetStatus(e.Message);}
        }
        void DrawLabBench()
        {
            if(!boardRoot)return;foreach(Transform child in boardRoot)Destroy(child.gameObject);labNodeReadouts.Clear();
            var wiring=Style.Rect(boardRoot,"Tensor Cables",0,0,2400,1600).gameObject.AddComponent<Lines>();wiring.cables=true;wiring.color=new Color(.65f,.62f,.37f);wiring.raycastTarget=false;
            var source=Hardware.Plate(boardRoot,"Source Cartridge",35,48,199,152,Hardware.Enamel);Style.Number(source.transform,LabData.IsClassifier(level.id)?"DATA / 数据抽屉":"SOURCE / 实验输入",12,11,175,26,13);Style.Text(source.transform,LabData.IsClassifier(level.id)?"样本 → 张量":"给定输入",13,47,171,32,21);Style.Text(source.transform,"0 → 1 → 2 …\n逐项真实读数",13,91,174,48,16,Style.Muted);
            Vector2 previous=new Vector2(234,-124);
            for(int n=0;n<lab.layers.Count;n++){
                int col=(n+1)%4,row=(n+1)/4;float x=35+col*222,y=48+row*211;var l=lab.layers[n];int index=n;
                var plate=Hardware.Plate(boardRoot,l.id,x,y,199,152,labSelection==n?new Color(.82f,.81f,.67f):Hardware.Enamel);Style.Number(plate.transform,"MODULE / "+(n+1).ToString("00"),12,10,171,25,12);
                Style.Text(plate.transform,LabMissions.Name(l.op),13,43,175,35,21);Style.Text(plate.transform,l.op==TensorOp.Dense?"宽度 "+l.width:l.op==TensorOp.Conv2D?l.width+" CH  /  "+l.kernel+"×"+l.kernel:l.op==TensorOp.Vision?"独立参数 / 可展开":""+l.op,13,84,170,27,14,Style.Muted);
                var readout=Style.Box(plate.transform,"Shape Readout",11,113,176,27,Hardware.Dark);labNodeReadouts.Add(Style.Number(readout.transform,"待测量",5,1,168,25,12,Hardware.Phosphor));plate.gameObject.AddComponent<Button>().onClick.AddListener(()=>{labSelection=index;labChannel=0;LabInspector();LabVisualize();});
                Hardware.Shape(plate.transform,"IN",-10,62,23,23,HardwareShape.Socket,Style.Brass);Hardware.Shape(plate.transform,"OUT",185,62,23,23,HardwareShape.Socket,Hardware.Phosphor);
                Vector2 next=new Vector2(x,-y-74);wiring.paths.Add(new[]{previous,previous+new Vector2(20,0),new Vector2(next.x-20,previous.y),next-new Vector2(20,0),next});previous=new Vector2(x+199,-y-74);
            }
            float ox=35+((lab.layers.Count+1)%4)*222,oy=48+((lab.layers.Count+1)/4)*211;var terminal=Hardware.Plate(boardRoot,"Readout Terminal",ox,oy,199,152,Hardware.Metal);Style.Number(terminal.transform,"READOUT / 输出",12,11,175,26,13);Style.Text(terminal.transform,"测量终点",13,47,171,34,21);Style.Text(terminal.transform,"选择模块检查中间值\n以及回流梯度",13,91,174,48,16,Style.Muted);Vector2 end=new Vector2(ox,-oy-74);wiring.paths.Add(new[]{previous,previous+new Vector2(17,0),new Vector2(end.x-17,previous.y),end-new Vector2(17,0),end});wiring.Refresh();
        }
        void LabInspector()
        {
            if(!labCabinet)return;foreach(Transform child in labCabinet)Destroy(child.gameObject);Style.Number(labCabinet,labSelection<0?"EXPERIMENT / 实验控制":"CALIBRATION / 模块校准",16,13,279,28,16);
            Style.Scroll(labCabinet,12,49,288,422,275,950,out var content);float y=8;var back=Style.Box(content,"Cabinet Interior",0,0,275,950,Hardware.Metal);back.raycastTarget=false;
            if(labSelection>=0&&labSelection<lab.layers.Count){var l=lab.layers[labSelection];LabText(content,LabMissions.Name(l.op),ref y,24,43);LabText(content,LabMissions.Lesson(l.op),ref y,17,160);
                if(l.op==TensorOp.Dense||l.op==TensorOp.Conv2D||l.op==TensorOp.MatMul||l.op==TensorOp.GateBackward){LabText(content,l.op==TensorOp.GateBackward?"上游 g（整数）":"输出宽度 / 通道",ref y);LabField(content,l.width.ToString(),ref y,s=>{if(int.TryParse(s,out int n)&&n>=-10&&n<=128&&n!=0){LabBefore();l.width=n;if(l.op!=TensorOp.GateBackward)l.Reset();LabChanged();}});}
                if(l.op==TensorOp.Conv2D||l.op==TensorOp.MaxPool||l.op==TensorOp.AvgPool){LabText(content,"核 / 窗口",ref y);LabField(content,l.kernel.ToString(),ref y,s=>LabInteger(l,s,"k"));LabText(content,"步幅",ref y);LabField(content,l.stride.ToString(),ref y,s=>LabInteger(l,s,"s"));if(l.op==TensorOp.Conv2D){LabText(content,"零填充",ref y);LabField(content,l.padding.ToString(),ref y,s=>LabInteger(l,s,"p"));}}
                if(l.op==TensorOp.Reshape){LabText(content,"目标形状（逗号分隔）",ref y);LabField(content,string.Join(",",l.reshape),ref y,s=>{try{var shape=s.Split(',').Select(int.Parse).ToArray();Tensor.Size(shape);LabBefore();l.reshape=shape;LabChanged();}catch(Exception e){SetStatus(e.Message);}});}
                if(l.op==TensorOp.Multiply||l.op==TensorOp.Dense||l.op==TensorOp.MatMul||l.op==TensorOp.Conv2D){LabText(content,"权重：按输出行存放",ref y);LabField(content,l.weights==null?"送样后初始化":string.Join(",",l.weights.Take(48).Select(v=>v.ToString("0.###",CultureInfo.InvariantCulture))),ref y,s=>LabValues(l,s,false));if(l.op==TensorOp.Dense||l.op==TensorOp.Conv2D){LabText(content,"偏置",ref y);LabField(content,l.bias==null?"0":string.Join(",",l.bias.Select(v=>v.ToString("0.###",CultureInfo.InvariantCulture))),ref y,s=>LabValues(l,s,true));}LabText(content,l.weights!=null&&l.weights.Length>48?"当前 "+l.weights.Length+" 项，仅显示前48项。整组随机重置可重新初始化。":"",ref y,14,45);}
                if(l.op==TensorOp.Vision){LabText(content,string.Join("\n",l.children.Select(c=>LabMissions.Name(c.op))),ref y,16,100);Style.Button(content,"展开积木",8,y,255,37,()=>{LabBefore();lab.layers.RemoveAt(labSelection);lab.layers.InsertRange(labSelection,ModelIO.Copy(l.children));LabChanged();});y+=49;}
                int selectedIndex=labSelection;Style.Button(content,"上移",8,y,78,37,()=>LabMove(-1));Style.Button(content,"下移",96,y,78,37,()=>LabMove(1));Style.Button(content,"拆除",185,y,78,37,()=>{LabBefore();lab.layers.RemoveAt(selectedIndex);labSelection=-1;LabChanged();});y+=49;
                Style.Button(content,"整组重新初始化",8,y,255,37,()=>{LabBefore();l.seed+=97;l.Reset();LabChanged();});y+=49;Style.Button(content,"实验控制",8,y,255,37,()=>{labSelection=-1;LabInspector();});y+=49;
            }else{
                LabText(content,"选择模块调参。每次编辑会暂停训练，重置当前步数；已有检查点仍保留。",ref y,17,94);
                Style.Button(content,"测量当前输入",8,y,255,36,LabObserve);y+=46;Style.Button(content,"查看回流 / 数值梯度",8,y,255,36,LabGradient);y+=48;
                if(LabData.IsClassifier(level.id)){LabText(content,"数据抽屉",ref y);foreach(var split in new[]{"训练","验证","密封测试"}){int p=Array.IndexOf(new[]{"训练","验证","密封测试"},split);Style.Button(content,(lab.partition==p?"● ":"")+split,8,y,255,36,()=>{if(p==2&&level.id!="G04"&&level.id!="S08"){SetStatus("独立测试抽屉封存到 G04，避免用测试结果调参。");return;}lab.partition=p;LabObserve();LabInspector();});y+=44;}LabText(content,"训练数量（32～20000）",ref y);LabField(content,lab.trainCount.ToString(),ref y,s=>{if(int.TryParse(s,out int n)&&n>=32&&n<=20000){LabBefore();lab.trainCount=n;LabChanged();}});}
                if(LabData.IsTrainable(level.id)){Style.Button(content,"重新开始控制实验",8,y,255,37,LabRestart);y+=48;LabText(content,"学习率 eta",ref y);LabField(content,lab.rate.ToString("0.###",CultureInfo.InvariantCulture),ref y,s=>{if(float.TryParse(s,NumberStyles.Float,CultureInfo.InvariantCulture,out float n)&&n>0&&n<=2){lab.rate=n;Save();}});Style.Button(content,labRunning?"暂停训练":"启动 / 继续训练",8,y,255,41,()=>{labRunning=!labRunning;LabInspector();});y+=51;Style.Button(content,"单步（一个小批量）",8,y,255,36,()=>{try{labRunning=false;LabData.Train(lab);LabVisualize();LabTrainingReadout(true);Save();}catch(Exception e){SetStatus(e.Message);}});y+=47;}
                if(level.id=="F05"){LabText(content,"先预测输出形状",ref y);LabField(content,lab.shapePrediction,ref y,s=>{lab.shapePrediction=s;lab.actions|=LabMissions.Shape;Save();});}
                if(level.id=="D05"||level.id=="F09"){Style.Button(content,"保存并复制：独立参数",8,y,255,37,LabCloneExperiment);y+=48;}
                if(level.id=="F09"){Style.Button(content,"封装整条视觉线路",8,y,255,37,LabPackage);y+=48;}
                if(level.id=="S04"){Style.Button(content,"仅拟合训练刻度",8,y,255,37,()=>{try{var norm=lab.layers.First(l=>l.op==TensorOp.Normalize);TensorEngine.FitNormalize(norm,LabMissions.Input("S04",0));lab.actions|=LabMissions.Fitted;LabObserve();Save();}catch(Exception e){SetStatus("先安装标准化模块。"+e.Message);}});y+=48;Style.Button(content,"沿用刻度测验证",8,y,255,37,()=>{lab.actions|=LabMissions.Validation;labSample=1;LabObserve();Save();});y+=48;}
                if(level.id=="S07"){Style.Button(content,"比较最大 / 平均池化",8,y,255,37,()=>{try{var x=LabMissions.Input("S07",0);var max=TensorEngine.Run(LabMissions.Reference("F06"),x.Copy());var avg=TensorEngine.Run(lab.layers,x.Copy());lab.actions|=LabMissions.Compared;labReport="最大池化："+string.Join(",",max.output.data)+"\n当前平均："+string.Join(",",avg.output.data);LabVisualize();Save();}catch(Exception e){SetStatus(e.Message);}});y+=48;}
                if(level.id=="S02"){Style.Button(content,"运行SGD轨迹",8,y,255,37,()=>LabMomentum(false));y+=48;Style.Button(content,"运行动量轨迹",8,y,255,37,()=>LabMomentum(true));y+=48;LabText(content,"第2步动量w（3位小数）",ref y);LabField(content,(lab.prediction/1000f).ToString("0.000"),ref y,s=>{if(float.TryParse(s,NumberStyles.Float,CultureInfo.InvariantCulture,out float n)){lab.prediction=(int)Math.Round(n*1000);Save();}});}
                if(level.id=="G04"||level.id=="S08"){Style.Button(content,"冻结并打开独立测试",8,y,255,39,LabFreeze);y+=50;Style.Button(content,"打开手写实验板",8,y,255,39,LabDrawing);y+=50;Style.Button(content,"导出冻结模型",8,y,255,39,LabExport);y+=50;}
            }
            content.sizeDelta=new Vector2(275,Math.Max(422,y+30));back.rectTransform.sizeDelta=content.sizeDelta;
        }
        void LabText(Transform p,string text,ref float y,int size=16,float h=31){Style.Text(p,text,9,y,254,h,size);y+=h+9;}
        void LabField(Transform p,string value,ref float y,Action<string> act){Style.Field(p,value,8,y,255,act);y+=48;}
        void LabInteger(LayerSpec l,string s,string which){if(!int.TryParse(s,out int n)||n<0||n>7){SetStatus("请输入0～7的整数（窗口和步幅需大于0）。");return;}LabBefore();if(which=="k"){l.kernel=n;l.Reset();}else if(which=="s")l.stride=n;else l.padding=n;LabChanged();}
        void LabValues(LayerSpec l,string text,bool bias){try{var values=text.Replace("，",",").Split(',').Select(s=>float.Parse(s.Trim(),CultureInfo.InvariantCulture)).ToArray();var old=bias?l.bias:l.weights;if(old==null){SetStatus("先送样初始化参数，再修改。");return;}if(values.Length!=old.Length){SetStatus("此处须输入完整的 "+old.Length+" 个数。大型层可重新初始化并训练。");return;}if(values.Any(v=>float.IsNaN(v)||float.IsInfinity(v)))throw new Exception("数值须有限。");LabBefore();if(bias)l.bias=values;else l.weights=values;LabChanged();}catch(Exception e){SetStatus("参数格式错误："+e.Message);}}
        void LabMove(int direction){int other=labSelection+direction;if(other<0||other>=lab.layers.Count)return;LabBefore();var item=lab.layers[labSelection];lab.layers.RemoveAt(labSelection);lab.layers.Insert(other,item);labSelection=other;LabChanged();}
        void LabObserve()
        {
            if(lab==null)return;labRun=null;try{
                if(LabData.IsClassifier(level.id)){if(lab.partition==2){if(!lab.tested||lab.frozenHash!=ModelIO.Hash(lab.layers)){SetStatus("先用冻结按钮验收独立测试。之后不再更新这份测试模型。");return;}}string split=lab.partition==0?"train":lab.partition==1?"validation":"test";
                    var b=LabData.Batch(lab,split,labSample,1);labRun=TensorEngine.Run(lab.layers,b.input);if(level.id=="C01"){lab.observed.Add(labSample%4);}else {lab.actions|=lab.partition==0?LabMissions.Partition:LabMissions.Validation;}
                    int predicted=TensorEngine.Argmax(labRun.output.data,0,labRun.output.data.Length);labReport=b.ids[0]+" / 真类别 "+b.labels[0]+" / 预测 "+predicted+"\n"+ModelIO.Signature(lab.layers);labMeasure=LabData.Measure(lab,split,level.id=="C01"||level.id=="C05"?4:level.id=="S03"?400:512);
                }else{labRun=TensorEngine.Run(lab.layers,LabMissions.Input(level.id,labSample),5);lab.observed.Add(labSample%3);labReport="输入 "+labRun.input.Shape+" → 输出 "+labRun.output.Shape+"\n"+string.Join("  ",labRun.output.data.Take(10).Select(v=>v.ToString("0.###")));}
                labBackward=false;UpdateLabNodeShapes();LabVisualize();Save();SetStatus("已测量。点击模块看中间张量，点击读数格子查看索引。");
            }catch(Exception e){if(e.Message.Contains("变形")&&e.Message.Contains("元素"))lab.signatures.Add("invalid-shape");LabVisualize();Save();SetStatus("线路未就绪："+e.Message);}
        }
        void LabGradient()
        {
            try{LabObserve();if(labRun==null)return;labRun.Backward(Enumerable.Repeat(3f,labRun.output.data.Length).ToArray());labBackward=true;lab.actions|=LabMissions.Gradient;
                var x=labRun.input.Copy();if(level.id=="F09")for(int j=0;j<x.data.Length;j++)x.data[j]+=(j%17+1)*.0031f;
                var check=LabMissions.GradientCheck(lab.layers,x,5);labReport=check.message+"\n"+string.Join("\n",check.rows.Take(5));LabVisualize();Save();SetStatus("显示回流：上游每项 g=3。"+check.message);
            }catch(Exception e){SetStatus("回流停止："+e.Message);}
        }
        void UpdateLabNodeShapes(){if(labRun==null)return;int cursor=0;for(int i=0;i<lab.layers.Count&&i<labNodeReadouts.Count;i++){var l=lab.layers[i];int n=l.op==TensorOp.Vision?l.children.Count:1;cursor+=n;if(cursor<=labRun.steps.Count)labNodeReadouts[i].text=labRun.steps[cursor-1].output.Shape;}}
        void TickLab()
        {
            if(lab==null||!labRunning||activeOverlay)return;try{if(lab.tested){labRunning=false;SetStatus("这份模型已冻结。恢复检查点或编辑结构后可创建新的训练实验。");return;}var clock=System.Diagnostics.Stopwatch.StartNew();int ticks=0;do{LabData.Train(lab);ticks++;}while(clock.ElapsedMilliseconds<8&&ticks<8);
                if(lab.steps%50<ticks){labMeasure=LabData.Measure(lab,level.id=="S08"?"personal-validation":"validation",level.id=="C05"?4:level.id=="S03"?400:level.id=="S08"?lab.personal.Count(d=>d.validation):512);lab.validationCurve.Add(labMeasure.loss);lab.bestAccuracy=Math.Max(lab.bestAccuracy,labMeasure.accuracy);LabTrainingReadout(true);Save();}else LabTrainingReadout(false);
            }catch(Exception e){labRunning=false;LabInspector();SetStatus("训练暂停："+e.Message);}
        }
        void LabTrainingReadout(bool full)
        {
            if(!labSummary){if(full)LabVisualize();return;}labSummary.text="CPU  /  "+lab.steps+" 更新  /  "+lab.cursor+" 件样本\n训练小批量 CE "+(lab.trainCurve.Count==0?"—":lab.trainCurve.Last().ToString("0.000"))+"\n验证 "+(labMeasure==null?"等待測量":labMeasure.accuracy.ToString("P1")+" / CE "+labMeasure.loss.ToString("0.000"));
            labTrainPlot?.Set(lab.trainCurve.Select(v=>(double)v).ToList());labValPlot?.Set(lab.validationCurve.Select(v=>(double)v).ToList());if(full)SetStatus("训练中可随时暂停、测量验证、保存检查点。GPU训练后端尚未提供，当前计算在本机CPU。");
        }
        void LabVisualize()
        {
            if(!labReadouts)return;foreach(Transform child in labReadouts)Destroy(child.gameObject);labSummary=null;labProbe=null;labTrainPlot=labValPlot=null;
            Style.Number(labReadouts,labBackward?"GRADIENT / 真实回流仪":"READOUT / 真实张量读数",14,9,598,28,15);
            if((LabData.IsTrainable(level.id)&&level.id!="E06"&&level.id!="C05"&&level.id!="S03"||level.id=="S02")&&lab.trainCurve.Count>0){labTrainPlot=new TracePlot(labReadouts,12,45,340,171){series="训练小批量 CE",domain="更新记录"};labValPlot=new TracePlot(labReadouts,363,45,340,171){series="固定验证 CE",domain="每50步采样"};labSummary=Style.Text(labReadouts,"",719,52,230,160,16,Style.Cream);LabTrainingReadout(false);Style.Button(labReadouts,"切到样本 / 特征图",714,199,240,31,()=>{var saved=lab.trainCurve;lab.trainCurve=new List<float>();LabVisualize();lab.trainCurve=saved;});return;}
            if(level.id=="C01"||level.id=="C05"||level.id=="S03"){LabBoundary();if(LabData.IsTrainable(level.id))labSummary=Style.Text(labReadouts,"CPU / "+lab.steps+" 步 / 小批量CE "+(lab.trainCurve.Count==0?"—":lab.trainCurve.Last().ToString("0.000")),210,221,741,23,14);return;}
            if(level.id=="E06"&&labMeasure!=null){LabConfusion();return;}
            if(labRun==null){Style.Text(labReadouts,level.id=="S02"?labReport:"设备等待输入。\n先安装部件，再送入样本。\n错误形状会在状态栏解释。",19,60,920,163,21);return;}
            Tensor tensor=labRun.output;if(labSelection>=0&&labSelection<lab.layers.Count){var spec=lab.layers[labSelection];var step=spec.op==TensorOp.Vision?labRun.steps.FirstOrDefault(s=>spec.children.Contains(s.layer)):labRun.steps.FirstOrDefault(s=>s.layer==spec);if(step!=null)tensor=step.output;}
            int cols=tensor.shape.Last(),plane=tensor.shape.Length>=3?tensor.shape[tensor.shape.Length-2]*cols:tensor.data.Length;cols=Math.Min(28,cols);plane=Math.Min(784,plane);int channels=Math.Max(1,tensor.data.Length/plane);labChannel=Math.Min(labChannel,channels-1);float[] values=labBackward?tensor.grad:tensor.data;
            var inputGrid=Style.Rect(labReadouts,"Input Display",15,47,174,174).gameObject.AddComponent<TensorGrid>();int inputCols=labRun.input.shape.Last();inputGrid.Set(labBackward?labRun.input.grad:labRun.input.data,Math.Min(28,inputCols),0,Math.Min(784,labRun.input.data.Length),labRun.input.shape.Length==4&&!labBackward);
            var grid=Style.Rect(labReadouts,"Selected Tensor Display",210,47,274,174).gameObject.AddComponent<TensorGrid>();grid.Set(values,cols,labChannel*plane,plane);
            var windowLayer=labSelection>=0?lab.layers[labSelection]:lab.layers.LastOrDefault();if(windowLayer!=null&&(windowLayer.op==TensorOp.Window||windowLayer.op==TensorOp.Scan||windowLayer.op==TensorOp.Conv2D)&&labRun.input.shape.Length==4){int row=lab.selectedCell/tensor.shape.Last(),col=lab.selectedCell%tensor.shape.Last(),iw=labRun.input.shape.Last(),ih=labRun.input.shape[2];var cells=new List<int>();for(int dy=0;dy<windowLayer.kernel;dy++)for(int dx=0;dx<windowLayer.kernel;dx++){int yy=row*windowLayer.stride-windowLayer.padding+dy,xx=col*windowLayer.stride-windowLayer.padding+dx;if(yy>=0&&xx>=0&&yy<ih&&xx<iw)cells.Add(yy*iw+xx);}inputGrid.highlighted=cells.ToArray();inputGrid.SetVerticesDirty();}
            labProbe=Style.Text(labReadouts,"点击格子读取对应索引。",501,49,450,38,15);
            grid.inspect=at=>{if(labProbe)labProbe.text="索引 "+at+" / 数值 "+values[at].ToString("0.0000")+" / "+tensor.Shape;};
            // Clicking a feature-map cell also identifies the original convolution window.
            grid.gameObject.AddComponent<Button>().onClick.AddListener(()=>{if(grid.selected<0)return;lab.selectedCell=grid.selected;lab.actions|=LabMissions.WindowMoved;var l=labSelection>=0?lab.layers[labSelection]:lab.layers.LastOrDefault();if(l!=null&&(l.op==TensorOp.Window||l.op==TensorOp.Scan||l.op==TensorOp.Conv2D)){int yy=grid.selected/(tensor.shape.Last()),xx=grid.selected%tensor.shape.Last();labReport="窗口左上：("+(yy*l.stride-l.padding)+","+(xx*l.stride-l.padding)+")\n核："+string.Join(",",l.weights.Take(9).Select(v=>v.ToString("0.##")));LabVisualize();}Save();});
            Style.Text(labReadouts,labReport,502,93,450,129,16);
            Style.Button(labReadouts,"通道 / 行 "+(labChannel+1)+"/"+channels,209,13,273,28,()=>{labChannel=(labChannel+1)%channels;LabVisualize();});
            if(labBackward&&labRun.steps.Any(s=>s.dw!=null)){var s=labRun.steps.First(t=>t.dw!=null);Style.Text(labReadouts,"参数梯度："+string.Join("  ",s.dw.Take(8).Select(v=>v.ToString("0.###"))),18,222,927,22,13);}
        }
        void LabBoundary()
        {
            try{int size=32;var x=new Tensor(new[]{size*size,2});for(int y=0;y<size;y++)for(int xx=0;xx<size;xx++){int i=y*size+xx;x.data[i*2]=-1.7f+3.7f*xx/(size-1);x.data[i*2+1]=-1.7f+3.7f*y/(size-1);}var r=TensorEngine.Run(lab.layers,x);if(r.output.shape.Length!=2||r.output.shape[1]!=2)throw new Exception("边界仪需两个输出分数。");var colors=new float[size*size];for(int i=0;i<colors.Length;i++)colors[i]=TensorEngine.Argmax(r.output.data,i*2,2)==0?-.5f:.5f;var image=Style.Rect(labReadouts,"Decision Boundary",15,45,174,174).gameObject.AddComponent<TensorGrid>();image.Set(colors,size);
                var b=level.id=="S03"?LabData.Moons(0,400,true):LabData.Xor(0,4);int correct=0;var p=TensorEngine.Run(lab.layers,b.input);for(int i=0;i<b.labels.Length;i++){int predicted=TensorEngine.Argmax(p.output.data,i*2,2);if(predicted==b.labels[i])correct++;float xx=15+(b.input.data[i*2]+1.7f)/3.7f*174,yy=45+(b.input.data[i*2+1]+1.7f)/3.7f*174;Hardware.Shape(labReadouts,"Point "+i,xx-3,yy-3,6,6,HardwareShape.Socket,predicted==b.labels[i]?Style.Paper:Style.Red);}
                Style.Text(labReadouts,"橙 / 绿：模型的两个预测区域\n白点正确，红点错误。\n实际验证："+correct+" / "+b.labels.Length,210,55,740,89,20);Style.Text(labReadouts,labReport,211,156,737,67,16);
            }catch(Exception e){Style.Text(labReadouts,"接通两个分类分数后显示决策区域。\n"+e.Message,20,60,920,160,21);}
        }
        void LabConfusion()
        {
            float[] cells=new float[100];for(int y=0;y<10;y++)for(int x=0;x<10;x++)cells[y*10+x]=labMeasure.confusion[y,x];var grid=Style.Rect(labReadouts,"Confusion Matrix",15,46,174,174).gameObject.AddComponent<TensorGrid>();grid.Set(cells,10);Style.Text(labReadouts,"行：真类别 / 列：预测类别\n选非对角格查看错例；读数来自固定验证抽屉。",210,46,740,69,19);labProbe=Style.Text(labReadouts,labReport,211,124,730,94,16);
            grid.inspect=at=>{int truth=at/10,pred=at%10;if(labProbe)labProbe.text="真 "+truth+" → 预测 "+pred+"："+labMeasure.confusion[truth,pred]+" 件";};grid.gameObject.AddComponent<Button>().onClick.AddListener(()=>{int at=grid.selected;if(at<0||at/10==at%10||cells[at]==0)return;lab.actions|=LabMissions.WrongPair;LabWrongGallery(at/10,at%10);Save();});
        }
        void LabWrongGallery(int truth,int prediction)
        {
            var overlay=Overlay();var panel=Hardware.Plate(overlay,"Wrong Samples",300,90,1000,714,Style.Paper);Style.Text(panel.transform,"错例检修 · "+truth+" → "+prediction,28,23,939,44,28);int shown=0;foreach(int index in labMeasure.wrong){var b=LabData.Batch(lab,"validation",index,1);if(b.labels[0]!=truth||labMeasure.predictions[index]!=prediction)continue;int col=shown%5,row=shown/5;var grid=Style.Rect(panel.transform,"Wrong "+index,32+col*188,96+row*217,148,148).gameObject.AddComponent<TensorGrid>();grid.Set(b.input.data,28,0,784,true);Style.Text(panel.transform,b.ids[0]+"\n真 "+truth+" / 预测 "+prediction,32+col*188,249+row*217,160,61,15);if(++shown>=10)break;}Style.Button(panel.transform,"回到混淆矩阵",680,638,290,46,CloseCurrentOverlay,Style.Brass);
        }
        void LabRecord()
        {
            try{labRunning=false;if(level.id=="S02"){LabMomentum(lab.momentum>0);return;}ModelMeasure m=LabData.IsClassifier(level.id)?LabData.Measure(lab,level.id=="S08"?"personal-validation":"validation",level.id=="C01"||level.id=="C05"?4:level.id=="S03"?400:level.id=="S08"?lab.personal.Count(s=>s.validation):2000):new ModelMeasure();if(!LabData.IsClassifier(level.id))TensorEngine.Run(lab.layers,LabMissions.Input(level.id,0),5);
                var record=LabData.Record(lab,m,"检查点 "+(lab.records.Count+1));lab.records.Add(record);if(lab.records.Count>12)lab.records.RemoveAt(0);lab.signatures.Add(ModelIO.Hash(lab.layers));labReport=record.name+" / "+record.steps+" 步 / "+record.accuracy.ToString("P1");labMeasure=m;LabInspector();LabVisualize();Save();SetStatus("检查点已保存：结构、参数、种子、数据、步数和曲线可恢复。");
            }catch(Exception e){SetStatus("记录失败："+e.Message);}
        }
        void LabCompare()
        {
            if(lab.records.Count<2){SetStatus("先保存至少两个实际检查点。");return;}labRunning=false;lab.actions|=LabMissions.Compared;var overlay=Overlay();var panel=Hardware.Plate(overlay,"Experiment Ledger",248,62,1104,777,Style.Paper);Style.Text(panel.transform,"实验记录 / 可恢复的模型",25,24,1050,41,28);Style.Scroll(panel.transform,23,91,1058,588,1043,lab.records.Count*139,out var content);float y=10;foreach(var item in lab.records){var record=item;var card=Hardware.Plate(content,"Record",10,y,1005,119,Hardware.Metal);Style.Text(card.transform,record.name+" · "+record.architecture,15,10,760,32,20);Style.Text(card.transform,record.steps+"步 / "+record.samples+"样本 / seed "+record.seed+" / eta "+record.rate+"\n验证 "+record.accuracy.ToString("P1")+" · CE "+record.loss.ToString("0.000")+" / "+record.dataset,15,46,775,59,16);Style.Button(card.transform,"恢复",816,30,168,51,()=>{CloseCurrentOverlay();LabBefore();lab.layers=ModelIO.Copy(record.layers);lab.steps=record.steps;lab.cursor=record.samples;lab.rate=record.rate;lab.seed=record.seed;lab.trainCount=record.trainCount>0?record.trainCount:lab.trainCount;lab.momentum=record.momentum;lab.freshExperiment=record.fromFreshInitialization;lab.trainCurve=new List<float>(record.trainCurve);lab.validationCurve=new List<float>(record.validationCurve);lab.tested=false;labRun=null;labMeasure=null;labSelection=-1;BuildLab();Save();SetStatus("已恢复检查点。参数与训练游标已恢复。");});y+=137;}Style.Button(panel.transform,"收起记录本",787,704,292,45,CloseCurrentOverlay,Style.Brass);Save();
        }
        void LabCloneExperiment()
        {
            try{TensorEngine.Run(lab.layers,LabMissions.Input(level.id,0));var copy=ModelIO.Copy(lab.layers);var original=ModelIO.Hash(lab.layers);var candidate=copy.SelectMany(l=>l.op==TensorOp.Vision?l.children:new List<LayerSpec>{l}).First(l=>l.weights!=null);candidate.weights[0]+=1;bool independent=original==ModelIO.Hash(lab.layers)&&ModelIO.Hash(copy)!=original;if(!independent)throw new Exception("副本参数独立性未通过。");lab.actions|=LabMissions.Compared;labReport="两份参数互不影响：副本权重[0] +1，原机指纹保持。\n保存、读取与副本检查通过。";LabVisualize();Save();SetStatus("已实际复制并扰动副本，原机参数保持独立。");}catch(Exception e){SetStatus(e.Message);}
        }
        void LabPackage()
        {try{if(lab.layers.Count!=3||lab.layers[0].op!=TensorOp.Conv2D||lab.layers[1].op!=TensorOp.Relu||lab.layers[2].op!=TensorOp.MaxPool)throw new Exception("按 Conv→ReLU→MaxPool 装配三个部件，再封装。");TensorEngine.Run(lab.layers,LabMissions.Input(level.id,0));LabBefore();lab.layers=new List<LayerSpec>{new LayerSpec{op=TensorOp.Vision,title="视觉积木",children=ModelIO.Copy(lab.layers)}};lab.actions|=LabMissions.Packaged;labSelection=-1;LabChanged();SetStatus("视觉线路已封装；后续可安装多个参数独立的积木。");}catch(Exception e){SetStatus(e.Message);}}
        void LabMomentum(bool enabled)
        {
            double w=-2,v=0;var curve=new List<float>();for(int i=0;i<40;i++){double g=w-2;v=(enabled?.8:0)*v+g;w-=.1*v;curve.Add((float)(.5*Math.Pow(w-2,2)));}lab.momentum=enabled?.8f:0;lab.trainCurve=curve;lab.records.Add(new ModelRecord{name=enabled?"动量0.8":"SGD",architecture=enabled?"momentum 0.8":"SGD",steps=40,rate=.1f,loss=curve.Last(),trainCurve=curve});labReport=(enabled?"动量":"SGD")+" 40步末端误差 "+curve.Last().ToString("0.000000");LabVisualize();Save();SetStatus("已计算并保存真实更新轨迹。第2步w请在控制柜填写。");
        }
        void LabRestart(){LabBefore();int i=0;foreach(var l in lab.layers){l.seed=lab.seed+i++*31;l.Reset();foreach(var c in l.children)c.seed=lab.seed+i++*31;}lab.steps=lab.cursor=0;lab.trainCurve.Clear();lab.validationCurve.Clear();lab.freshExperiment=true;lab.tested=false;labRun=null;labMeasure=null;LabVisualize();LabInspector();Save();SetStatus("已从当前种子重新初始化全部参数；原检查点仍可比较。记录相同步数的不同结构。");}
        void LabFreeze()
        {try{labRunning=false;lab.frozenHash=ModelIO.Hash(lab.layers);var measured=LabData.Measure(lab,"test",2000);labMeasure=measured;lab.tested=true;lab.actions|=LabMissions.Frozen;labReport="独立测试 2000件 / "+measured.accuracy.ToString("P1")+" / CE "+measured.loss.ToString("0.000")+"\n冻结指纹 "+lab.frozenHash.Substring(0,12);LabVisualize();LabInspector();Save();Modal("冻结验收报告",labReport+"\n\n封存的官方测试数据没有参与梯度更新。接下来可在手写板观察自己的笔迹。", "回到设备",CloseCurrentOverlay);}catch(Exception e){SetStatus("冻结验收失败："+e.Message);}}
        void LabDrawing()
        {
            labRunning=false;var overlay=Overlay();var page=Hardware.Plate(overlay,"Handwriting Bench",300,55,1000,788,Hardware.Metal);Style.Text(page.transform,"手写实验台 · 把你的笔迹送进机器",27,18,945,49,27);Style.Text(page.transform,"左键画数字。裁边→缩放到20像素→按重心居中，右边显示真实28×28输入。",28,79,937,46,18);
            var pad=Style.Rect(page.transform,"Drawing Pad",31,143,280,280).gameObject.AddComponent<DigitPad>();pad.pixels=lab.drawing;var adapted=Style.Rect(page.transform,"Adapted Input",347,143,280,280).gameObject.AddComponent<TensorGrid>();adapted.Set(DigitPad.Adapt(pad.pixels),28,0,784,true);var text=Style.Text(page.transform,"等待推理",661,145,307,274,21);pad.changed=()=>{adapted.Set(DigitPad.Adapt(pad.pixels),28,0,784,true);};
            Style.Button(page.transform,"清空笔迹",31,446,280,45,()=>{Array.Clear(pad.pixels,0,784);pad.SetVerticesDirty();pad.changed();Save();});
            Style.Button(page.transform,"推理这张图",347,446,280,45,()=>{try{var pixels=DigitPad.Adapt(pad.pixels);if(pixels.Sum()<1){text.text="先画一个数字。";return;}var r=TensorEngine.Run(lab.layers,new Tensor(new[]{1,1,28,28},pixels));if(r.output.data.Length!=10)throw new Exception("需要10个类别分数。");var probs=TensorEngine.Run(new List<LayerSpec>{new LayerSpec{op=TensorOp.Softmax}},r.output).output;int p=TensorEngine.Argmax(probs.data,0,10);text.text="预测数字  "+p+"\n\n"+string.Join("\n",Enumerable.Range(0,10).OrderByDescending(i=>probs.data[i]).Take(5).Select(i=>i+"  "+probs.data[i].ToString("P1")));lab.actions|=LabMissions.Drawn;Save();}catch(Exception e){text.text=e.Message;}});
            if(level.id=="S08"){Style.Text(page.transform,"真标签 0～9",31,522,166,32,18);Style.Field(page.transform,lab.prediction.ToString(),210,520,101,s=>{if(int.TryParse(s,out int n)&&n>=0&&n<10)lab.prediction=n;});Style.Button(page.transform,"保存个人训练件",347,520,280,45,()=>SaveDrawing(false));Style.Button(page.transform,"保存独立验证件",661,520,307,45,()=>SaveDrawing(true));Style.Text(page.transform,"训练 "+lab.personal.Count(s=>!s.validation)+" / 验证 "+lab.personal.Count(s=>s.validation)+"\n先记录原机个人验证，再微调≥20步并记录。",31,589,937,83,18);}
            else Style.Text(page.transform,"自己的手写笔迹与MNIST分布可能不同。\n这台机器的预测是实验结果；最终验收看独立数据集，不要求每张笔迹都正确。",31,532,937,121,20);
            Style.Button(page.transform,"回到工作台",661,701,307,49,()=>{CloseCurrentOverlay();Save();},Style.Brass);
        }
        void SaveDrawing(bool validation){var pixels=DigitPad.Adapt(lab.drawing);if(pixels.Sum()<1){SetStatus("请先画一个数字。");return;}if(lab.personal.Any(s=>s.pixels.SequenceEqual(pixels))){SetStatus("同一笔迹不能重复放入训练与验证。先清空并画新的一张。");return;}lab.personal.Add(new DrawSample{pixels=pixels,label=lab.prediction,validation=validation});lab.actions|=LabMissions.PersonalSplit;Save();CloseCurrentOverlay();LabDrawing();}
        void LabExport(){try{if(!lab.tested||lab.frozenHash!=ModelIO.Hash(lab.layers)){SetStatus("先冻结模型并完成独立测试。");return;}string exportRoot=Application.persistentDataPath;if(smoke){var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--capture-output");if(at>=0&&at+1<args.Length)exportRoot=args[at+1];}string path=Path.Combine(exportRoot,"exports","classifier-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+".json");Directory.CreateDirectory(Path.GetDirectoryName(path));var m=LabData.Record(lab,labMeasure??new ModelMeasure(),"冻结分类器 / MNIST pixels /255; handwritten crop20,center-of-mass");File.WriteAllText(path,JsonUtility.ToJson(m,true));lab.signatures.Add("export:"+lab.frozenHash);Save();Modal("模型已导出",path+"\n\n包含结构、参数、随机种子、数据划分与训练记录。", "返回实验台",CloseCurrentOverlay);}catch(Exception e){SetStatus("导出失败："+e.Message);}}
        void LabCheck(){labRunning=false;var result=LabMissions.Check(lab);labReport=result.message+"\n"+string.Join("\n",result.rows.Take(6));LabVisualize();LabInspector();if(!result.passed){SetStatus(result.message);Modal("检修报告",labReport,"回到装配",CloseCurrentOverlay);return;}if(!profile.completed.Contains(level.id))profile.completed.Add(level.id);Save();var next=campaign.levels.FirstOrDefault(l=>Unlocked(l)&&!profile.completed.Contains(l.id));Modal("委托完成 ✓",labReport+(level.id=="G04"?"\n\n从最小运算到你的手写分类器，主线路已经接通。可继续支线实验，或回到任意设备探索。":""),next==null?"回到委托板":"下一份委托 · "+next.id,()=>{if(next==null)Map();else OpenLevel(next.id);});}
    }
}
