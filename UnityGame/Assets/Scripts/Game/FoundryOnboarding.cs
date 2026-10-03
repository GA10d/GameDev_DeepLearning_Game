using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using LearningFoundry.Core;
using LearningFoundry.UI;
using UnityEngine;
using UnityEngine.UI;

namespace LearningFoundry.Game
{
    public sealed partial class FoundryGame
    {
        const int OnboardingVersion = 1;
        bool roadmapOpen, roadmapFromLevel, guideReplay, baselineCaptured, comparisonCaptured;
        int baselineSample, guideState = -99;
        double baselinePrediction, baselineTarget, comparisonPrediction;
        float trainingClock;
        RectTransform guidanceLayer;
        readonly List<RectTransform> guideShutters = new List<RectTransform>();
        readonly List<RectTransform> guideBorders = new List<RectTransform>();
        Text guideTitle, guideBody, guideMetrics, guideAction;

        void CloseCurrentOverlay()
        {
            if (activeOverlay) { activeOverlay.gameObject.SetActive(false); Destroy(activeOverlay.gameObject); }
            activeOverlay = null; roadmapOpen = false;
        }

        void MaybeShowRoadmap()
        {
            if (profile.onboardingVersion < OnboardingVersion) ShowRoadmap();
        }

        void ShowRoadmap()
        {
            running = false; Save(); CloseCurrentOverlay();
            roadmapFromLevel = level != null;
            var overlay = Overlay(); roadmapOpen = true;
            if (guidanceLayer) guidanceLayer.gameObject.SetActive(false);
            var page = Hardware.Plate(overlay, "Learning Route Blueprint", 76, 65, 1448, 770, Style.Paper);
            Hardware.Shape(page.transform, "Route Clip", 630, -15, 188, 27, HardwareShape.Chip, Hardware.Metal);
            Style.Number(page.transform, "FOUNDRY / APPRENTICE ROUTE / 01", 29, 22, 1190, 28, 16, Style.Muted);
            Style.Text(page.transform, "亲手造一台会学习的机器", 28, 65, 1340, 58, 36);
            Style.Text(page.transform, "先看它怎样学，再从最小的计算部件开始搭。最后，把它变成能识别手写数字的机器。", 30, 140, 1378, 48, 21);
            var names = new[] { "看机器学会", "造计算部件", "造学习引擎", "造神经网络", "检验数据", "识别手写数字" };
            var ids = new[] { "P00", "A01–A07", "B01–B09", "C–D", "E", "F–G" };
            var activities = new[] { "送入样本\n训练后再比较", "接线、加法、乘法\n参数、误差与封装", "让误差指明方向\n反向传播与更新", "加入非线性\n从数扩展到张量", "分批训练\n用新数据检验", "拼接卷积与池化\n完成数字分类" };
            int current = RouteStation();
            Style.Box(page.transform, "Route Wire", 126, 222, 1180, 4, Style.Muted).raycastTarget = false;
            for (int i = 0; i < names.Length; i++)
            {
                float x = 28 + i * 234;
                Hardware.Shape(page.transform, "Station Socket " + i, x + 87, 204, 38, 38, HardwareShape.Socket, i == current ? Style.Brass : Hardware.Metal);
                var station = Hardware.Plate(page.transform, "Route Station " + i, x, 261, 212, 264, i < 3 ? Hardware.Enamel : Hardware.Metal);
                Style.Number(station.transform, "0" + (i + 1) + " / " + ids[i], 15, 14, 184, 28, 14);
                Style.Text(station.transform, names[i], 15, 49, 184, 41, 23);
                DrawRouteSymbol(station.transform, i);
                Style.Text(station.transform, activities[i], 15, 154, 183, 76, 18);
                Style.Text(station.transform, i == current ? "● 你在这里" : "本版可玩", 15, 232, 183, 28, 15, i == current ? Style.Brass : Style.Muted);
            }
            Style.Text(page.transform, "每份委托：读目标  →  搭部件 / 做实验  →  看实际结果  →  验收并解锁", 31, 554, 1375, 36, 22);
            string job = roadmapFromLevel ? "你正在做 " + level.id + "：" + level.name + "。回到设备后，按当前委托目标继续。"
                : profile.completed.Contains("P00") ? "你已完成入职实验。继续当前委托，或重看第一关的操作。"
                : "第一份委托准备好了现成设备。先送入一个样本，再启动训练，看看预测发生什么变化。";
            Style.Text(page.transform, job, 31, 610, 1375, 52, 21);
            Style.Button(page.transform, "重看第一关操作", 30, 696, 268, 49, ReplayFirstLesson);
            Style.Button(page.transform, roadmapFromLevel ? "暂时收起" : "稍后再看", 856, 696, 210, 49, () => FinishRoadmap(false));
            string enter = roadmapFromLevel ? "回到这台设备  →" : profile.completed.Contains("P00") ? "继续当前委托  →" : "进入第一份委托  →";
            Style.Button(page.transform, enter, 1083, 688, 333, 57, () => FinishRoadmap(true), Style.Brass);
        }

