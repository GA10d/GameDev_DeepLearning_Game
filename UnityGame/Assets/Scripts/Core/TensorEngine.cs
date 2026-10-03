using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;

namespace LearningFoundry.Core
{
    // Contiguous row-major tensors. The outer batch axis is kept by Flatten/Dense.
    public sealed class Tensor
    {
        public int[] shape; public float[] data, grad;
        public Tensor(int[] dimensions, float[] values = null) { shape = (int[])dimensions.Clone(); int n = Size(shape); if (n <= 0 || n > 2000000) throw new InvalidOperationException("张量大小须为 1～200 万。"); data = values == null ? new float[n] : (float[])values.Clone(); if (data.Length != n) throw new InvalidOperationException("元素数量与形状不一致。"); grad = new float[n]; }
        public static int Size(int[] s) { long n = 1; foreach (int d in s) { if (d < 1) throw new InvalidOperationException("形状的每条轴须大于零。"); n *= d; if (n > 2000000) throw new InvalidOperationException("超过工作台的张量预算。"); } return (int)n; }
        public string Shape { get { return "[" + string.Join(" × ", shape) + "]"; } }
        public Tensor Copy() { return new Tensor(shape, data); }
    }
    public enum TensorOp { Pack, Unpack, Reshape, Multiply, Sum, Transpose, MatMul, Dense, Gate, Relu, Softmax, Argmax, CrossEntropy, Window, Scan, Conv2D, MaxPool, AvgPool, Flatten, Normalize, MeanBatch, Vision, GateBackward }
    [Serializable] public sealed class LayerSpec
    {
        public string id = Guid.NewGuid().ToString("N"), title;
        public TensorOp op; public int width = 2, kernel = 3, stride = 1, padding, seed = 17;
        public int[] reshape = { 6 }; public float[] weights, bias, velocity, biasVelocity;
        public bool learnable = true; public List<LayerSpec> children = new List<LayerSpec>();
        public int Parameters { get { return op == TensorOp.Vision ? children.Sum(l => l.Parameters) : learnable && (op == TensorOp.Dense || op == TensorOp.Conv2D || op == TensorOp.MatMul) ? (weights == null ? 0 : weights.Length) + (bias == null ? 0 : bias.Length) : 0; } }
        public void Init(int inputs, bool convolution = false)
        {
            int n = inputs * width * (convolution ? kernel * kernel : 1);
            if (n < 1 || n > 150000) throw new InvalidOperationException("单个模块参数预算为 15 万。减小通道或宽度。");
            if (weights != null && weights.Length == n && bias != null && bias.Length == width) return;
            var rng = new System.Random(seed); float scale = (float)Math.Sqrt(6.0 / (inputs * (convolution ? kernel * kernel : 1) + width));
            weights = new float[n]; bias = new float[width]; velocity = new float[n]; biasVelocity = new float[width];
            for (int i = 0; i < n; i++) weights[i] = (float)(rng.NextDouble() * 2 - 1) * scale;
        }
        public void Reset() { weights = bias = velocity = biasVelocity = null; foreach (var c in children) c.Reset(); }
    }
    public sealed class TensorStep
    {
        public LayerSpec layer; public Tensor input, output; public float[] dw, db; public int[] routing; public Action backward;
    }
    public sealed class TensorRun
    {
        public Tensor input, output; public List<TensorStep> steps = new List<TensorStep>();
        public void Backward(float[] upstream = null)
        {
            Array.Clear(input.grad, 0, input.grad.Length);
            foreach (var s in steps) { Array.Clear(s.output.grad, 0, s.output.grad.Length); if (s.dw != null) Array.Clear(s.dw, 0, s.dw.Length); if (s.db != null) Array.Clear(s.db, 0, s.db.Length); }
            if (upstream != null && upstream.Length != output.data.Length) throw new InvalidOperationException("上游梯度形状不匹配。");
            for (int i = 0; i < output.grad.Length; i++) output.grad[i] = upstream == null ? 1 : upstream[i];
            for (int i = steps.Count - 1; i >= 0; i--) steps[i].backward();
        }
        public void Update(float eta, float momentum = 0)
        {
            foreach (var s in steps) if (s.layer.learnable && s.dw != null)
            {
                var l = s.layer; if (l.velocity == null || l.velocity.Length != l.weights.Length) l.velocity = new float[l.weights.Length];
                if (l.biasVelocity == null || l.biasVelocity.Length != l.bias.Length) l.biasVelocity = new float[l.bias.Length];
                for (int j = 0; j < l.weights.Length; j++) { l.velocity[j] = momentum * l.velocity[j] + s.dw[j]; l.weights[j] -= eta * l.velocity[j]; }
                for (int j = 0; j < l.bias.Length; j++) { l.biasVelocity[j] = momentum * l.biasVelocity[j] + s.db[j]; l.bias[j] -= eta * l.biasVelocity[j]; }
            }
        }
    }
    public static class TensorEngine
    {
        public static TensorRun Run(List<LayerSpec> layers, Tensor input, int label = 0)
        {
            if (layers.Count > 32) throw new InvalidOperationException("工作台最多安装 32 个模块。");
            var run = new TensorRun { input = input, output = input };
            foreach (var l in layers) Apply(l, run, label);
            if (run.output.data.Any(v => float.IsNaN(v) || float.IsInfinity(v))) throw new InvalidOperationException("非有限值：请检查参数或学习率。");
            return run;
        }
        static void Apply(LayerSpec l, TensorRun r, int label)
        {
            if (l.op == TensorOp.Vision) { if (l.children == null || l.children.Count == 0 || l.children.Count > 12 || l.children.Any(c => c.op == TensorOp.Vision)) throw new InvalidOperationException("视觉盒需包含 1～12 个基础模块。"); foreach (var c in l.children) Apply(c, r, label); return; }
            Tensor x = r.output, y = null; var s = new TensorStep { layer = l, input = x };
            switch (l.op)
            {
                case TensorOp.Pack: case TensorOp.Unpack: case TensorOp.Reshape: case TensorOp.Flatten:
                    int[] shape = l.op == TensorOp.Flatten ? new[] { x.shape[0], x.data.Length / x.shape[0] } : l.op == TensorOp.Reshape ? l.reshape : new[] { x.data.Length };
                    if (Tensor.Size(shape) != x.data.Length) throw new InvalidOperationException("变形必须保持元素总数：输入 " + x.data.Length + "，目标 " + Tensor.Size(shape) + "。");
                    y = new Tensor(shape, x.data); s.backward = () => { for (int i = 0; i < x.grad.Length; i++) x.grad[i] += y.grad[i]; }; break;
                case TensorOp.Transpose:
                    if (x.shape.Length != 2) throw new InvalidOperationException("转置需要二维矩阵。"); int rows = x.shape[0], cols = x.shape[1]; y = new Tensor(new[] { cols, rows });
                    for (int a = 0; a < rows; a++) for (int b = 0; b < cols; b++) y.data[b * rows + a] = x.data[a * cols + b];
                    s.backward = () => { for (int a = 0; a < rows; a++) for (int b = 0; b < cols; b++) x.grad[a * cols + b] += y.grad[b * rows + a]; }; break;
                case TensorOp.Multiply:
                    if (l.weights == null || l.weights.Length != x.data.Length) l.weights = Enumerable.Repeat(1f, x.data.Length).ToArray(); y = new Tensor(x.shape);
                    for (int i = 0; i < x.data.Length; i++) y.data[i] = x.data[i] * l.weights[i];
                    s.backward = () => { for (int i = 0; i < x.grad.Length; i++) x.grad[i] += y.grad[i] * l.weights[i]; }; break;
                case TensorOp.Sum: case TensorOp.MeanBatch:
                    y = new Tensor(new[] { 1 }, new[] { x.data.Sum() / (l.op == TensorOp.MeanBatch ? x.data.Length : 1) });
                    s.backward = () => { for (int i = 0; i < x.grad.Length; i++) x.grad[i] += y.grad[0] / (l.op == TensorOp.MeanBatch ? x.data.Length : 1); }; break;
                case TensorOp.Dense: case TensorOp.MatMul:
                    if (x.shape.Length != 2) throw new InvalidOperationException("矩阵 / Dense 输入须为 [批量,特征]。先变形或展平。"); int batch = x.shape[0], features = x.shape[1]; l.Init(features);
                    y = new Tensor(new[] { batch, l.width }); s.dw = new float[l.weights.Length]; s.db = new float[l.width];
                    for (int n = 0; n < batch; n++) for (int o = 0; o < l.width; o++) { float v = l.op == TensorOp.Dense ? l.bias[o] : 0; int wi = o * features, xi = n * features; for (int j = 0; j < features; j++) v += x.data[xi + j] * l.weights[wi + j]; y.data[n * l.width + o] = v; }
                    s.backward = () => { for (int n = 0; n < batch; n++) for (int o = 0; o < l.width; o++) { float g = y.grad[n * l.width + o]; int wi = o * features, xi = n * features; if (l.op == TensorOp.Dense) s.db[o] += g; for (int j = 0; j < features; j++) { x.grad[xi + j] += g * l.weights[wi + j]; s.dw[wi + j] += g * x.data[xi + j]; } } }; break;
                case TensorOp.Gate: case TensorOp.Relu: case TensorOp.GateBackward:
                    y = new Tensor(x.shape); for (int i = 0; i < x.data.Length; i++) y.data[i] = x.data[i] > 0 ? (l.op == TensorOp.GateBackward ? l.width : x.data[i]) : 0;
                    s.backward = () => { for (int i = 0; i < x.grad.Length; i++) x.grad[i] += x.data[i] > 0 && l.op != TensorOp.GateBackward ? y.grad[i] : 0; }; break;
                case TensorOp.Softmax:
                    int count = x.shape.Last(); y = new Tensor(x.shape);
                    for (int row = 0; row < x.data.Length / count; row++) { int off = row * count; float max = float.NegativeInfinity; for (int j = 0; j < count; j++) max = Math.Max(max, x.data[off + j]); double total = 0; for (int j = 0; j < count; j++) { y.data[off + j] = (float)Math.Exp(x.data[off + j] - max); total += y.data[off + j]; } for (int j = 0; j < count; j++) y.data[off + j] /= (float)total; }
                    s.backward = () => { for (int row = 0; row < x.data.Length / count; row++) { int off = row * count; float dot = 0; for (int j = 0; j < count; j++) dot += y.grad[off + j] * y.data[off + j]; for (int j = 0; j < count; j++) x.grad[off + j] += y.data[off + j] * (y.grad[off + j] - dot); } }; break;
                case TensorOp.Argmax:
                    int nc = x.shape.Last(), nb = x.data.Length / nc; y = new Tensor(new[] { nb }); for (int n = 0; n < nb; n++) y.data[n] = Argmax(x.data, n * nc, nc); s.backward = () => {}; break;
                case TensorOp.CrossEntropy:
                    if (label < 0 || label >= x.shape.Last()) throw new InvalidOperationException("标签超出类别范围。"); var losses = CrossEntropy(x, Enumerable.Repeat(label, x.data.Length / x.shape.Last()).ToArray()); y = new Tensor(new[] { 1 }, new[] { losses.loss }); s.backward = () => { for (int i = 0; i < x.grad.Length; i++) x.grad[i] += losses.gradient[i] * y.grad[0]; }; break;
                case TensorOp.Window: case TensorOp.Scan: case TensorOp.Conv2D:
                    if (x.shape.Length != 4) throw new InvalidOperationException("卷积输入须为 [批量,通道,高,宽]。");
                    int bn = x.shape[0], ic = x.shape[1], ih = x.shape[2], iw = x.shape[3], k = l.kernel, st = l.stride, pad = l.padding;
                    if (k < 1 || k > 7 || st < 1 || st > 4 || pad < 0 || pad > 3) throw new InvalidOperationException("核 1～7、步幅 1～4、填充 0～3。");
                    int oh = (ih + pad * 2 - k) / st + 1, ow = (iw + pad * 2 - k) / st + 1; if (oh < 1 || ow < 1 || ih + pad * 2 < k || iw + pad * 2 < k) throw new InvalidOperationException("卷积核大于输入。");
                    l.Init(ic, true); int oc = l.width; y = new Tensor(new[] { bn, oc, oh, ow }); s.dw = new float[l.weights.Length]; s.db = new float[oc];
                    for (int n = 0; n < bn; n++) for (int o = 0; o < oc; o++) for (int h = 0; h < oh; h++) for (int w = 0; w < ow; w++)
                    { float v = l.bias[o]; for (int c = 0; c < ic; c++) for (int a = 0; a < k; a++) for (int b = 0; b < k; b++) { int py = h * st + a - pad, px = w * st + b - pad; if (py >= 0 && px >= 0 && py < ih && px < iw) v += x.data[((n * ic + c) * ih + py) * iw + px] * l.weights[((o * ic + c) * k + a) * k + b]; } y.data[((n * oc + o) * oh + h) * ow + w] = v; }
                    s.backward = () => { for (int n = 0; n < bn; n++) for (int o = 0; o < oc; o++) for (int h = 0; h < oh; h++) for (int w = 0; w < ow; w++) { float g = y.grad[((n * oc + o) * oh + h) * ow + w]; s.db[o] += g; for (int c = 0; c < ic; c++) for (int a = 0; a < k; a++) for (int b = 0; b < k; b++) { int py = h * st + a - pad, px = w * st + b - pad; if (py >= 0 && px >= 0 && py < ih && px < iw) { int xi = ((n * ic + c) * ih + py) * iw + px, wi = ((o * ic + c) * k + a) * k + b; x.grad[xi] += l.weights[wi] * g; s.dw[wi] += x.data[xi] * g; } } } }; break;
                case TensorOp.MaxPool: case TensorOp.AvgPool:
                    if (x.shape.Length != 4) throw new InvalidOperationException("池化输入须为 [批量,通道,高,宽]。");
                    int ph = x.shape[2], pw = x.shape[3], pk = l.kernel, ps = l.stride; if (pk < 1 || pk > ph || pk > pw || ps < 1) throw new InvalidOperationException("池化窗口或步幅无效。");
                    int qh = (ph - pk) / ps + 1, qw = (pw - pk) / ps + 1, planes = x.shape[0] * x.shape[1]; y = new Tensor(new[] { x.shape[0], x.shape[1], qh, qw }); int[] winners = new int[y.data.Length];
                    for (int p = 0; p < planes; p++) for (int a = 0; a < qh; a++) for (int b = 0; b < qw; b++) { int oi = (p * qh + a) * qw + b; float v = l.op == TensorOp.MaxPool ? float.NegativeInfinity : 0; for (int h = 0; h < pk; h++) for (int w = 0; w < pk; w++) { int xi = (p * ph + a * ps + h) * pw + b * ps + w; if (l.op == TensorOp.MaxPool) { if (x.data[xi] > v) { v = x.data[xi]; winners[oi] = xi; } } else v += x.data[xi] / (pk * pk); } y.data[oi] = v; }
                    if(l.op==TensorOp.MaxPool)s.routing=winners;
                    s.backward = () => { for (int p = 0; p < planes; p++) for (int a = 0; a < qh; a++) for (int b = 0; b < qw; b++) { int oi = (p * qh + a) * qw + b; if (l.op == TensorOp.MaxPool) x.grad[winners[oi]] += y.grad[oi]; else for (int h = 0; h < pk; h++) for (int w = 0; w < pk; w++) x.grad[(p * ph + a * ps + h) * pw + b * ps + w] += y.grad[oi] / (pk * pk); } }; break;
                case TensorOp.Normalize:
                    if (x.shape.Length != 2 || l.weights == null || l.bias == null || l.weights.Length != x.shape[1]) throw new InvalidOperationException("先用训练集合拟合每列均值与标准差。");
                    y = new Tensor(x.shape); for (int i = 0; i < x.data.Length; i++) y.data[i] = (x.data[i] - l.bias[i % x.shape[1]]) / l.weights[i % x.shape[1]];
                    s.backward = () => { for (int i = 0; i < x.grad.Length; i++) x.grad[i] += y.grad[i] / l.weights[i % x.shape[1]]; }; break;
                default: throw new InvalidOperationException("未知张量模块。");
            }
            s.output = y; r.output = y; r.steps.Add(s);
        }
        public sealed class Loss { public float loss; public float[] gradient; }
        public static Loss CrossEntropy(Tensor logits, int[] labels)
        {
            if (logits.shape.Length != 2 || labels.Length != logits.shape[0]) throw new InvalidOperationException("分类器输出须为 [批量,类别]，每件样本一个标签。");
            int n = logits.shape[0], c = logits.shape[1]; var result = new Loss { gradient = new float[logits.data.Length] };
            for (int row = 0; row < n; row++) { if (labels[row] < 0 || labels[row] >= c) throw new InvalidOperationException("标签范围错误。"); int off = row * c; float max = float.NegativeInfinity; for (int j = 0; j < c; j++) max = Math.Max(max, logits.data[off + j]); double sum = 0; for (int j = 0; j < c; j++) { result.gradient[off + j] = (float)Math.Exp(logits.data[off + j] - max); sum += result.gradient[off + j]; } result.loss += (float)(Math.Log(sum) + max - logits.data[off + labels[row]]) / n; for (int j = 0; j < c; j++) result.gradient[off + j] = (result.gradient[off + j] / (float)sum - (j == labels[row] ? 1 : 0)) / n; }
            return result;
        }
        public static int Argmax(float[] a, int off, int count) { int best = 0; for (int i = 1; i < count; i++) if (a[off + i] > a[off + best]) best = i; return best; }
        public static void FitNormalize(LayerSpec l, Tensor train)
        { int n = train.shape[0], f = train.shape[1]; l.bias = new float[f]; l.weights = new float[f]; for (int j = 0; j < f; j++) { for (int i = 0; i < n; i++) l.bias[j] += train.data[i * f + j] / n; double sum = 0; for (int i = 0; i < n; i++) sum += Math.Pow(train.data[i * f + j] - l.bias[j], 2); l.weights[j] = (float)Math.Sqrt(sum / n); if (l.weights[j] < 1e-8) l.weights[j] = 1; } }
    }
    [Serializable] public sealed class ModelRecord
    {
        public string name, dataset, architecture, fingerprint; public List<LayerSpec> layers = new List<LayerSpec>(); public int steps, samples, seed;
        public float rate, loss, accuracy, momentum; public int trainCount; public bool fromFreshInitialization; public List<float> trainCurve = new List<float>(), validationCurve = new List<float>();
    }
    [Serializable] public sealed class LabWorkspace
    {
        public string levelId; public List<LayerSpec> layers = new List<LayerSpec>(); public List<ModelRecord> records = new List<ModelRecord>();
        public List<int> observed = new List<int>(); public List<string> signatures = new List<string>(); public int steps, cursor, seed = 17, actions, poolMode, partition = 1, selectedCell, prediction;
        public int trainCount = 2000; public float rate = .12f, momentum; public string shapePrediction = ""; public List<float> trainCurve = new List<float>(), validationCurve = new List<float>();
        public List<DrawSample> personal = new List<DrawSample>(); public float[] drawing = new float[784]; public float bestAccuracy; public bool tested, freshExperiment = true; public string frozenHash;
    }
    [Serializable] public sealed class DrawSample { public float[] pixels; public int label; public bool validation; }
    public static class ModelIO
    {
        [Serializable] sealed class LayerList { public List<LayerSpec> values; }
        [Serializable] sealed class RecordList { public List<ModelRecord> values; }
        public static T Copy<T>(T value) { if(value is List<LayerSpec>)return (T)(object)JsonUtility.FromJson<LayerList>(JsonUtility.ToJson(new LayerList{values=(List<LayerSpec>)(object)value})).values;if(value is List<ModelRecord>)return (T)(object)JsonUtility.FromJson<RecordList>(JsonUtility.ToJson(new RecordList{values=(List<ModelRecord>)(object)value})).values;return JsonUtility.FromJson<T>(JsonUtility.ToJson(value)); }
        public static string Signature(List<LayerSpec> layers) { return string.Join(" → ", layers.Select(l => l.op + (l.op == TensorOp.Dense || l.op == TensorOp.Conv2D ? "(" + l.width + ")" : ""))); }
        // Identity is model behavior: operation, shapes, weights and bias. Cosmetic titles,
        // generated instance ids and optimizer velocity do not change a frozen classifier.
        public static string Hash(List<LayerSpec> layers) { using(var stream=new MemoryStream()) { using(var writer=new BinaryWriter(stream,System.Text.Encoding.UTF8,true))WriteLayers(writer,layers);using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-",""); } }
        static void WriteLayers(BinaryWriter w,List<LayerSpec> layers){w.Write(layers.Count);foreach(var l in layers){w.Write((int)l.op);w.Write(l.width);w.Write(l.kernel);w.Write(l.stride);w.Write(l.padding);w.Write(l.reshape==null?0:l.reshape.Length);if(l.reshape!=null)foreach(int n in l.reshape)w.Write(n);foreach(var a in new[]{l.weights,l.bias}){w.Write(a==null?0:a.Length);if(a!=null)foreach(float v in a)w.Write(v);}if(l.op==TensorOp.Vision)WriteLayers(w,l.children);}}
    }
}
