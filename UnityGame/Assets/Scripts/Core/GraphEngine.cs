using System;
using System.Collections.Generic;
using System.Linq;

namespace LearningFoundry.Core
{
    public enum NodeKind { Input, Constant, Parameter, Add, Multiply, PositiveGate, Output, Module }

    [Serializable] public sealed class GraphNode
    {
        public string id, name, moduleId;
        public NodeKind kind;
        public double value;
        public float x, y;
        public List<string> inputs = new List<string>();
        public List<ParameterBinding> bindings = new List<ParameterBinding>();
    }
    [Serializable] public sealed class ParameterBinding { public string key; public double value; }
    [Serializable] public sealed class GraphSpec
    {
        public List<GraphNode> nodes = new List<GraphNode>();
        public GraphNode Node(string id) { return nodes.Find(n => n.id == id); }
        public GraphNode Add(NodeKind kind, string name, double value = 0, params string[] inputs)
        {
            var node = new GraphNode { id = Guid.NewGuid().ToString("N"), kind = kind, name = name, value = value, x = 80 + (nodes.Count % 4) * 205, y = 70 + (nodes.Count / 4) * 145 };
            node.inputs.AddRange(inputs); nodes.Add(node); return node;
        }
        public GraphNode Output(string name) { return nodes.Find(n => n.kind == NodeKind.Output && n.name == name); }
    }
    [Serializable] public sealed class ModuleSpec
    {
        public string id, name;
        public GraphSpec graph;
        public List<string> inputNames = new List<string>();
        public string outputName;
    }
    public sealed class ParameterSlot
    {
        public string key, name;
        public Func<double> get;
        public Action<double> set;
        public double Value { get { return get(); } set { set(value); } }
    }
    public sealed class TapeValue
    {
        public string key, name;
        public double value, gradient;
        internal Action reverse;
        internal bool operation;
    }
    public sealed class Evaluation
    {
        public readonly Dictionary<string, TapeValue> values = new Dictionary<string, TapeValue>();
        public readonly Dictionary<string, TapeValue> parameters = new Dictionary<string, TapeValue>();
        public readonly List<TapeValue> tape = new List<TapeValue>();
        public readonly Dictionary<string, TapeValue> outputs = new Dictionary<string, TapeValue>();
        public bool accumulate = true;
        internal void Contribute(TapeValue target, double gradient)
        {
            if (accumulate) target.gradient += gradient; else target.gradient = gradient;
        }
        public void Backward(string output, bool reverseOrder = true)
        {
            if (!outputs.ContainsKey(output)) throw new InvalidOperationException("缺少输出端口：" + output);
            foreach (var v in tape) v.gradient = 0;
            outputs[output].gradient = 1;
            if (reverseOrder) for (int i = tape.Count - 1; i >= 0; i--) tape[i].reverse?.Invoke();
            else for (int i = 0; i < tape.Count; i++) tape[i].reverse?.Invoke();
            foreach (var v in tape) if (!GraphEngine.Finite(v.gradient)) throw new InvalidOperationException("梯度不是有限数值：" + v.name);
        }
        public double Output(string name) { return outputs[name].value; }
    }
    public sealed class DerivativeRules
    {
        public GraphSpec add, multiply;
        public bool usePlayerRules;
    }
    public sealed class GraphEngine
    {
        readonly List<ModuleSpec> modules;
        readonly DerivativeRules rules;
        public GraphEngine(List<ModuleSpec> modules = null, DerivativeRules rules = null)
        { this.modules = modules ?? new List<ModuleSpec>(); this.rules = rules ?? new DerivativeRules(); }
        public static bool Finite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value) && Math.Abs(value) < 1e15; }
        public int Arity(GraphNode node)
        {
            switch (node.kind)
            {
                case NodeKind.Add: case NodeKind.Multiply: return 2;
                case NodeKind.Output: return 1;
                case NodeKind.PositiveGate: return 3;
                case NodeKind.Module:
                    var m = modules.Find(x => x.id == node.moduleId);
                    if (m == null) throw new InvalidOperationException("找不到已封装模块。");
                    return m.inputNames.Count;
                default: return 0;
            }
        }
        public List<ParameterSlot> Parameters(GraphSpec graph)
        {
            var result = new List<ParameterSlot>();
            Collect(graph, "", null, "", result, new HashSet<string>(), 0);
            return result;
        }
        void Collect(GraphSpec graph, string prefix, GraphNode owner, string relative, List<ParameterSlot> slots, HashSet<string> path, int depth)
        {
            if (depth > 8 || graph.nodes.Count > 400) throw new InvalidOperationException("当前工程超过原型的模块容量。");
            foreach (var n in graph.nodes)
            {
                var node = n;
                if (node.kind == NodeKind.Parameter)
                {
                    var key = prefix + node.id;
                    var localKey = relative + node.id;
                    if (owner == null) slots.Add(new ParameterSlot { key = key, name = node.name, get = () => node.value, set = v => node.value = v });
                    else
                    {
                        var binding = owner.bindings.Find(x => x.key == localKey);
                        if (binding == null) { binding = new ParameterBinding { key = localKey, value = node.value }; owner.bindings.Add(binding); }
                        var saved = binding;
                        slots.Add(new ParameterSlot { key = key, name = owner.name + " / " + node.name, get = () => saved.value, set = v => saved.value = v });
                    }
                }
                if (node.kind == NodeKind.Module)
                {
                    var module = modules.Find(x => x.id == node.moduleId);
                    if (module == null || !path.Add(module.id)) throw new InvalidOperationException("模块缺失或包含递归引用。");
                    Collect(module.graph, prefix + node.id + "/", owner ?? node, owner == null ? "" : relative + node.id + "/", slots, path, depth + 1);
                    path.Remove(module.id);
                }
            }
        }
        public Evaluation Evaluate(GraphSpec graph, Dictionary<string, double> inputs)
        {
            if (graph.nodes.Count > 400) throw new InvalidOperationException("当前工程节点过多。");
            if (graph.nodes.Select(n => n.id).Distinct().Count() != graph.nodes.Count) throw new InvalidOperationException("节点身份重复。");
            var slots = Parameters(graph).ToDictionary(s => s.key);
            var e = new Evaluation();
            var arguments = inputs.ToDictionary(p => p.Key, p => new TapeValue { key = "input:" + p.Key, name = p.Key, value = p.Value });
            foreach (var argument in arguments.Values)
            {
                if (!Finite(argument.value)) throw new InvalidOperationException("输入不是有限数值：" + argument.name);
                e.tape.Add(argument);
            }
            var visiting = new HashSet<string>();
            foreach (var node in graph.nodes.Where(n => n.kind == NodeKind.Output))
            {
                if (e.outputs.ContainsKey(node.name)) throw new InvalidOperationException("输出名称重复：" + node.name);
                e.outputs[node.name] = Visit(graph, node.id, "", arguments, slots, visiting, e, 0);
            }
            return e;
        }
        TapeValue Visit(GraphSpec graph, string id, string scope, Dictionary<string, TapeValue> args, Dictionary<string, ParameterSlot> slots, HashSet<string> visiting, Evaluation e, int depth)
        {
            if (depth > 1000) throw new InvalidOperationException("计算路径过深。");
            var key = scope + id;
            if (e.values.TryGetValue(key, out var previous)) return previous;
            if (!visiting.Add(key)) throw new InvalidOperationException("出现循环连线，请断开回路。");
            var node = graph.Node(id);
            if (node == null) throw new InvalidOperationException("连线指向一个已经移除的模块。");
            var children = new List<TapeValue>();
            for (int i = 0; i < Arity(node); i++)
            {
                if (i >= node.inputs.Count || string.IsNullOrEmpty(node.inputs[i])) throw new InvalidOperationException(node.name + " 的输入 " + (i + 1) + " 尚未连接。");
                children.Add(Visit(graph, node.inputs[i], scope, args, slots, visiting, e, depth + 1));
            }
            TapeValue value;
            if (node.kind == NodeKind.Input)
            {
                if (!args.TryGetValue(node.name, out value)) throw new InvalidOperationException("样本缺少输入：" + node.name);
            }
            else if (node.kind == NodeKind.Output) value = children[0];
            else if (node.kind == NodeKind.Module)
            {
                var module = modules.Find(m => m.id == node.moduleId);
                var local = new Dictionary<string, TapeValue>();
                for (int i = 0; i < module.inputNames.Count; i++) local[module.inputNames[i]] = children[i];
                var output = module.graph.Output(module.outputName);
                if (output == null) throw new InvalidOperationException("盒子没有有效输出。");
                value = Visit(module.graph, output.id, scope + node.id + "/", local, slots, visiting, e, depth + 1);
            }
            else
            {
                value = new TapeValue { key = key, name = node.name };
                var v = value;
                switch (node.kind)
                {
                    case NodeKind.Constant: value.value = node.value; break;
                    case NodeKind.Parameter: value.value = slots[key].Value; e.parameters[key] = value; break;
                    case NodeKind.Add:
                        value.value = children[0].value + children[1].value; value.operation = true;
                        value.reverse = () => {
                            if (rules.usePlayerRules) ApplyRule(rules.add, children, v.gradient, e);
                            else { e.Contribute(children[0], v.gradient); e.Contribute(children[1], v.gradient); }
                        }; break;
                    case NodeKind.Multiply:
                        value.value = children[0].value * children[1].value; value.operation = true;
                        value.reverse = () => {
                            if (rules.usePlayerRules) ApplyRule(rules.multiply, children, v.gradient, e);
                            else { e.Contribute(children[0], v.gradient * children[1].value); e.Contribute(children[1], v.gradient * children[0].value); }
                        }; break;
                    case NodeKind.PositiveGate:
                        var chosen = children[0].value > 0 ? children[1] : children[2];
                        value.value = chosen.value; value.operation = true;
                        value.reverse = () => e.Contribute(chosen, v.gradient); break;
                    default: throw new InvalidOperationException("不支持的计算部件。");
                }
                if (!Finite(value.value)) throw new InvalidOperationException("数值超出范围：" + node.name);
                e.tape.Add(value);
            }
            e.values[key] = value; visiting.Remove(key); return value;
        }
        void ApplyRule(GraphSpec rule, List<TapeValue> children, double gradient, Evaluation e)
        {
            if (rule == null) throw new InvalidOperationException("请先完成加法和乘法的反向规则。");
            var input = new Dictionary<string, double> { { "a", children[0].value }, { "b", children[1].value }, { "g", gradient } };
            var local = new GraphEngine().Evaluate(rule, input);
            e.Contribute(children[0], local.Output("da")); e.Contribute(children[1], local.Output("db"));
        }
    }
    [Serializable] public sealed class Sample
    {
        public double x, z, target;
        public Sample(double x, double z, double target) { this.x = x; this.z = z; this.target = target; }
        public Dictionary<string, double> Inputs() { return new Dictionary<string, double> { { "x", x }, { "z", z }, { "target", target }, { "prediction", x } }; }
    }
    public sealed class TrainingSession
    {
        public readonly GraphSpec graph;
        public readonly List<Sample> samples;
        readonly GraphEngine engine;
        readonly GraphSpec optimizer;
        readonly GraphSpec average;
        readonly List<ParameterSlot> parameters;
        readonly Dictionary<string, double> gradients = new Dictionary<string, double>();
        List<Evaluation> cache;
        int version, cachedVersion;
        public int step;
        public double loss;
        public double learningRate = .04;
        public string[] phases = { "forward", "clear", "backward", "update" };
        public bool reverseOrder = true, accumulate = true;
        public TrainingSession(GraphSpec graph, List<ModuleSpec> modules, DerivativeRules rules, GraphSpec optimizer, List<Sample> samples, GraphSpec average = null)
        {
            this.graph = graph; this.samples = samples; this.optimizer = optimizer; this.average = average;
            engine = new GraphEngine(modules, rules); parameters = engine.Parameters(graph);
            if (parameters.Count == 0) throw new InvalidOperationException("模型没有可训练参数。");
            foreach (var p in parameters) gradients[p.key] = 0;
            loss = Measure();
        }
        public double Measure()
        { return Reduce(samples.Select(s => engine.Evaluate(graph, s.Inputs()).Output("loss")).ToList()); }
        double Reduce(List<double> values)
        {
            if (average == null) return values.Average();
            if (values.Count != 4) throw new InvalidOperationException("四读数平均器需要恰好四个样本。");
            var input = new Dictionary<string, double>(); for (int i = 0; i < 4; i++) input["L" + (i + 1)] = values[i];
            return new GraphEngine().Evaluate(average, input).Output("mean");
        }
        public void Tick()
        {
            bool clearedThisRound = false, forwardThisRound = false, backwardThisRound = false;
            if (phases.Length != 4 || phases.Distinct().Count() != 4) throw new InvalidOperationException("一轮必须恰好包含四个不同的训练阶段。");
            foreach (var phase in phases)
            {
                switch (phase)
                {
                    case "forward": cache = samples.Select(s => engine.Evaluate(graph, s.Inputs())).ToList(); cachedVersion = version; forwardThisRound = true; break;
                    case "clear": foreach (var p in parameters) gradients[p.key] = 0; clearedThisRound = true; break;
                    case "backward":
                        if (!clearedThisRound) throw new InvalidOperationException("本轮反向前需要清空旧梯度。");
                        if (!forwardThisRound) throw new InvalidOperationException("本轮反向前需要当前参数的正向预测。");
                        if (cache == null || cachedVersion != version) throw new InvalidOperationException("反向使用的预测不属于当前参数，请重新正向运行。");
                        var contributions = parameters.ToDictionary(p => p.key, p => new List<double>());
                        foreach (var e in cache)
                        {
                            e.accumulate = accumulate; e.Backward("loss", reverseOrder);
                            foreach (var p in parameters) contributions[p.key].Add(e.parameters.TryGetValue(p.key, out var v) ? v.gradient : 0);
                        }
                        foreach (var p in parameters) gradients[p.key] += Reduce(contributions[p.key]);
                        backwardThisRound = true;
                        break;
                    case "update":
                        if (!backwardThisRound) throw new InvalidOperationException("本轮参数更新必须等待反向传播结束。");
                        if (cache == null) throw new InvalidOperationException("更新前需要本轮的预测与梯度。");
                        foreach (var p in parameters)
                        {
                            if (optimizer == null) p.Value -= learningRate * gradients[p.key];
                            else p.Value = new GraphEngine().Evaluate(optimizer, new Dictionary<string, double> { { "w", p.Value }, { "g", gradients[p.key] }, { "eta", learningRate } }).Output("newW");
                            if (!GraphEngine.Finite(p.Value)) throw new InvalidOperationException("参数发散，请减小学习率。");
                        }
                        version++; break;
                    default: throw new InvalidOperationException("训练阶段配置无效。");
                }
            }
            step++; loss = Measure();
        }
    }
}