        int RouteStation()
        {
            string id = level != null ? level.id : profile.currentLevel;
            if (id.StartsWith("B", StringComparison.Ordinal)) return 2;
            if (id.StartsWith("A", StringComparison.Ordinal)) return 1;
            return 0;
        }

        static void DrawRouteSymbol(Transform parent, int index)
        {
            // Original, code-drawn schematic symbols; no borrowed game art.
            var plate = Style.Box(parent, "Schematic", 16, 98, 180, 47, Style.Frame);
            plate.raycastTarget = false;
            string[] symbols = { "0 → 1", "+  ×", "← g", "● → ●", "4 / 3", "0 1 2 3" };
            Style.Text(plate.transform, symbols[index], 8, 0, 164, 47, 25, Hardware.Phosphor, TextAnchor.MiddleCenter);
        }

        void FinishRoadmap(bool enter)
        {
            bool fromLevel = roadmapFromLevel;
            profile.onboardingVersion = OnboardingVersion; Save(); CloseCurrentOverlay();
            if (enter && !fromLevel) OpenLevel(profile.completed.Contains("P00") ? profile.currentLevel : "P00");
            else RefreshGuide();
        }

        void ReplayFirstLesson()
        {
            profile.onboardingVersion = OnboardingVersion; profile.p00GuideHidden = false;
            Save(); CloseCurrentOverlay(); OpenLevel("P00"); guideReplay = true; RefreshGuide();
        }

        void ResetGuideSession()
        {
            guideReplay = baselineCaptured = comparisonCaptured = false;
            baselineSample = 0; baselinePrediction = baselineTarget = comparisonPrediction = 0; trainingClock = 0;
        }

        bool GuideEnabled { get { return level != null && level.id == "P00" && !profile.p00GuideHidden && (guideReplay || !profile.completed.Contains("P00")); } }
        int GuideStep
        {
            get
            {
                if (!baselineCaptured) return 0;
                if (training == null || training.step < 8 || running || training.loss >= .02) return 1;
                return comparisonCaptured ? 3 : 2;
            }
        }

        void ObserveButton()
        {
            // Use exactly the baseline input for the tutorial comparison. Ordinary sampling stays unchanged.
            if (GuideEnabled && GuideStep == 2) sampleIndex = baselineSample;
            Observe(); RefreshGuide();
        }

        void GuideOnObservation(Evaluation value, Dictionary<string, double> inputs)
        {
            if (!GuideEnabled || !value.outputs.ContainsKey("y")) return;
            if (training == null || training.step == 0)
            {
                baselineCaptured = true; baselineSample = sampleIndex; baselinePrediction = value.Output("y"); baselineTarget = inputs["target"];
            }
            else if (baselineCaptured && !running && training.step >= 8 && training.loss < .02 && sampleIndex == baselineSample)
            {
                comparisonCaptured = true; comparisonPrediction = value.Output("y");
            }
        }

        void ClearGuide()
        {
            if (guidanceLayer) { guidanceLayer.gameObject.SetActive(false); Destroy(guidanceLayer.gameObject); }
            guidanceLayer = null; guideShutters.Clear(); guideBorders.Clear(); guideState = -99;
        }

        void BuildGuide()
        {
            guidanceLayer = Style.Rect(canvas.transform, "Apprentice Guidance", 0, 0, 1600, 900);
            for (int i = 0; i < 4; i++)
            {
                var shutter = Style.Box(guidanceLayer, "Focus Shade " + i, 0, 0, 1, 1, new Color(.04f, .07f, .05f, .38f));
                shutter.raycastTarget = false; guideShutters.Add(shutter.rectTransform);
                var border = Style.Box(guidanceLayer, "Focus Edge " + i, 0, 0, 1, 1, new Color(.88f, .69f, .35f));
                border.raycastTarget = false; guideBorders.Add(border.rectTransform);
            }
            var note = Hardware.Plate(guidanceLayer, "Apprentice Work Order", 40, 395, 578, 335, Style.Paper);
            Hardware.Shape(note.transform, "Order Tape", 219, -9, 138, 19, HardwareShape.Tape, Hardware.Enamel);
            Style.Number(note.transform, "BENCH 01 / FIRST EXPERIMENT", 22, 14, 532, 25, 14, Style.Muted);
            guideTitle = Style.Text(note.transform, "", 21, 47, 537, 44, 27);
            guideBody = Style.Text(note.transform, "", 22, 105, 532, 93, 21);
            guideMetrics = Style.Text(note.transform, "", 22, 208, 532, 70, 17, Style.Muted);
            guideAction = Style.Text(note.transform, "", 22, 292, 323, 28, 17, Style.Brass);
            Style.Button(note.transform, "收起指引", 386, 292, 170, 31, () => { profile.p00GuideHidden = true; Save(); ClearGuide(); });
        }

