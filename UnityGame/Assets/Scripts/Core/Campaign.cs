using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace LearningFoundry.Core
{
    [Serializable] public class Teaching { public string text, demo, check, delivery; }
    [Serializable] public class Visualization { public string main, interaction, failure, completion; }
    [Serializable] public class Level
    {
        public string id, chapter, name, concept, scene, given, play, feedback, pass, reward, hint, type;
        public string[] deps, introduces, uses;
        public Teaching teaching;
        public Visualization visualization;
    }
    [Serializable] public class Chapter { public string id, name, goal; }
    [Serializable] public class Campaign { public string version, title, audience; public Chapter[] chapters; public Level[] levels; }
    [Serializable] public class Workspace { public string levelId; public GraphSpec graph; }
    [Serializable] public class Profile
    {
        public int schema = 1;
        public List<string> completed = new List<string>();
        public List<Workspace> workspaces = new List<Workspace>();
        public List<ModuleSpec> modules = new List<ModuleSpec>();
        public GraphSpec addRule, multiplyRule, optimizer, average;
        public GraphSpec snapshotB09;
        public List<LabWorkspace> labs = new List<LabWorkspace>();
        public string[] phases = { "update", "backward", "clear", "forward" };
        public bool reverseOrder, accumulate, reduceMotion;
        // Additive fields: v0.3 saves keep their circuits and progress.
        public int onboardingVersion;
        public bool p00GuideHidden;
        public string currentLevel = "P00";
        public float volume = .25f;
        public GraphSpec Graph(string id)
        {
            var existing = workspaces.Find(w => w.levelId == id);
            if (existing != null) return existing.graph;
            var graph = Missions.Initial(id, this);
            workspaces.Add(new Workspace { levelId = id, graph = graph }); return graph;
        }
        public DerivativeRules Rules() { return new DerivativeRules { add = addRule, multiply = multiplyRule, usePlayerRules = true }; }
    }
    public static class ProfileStore
    {
        public static string PathName { get { return Path.Combine(Application.persistentDataPath, "profile-v1.json"); } }
        public static string LoadWarning;
        public static Profile Load()
        {
            try
            {
                if (!File.Exists(PathName)) return new Profile();
                var result = JsonUtility.FromJson<Profile>(File.ReadAllText(PathName));
                if (result == null || result.schema != 1 || result.workspaces == null || result.modules == null || result.completed == null)
                    throw new InvalidDataException("存档格式不能读取。");
                if(result.labs == null) result.labs = new List<LabWorkspace>();
                foreach(var w in result.labs) { if(w.layers==null)w.layers=new List<LayerSpec>(); if(w.records==null)w.records=new List<ModelRecord>(); if(w.observed==null)w.observed=new List<int>(); if(w.signatures==null)w.signatures=new List<string>(); if(w.personal==null)w.personal=new List<DrawSample>(); if(w.drawing==null||w.drawing.Length!=784)w.drawing=new float[784]; }
                return result;
            }
            catch (Exception e)
            {
                LoadWarning = "存档无法读取，已保留原文件并使用新工作台：" + e.Message;
                try { if (File.Exists(PathName)) File.Copy(PathName, PathName + ".unreadable-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss")); } catch { }
                return new Profile();
            }
        }
        public static void Save(Profile profile)
        {
            Directory.CreateDirectory(Application.persistentDataPath);
            var temp = PathName + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(profile, true));
            if (File.Exists(PathName)) File.Replace(temp, PathName, PathName + ".bak");
            else File.Move(temp, PathName);
        }
        public static GraphSpec Copy(GraphSpec source) { return JsonUtility.FromJson<GraphSpec>(JsonUtility.ToJson(source)); }
    }
    public static class Recipes
    {
        public static GraphSpec Empty(string[] inputs, params string[] outputs)
        {
            var graph = new GraphSpec();
            for (int i = 0; i < inputs.Length; i++) { var n = graph.Add(NodeKind.Input, inputs[i]); n.x = 45; n.y = 70 + i * 135; }
            for (int i = 0; i < outputs.Length; i++) { var n = graph.Add(NodeKind.Output, outputs[i]); n.x = 900; n.y = 170 + i * 180; }
            return graph;
        }
        public static GraphSpec Affine(double a, double b, double bias, bool withLoss = false)
        {
            var g = new GraphSpec();
            var x = g.Add(NodeKind.Input, "x"); var z = g.Add(NodeKind.Input, "z");
            var w1 = g.Add(NodeKind.Parameter, "w1", a); var w2 = g.Add(NodeKind.Parameter, "w2", b); var offset = g.Add(NodeKind.Parameter, "bias", bias);
            var t1 = g.Add(NodeKind.Multiply, "×", 0, x.id, w1.id); var t2 = g.Add(NodeKind.Multiply, "×", 0, z.id, w2.id);
            var sum = g.Add(NodeKind.Add, "+", 0, t1.id, t2.id); var y = g.Add(NodeKind.Add, "+", 0, sum.id, offset.id);
            g.Add(NodeKind.Output, "y", 0, y.id);
            if (withLoss) AttachLoss(g, y.id, g.Add(NodeKind.Input, "target").id);
            Arrange(g); return g;
        }
        public static void AttachLoss(GraphSpec g, string prediction, string target)
        {
            var minus = g.Add(NodeKind.Constant, "−1", -1); var half = g.Add(NodeKind.Constant, "½", .5);
            var neg = g.Add(NodeKind.Multiply, "×", 0, target, minus.id);
            var residual = g.Add(NodeKind.Add, "+", 0, prediction, neg.id);
            var square = g.Add(NodeKind.Multiply, "×", 0, residual.id, residual.id);
            var loss = g.Add(NodeKind.Multiply, "×", 0, square.id, half.id);
            g.Add(NodeKind.Output, "loss", 0, loss.id);
        }
        public static GraphSpec Chain()
        {
            var g = new GraphSpec(); var x = g.Add(NodeKind.Input, "x");
            var a = g.Add(NodeKind.Parameter, "gain", 2); var b = g.Add(NodeKind.Parameter, "offset", .5); var c = g.Add(NodeKind.Parameter, "scale", 3);
            var p = g.Add(NodeKind.Multiply, "① ×", 0, x.id, a.id); var q = g.Add(NodeKind.Add, "② +", 0, p.id, b.id);
            var r = g.Add(NodeKind.Multiply, "③ ×", 0, q.id, c.id); g.Add(NodeKind.Output, "goal", 0, r.id); Arrange(g); return g;
        }
        public static GraphSpec Shared(bool square = false)
        {
            var g = new GraphSpec(); var w = g.Add(NodeKind.Parameter, "shared w", 2);
            if (square) { var sq = g.Add(NodeKind.Multiply, "w × w", 0, w.id, w.id); g.Add(NodeKind.Output, "goal", 0, sq.id); }
            else
            {
                var x = g.Add(NodeKind.Input, "x"); var z = g.Add(NodeKind.Input, "z");
                var a = g.Add(NodeKind.Multiply, "左路", 0, w.id, x.id); var b = g.Add(NodeKind.Multiply, "右路", 0, w.id, z.id);
                var sum = g.Add(NodeKind.Add, "汇合", 0, a.id, b.id); g.Add(NodeKind.Output, "goal", 0, sum.id);
            }
            Arrange(g); return g;
        }
        public static GraphSpec Rule(bool multiply)
        {
            var g = Empty(new[] { "a", "b", "g" }, "da", "db");
            if (multiply)
            {
                var p = g.Add(NodeKind.Multiply, "×", 0, g.nodes[2].id, g.nodes[1].id);
                var q = g.Add(NodeKind.Multiply, "×", 0, g.nodes[2].id, g.nodes[0].id);
                g.Output("da").inputs.Add(p.id); g.Output("db").inputs.Add(q.id);
            }
            else { g.Output("da").inputs.Add(g.nodes[2].id); g.Output("db").inputs.Add(g.nodes[2].id); }
            return g;
        }
        public static GraphSpec Optimizer()
        {
            var g = Empty(new[] { "w", "g", "eta" }, "newW");
            var neg = g.Add(NodeKind.Constant, "−1", -1);
            var step = g.Add(NodeKind.Multiply, "步幅", 0, g.nodes[1].id, g.nodes[2].id);
            var down = g.Add(NodeKind.Multiply, "反向", 0, step.id, neg.id);
            var next = g.Add(NodeKind.Add, "更新", 0, g.nodes[0].id, down.id); g.Output("newW").inputs.Add(next.id); return g;
        }
        public static GraphSpec Average()
        {
            var g = Empty(new[] { "L1", "L2", "L3", "L4" }, "mean");
            var a = g.Add(NodeKind.Add, "+", 0, g.nodes[0].id, g.nodes[1].id); var b = g.Add(NodeKind.Add, "+", 0, g.nodes[2].id, g.nodes[3].id);
            var sum = g.Add(NodeKind.Add, "+", 0, a.id, b.id); var quarter = g.Add(NodeKind.Constant, "¼", .25);
            var mean = g.Add(NodeKind.Multiply, "平均", 0, sum.id, quarter.id); g.Output("mean").inputs.Add(mean.id); return g;
        }
        public static void Arrange(GraphSpec g)
        {
            // Layer by dependency; input/value sources stay visible and no node overlaps another.
            var depths = new Dictionary<string, int>();
            Func<GraphNode, int> depth = null;
            depth = n => { if (depths.ContainsKey(n.id)) return depths[n.id]; int d = n.inputs.Count == 0 ? 0 : 1 + n.inputs.Max(id => depth(g.Node(id))); depths[n.id] = d; return d; };
            var rows = new Dictionary<int, int>();
            foreach (var n in g.nodes) { int d = depth(n); if (!rows.ContainsKey(d)) rows[d] = 0; n.x = 35 + d * 210; n.y = 65 + rows[d]++ * 130; }
        }
    }
    public sealed class CheckResult
    {
        public bool passed; public string message; public List<string> rows = new List<string>();
        public static CheckResult Fail(string text) { return new CheckResult { message = text }; }
    }
    public static class Missions
    {
        public static readonly string[] Implemented = new[] { "P00", "A01", "A02", "A03", "A04", "A05", "A06", "A07", "B01", "B02", "B03", "B04", "B05", "B06", "B07", "B08", "B09" }.Concat(LabMissions.Ids).ToArray();
        public static List<Sample> Samples(bool heldOut = false)
        {
            var coordinates = heldOut ? new[] { new[] { .4, -.8 }, new[] { 1.3, .2 }, new[] { -.6, .7 } } : new[] { new[] { -1.0, -.5 }, new[] { 0.0, 1.0 }, new[] { 1.0, -1.0 }, new[] { .5, .5 } };
            return coordinates.Select(v => new Sample(v[0], v[1], 1.75 * v[0] + .5 * v[1] - .25)).ToList();
        }
        public static GraphSpec Initial(string id, Profile profile)
        {
            switch (id)
            {
                case "P00": case "B01": case "B08": return Recipes.Affine(0, 0, 0, true);
                case "A01": case "A02": case "A03": return Recipes.Empty(new[] { "x" }, "y");
                case "A04": return Recipes.Empty(new[] { "x", "z" }, "y");
                case "A05": return Recipes.Empty(new[] { "prediction", "target" }, "loss");
                case "A06": return Recipes.Empty(new[] { "x", "z" }, "yA", "yB");
                case "A07": return Recipes.Empty(new[] { "x", "z", "target" }, "y", "loss");
                case "B02": case "B03": return Recipes.Empty(new[] { "a", "b", "g" }, "da", "db");
                case "B04": return Recipes.Chain();
                case "B05": return Recipes.Shared();
                case "B06": return Recipes.Empty(new[] { "w", "g", "eta" }, "newW");
                case "B07": return ProfileStore.Copy(profile.optimizer);
                case "B09":
                    var machine = ProfileStore.Copy(profile.Graph("A07"));
                    foreach (var p in new GraphEngine(profile.modules).Parameters(machine)) p.Value = 0;
                    return machine;
                default: return new GraphSpec();
            }
        }
        public static NodeKind[] Palette(string id)
        {
            if (id == "A01") return new[] { NodeKind.Constant };
            if (id == "B02") return new NodeKind[0];
            if (id == "A02" || id == "B03") return new[] { NodeKind.Parameter, NodeKind.Multiply };
            if (id == "A03") return new[] { NodeKind.Parameter, NodeKind.Add };
            return new[] { NodeKind.Constant, NodeKind.Parameter, NodeKind.Add, NodeKind.Multiply };
        }
        public static CheckResult Check(string id, GraphSpec graph, Profile profile)
        {
            try
            {
                var engine = new GraphEngine(profile.modules); var result = new CheckResult { passed = true };
                if (id == "A07" && !CheckAverage(profile.average).passed) return CheckResult.Fail("先完成四个误差读数的平均器，再校准整台机器。");
                if (id == "B02" || id == "B03") return CheckRule(graph, id == "B03");
                if (id == "B06") return CheckOptimizer(graph);
                if (id == "A05")
                {
                    foreach (var pair in new[] { new[] { 0.0, 0.0 }, new[] { 2.0, 1.0 }, new[] { 0.0, 1.0 }, new[] { 3.0, -1.0 }, new[] { -1.0, 3.0 } })
                    {
                        var e = engine.Evaluate(graph, new Dictionary<string, double> { { "prediction", pair[0] }, { "target", pair[1] } });
                        double want = .5 * Math.Pow(pair[0] - pair[1], 2); bool good = Math.Abs(e.Output("loss") - want) < 1e-8;
                        result.passed &= good; result.rows.Add("残差 " + (pair[0] - pair[1]) + " → " + e.Output("loss") + " / " + want + (good ? " ✓" : " ×"));
                    }
                    result.message = result.passed ? "零残差、正负同幅残差和大误差通过检查。" : "半平方误差的数值不符合规格。"; return result;
                }
                foreach (var sample in Samples().Concat(Samples(true)))
                {
                    var args = sample.Inputs(); double expected = sample.target; string output = "y";
                    if (id == "A01") expected = sample.x;
                    if (id == "A02") expected = 2 * sample.x;
                    if (id == "A03") expected = sample.x + .75;
                    if (id == "A04" || id == "A06") expected = 2 * sample.x - sample.z + .5;
                    if (id == "A05") { output = "loss"; expected = .5 * Math.Pow(sample.x - sample.target, 2); }
                    if (id == "A06") output = "yA";
                    var e = engine.Evaluate(graph, args);
                    double actual = e.Output(output);
                    bool good = Math.Abs(actual - expected) < (id == "B09" ? .08 : .035);
                    result.rows.Add(string.Format("x {0:0.00}  z {1:0.00}  → {2:0.000} / {3:0.000} {4}", sample.x, sample.z, actual, expected, good ? "✓" : "×"));
                    result.passed &= good;
                    if (id == "A06") result.passed &= e.parameters.Count >= 6 && Math.Abs(e.Output("yB") - (-sample.x + 2 * sample.z + 1)) < .035;
                    if (id == "A07" || id == "B09") result.passed &= Math.Abs(e.Output("loss") - .5 * Math.Pow(actual - sample.target, 2)) < .0001;
                    if (id == "A02" || id == "A03") result.passed &= e.parameters.Count > 0;
                    if (id == "A04") result.passed &= e.parameters.Count >= 3;
                }
                if (id == "A06")
                {
                    var instances = graph.nodes.Where(n => n.kind == NodeKind.Module).ToList();
                    if (instances.Count < 2) return CheckResult.Fail("需要放置两个已封装的模块实例。");
                    var slots = engine.Parameters(graph); if (slots.Count < 6) return CheckResult.Fail("两个盒子各自需要独立的三个参数。");
                    // Mutating one instance must leave the second output unchanged; restore the original value even on errors.
                    var p = slots.Find(s => s.key.StartsWith(instances[0].id + "/"));
                    var input = Samples()[0].Inputs(); double beforeA = engine.Evaluate(graph, input).Output("yA"), beforeB = engine.Evaluate(graph, input).Output("yB"), old = p.Value;
                    try { p.Value += .17; var after = engine.Evaluate(graph, input); bool aChanged = Math.Abs(after.Output("yA") - beforeA) > 1e-6, bChanged = Math.Abs(after.Output("yB") - beforeB) > 1e-6; result.passed &= aChanged != bChanged; }
                    finally { p.Value = old; }
                }
                if (id == "A07" || id == "B09")
                {
                    var args = Samples()[0].Inputs(); var baseline = engine.Evaluate(graph, args);
                    var active = engine.Parameters(graph).Where(p => baseline.parameters.ContainsKey(p.key)).Take(3).ToList();
                    if (active.Count == 0) return CheckResult.Fail("模型必须有实际参与计算的参数。");
                    foreach (var p in active)
                    {
                        double old = p.Value;
                        try
                        {
                            p.Value += .33; var changed = engine.Evaluate(graph, args);
                            double actualLoss = changed.Output("loss"), expectedLoss = .5 * Math.Pow(changed.Output("y") - args["target"], 2);
                            if (Math.Abs(actualLoss - expectedLoss) >= 1e-8)
                                return CheckResult.Fail("参数改变后，误差计没有继续跟踪实际预测。loss=" + actualLoss.ToString("0.#####") + "，应为 " + expectedLoss.ToString("0.#####") + "。请将同一模型输出接入半平方误差计。");
                        }
                        finally { p.Value = old; }
                    }
                }
                if (id == "B09") result.passed &= CheckRule(profile.addRule, false).passed && CheckRule(profile.multiplyRule, true).passed && CheckOptimizer(profile.optimizer).passed;
                result.message = result.passed ? "全部校准样本与新输入通过。" : "存在未达标的输出。检查线路与参数；表格右侧是目标值。";
                return result;
            }
            catch (Exception e) { return CheckResult.Fail(e.Message); }
        }
        public static CheckResult CheckRule(GraphSpec graph, bool multiply)
        {
            if (graph == null) return CheckResult.Fail("反向规则尚未保存。");
            var result = new CheckResult { passed = true };
            try
            {
                foreach (var v in new[] { new[] { 2.0, 3.0, 1.0 }, new[] { -2.0, .5, -3.0 }, new[] { 0.0, -4.0, .25 }, new[] { 3.0, 0.0, 2.0 } })
                {
                    var e = new GraphEngine().Evaluate(graph, new Dictionary<string, double> { { "a", v[0] }, { "b", v[1] }, { "g", v[2] } });
                    double da = multiply ? v[2] * v[1] : v[2], db = multiply ? v[2] * v[0] : v[2];
                    bool pass = Math.Abs(e.Output("da") - da) < 1e-8 && Math.Abs(e.Output("db") - db) < 1e-8;
                    result.passed &= pass; result.rows.Add(string.Format("a {0} b {1} g {2} → {3:0.###}, {4:0.###} {5}", v[0], v[1], v[2], e.Output("da"), e.Output("db"), pass ? "✓" : "×"));
                }
                result.message = result.passed ? "规则通过正数、负数、零与非单位上游梯度检查。" : "回流数值不符合局部反向规则。";
            }
            catch (Exception e) { return CheckResult.Fail(e.Message); }
            return result;
        }
        public static CheckResult CheckOptimizer(GraphSpec graph)
        {
            if (graph == null) return CheckResult.Fail("更新器尚未保存。");
            var result = new CheckResult { passed = true };
            try
            {
                foreach (var v in new[] { new[] { 2.0, 3.0, .1 }, new[] { -1.0, -2.0, .25 }, new[] { .7, 0.0, .4 } })
                {
                    var e = new GraphEngine().Evaluate(graph, new Dictionary<string, double> { { "w", v[0] }, { "g", v[1] }, { "eta", v[2] } });
                    double want = v[0] - v[1] * v[2]; bool good = Math.Abs(e.Output("newW") - want) < 1e-8;
                    result.passed &= good; result.rows.Add(string.Format("w {0} g {1} η {2} → {3:0.###} / {4:0.###} {5}", v[0], v[1], v[2], e.Output("newW"), want, good ? "✓" : "×"));
                }
                result.message = result.passed ? "更新器符合 w′ = w − ηg。" : "参数更新方向或步长不正确。";
            }
            catch (Exception e) { return CheckResult.Fail(e.Message); }
            return result;
        }
        public static CheckResult CheckAverage(GraphSpec graph)
        {
            if (graph == null) return CheckResult.Fail("平均器尚未搭建。");
            try
            {
                foreach (var v in new[] { new[] { 1.0, 2.0, 3.0, 4.0 }, new[] { 0.0, 0.0, 0.0, 8.0 }, new[] { 1.3, .2, .7, 0.0 }, new[] { -2.0, 1.0, 0.0, -.5 } })
                {
                    var args = new Dictionary<string, double>(); for (int i = 0; i < 4; i++) args["L" + (i + 1)] = v[i];
                    if (Math.Abs(new GraphEngine().Evaluate(graph, args).Output("mean") - v.Average()) > 1e-8) return CheckResult.Fail("将四个读数相加，再乘以 ¼。单个读数不能代表整批误差。");
                }
                return new CheckResult { passed = true, message = "四个读数的平均器已校准。现在可以装配模型与误差计。" };
            }
            catch (Exception e) { return CheckResult.Fail(e.Message); }
        }
    }
}
