using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace LearningFoundry.Core
{
    public sealed class DataBatch { public Tensor input; public int[] labels; public string[] ids; }
    public sealed class ModelMeasure { public float loss, accuracy; public int[,] confusion = new int[10,10]; public List<int> wrong = new List<int>(); public List<int> predictions = new List<int>(); public int count; }
    public static class LabData
    {
        static byte[] images, labels, testImages, testLabels; static readonly Dictionary<string,int[]> orders = new Dictionary<string,int[]>();
        static byte[] Read(string name) { var asset = Resources.Load<TextAsset>("MNIST/" + name); if (!asset) throw new InvalidOperationException("MNIST 数据文件缺失。请使用完整的游戏文件夹。"); using (var stream = new GZipStream(new MemoryStream(asset.bytes), CompressionMode.Decompress)) using (var output = new MemoryStream()) { stream.CopyTo(output); return output.ToArray(); } }
        static int BE(byte[] data, int i) { return data[i] << 24 | data[i+1] << 16 | data[i+2] << 8 | data[i+3]; }
        public static void Load(bool test = false)
        {
            if (images == null) { images = Read("train-images-idx3-ubyte"); labels = Read("train-labels-idx1-ubyte"); if (BE(images,0) != 2051 || BE(images,4) != 60000 || BE(images,8) != 28 || BE(images,12) != 28 || BE(labels,0) != 2049 || BE(labels,4) != 60000 || images.Length != 47040016 || labels.Length != 60008) throw new InvalidDataException("MNIST 训练文件格式错误。"); }
            if (test && testImages == null) { testImages = Read("t10k-images-idx3-ubyte"); testLabels = Read("t10k-labels-idx1-ubyte"); if (BE(testImages,4) != 10000 || testImages.Length != 7840016 || testLabels.Length != 10008) throw new InvalidDataException("MNIST 测试文件格式错误。"); }
        }
        // IDs are original IDX indices. Validation is the final 2,000 training-file entries.
        // Official test images are loaded only by an explicitly frozen G04/S08 evaluation.
        public static DataBatch Mnist(string split, int start, int count, int trainCount = 2000, int seed = 17, bool shuffled = false)
        {
            bool test = split == "test"; Load(test); int size = test || split == "validation" ? 2000 : Math.Max(32, Math.Min(20000,trainCount));
            int[] order = null;
            if (shuffled) { int epoch = start / size; string key = size + "/" + seed + "/" + epoch; if (!orders.TryGetValue(key, out order)) { order = Enumerable.Range(0,size).ToArray(); var rng = new System.Random(seed + epoch * 7919); for (int i = size-1; i > 0; i--) { int j=rng.Next(i+1), swap=order[i]; order[i]=order[j]; order[j]=swap; } if (orders.Count > 24) orders.Clear(); orders[key]=order; } }
            var batch = new DataBatch { input = new Tensor(new[] { count,1,28,28 }), labels = new int[count], ids = new string[count] };
            byte[] source = test ? testImages : images, targets = test ? testLabels : labels;
            for (int i=0;i<count;i++) { int index=(start+i)%size; if (order != null) index=order[index]; if (split=="validation") index+=58000; batch.ids[i]=(test?"test:":"train:")+index; batch.labels[i]=targets[index+8]; for(int p=0;p<784;p++) batch.input.data[i*784+p]=source[16+index*784+p]/255f; }
            return batch;
        }
        public static DataBatch Xor(int start, int count)
        { var b = new DataBatch { input = new Tensor(new[] {count,2}), labels=new int[count],ids=new string[count] }; for(int i=0;i<count;i++) { int p=(start+i)%4; b.input.data[i*2]=(p&1)==0?-1:1; b.input.data[i*2+1]=(p&2)==0?-1:1; b.labels[i]=((p&1)==0)==((p&2)==0)?0:1; b.ids[i]="xor:"+p; } return b; }
        public static DataBatch Moons(int start,int count,bool validation=false)
        { var b=new DataBatch{input=new Tensor(new[]{count,2}),labels=new int[count],ids=new string[count]}; var rng=new System.Random(validation?193:17); var points=new float[800]; for(int i=0;i<400;i++){float a=(float)(i%200*Math.PI/199);points[i*2]=(i<200?(float)Math.Cos(a):1-(float)Math.Cos(a))+(float)(rng.NextDouble()-.5)*.18f;points[i*2+1]=(i<200?(float)Math.Sin(a):.5f-(float)Math.Sin(a))+(float)(rng.NextDouble()-.5)*.18f;} for(int i=0;i<count;i++){int p=(start+i)%400;b.input.data[i*2]=points[p*2];b.input.data[i*2+1]=points[p*2+1];b.labels[i]=p<200?0:1;b.ids[i]=(validation?"moons-val:":"moons-train:")+p;}return b; }
        public static bool IsClassifier(string id) { return new[]{"C01","C05","E01","E05","E06","G01","G02","G03","G04","S03","S05","S06","S08"}.Contains(id); }
        public static bool IsTrainable(string id) { return new[]{"C05","E05","E06","G02","G03","G04","S03","S05","S06","S08"}.Contains(id); }
        public static DataBatch Batch(LabWorkspace w,string split,int start,int count)
        {
            if(w.levelId=="C01"||w.levelId=="C05")return Xor(start,count);
            if(w.levelId=="S03")return Moons(start,count,split!="train");
            if(split=="personal-train"||split=="personal-validation") { var samples=w.personal.Where(d=>d.validation==(split=="personal-validation")).ToArray();if(samples.Length==0)throw new InvalidOperationException("先保存带标签的手写样本。");var b=new DataBatch{input=new Tensor(new[]{count,1,28,28}),labels=new int[count],ids=new string[count]};for(int i=0;i<count;i++){int p=(start+i)%samples.Length;Array.Copy(samples[p].pixels,0,b.input.data,i*784,784);b.labels[i]=samples[p].label;b.ids[i]=split+":"+p;}return b; }
            return Mnist(split,start,count,w.trainCount,w.seed,split=="train");
        }
        public static float Train(LabWorkspace w,int batchSize=32)
        {
            if(!IsTrainable(w.levelId))throw new InvalidOperationException("本关用测量和校准，不更新模型。");
            if(w.tested)throw new InvalidOperationException("模型已经冻结。恢复检查点或重新开始实验后再训练。");
            var b=Batch(w,w.levelId=="S08"?"personal-train":"train",w.cursor,w.levelId=="C05"?4:batchSize);var r=TensorEngine.Run(w.layers,b.input);
            if(r.output.shape.Length!=2||r.output.shape[1]!=(w.levelId=="C05"||w.levelId=="S03"?2:10))throw new InvalidOperationException("末端需要原始分类分数：[批量,类别]。不要在训练链末端加 Argmax / Softmax。");
            if(w.layers.Sum(l=>l.Parameters)==0)throw new InvalidOperationException("机器没有可训练参数。");
            var loss=TensorEngine.CrossEntropy(r.output,b.labels);if(float.IsNaN(loss.loss)||float.IsInfinity(loss.loss))throw new InvalidOperationException("训练出现非有限误差。");
            r.Backward(loss.gradient);r.Update(w.rate,w.momentum);w.steps++;w.cursor+=b.labels.Length;w.trainCurve.Add(loss.loss);if(w.trainCurve.Count>600)w.trainCurve.RemoveAt(0);w.tested=false;return loss.loss;
        }
        public static ModelMeasure Measure(LabWorkspace w,string split,int count)
        {
            var m=new ModelMeasure{count=count};for(int at=0;at<count;at+=32){int n=Math.Min(32,count-at);var b=Batch(w,split,at,n);var r=TensorEngine.Run(w.layers,b.input);var loss=TensorEngine.CrossEntropy(r.output,b.labels);m.loss+=loss.loss*n/count;int c=r.output.shape[1];for(int i=0;i<n;i++){int p=TensorEngine.Argmax(r.output.data,i*c,c),label=b.labels[i];if(p>=10||label>=10)throw new InvalidOperationException("类别超出仪表范围。");m.confusion[label,p]++;m.predictions.Add(p);if(p==label)m.accuracy+=1f/count;else m.wrong.Add(at+i);}}return m;
        }
        public static ModelRecord Record(LabWorkspace w,ModelMeasure m,string name)
        { return new ModelRecord{name=name,layers=ModelIO.Copy(w.layers),steps=w.steps,samples=w.cursor,seed=w.seed,rate=w.rate,momentum=w.momentum,trainCount=w.trainCount,fromFreshInitialization=w.freshExperiment,loss=m.loss,accuracy=m.accuracy,dataset=w.levelId=="C05"?"XOR":w.levelId=="S03"?"Moons 17/193":w.levelId=="S08"?"Personal separate training / validation":"MNIST train:0.."+(w.trainCount-1)+"; validation:58000..59999",architecture=ModelIO.Signature(w.layers),fingerprint=ModelIO.Hash(w.layers),trainCurve=new List<float>(w.trainCurve),validationCurve=new List<float>(w.validationCurve)}; }
    }
}