        void RefreshGuide()
        {
            if (!GuideEnabled || board == null) { if (guidanceLayer) ClearGuide(); return; }
            if (activeOverlay) { if (guidanceLayer) guidanceLayer.gameObject.SetActive(false); return; }
            if (!guidanceLayer) BuildGuide();
            guidanceLayer.gameObject.SetActive(true);
            int step = GuideStep;
            if (step != guideState)
            {
                guideState = step;
                if (step == 1) { selected = null; Instruments(); inspector.gameObject.SetActive(true); }
            }
            string[] titles = { "1 / 4  先看机器的预测", "2 / 4  让机器从样本中学习", "3 / 4  用同一输入再比较", "4 / 4  验收，开始亲手搭建" };
            guideTitle.text = titles[step];
            guideMetrics.text = "";
            string target = "送入样本";
            if (step == 0)
            {
                guideBody.text = "这台机器已经接好了，但还没学会。\n点亮框中的‘送入样本’，记录它现在的预测。";
                guideAction.text = "↓ 点击亮框中的真实按钮";
                guideMetrics.text = "这一关只体验训练；后面的委托教你搭出它。";
            }
            else if (step == 1)
            {
                guideMetrics.text = "训练前预测 " + F(baselinePrediction) + "   /   目标 " + F(baselineTarget);
                if (running)
                {
                    guideBody.text = "机器正在用样本调整参数旋钮。\n看亮框中的曲线：平均损失越低，整组预测越接近目标。";
                    guideAction.text = "正在训练 · " + training.step + " / " + trainingBudget + " 步";
                    guideMetrics.text += "\n当前训练平均损失 " + F(training.loss);
                    target = "Portable Oscilloscope";
                }
                else
                {
                    guideBody.text = "点‘启动 / 继续训练’。机器会做一轮轮小调整。\n等训练结束，再检查预测；暂时不用修改线路。";
                    guideAction.text = "→ 点击亮框启动训练";
                    target = inspector.gameObject.activeSelf ? "启动 / 继续训练" : "控制盒 / Tab";
                }
            }
            else if (step == 2)
            {
                guideBody.text = "训练后的平均误差已达标。再点‘送入样本’，\n指引会送回刚才的同一输入，让前后结果可以直接比较。";
                guideMetrics.text = "训练前预测 " + F(baselinePrediction) + "   /   目标 " + F(baselineTarget) + "\n已训练 " + training.step + " 步 · 平均损失 " + F(training.loss);
                guideAction.text = "↓ 送回同一个样本";
            }
            else
            {
                double before = Math.Abs(baselinePrediction - baselineTarget), after = Math.Abs(comparisonPrediction - baselineTarget);
                guideBody.text = (after < before - .00001 ? "同一个输入，预测更接近目标了。" : "同一个输入，检查预测与目标的距离。") + "\n点‘验收设备’完成委托。下一关从最简单的接线开始。";
                guideMetrics.text = "预测 " + F(baselinePrediction) + " → " + F(comparisonPrediction) + "   /   目标 " + F(baselineTarget) + "\n与目标的距离 " + F(before) + " → " + F(after);
                guideAction.text = "↓ 验收后解锁 A01"; target = "验收设备";
            }
            var focus = screen.GetComponentsInChildren<RectTransform>().FirstOrDefault(r => r.name == target && r.gameObject.activeInHierarchy);
            if (focus) PositionGuideFocus(focus);
        }

