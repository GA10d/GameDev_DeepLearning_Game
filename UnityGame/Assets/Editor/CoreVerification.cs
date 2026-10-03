using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using LearningFoundry.Core;
using UnityEngine;

namespace LearningFoundry.Editor
{
    public static class CoreVerification
    {
        static readonly List<string> checks = new List<string>();
        static void Require(bool condition, string name) { if (!condition) throw new Exception("VERIFY FAILED: " + name); checks.Add(name); }
        static void Near(double a, double b, string name) { Require(Math.Abs(a - b) < 1e-7, name); }
        public static void Run()
        {
            checks.Clear(); var profile = new Profile { addRule = Recipes.Rule(false), multiplyRule = Recipes.Rule(true), optimizer = Recipes.Optimizer(), average = Recipes.Average(), reverseOrder = true, accumulate = true };
            var campaign = JsonUtility.FromJson<Campaign>(Resources.Load<TextAsset>("Campaign/levels").text);
            Require(campaign.levels.Length == 55, "55 campaign nodes loaded");
            Require(Missions.Implemented.Length == 55, "55 playable levels registered");
            Require(campaign.levels.Select(l => l.id).Distinct().Count() == 55, "campaign ids unique");
            foreach (var id in Missions.Implemented)
                Require(campaign.levels.First(l => l.id == id).deps.All(Missions.Implemented.Contains), id + " prerequisites implemented");
            Require(Missions.CheckRule(profile.addRule, false).passed, "addition player rule positive negative zero nonunit g");
            Require(Missions.CheckRule(profile.multiplyRule, true).passed, "multiplication player rule positive negative zero nonunit g");
            Require(Missions.CheckOptimizer(profile.optimizer).passed, "player SGD update signs and zero gradient");
            Require(Missions.CheckAverage(profile.average).passed, "player average four values including signed gradients");
            foreach (var id in new[] { "A01", "A02", "A03" })
            {
                var g = Recipes.Empty(new[] { "x" }, "y"); var x = g.nodes[0]; string result = x.id;
                if (id != "A01") { var parameter = g.Add(NodeKind.Parameter, "calibration", id == "A02" ? 2 : .75); result = g.Add(id == "A02" ? NodeKind.Multiply : NodeKind.Add, "op", 0, x.id, parameter.id).id; }
                g.Output("y").inputs.Add(result); Require(Missions.Check(id, g, profile).passed, id + " valid circuit passes");
                g.Output("y").inputs[0] = g.Add(NodeKind.Constant, "constant cheat", 0).id; Require(!Missions.Check(id, g, profile).passed, id + " constant cheat rejected");
            }
            var affine = Recipes.Affine(2, -1, .5); Require(Missions.Check("A04", affine, profile).passed, "A04 weighted sum three effective parameters");
            var loss = Recipes.Empty(new[] { "prediction", "target" }); Recipes.AttachLoss(loss, loss.nodes[0].id, loss.nodes[1].id);
            Require(Missions.Check("A05", loss, profile).passed, "A05 loss positive negative zero residuals");
            profile.modules.Add(new ModuleSpec { id = "affine", name = "Mixer", graph = ProfileStore.Copy(affine), inputNames = new List<string> { "x", "z" }, outputName = "y" });
            var dual = Recipes.Empty(new[] { "x", "z" }, "yA", "yB");
            var left = dual.Add(NodeKind.Module, "A", 0, dual.nodes[0].id, dual.nodes[1].id); left.moduleId = "affine";
            var right = dual.Add(NodeKind.Module, "B", 0, dual.nodes[0].id, dual.nodes[1].id); right.moduleId = "affine";
            dual.Output("yA").inputs.Add(left.id); dual.Output("yB").inputs.Add(right.id);
            var engine = new GraphEngine(profile.modules, profile.Rules()); var slots = engine.Parameters(dual);
            double[] targetB = { -1, 2, 1 }; int j = 0; foreach (var p in slots.Where(p => p.key.StartsWith(right.id + "/"))) p.Value = targetB[j++];
            Require(Missions.Check("A06", dual, profile).passed, "A06 two independently calibrated boxes pass");
            var clone = ProfileStore.Copy(dual); Require(Missions.Check("A06", clone, profile).passed, "module parameters survive JSON serialization");
            var model = Recipes.Affine(1.75, .5, -.25, true); Require(Missions.Check("A07", model, profile).passed, "A07 composed model loss and player average");
            var fakeLoss = ProfileStore.Copy(model); fakeLoss.Output("loss").inputs[0] = fakeLoss.Add(NodeKind.Constant, "fake zero loss", 0).id;
            Require(!Missions.Check("A07", fakeLoss, profile).passed, "A07 constant loss rejected after parameter perturbation");
            Require(!Missions.Check("B09", fakeLoss, profile).passed, "B09 constant loss rejected even at perfectly fitted weights");
            var chain = Recipes.Chain(); var args = Missions.Samples()[0].Inputs();
            var e = engine.Evaluate(chain, args); e.Backward("goal");
            foreach (var p in engine.Parameters(chain)) FiniteDifference(engine, chain, args, e, p, "goal");
            Require(e.tape.Any(t => Math.Abs(t.gradient) > 0), "B04 reverse propagates through chain");
            var wrong = engine.Evaluate(chain, args); wrong.Backward("goal", false);
            Require(wrong.parameters.Values.Any(t => Math.Abs(t.gradient - e.parameters[t.key].gradient) > .01), "B04 forward order loses dependent gradient");
            foreach (bool square in new[] { false, true })
            {
                var g = Recipes.Shared(square); var ev = engine.Evaluate(g, args); ev.Backward("goal");
                foreach (var p in engine.Parameters(g)) FiniteDifference(engine, g, args, ev, p, "goal");
                var overwritten = engine.Evaluate(g, args); overwritten.accumulate = false; overwritten.Backward("goal");
                Require(overwritten.parameters.Values.Any(v => Math.Abs(v.gradient - ev.parameters[v.key].gradient) > .01), square ? "B05 repeated operand contributions sum" : "B05 branch contributions sum");
            }
            double w = -2; for (int i = 0; i < 20; i++) w = new GraphEngine().Evaluate(profile.optimizer, new Dictionary<string, double> { { "w", w }, { "g", w - 2 }, { "eta", .5 } }).Output("newW");
            Require(.5 * Math.Pow(w - 2, 2) < .0001, "B07 stable rate converges within twenty steps");
            w = -2; for (int i = 0; i < 20; i++) w = new GraphEngine().Evaluate(profile.optimizer, new Dictionary<string, double> { { "w", w }, { "g", w - 2 }, { "eta", 2.1 } }).Output("newW");
            Require(.5 * Math.Pow(w - 2, 2) > 8, "B07 large rate diverges on same objective");
            var reference = Train(profile, new[] { "forward", "clear", "backward", "update" }, 12);
            var alternate = Train(profile, new[] { "clear", "forward", "backward", "update" }, 12);
            Near(reference.loss, alternate.loss, "B08 valid equivalent phase ordering accepted");
            bool rejected = false; try { Train(profile, new[] { "forward", "backward", "update", "clear" }, 1); } catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "B08 missing current-round gradient reset rejected");
            var bad = Train(profile, new[] { "forward", "clear", "update", "backward" }, 0);
            rejected = false; try { bad.Tick(); } catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "B08 update before current backward rejected");
            var training = Train(profile, new[] { "forward", "clear", "backward", "update" }, 180);
            Require(training.loss < .002, "B09 CPU training reaches loss target with player rules and average");
            Require(Missions.Check("B09", training.graph, profile).passed, "B09 unseen inputs never used for updates pass");
            var serial = ProfileStore.Copy(training.graph); Require(Missions.Check("B09", serial, profile).passed, "trained weights survive save/load graph roundtrip");
            var initial = Recipes.Affine(0, 0, 0, true); var borrowed = new TrainingSession(initial, null, null, null, Missions.Samples()) { learningRate = .2 };
            double before = borrowed.loss; for (int i = 0; i < 80; i++) borrowed.Tick(); Require(borrowed.loss < .02 && borrowed.loss < before, "P00 borrowed machine really improves on same samples");
            var cycle = Recipes.Empty(new[] { "x" }, "y"); var cyclic = cycle.Add(NodeKind.Add, "cycle"); cyclic.inputs.Add(cyclic.id); cyclic.inputs.Add(cycle.nodes[0].id); cycle.Output("y").inputs.Add(cyclic.id);
            rejected = false; try { new GraphEngine().Evaluate(cycle, args); } catch (InvalidOperationException) { rejected = true; } Require(rejected, "cyclic graph fails gracefully");
            rejected = false; try { new GraphEngine().Evaluate(Recipes.Empty(new[] { "x" }, "y"), args); } catch (InvalidOperationException) { rejected = true; } Require(rejected, "disconnected output fails gracefully");
            var repeated = engine.Evaluate(training.graph, args); repeated.Backward("loss"); var first = repeated.parameters.Values.Select(v => v.gradient).ToArray(); repeated.Backward("loss"); Require(first.SequenceEqual(repeated.parameters.Values.Select(v => v.gradient)), "repeated backward clears tape gradients");
            var report = "{\"passed\":true,\"checks\":" + checks.Count + ",\"trainingSteps\":180,\"finalLoss\":" + training.loss.ToString("R", CultureInfo.InvariantCulture) + ",\"heldOutInputs\":3,\"names\":[" + string.Join(",", checks.Select(c => "\"" + c.Replace("\"", "'") + "\"")) + "]}";
            Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/core-verification.json", report);
            Debug.Log("LEARNING_FOUNDRY_VERIFY_PASSED " + checks.Count + " checks; loss=" + training.loss.ToString("R"));
        }
        static TrainingSession Train(Profile p, string[] phases, int steps)
        {
            var training = new TrainingSession(Recipes.Affine(0, 0, 0, true), p.modules, p.Rules(), p.optimizer, Missions.Samples(), p.average) { phases = phases, learningRate = .1 };
            for (int i = 0; i < steps; i++) training.Tick(); return training;
        }
        static void FiniteDifference(GraphEngine engine, GraphSpec g, Dictionary<string, double> args, Evaluation e, ParameterSlot p, string output)
        {
            double old = p.Value, plus, minus;
            try { p.Value = old + 1e-5; plus = engine.Evaluate(g, args).Output(output); p.Value = old - 1e-5; minus = engine.Evaluate(g, args).Output(output); } finally { p.Value = old; }
            Near(e.parameters[p.key].gradient, (plus - minus) / 2e-5, "gradient finite difference " + p.name + " / " + output);
        }
    }
}