        static string F(double value) { return value.ToString("0.####", CultureInfo.InvariantCulture); }
        void PositionGuideFocus(RectTransform target)
        {
            var corners = new Vector3[4]; target.GetWorldCorners(corners);
            var a = guidanceLayer.InverseTransformPoint(corners[1]); var b = guidanceLayer.InverseTransformPoint(corners[3]);
            float x = Mathf.Clamp(a.x - 7, 0, 1600), y = Mathf.Clamp(-a.y - 7, 0, 900);
            float right = Mathf.Clamp(b.x + 7, x, 1600), bottom = Mathf.Clamp(-b.y + 7, y, 900);
            Place(guideShutters[0], 0, 0, 1600, y); Place(guideShutters[1], 0, bottom, 1600, 900 - bottom);
            Place(guideShutters[2], 0, y, x, bottom - y); Place(guideShutters[3], right, y, 1600 - right, bottom - y);
            Place(guideBorders[0], x, y, right - x, 3); Place(guideBorders[1], x, bottom - 3, right - x, 3);
            Place(guideBorders[2], x, y, 3, bottom - y); Place(guideBorders[3], right - 3, y, 3, bottom - y);
        }
        static void Place(RectTransform rect, float x, float y, float w, float h)
        { rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h); }

        IEnumerator SmokeOnboarding(string output, Action<bool> complete)
        {
            var report = new OnboardingReport();
            report.freshLaunchShowsRoute = roadmapOpen && activeOverlay;
            yield return new WaitForEndOfFrame(); CaptureNative(Path.Combine(output, "09-first-run-route.png"));
            Press("稍后再看"); yield return null;
            var persisted = JsonUtility.FromJson<Profile>(JsonUtility.ToJson(profile));
            report.skipPersistsWithoutUnlock = persisted.onboardingVersion == OnboardingVersion && persisted.completed.Count == 0;
            MainMenu(); yield return null;
            report.routeShownOnce = !activeOverlay;
            yield return new WaitForEndOfFrame(); CaptureNative(Path.Combine(output, "01-main-menu.png"));
            Map(); yield return new WaitForEndOfFrame(); CaptureNative(Path.Combine(output, "02-campaign.png"));
            MainMenu(); Press("学习路线 / 新手指引"); yield return null;
            report.routeCanBeReopened = roadmapOpen;
            Press("进入第一份委托  →"); yield return null;
            Press("启动 / 继续训练"); RefreshGuide();
            report.noTrainingBeforeObservation = training == null && GuideStep == 0 && profile.completed.Count == 0;
            yield return new WaitForEndOfFrame(); CaptureNative(Path.Combine(output, "10-guide-observe.png"));
            Press("送入样本"); RefreshGuide();
            report.observationAdvancesGuide = beforeObserved && baselineCaptured && GuideStep == 1;
            string priorGraph = JsonUtility.ToJson(graph);
            Press("收起指引"); yield return null;
            report.guideSkipKeepsWork = profile.p00GuideHidden && !guidanceLayer && JsonUtility.ToJson(graph) == priorGraph;
            Notebook(); Press("学习路线 / 新手指引"); yield return null;
            Press("重看第一关操作"); yield return null;
            report.guideReplayKeepsWork = GuideEnabled && GuideStep == 0 && JsonUtility.ToJson(graph) == priorGraph;
            Press("送入样本"); RefreshGuide();
            yield return new WaitForEndOfFrame(); CaptureNative(Path.Combine(output, "11-guide-train.png"));
            Press("启动 / 继续训练");
            float deadline = Time.realtimeSinceStartup + 12;
            while (running && training.step < 20 && Time.realtimeSinceStartup < deadline) yield return null;
            yield return new WaitForEndOfFrame(); CaptureNative(Path.Combine(output, "12-guide-learning.png"));
            while (running && Time.realtimeSinceStartup < deadline) yield return null;
            RefreshGuide();
            report.realTrainingReachesComparison = training != null && training.step == 80 && !running && GuideStep == 2 && !profile.completed.Contains("P00");
            report.trainingSteps = training == null ? 0 : training.step;
            report.trainingLoss = training == null ? -1 : training.loss;
            report.learningRateMatchesControl = training != null && training.learningRate == eta && eta == .2;
            yield return new WaitForEndOfFrame(); CaptureNative(Path.Combine(output, "13-guide-compare.png"));
            Press("验收设备"); yield return null;
            report.checkRequiresComparison = !profile.completed.Contains("P00") && !activeOverlay;
            Press("下一样本"); RefreshGuide();
            bool differentInputIsNotComparison = GuideStep == 2 && !comparisonCaptured;
            Press("送入样本"); RefreshGuide();
            report.sameInputCompared = differentInputIsNotComparison && sampleIndex == baselineSample && GuideStep == 3 && afterObserved;
            report.errorBefore = Math.Abs(baselinePrediction - baselineTarget);
            report.errorAfter = Math.Abs(comparisonPrediction - baselineTarget);
            report.actualPredictionImproves = report.errorAfter < report.errorBefore;
            yield return new WaitForEndOfFrame(); CaptureNative(Path.Combine(output, "14-guide-check.png"));
            Press("启动 / 继续训练"); RefreshGuide();
            report.newTrainingRequiresNewComparison = running && !comparisonCaptured && GuideStep == 1;
            Press("暂停"); RefreshGuide();
            report.newTrainingRequiresNewComparison &= GuideStep == 2;
            Press("送入样本"); RefreshGuide();
            report.newTrainingRequiresNewComparison &= GuideStep == 3;
            // Old-version JSON has neither new field; deserialization must preserve earned progress and graph values.
            var old = new Profile { currentLevel = "A02", onboardingVersion = 77, p00GuideHidden = true };
            old.completed.AddRange(new[] { "P00", "A01" });
            old.Graph("A02").Add(NodeKind.Parameter, "saved test knob", .137);
            string oldGraph = JsonUtility.ToJson(old.Graph("A02"));
            string legacy = JsonUtility.ToJson(old).Replace("\"onboardingVersion\":77,", "").Replace("\"p00GuideHidden\":true,", "");
            var migrated = JsonUtility.FromJson<Profile>(legacy);
            report.legacyProgressPreserved = migrated.onboardingVersion == 0 && !migrated.p00GuideHidden && migrated.currentLevel == "A02" && migrated.completed.SequenceEqual(old.completed) && JsonUtility.ToJson(migrated.Graph("A02")) == oldGraph;
            var actual = profile; profile = migrated; MainMenu(); yield return null;
            report.returningPlayerKeepsCurrentJob = roadmapOpen && RouteStation() == 1;
            Press("继续当前委托  →"); yield return null;
            report.returningPlayerKeepsCurrentJob &= level.id == "A02" && !guidanceLayer && JsonUtility.ToJson(graph) == oldGraph;
            profile = actual; OpenLevel("P00");
            // Reopening an unfinished session uses the saved machine as-is, then takes a new honest baseline.
            report.resumeKeepsTrainedParameters = new GraphEngine().Parameters(graph).Any(p => Math.Abs(p.Value) > .1) && GuideStep == 0;
            // Finish the resumed session through the same live controls; do not inject tutorial completion flags.
            Press("送入样本"); Press("启动 / 继续训练"); deadline = Time.realtimeSinceStartup + 12;
            while (running && Time.realtimeSinceStartup < deadline) yield return null;
            Press("送入样本"); RefreshGuide();
            Press("验收设备"); yield return null;
            report.actualAcceptanceUnlocksA01 = profile.completed.Contains("P00") && activeOverlay && Unlocked(campaign.levels.First(l => l.id == "A01"));
            yield return new WaitForEndOfFrame(); CaptureNative(Path.Combine(output, "15-first-handoff.png"));
            report.passed = report.freshLaunchShowsRoute && report.skipPersistsWithoutUnlock && report.routeShownOnce && report.routeCanBeReopened && report.noTrainingBeforeObservation && report.observationAdvancesGuide && report.guideSkipKeepsWork && report.guideReplayKeepsWork && report.realTrainingReachesComparison && report.learningRateMatchesControl && report.checkRequiresComparison && report.sameInputCompared && report.actualPredictionImproves && report.newTrainingRequiresNewComparison && report.legacyProgressPreserved && report.returningPlayerKeepsCurrentJob && report.resumeKeepsTrainedParameters && report.actualAcceptanceUnlocksA01;
            File.WriteAllText(Path.Combine(output, "onboarding-smoke.json"), JsonUtility.ToJson(report, true));
            complete(report.passed);
        }

        void Press(string title)
        {
            var button = canvas.GetComponentsInChildren<Button>().FirstOrDefault(b => b.name == title && b.gameObject.activeInHierarchy);
            if (button == null) throw new InvalidOperationException("Missing live test button: " + title);
            button.onClick.Invoke();
        }

        [Serializable] sealed class OnboardingReport
        {
            public bool passed, freshLaunchShowsRoute, skipPersistsWithoutUnlock, routeShownOnce, routeCanBeReopened,
                noTrainingBeforeObservation, observationAdvancesGuide, guideSkipKeepsWork, guideReplayKeepsWork,
                realTrainingReachesComparison, learningRateMatchesControl, checkRequiresComparison, sameInputCompared,
                actualPredictionImproves, newTrainingRequiresNewComparison, legacyProgressPreserved, returningPlayerKeepsCurrentJob, resumeKeepsTrainedParameters, actualAcceptanceUnlocksA01;
            public int trainingSteps;
            public double trainingLoss, errorBefore, errorAfter;
        }
    }
}
