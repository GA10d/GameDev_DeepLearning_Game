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
using UnityEngine.EventSystems;

namespace LearningFoundry.Game
{
    public sealed class FoundryGame : MonoBehaviour
    {
        Campaign campaign; Profile profile; Canvas canvas; Camera uiCamera; RectTransform screen, inspector, boardRoot, meterDock, boardViewport, mapDetails, activeOverlay; GraphBoard board;
        Text status, instrument, resultTable, stageReadout; TracePlot plot;
        Level level; GraphSpec graph; GraphNode selected;
        readonly List<string> undo = new List<string>(), redo = new List<string>();
        readonly List<double> trace = new List<double>(); readonly HashSet<int> probeRecorded = new HashSet<int>();
        TrainingSession training; bool running, beforeObserved, afterObserved, unstableObserved, stableObserved, averageTab;
        int sampleIndex, probeState, trainingBudget = 240; double eta = .1, epsilon = .01, estimate, quadraticW = -2;
        string message; bool smoke;
        float boardZoom = .8f; bool debugOpen; string mapChapter = "A";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindObjectOfType<FoundryGame>()) return;
            var go = new GameObject("Learning Foundry"); DontDestroyOnLoad(go); go.AddComponent<FoundryGame>();
        }
        void Start()
        {
            campaign = JsonUtility.FromJson<Campaign>(Resources.Load<TextAsset>("Campaign/levels").text);
            smoke = Environment.GetCommandLineArgs().Contains("--smoke");
            profile = smoke ? new Profile() : ProfileStore.Load(); Sound.Volume = profile.volume;
            var canvasObject = new GameObject("Workshop Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            uiCamera = new GameObject("Instrument Camera").AddComponent<Camera>(); uiCamera.orthographic = true; uiCamera.transform.position = new Vector3(0, 0, -10); uiCamera.depth = 100; uiCamera.backgroundColor = Style.Ink; uiCamera.clearFlags = CameraClearFlags.SolidColor; uiCamera.tag = "MainCamera";
            DontDestroyOnLoad(uiCamera.gameObject);
            canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = uiCamera; canvas.planeDistance = 1; canvas.sortingOrder = 10;
            var scale = canvasObject.GetComponent<CanvasScaler>(); scale.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scale.referenceResolution = new Vector2(1600, 900); scale.matchWidthOrHeight = .5f;
            DontDestroyOnLoad(canvasObject);
            if (!FindObjectOfType<EventSystem>()) { var es = new GameObject("Event System", typeof(EventSystem), typeof(StandaloneInputModule)); DontDestroyOnLoad(es); }
            if (!FindObjectOfType<AudioListener>() && Camera.main) Camera.main.gameObject.AddComponent<AudioListener>();
            MainMenu();
            if (smoke) StartCoroutine(SmokeCapture());
        }
        void Update()
        {
            if (board != null) board.Pulse(Time.unscaledTime, profile.reduceMotion);
            if (board != null && !activeOverlay) board.TickPointer(uiCamera);
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (activeOverlay) { Destroy(activeOverlay.gameObject); activeOverlay = null; }
                else { if (board != null) board.Cancel(); running = false; if (level != null) SetStatus("线路选择已取消，训练已暂停。工作台会自动保存。"); }
            }
            var focus = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            bool editingText = activeOverlay || (focus && focus.GetComponent<InputField>() && focus.GetComponent<InputField>().isFocused);
            if (!editingText && level != null && Input.GetKeyDown(KeyCode.F1)) Notebook();
            if (!editingText && level != null && Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.Z)) Undo(false);
            if (!editingText && level != null && Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.Y)) Undo(true);
            if (!editingText && board != null)
            {
                if (Input.GetKeyDown(KeyCode.Space)) Observe();
                if (Input.GetKeyDown(KeyCode.Tab)) { selected = null; Instruments(); inspector.gameObject.SetActive(!inspector.gameObject.activeSelf); }
                var available = Missions.Palette(averageTab ? "A07" : level.id).ToArray();
                for (int i = 0; i < available.Length && i < 4; i++) if ((!ReadOnly || averageTab) && Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i))) AddNode(available[i]);
            }
            if (running && training != null)
            {
                try
                {
                    int batch = profile.reduceMotion ? 6 : 2;
                    for (int i = 0; i < batch && training.step < trainingBudget; i++) { training.Tick(); trace.Add(training.loss); }
                    UpdateTrainingUI();
                    if (training.step >= trainingBudget) { running = false; Save(); SetStatus("训练预算已用完。可检查新输入，或继续增加 120 步。 "); }
                }
                catch (Exception e) { running = false; SetStatus("训练停止：" + e.Message); }
            }
        }
        void OnApplicationQuit() { if (profile != null && !smoke) Save(); }
        void OnApplicationFocus(bool focus) { if (!focus && profile != null && !smoke) Save(); }
        void Save()
        {
            if (smoke) return;
            try { ProfileStore.Save(profile); }
            catch (Exception e) { SetStatus("存档写入失败：" + e.Message); }
        }
        void BaseScreen()
        {
            running = false; training = null; selected = null; board = null;
            if (screen) Destroy(screen.gameObject);
            screen = Style.Rect(canvas.transform, "Game Screen", 0, 0, 1600, 900);
            Hardware.Surface(screen, "Workshop Timber", 0, 0, 1600, 900, false).raycastTarget = false;
            meterDock = boardViewport = null;
            status = null;
        }
        public void MainMenu()
        {
            level = null; BaseScreen();
            var sign = Hardware.Plate(screen, "Foundry Sign", 46, 28, 1022, 128, Style.Frame);
            Style.Number(sign.transform, "LEARNING FOUNDRY / BENCH 01", 31, 16, 900, 30, 16, Hardware.Phosphor);
            Style.Text(sign.transform, "学习工坊", 29, 44, 390, 70, 45, Style.Paper);
            Style.Text(sign.transform, "从一条线路，造出一台学习机器。", 438, 68, 545, 42, 22, Style.Paper);
            Hardware.Plate(screen, "Bench Rim", 46, 183, 1022, 586, Hardware.Metal);
            var viewport = Style.Scroll(screen, 59, 196, 996, 559, 1450, 790, out var contents);
            contents.localScale = Vector3.one * .69f;
            var reference = Recipes.Affine(1.75, .5, -.25);
            var definition = new ModuleSpec { id = "menu-preview", name = "双路混合器", graph = reference, inputNames = new List<string> { "x", "z" }, outputName = "y" };
            var preview = Recipes.Empty(new[] { "x", "z" }, "y");
            var xInput = preview.nodes.First(n => n.name == "x"); xInput.x = 100; xInput.y = 178;
            var zInput = preview.nodes.First(n => n.name == "z"); zInput.x = 100; zInput.y = 476;
            var module = preview.Add(NodeKind.Module, "双路混合器", 0, xInput.id, zInput.id); module.moduleId = definition.id; module.x = 601; module.y = 317;
            var output = preview.Output("y"); output.x = 1100; output.y = 317; output.inputs.Add(module.id);
            var previewModules = new List<ModuleSpec> { definition };
            var diagram = new GraphBoard(contents, preview, previewModules, true, () => {}, () => {}, n => {}, t => {});
            diagram.Show(new GraphEngine(previewModules).Evaluate(preview, new Dictionary<string, double> { { "x", .5 }, { "z", -.5 } }));
            Style.Number(screen, "01   INPUT     /     COMPUTE     /     OBSERVE", 87, 220, 900, 32, 16, Style.Paper);
            Style.Text(screen, "接上输入，调好参数，让机器自己学会。", 89, 701, 908, 36, 21, Style.Paper);
            var power = Hardware.Plate(screen, "Main Switch Cabinet", 1110, 28, 443, 742, Hardware.Metal);
            Style.Number(power.transform, "WORKSHOP / MAIN POWER", 27, 25, 389, 32, 16);
            var current = campaign.levels.First(l => l.id == profile.currentLevel);
            var record = Style.Box(power.transform, "Job Ticket", 25, 91, 393, 150, Style.Paper);
            Hardware.Shape(record.transform, "Tape", 127, -10, 137, 22, HardwareShape.Tape, Hardware.Enamel);
            Style.Number(record.transform, "JOB " + current.id, 18, 18, 357, 26, 15, Style.Muted);
            Style.Text(record.transform, current.name, 18, 52, 357, 42, 26);
            Style.Text(record.transform, current.concept, 18, 105, 357, 35, 17, Style.Muted);
            Style.Button(power.transform, profile.completed.Count > 0 ? "继续装配  →" : "接受第一份委托  →", 25, 276, 393, 76, () => OpenLevel(profile.currentLevel), Style.Brass);
            Style.Button(power.transform, "委托路线", 25, 380, 393, 56, Map);
            Style.Button(power.transform, "操作手册", 25, 454, 393, 56, Controls);
            Style.Button(power.transform, "设置", 25, 566, 187, 45, Settings);
            Style.Button(power.transform, "退出", 232, 566, 186, 45, () => Application.Quit());
            Style.Number(power.transform, "REPAIRED  " + profile.completed.Count + " / 17", 27, 658, 360, 35, 19);
            Style.Text(screen, "可玩原型 0.3.0 · 17 关 / 后续章节规划中", 48, 816, 715, 35, 17, Style.Paper);
            Style.Text(screen, "自动存档  /  本机 CPU 训练", 1114, 816, 436, 35, 17, Style.Paper, TextAnchor.MiddleRight);
            if (!string.IsNullOrEmpty(ProfileStore.LoadWarning)) Modal("存档说明", ProfileStore.LoadWarning, "返回工坊", () => { });
        }

        void Header(string title, string sub)
        {
            var plate = Hardware.Plate(screen, "Bench Nameplate", 24, 23, 774, 71, Style.Frame);
            Style.Text(plate.transform, title, 18, 6, 734, 32, 24, Style.Paper);
            Style.Text(plate.transform, sub, 20, 42, 732, 25, 15, new Color(.72f, .77f, .67f));
            Style.Button(screen, "← 工坊", 1143, 28, 128, 49, MainMenu);
            Style.Button(screen, "委托板", 1283, 28, 128, 49, Map);
            Style.Button(screen, "手册 / F1", 1423, 28, 153, 49, () => { if (level == null) Controls(); else Notebook(); });
            if (level != null)
            {
                var ticket = Style.Box(screen, "Specification Ticket", 820, 23, 294, 71, Style.Paper);
                Hardware.Shape(ticket.transform, "Tape", 100, -7, 95, 14, HardwareShape.Tape, Hardware.Enamel);
                Style.Text(ticket.transform, "验收规格", 14, 5, 266, 18, 11, Style.Muted);
                string spec = Specification().Replace("\n", "；");
                Style.Text(ticket.transform, spec.Length > 38 ? spec.Substring(0, 28) + "… / F1" : spec, 14, 22, 266, 46, 13);
                ticket.gameObject.AddComponent<Button>().onClick.AddListener(Notebook);
            }
        }

        bool Unlocked(Level l) { return Missions.Implemented.Contains(l.id) && (l.deps ?? new string[0]).All(profile.completed.Contains); }
        public void Map()
        {
            level = null; BaseScreen(); Header("工坊委托板", "章节线路通向更完整的学习机器。点亮的接点可以进入。");
            float cx = 24;
            foreach (var item in campaign.chapters)
            {
                var chapter = item;
                Style.Button(screen, chapter.id + "  " + chapter.name.Replace("序章 ", ""), cx, 112, 169, 48, () => { mapChapter = chapter.id; Map(); }, mapChapter == chapter.id ? Style.Brass : Hardware.Metal); cx += 174;
            }
            Style.Button(screen, "总览", 1420, 112, 156, 48, () => { mapChapter = "ALL"; Map(); }, mapChapter == "ALL" ? Style.Brass : Hardware.Metal);
            var nodes = campaign.levels.Where(l => mapChapter == "ALL" || l.chapter == mapChapter).ToArray();
            var ranks = new Dictionary<string, int>(); foreach (var l in campaign.levels) Rank(l, ranks);
            int minimum = nodes.Min(l => ranks[l.id]), maximum = nodes.Max(l => ranks[l.id]) - minimum;
            int maxLanes = nodes.GroupBy(l => ranks[l.id]).Max(g => g.Count()); float mapWidth = Mathf.Max(1174, 56 + maxLanes * 365);
            Hardware.Plate(screen, "Routing Frame", 24, 181, 1198, 674, Hardware.Metal);
            Style.Scroll(screen, 35, 192, 1176, 652, mapWidth, Mathf.Max(652, 80 + (maximum + 1) * 152), out var content);
            var positions = new Dictionary<string, Vector2>(); var counts = new Dictionary<int, int>();
            foreach (var l in nodes)
            {
                int r = ranks[l.id] - minimum; if (!counts.ContainsKey(r)) counts[r] = 0;
                int lane = counts[r]++; positions[l.id] = new Vector2(34 + lane * 365, 29 + r * 152);
            }
            var cables = Style.Rect(content, "Commission Wiring", 0, 0, mapWidth, content.rect.height).gameObject.AddComponent<Lines>(); cables.cables = true; cables.color = new Color(.61f, .60f, .36f); cables.raycastTarget = false;
            foreach (var l in nodes) foreach (var dep in l.deps ?? new string[0])
            {
                if (!positions.ContainsKey(dep)) continue;
                var from = positions[dep]; var to = positions[l.id];
                cables.paths.Add(new[] { new Vector2(from.x + 161, -from.y - 119), new Vector2(from.x + 161, -to.y + 15), new Vector2(to.x + 161, -to.y + 15), new Vector2(to.x + 161, -to.y) });
            }
            foreach (var item in nodes)
            {
                var l = item; var pos = positions[l.id]; bool implemented = Missions.Implemented.Contains(l.id), complete = profile.completed.Contains(l.id), unlocked = Unlocked(l);
                var card = Hardware.Plate(content, l.id, pos.x, pos.y, 322, 119, complete ? new Color(.60f, .70f, .58f) : Hardware.Enamel);
                Hardware.Shape(card.transform, "Socket", 149, -8, 24, 24, HardwareShape.Socket, complete ? Hardware.Phosphor : Hardware.Metal);
                Style.Text(card.transform, l.id + "  " + l.name, 16, 13, 285, 29, 20);
                Style.Text(card.transform, "知识点 · " + l.concept, 16, 47, 285, 43, 16, Style.Muted);
                Style.Text(card.transform, complete ? "✓ 修复完成" : !implemented ? "规划关卡 / 未实现" : unlocked ? "→ 接受委托" : "锁 / 前置 " + string.Join(" + ", l.deps), 16, 93, 288, 22, 14, unlocked ? Style.Cream : Style.Muted);
                card.gameObject.AddComponent<Button>().onClick.AddListener(() => MapDetail(l));
            }
            mapDetails = Hardware.Plate(screen, "Commission Clipboard", 1245, 181, 331, 674, Style.Paper).rectTransform;
            Hardware.Shape(screen, "Clipboard Clip", 1344, 170, 136, 22, HardwareShape.Chip, Style.Steel);
            MapDetail(nodes.FirstOrDefault(l => l.id == profile.currentLevel) ?? nodes.First()); cables.Refresh();
        }

        void MapDetail(Level l)
        {
            foreach (Transform child in mapDetails) Destroy(child.gameObject);
            Style.Text(mapDetails, "委托 " + l.id, 18, 19, 264, 29, 17, Style.Muted);
            Style.Text(mapDetails, l.name, 18, 70, 264, 89, 29);
            Style.Text(mapDetails, "知识点", 18, 167, 264, 28, 16, Style.Brass);
            Style.Text(mapDetails, l.concept, 18, 205, 264, 85, 21);
            Style.Text(mapDetails, "前置条件", 18, 308, 264, 26, 16, Style.Muted);
            Style.Text(mapDetails, l.deps.Length == 0 ? "无前置" : string.Join(" + ", l.deps), 18, 347, 264, 55, 19);
            Style.Text(mapDetails, "解锁 / 成果", 18, 422, 264, 26, 16, Style.Muted);
            Style.Text(mapDetails, l.reward, 18, 461, 294, 100, 18);
            bool enter = Unlocked(l) || profile.completed.Contains(l.id);
            var b = Style.Button(mapDetails, enter ? "进入工作台  →" : Missions.Implemented.Contains(l.id) ? "先完成前置委托" : "规划关卡 / 未实现", 18, 590, 294, 53, () => OpenLevel(l.id), enter ? Style.Brass : Style.Steel); b.interactable = enter;
        }
        int Rank(Level l, Dictionary<string, int> ranks)
        { if (ranks.ContainsKey(l.id)) return ranks[l.id]; int r = (l.deps == null || l.deps.Length == 0) ? 0 : l.deps.Max(d => Rank(campaign.levels.First(n => n.id == d), ranks)) + 1; ranks[l.id] = r; return r; }
        public void OpenLevel(string id)
        {
            var target = campaign.levels.FirstOrDefault(l => l.id == id) ?? campaign.levels.First(l => l.id == "P00");
            if (!Unlocked(target) && !profile.completed.Contains(target.id)) { Map(); return; }
            level = target; profile.currentLevel = target.id; graph = profile.Graph(target.id);
            averageTab = target.id == "A07" && !Missions.CheckAverage(profile.average).passed;
            if (averageTab && profile.average == null) profile.average = Recipes.Empty(new[] { "L1", "L2", "L3", "L4" }, "mean");
            undo.Clear(); redo.Clear(); trace.Clear(); beforeObserved = afterObserved = false; probeRecorded.Clear(); probeState = 0; sampleIndex = 0; unstableObserved = stableObserved = false; eta = .1; trainingBudget = 240;
            mapChapter = level.chapter; debugOpen = ReadOnly || level.id == "B09"; BuildWorkshop(); Save();
        }
        GraphSpec ActiveGraph { get { return averageTab ? profile.average : graph; } }
        bool ReadOnly { get { return new[] { "P00", "B01", "B04", "B05", "B07", "B08" }.Contains(level.id); } }
        void BuildWorkshop()
        {
            BaseScreen(); Header(level.id + "   " + level.name, "知识点 · " + level.concept + (averageTab ? " / 四读数平均器" : ""));
            Hardware.Plate(screen, "Workbench Frame", 24, 113, 1552, 641, Hardware.Metal);
            float contentW = Mathf.Max(1800, ActiveGraph.nodes.Count == 0 ? 1800 : ActiveGraph.nodes.Max(n => n.x) + 280);
            float contentH = Mathf.Max(1100, ActiveGraph.nodes.Count == 0 ? 1100 : ActiveGraph.nodes.Max(n => n.y) + 250);
            var scroller = Style.Scroll(screen, 36, 125, 1528, 617, contentW, contentH, out boardRoot);
            boardViewport = (RectTransform)scroller.transform;
            scroller.horizontalNormalizedPosition = 0; scroller.verticalNormalizedPosition = 1; boardRoot.localScale = Vector3.one * boardZoom;
            board = new GraphBoard(boardRoot, ActiveGraph, profile.modules, ReadOnly && !averageTab, BeforeEdit, Edited, Select, SetStatus, n => { Edited(); Observe(); });
            Style.Button(screen, "−", 1430, 689, 36, 34, () => { boardZoom = Mathf.Max(.45f, boardZoom - .1f); boardRoot.localScale = Vector3.one * boardZoom; });
            Style.Button(screen, "+", 1473, 689, 36, 34, () => { boardZoom = Mathf.Min(1.3f, boardZoom + .1f); boardRoot.localScale = Vector3.one * boardZoom; });
            Style.Button(screen, "全图", 1516, 689, 44, 34, () => {
                float actualW = ActiveGraph.nodes.Count == 0 ? 1450 : ActiveGraph.nodes.Max(n => n.x) + 225;
                float actualH = ActiveGraph.nodes.Count == 0 ? 600 : ActiveGraph.nodes.Max(n => n.y) + 140;
                boardZoom = Mathf.Min(.95f, 1470 / actualW, 570 / actualH); boardRoot.localScale = Vector3.one * boardZoom; scroller.horizontalNormalizedPosition = 0; scroller.verticalNormalizedPosition = 1;
            });
            BuildPalette();
            inspector = Hardware.Plate(screen, "Portable Control Box", 1246, 139, 294, 438, Hardware.Metal).rectTransform;
            meterDock = Hardware.Plate(screen, "Portable Oscilloscope", 680, 579, 860, 158, Hardware.Metal).rectTransform;
            Instruments(); SetDebugOpen(debugOpen); inspector.gameObject.SetActive(ReadOnly || level.id == "B09");
            var console = Hardware.Plate(screen, "Control Console", 740, 770, 836, 108, Hardware.Metal);
            Style.Button(console.transform, "送入样本", 14, 16, 139, 70, Observe);
            Style.Button(console.transform, "下一样本", 163, 16, 123, 70, () => { sampleIndex++; Observe(); });
            Style.Button(console.transform, "验收设备", 300, 16, 147, 70, Check, Style.Brass);
            Style.Button(console.transform, "控制盒 / Tab", 469, 13, 170, 34, () => { selected = null; Instruments(); inspector.gameObject.SetActive(!inspector.gameObject.activeSelf); });
            Style.Button(console.transform, "读数仪", 650, 13, 170, 34, () => SetDebugOpen(!debugOpen));
            Style.Button(console.transform, "撤销", 469, 57, 105, 34, () => Undo(false));
            Style.Button(console.transform, "重置", 585, 57, 105, 34, ConfirmReset);
            Style.Button(console.transform, "知识 / F1", 701, 57, 119, 34, Notebook);
            status = Style.Text(screen, "拿取零件后在空位安装。接线从右侧插孔到左侧插孔；旋钮可上下拖动调参。", 27, 881, 1546, 24, 14, Style.Paper);
            if (level.id == "P00") SetStatus("先送入样本观察，再在控制盒启动训练。Space 送入样本；Tab 开合控制盒。");
        }

        void SetDebugOpen(bool open)
        {
            debugOpen = open;
            if (meterDock) meterDock.gameObject.SetActive(open);
        }

        void Notebook()
        {
            var overlay = Overlay(); var page = Hardware.Plate(overlay, "Technical Manual", 362, 63, 876, 776, Style.Paper);
            Hardware.Shape(page.transform, "Binder Clip", 342, -15, 190, 25, HardwareShape.Chip, Hardware.Metal);
            Style.Text(page.transform, level.id + " / " + level.name, 32, 25, 812, 43, 28);
            Style.Text(page.transform, "知识点 · " + level.concept, 33, 82, 810, 41, 20, Style.Muted);
            Style.Scroll(page.transform, 24, 144, 828, 542, 812, 1000, out var content);
            float y = 16;
            foreach (var entry in new[] { new[] { "现场", level.scene }, new[] { "新知识", LessonText() }, new[] { "动手做", Instructions() }, new[] { "验收规格", Specification() }, new[] { "观察什么", level.visualization.main }, new[] { "维修提示", level.hint } })
            {
                Style.Text(content, entry[0], 14, y, 774, 30, 18, Style.Brass); y += 40;
                float height = Mathf.Max(64, 26 * (2 + entry[1].Length / 36 + entry[1].Count(c => c == '\n')));
                Style.Text(content, entry[1], 14, y, 774, height, 20); y += height + 25;
            }
            content.sizeDelta = new Vector2(812, Mathf.Max(542, y));
            Style.Button(page.transform, "回到装配", 565, 712, 281, 44, () => Destroy(overlay.gameObject), Style.Brass);
        }
        static void Note(Transform parent, string title, string body, ref float y)
        {
            Style.Text(parent, title.ToUpperInvariant(), 8, y, 221, 26, 15, Style.Brass); y += 31;
            int lines = Math.Max(2, body.Length / 14 + body.Count(c => c == '\n') + 2); float height = lines * 24;
            Style.Text(parent, body, 8, y, 221, height, 17); y += height + 22;
        }
        string LessonText()
        {
            string extra = "";
            if (level.id == "A02") extra = "\n参数旋钮保存一个可调数；乘法器输出两个端口的乘积。";
            if (level.id == "A03") extra = "\n加法器将两个输入相加。偏置是独立于输入的可调常量。";
            if (level.id == "A05") extra = "\n残差＝预测−目标。半平方误差＝½×残差×残差。负号可由乘 −1 实现。";
            if (level.id == "A06") extra = "\n封装会复制已通过 A04 的机器，保留 x、z 输入和 y 输出。每次放置都有独立旋钮。当前支持整个工作台的单输出封装。";
            if (level.id == "A07") extra = "\n先亲手搭出四个误差的平均器。然后把混合器与误差计组合；仪表用此平均器汇总四件样本。";
            if (level.id == "B02") extra = "\na+b 中，每个输入增加 1 都使输出增加 1。来自后级的梯度 g，应原样到达 da、db。";
            if (level.id == "B03") extra = "\na×b 中，a 的影响由 b 决定；b 的影响由 a 决定。da=g×b，db=g×a。";
            if (level.id == "B04") extra = "\n这条目标链没有分支。先让目标端有梯度 1，再从最下游运算向前回流。";
            if (level.id == "B05") extra = "\n同一个参数被多处使用，全部路径的梯度必须相加。同一乘法器的两个端口也可能使用同一个参数。";
            if (level.id == "B06") extra = "\n当前参数 w、梯度 g、学习率 eta 都是给定输入。输出 newW = w − eta×g。";
            if (level.id == "B07") extra = "\n用参考损失 ½(w−2)² 和给定梯度 w−2，隔离观察步长。大步可能越过谷底，甚至离目标越来越远。";
            if (level.id == "B08") extra = "\n每一轮需要当前参数的预测、清空旧梯度、反向传播和更新。预测与清空可交换，反向必须等它们完成。";
            if (level.id == "B09") extra = "\n此处调用你保存的反向规则、更新器与阶段顺序，在本机 CPU 上实际训练 A07 设备。验证样本不参与更新。";
            return level.teaching.text + extra;
        }
        string Instructions()
        {
            if (averageTab) return "四个输入 L1～L4 表示四件样本的误差。用加法器相加，再乘常量 0.25。验收通过后切换到模型装配。";
            if (level.id == "A06") return "点击‘封装 A04’，再放置两个盒子。将 x、z 接到每个盒子，输出接 yA、yB；点击盒子可分别调内部参数。";
            if (level.id == "B01") return "右侧切换三个旋钮状态。查看原损失、加 epsilon 后的损失；计算每单位位移的变化，填入你的估计并记录。";
            if (level.id == "B04") return "切换回流方向。送入样本观察蓝绿色的梯度读数，再与参数旁的数值检查比较。";
            if (level.id == "B05") return "切换覆盖/累加，再对‘两条路径’和‘w×w’送入样本。检查共享参数的梯度。";
            if (level.id == "B07") return "选择学习率，运行 20 步。记录一次不稳定轨迹，再用稳定步长在相同预算内修好它。";
            if (level.id == "B08") return "点阶段卡片右侧箭头交换相邻阶段。让连续 12 步和参考流程一致；机器会实际执行这个顺序。";
            return level.play;
        }
        string Specification()
        {
            switch (level.id)
            {
                case "A01": return "输出 y = x。全部正数、负数、零与新输入都正确。";
                case "A02": return "输出 y = 2x。至少一个实际参与运算的可调参数。";
                case "A03": return "输出 y = x + 0.75。用参数旋钮修正零点。";
                case "A04": return "输出 y = 2x − z + 0.5。两个权重与一个偏置，共三个实际参与运算的参数。";
                case "A05": return "输出 loss = ½(prediction − target)²。包括正负同幅残差与零残差。";
                case "A06": return "yA=2x−z+0.5\nyB=−x+2z+1\n两个实例内部参数独立。";
                case "A07": return "输出 y=1.75x+0.5z−0.25，同时输出正确的 loss。平均误差仪表与新输入均通过。";
                case "B09": return "真实训练至少 8 步；平均损失 < 0.002；三组未参与训练的新输入偏差 < 0.08。保存训练后的参数。";
                default: return level.pass;
            }
        }
        void BuildPalette()
        {
            var tray = Hardware.Plate(screen, "Parts Tray", 24, 770, 698, 108, Hardware.Metal);
            if (ReadOnly && !averageTab)
            {
                Hardware.Miniature(tray.transform, NodeKind.Parameter, 26, 25, 74, 57);
                Style.Text(tray.transform, "参考设备", 126, 19, 540, 31, 22);
                Style.Text(tray.transform, "用控制盒实验，观察设备的响应。", 127, 61, 535, 28, 17, Style.Cream);
            }
            else
            {
                int slot = 0;
                foreach (var k in Missions.Palette(averageTab ? "A07" : level.id))
                {
                    var kind = k; string label = kind == NodeKind.Constant ? "常量" : kind == NodeKind.Parameter ? "参数" : kind == NodeKind.Add ? "加法器" : "乘法器";
                    var button = Style.Button(tray.transform, "", 14 + slot * 109, 12, 99, 84, () => AddNode(kind), Hardware.Enamel);
                    button.name = "零件 / " + label; Hardware.Miniature(button.transform, kind, 20, 8, 60, 40);
                    Style.Text(button.transform, (slot + 1) + "  " + label, 8, 54, 83, 27, 15, Style.Cream, TextAnchor.MiddleCenter); slot++;
                }
                if (!averageTab && (level.id == "A06" || level.id == "A07" || level.id == "B09"))
                {
                    Style.Button(tray.transform, "封装\nA04", 14 + slot * 109, 12, 99, 84, Package, Hardware.Enamel); slot++;
                    var button = Style.Button(tray.transform, "", 14 + slot * 109, 12, 99, 84, AddModule, Hardware.Enamel);
                    Hardware.Miniature(button.transform, NodeKind.Module, 17, 8, 65, 40); Style.Text(button.transform, "模型盒", 8, 54, 83, 27, 15, Style.Cream, TextAnchor.MiddleCenter);
                }
            }
            if (level.id == "A07") Style.Button(screen, averageTab ? "模型装配 →" : "查看平均器", 824, 104, 196, 36, () => { if (averageTab && !Missions.CheckAverage(profile.average).passed) { SetStatus("先验收平均器。四个读数相加，再乘 ¼。"); return; } averageTab = !averageTab; undo.Clear(); redo.Clear(); BuildWorkshop(); });
        }

        void AddNode(NodeKind kind)
        {
            if (ReadOnly && !averageTab) return;
            board.BeginPlacement(kind, position => {
                BeforeEdit(); var n = ActiveGraph.Add(kind, kind == NodeKind.Parameter ? "p" + (ActiveGraph.nodes.Count(n0 => n0.kind == NodeKind.Parameter) + 1) : kind == NodeKind.Constant ? "C" : kind == NodeKind.Add ? "+" : "×", kind == NodeKind.Parameter || kind == NodeKind.Constant ? 1 : 0);
                n.x = position.x; n.y = position.y; board.Build(); Edited(); Select(n);
            });
        }

        void Package()
        {
            var source = profile.Graph("A04"); var result = Missions.Check("A04", source, profile);
            if (!result.passed) { SetStatus("A04 保存的机器未通过当前校准，请回去修复后再封装。"); return; }
            if (profile.modules.Any(m => m.id == "affine")) { SetStatus("双路混合器已在零件库；每个新实例会复制独立参数。"); return; }
            profile.modules.Add(new ModuleSpec { id = "affine", name = "双路混合器", graph = ProfileStore.Copy(source), inputNames = new List<string> { "x", "z" }, outputName = "y" }); Save();
            SetStatus("已封装 A04：输入 x、z → 输出 y。现在放置两个盒子并分别校准。");
        }
        void AddModule()
        {
            if (!profile.modules.Any(m => m.id == "affine")) { SetStatus("先封装 A04，生成自己的模型盒。"); return; }
            board.BeginPlacement(NodeKind.Module, position => {
                BeforeEdit(); var node = ActiveGraph.Add(NodeKind.Module, "混合器 " + (ActiveGraph.nodes.Count(n => n.kind == NodeKind.Module) + 1)); node.moduleId = "affine"; node.x = position.x; node.y = position.y;
                board.Build(); Edited(); Select(node);
            });
        }

        void BeforeEdit()
        {
            undo.Add(JsonUtility.ToJson(ActiveGraph)); if (undo.Count > 60) undo.RemoveAt(0); redo.Clear();
        }
        void Edited() { running = false; training = null; Save(); }
        void Undo(bool forward)
        {
            var from = forward ? redo : undo; var to = forward ? undo : redo;
            if (from.Count == 0) { SetStatus("没有可" + (forward ? "重做" : "撤销") + "的操作。"); return; }
            to.Add(JsonUtility.ToJson(ActiveGraph)); var next = JsonUtility.FromJson<GraphSpec>(from[from.Count - 1]); from.RemoveAt(from.Count - 1);
            ReplaceGraph(next); BuildWorkshop(); Save();
        }
        void ReplaceGraph(GraphSpec next) { if (averageTab) profile.average = next; else { graph = next; profile.workspaces.First(w => w.levelId == level.id).graph = next; } }
        void ConfirmReset()
        {
            Modal("重置当前工作台", "本关的线路与参数会回到初始状态。其他关卡与已封装模块会保留。重置后可按 Ctrl+Z 撤销。", "重置这台设备", () => { BeforeEdit(); ReplaceGraph(averageTab ? Recipes.Empty(new[] { "L1", "L2", "L3", "L4" }, "mean") : Missions.Initial(level.id, profile)); trace.Clear(); BuildWorkshop(); Save(); }, true);
        }
        void Select(GraphNode node)
        {
            selected = node; if (instrument == null) return; inspector.gameObject.SetActive(true);
            Instruments(true); Style.Text(inspector, "选中 · " + node.name, 20, 72, 256, 27, 21, Style.Brass);
            float y = 118;
            if (node.kind == NodeKind.Parameter || node.kind == NodeKind.Constant)
            {
                if (!ReadOnly || averageTab)
                {
                    Style.Field(inspector, node.value.ToString("0.####", CultureInfo.InvariantCulture), 20, y, 248, value => { if (Parse(value, out var number) && GraphEngine.Finite(number)) { BeforeEdit(); node.value = number; Edited(); Observe(); } else SetStatus("请输入有限数值，例如 −1 或 0.5。"); }); y += 49;
                    bool recorded = false;
                    Style.Slider(inspector, 22, y, 246, -4, 4, (float)node.value, v => { if (!recorded) { BeforeEdit(); recorded = true; } node.value = v; running = false; training = null; Observe(); }); y += 46;
                }
                else { Style.Text(inspector, "当前值 " + node.value.ToString("0.####"), 20, y, 250, 37, 20); y += 43; }
            }
            if (node.kind == NodeKind.Module)
            {
                var slots = new GraphEngine(profile.modules).Parameters(ActiveGraph).Where(p => p.key.StartsWith(node.id + "/")).ToList();
                foreach (var slot in slots.Take(3))
                {
                    var p = slot; Style.Text(inspector, p.name.Split('/').Last(), 20, y, 93, 30, 15, Style.Muted);
                    Style.Field(inspector, p.Value.ToString("0.###", CultureInfo.InvariantCulture), 116, y - 3, 152, s => { if (Parse(s, out var value) && GraphEngine.Finite(value)) { BeforeEdit(); p.Value = value; Edited(); Observe(); } }); y += 46;
                }
                Style.Button(inspector, "展开盒内结构", 20, y, 248, 35, () => ModuleView(node)); y += 44;
            }
            if ((!ReadOnly || averageTab) && node.kind != NodeKind.Input && node.kind != NodeKind.Output)
                Style.Button(inspector, "拆下模块", 20, y, 116, 35, () => { BeforeEdit(); ActiveGraph.nodes.Remove(node); foreach (var n in ActiveGraph.nodes) for (int i = 0; i < n.inputs.Count; i++) if (n.inputs[i] == node.id) n.inputs[i] = ""; board.Build(); Edited(); selected = null; Instruments(); });
            if ((!ReadOnly || averageTab) && node.inputs.Count > 0)
                Style.Button(inspector, "断开输入", 148, y, 120, 35, () => { BeforeEdit(); node.inputs.Clear(); board.RefreshWires(); Edited(); Observe(); });
            Style.Button(inspector, "返回实验设置", 20, 380, 248, 42, () => { selected = null; Instruments(); });
        }
        void ModuleView(GraphNode node)
        {
            var module = profile.modules.First(m => m.id == node.moduleId); var overlay = Overlay();
            var rim = Style.Box(overlay, "Module Inspection", 180, 130, 1240, 660, Style.Panel);
            Style.Text(rim.transform, module.name + " · 内部线路", 27, 23, 930, 36, 28, Style.Brass);
            Style.Text(rim.transform, "接口 x、z → y。实例参数独立保存；这里展示封装时的定义，当前实例读数在外侧仪表。", 28, 72, 1140, 40, 17);
            Style.Scroll(rim.transform, 20, 125, 1200, 458, 1800, 850, out var content);
            new GraphBoard(content, module.graph, profile.modules, true, () => { }, () => { }, n => { }, SetStatus);
            Style.Button(rim.transform, "合上盒盖", 971, 598, 245, 42, () => Destroy(overlay.gameObject));
        }
        void Instruments(bool forSelection = false)
        {
            foreach (Transform child in inspector) Destroy(child.gameObject);
            var handle = Style.Box(inspector, "Control Box Handle", 8, 8, 278, 43, Hardware.Metal);
            var drag = handle.gameObject.AddComponent<PanelDrag>(); drag.panel = inspector;
            Style.Text(handle.transform, forSelection ? "部件校准" : "实验控制", 12, 5, 205, 32, 22);
            Style.Button(handle.transform, "×", 231, 3, 38, 32, () => inspector.gameObject.SetActive(false));
            if (meterDock && (!instrument || instrument.transform.parent != meterDock))
            {
                foreach (Transform child in meterDock) Destroy(child.gameObject);
                var meterHandle = Style.Box(meterDock, "Oscilloscope Carry Handle", 8, 4, 844, 25, Hardware.Metal);
                var meterDrag = meterHandle.gameObject.AddComponent<PanelDrag>(); meterDrag.panel = meterDock;
                Style.Text(meterHandle.transform, "示波器 / 实际测量", 9, 1, 782, 22, 13);
                Style.Button(meterHandle.transform, "×", 806, 0, 28, 25, () => SetDebugOpen(false));
                Style.Box(meterDock, "Instrument Screen", 14, 33, 211, 111, Hardware.Dark).raycastTarget = false;
                instrument = Style.Text(meterDock, "等待送入样本", 24, 42, 191, 99, 18, Hardware.Phosphor);
                plot = new TracePlot(meterDock, 238, 33, 363, 111); plot.Set(trace);
                Style.Box(meterDock, "Measurement Slip", 614, 33, 229, 111, Style.Paper).raycastTarget = false;
                resultTable = Style.Text(meterDock, "等待设备响应", 624, 42, 209, 96, 15, Style.Muted);
            }
            if (forSelection) return;
            int controlsStart = inspector.childCount;
            if (level.id == "P00" || level.id == "B09")
            {
                Style.Button(inspector, "启动 / 继续训练", 20, 403, 248, 42, ToggleTraining);
                Style.Button(inspector, "单步", 20, 457, 117, 38, OneStep);
                Style.Button(inspector, "暂停", 149, 457, 119, 38, () => { running = false; Save(); SetStatus("已暂停；当前参数已保存。"); });
                Style.Text(inspector, "学习率 eta", 20, 511, 158, 25, 15, Style.Muted);
                Style.Field(inspector, eta.ToString("0.###", CultureInfo.InvariantCulture), 178, 504, 90, s => { if (Parse(s, out var value) && value > 0 && value <= 2) { eta = value; if (training != null) training.learningRate = eta; Save(); } else SetStatus("学习率可设为 0 < eta ≤ 2。"); });
                if (level.id == "B09")
                {
                    Style.Button(inspector, "保存快照", 20, 555, 117, 37, () => { profile.snapshotB09 = ProfileStore.Copy(graph); Save(); SetStatus("已保存此模型的线路与实际参数快照。"); });
                    Style.Button(inspector, "载入快照", 149, 555, 119, 37, () => { if (profile.snapshotB09 == null) { SetStatus("还没有模型快照，先保存一次。"); return; } BeforeEdit(); ReplaceGraph(ProfileStore.Copy(profile.snapshotB09)); trace.Clear(); BuildWorkshop(); Save(); SetStatus("模型快照已恢复；可送入相同样本检查，或重新启动训练。"); });
                }
                Style.Text(inspector, "预算 " + trainingBudget + " 步 · 本机 CPU\n工作台自动保存；模型快照保留线路与参数。", 20, 610, 258, 58, 14, Style.Muted);
            }
            else if (level.id == "B01") ProbeControls();
            else if (level.id == "B04")
            {
                Style.Text(inspector, "目标端种子 = 1", 20, 409, 252, 30, 19);
                Style.Button(inspector, profile.reverseOrder ? "回流：③ → ② → ①" : "回流：① → ② → ③", 20, 454, 248, 43, () => { profile.reverseOrder = !profile.reverseOrder; Save(); Instruments(); Observe(); });
                Style.Text(inspector, "点击切换方向，再送入样本。\n梯度值会显示在每个模块上。\n正确顺序应通过数值检查。", 20, 521, 252, 94, 16, Style.Muted);
            }
            else if (level.id == "B05")
            {
                Style.Button(inspector, profile.accumulate ? "收集方式：贡献相加" : "收集方式：后值覆盖", 20, 406, 248, 43, () => { profile.accumulate = !profile.accumulate; Save(); Instruments(); Observe(); });
                Style.Button(inspector, "查看两条路径", 20, 465, 248, 40, () => { ReplaceGraph(Recipes.Shared()); BuildWorkshop(); Observe(); });
                Style.Button(inspector, "查看 w × w", 20, 519, 248, 40, () => { ReplaceGraph(Recipes.Shared(true)); BuildWorkshop(); Observe(); });
                Style.Text(inspector, "两条路径：dw=x+z\n重复操作数：dw=2w", 20, 581, 252, 63, 17, Style.Muted);
            }
            else if (level.id == "B07")
            {
                Style.Text(inspector, "损失 ½(w−2)²", 20, 401, 250, 30, 19);
                Style.Button(inspector, "慢步 0.05", 20, 449, 116, 38, () => RateExperiment(.05));
                Style.Button(inspector, "稳步 0.5", 149, 449, 119, 38, () => RateExperiment(.5));
                Style.Button(inspector, "大步 2.1", 20, 501, 248, 38, () => RateExperiment(2.1));
                Style.Text(inspector, "每次从 w=−2 重新运行 20 步。\n需要亲自比较稳定与不稳定轨迹。\n进度：" + (unstableObserved ? "已观察大步" : "未观察大步") + " / " + (stableObserved ? "稳定达标" : "待稳定达标"), 20, 565, 252, 94, 16, Style.Muted);
            }
            else if (level.id == "B08")
            {
                var labels = new Dictionary<string, string> { { "forward", "当前参数正向" }, { "clear", "清空旧梯度" }, { "backward", "反向传播" }, { "update", "更新参数" } };
                for (int i = 0; i < 4; i++) { int index = i; Style.Text(inspector, (i + 1) + "  " + labels[profile.phases[i]], 22, 403 + i * 43, 190, 33, 17); Style.Button(inspector, "↕", 223, 400 + i * 43, 46, 33, () => { int other = (index + 1) % 4; var t = profile.phases[index]; profile.phases[index] = profile.phases[other]; profile.phases[other] = t; Save(); Instruments(); }); }
                Style.Button(inspector, "执行 12 轮并比较", 20, 595, 248, 43, () => LoopExperiment(false));
            }
            else Style.Text(inspector, "在设备上直接拖动旋钮调参。Shift 拖动可微调。\n\n需要精确数值时，点击部件打开校准盒。\n\n读数仪可拖动和收起。", 20, 427, 252, 157, 18, Style.Muted);
            for (int i = controlsStart; i < inspector.childCount; i++)
            {
                var r = inspector.GetChild(i) as RectTransform;
                if (r) r.anchoredPosition += new Vector2(0, 320);
            }
        }
        void Observe()
        {
            SetDebugOpen(true);
            try
            {
                if (level.id == "B01") { ProbeMeasure(); return; }
                if (level.id == "B07") { SetStatus("选一个步长，运行相同预算，比较真实轨迹。"); return; }
                var engine = new GraphEngine(profile.modules, (level.id == "B04" || level.id == "B05") ? profile.Rules() : null);
                var args = InputSample(); var e = engine.Evaluate(ActiveGraph, args);
                bool gradients = level.id == "B04" || level.id == "B05";
                if (gradients) { e.accumulate = profile.accumulate || level.id == "B04"; e.Backward("goal", profile.reverseOrder || level.id == "B05"); }
                instrument.color = Hardware.Phosphor; board.Show(e, gradients); instrument.text = string.Join("\n", e.outputs.Select(p => p.Key + "  " + p.Value.value.ToString("0.####")));
                var inputNames = new HashSet<string>(ActiveGraph.nodes.Where(n => n.kind == NodeKind.Input).Select(n => n.name));
                resultTable.text = gradients ? string.Join("\n", e.parameters.Values.Select(p => p.name + " 梯度 " + p.gradient.ToString("0.###"))) : "样本 " + (sampleIndex % 7 + 1) + "\n" + string.Join("  ", args.Where(p => inputNames.Contains(p.Key)).Take(5).Select(p => p.Key + " " + p.Value.ToString("0.##")));
                if (e.outputs.ContainsKey("loss"))
                {
                    double value = e.Output("loss");
                    if (level.id == "A07" && !averageTab) value = AverageLoss();
                    plot.series = level.id == "A07" && !averageTab ? "平均损失" : "损失";
                    trace.Add(value); if (trace.Count > 300) trace.RemoveAt(0); plot.Set(trace);
                    if (level.id == "A07") instrument.text += "\n四件平均 " + value.ToString("0.####");
                }
                else
                {
                    var first = e.outputs.First(); double value = first.Value.value; plot.series = "输出 " + first.Key;
                    if (gradients && e.parameters.Count > 0) { var parameter = e.parameters.Values.First(); value = parameter.gradient; plot.series = "梯度 " + parameter.name; }
                    trace.Add(value); if (trace.Count > 300) trace.RemoveAt(0); plot.Set(trace);
                }
                if (level.id == "P00") { if (training == null || training.step == 0) beforeObserved = true; else if (training.step > 0) afterObserved = true; }
                SetStatus(gradients ? "模块读数现在显示回流梯度。对照仪表检查是否丢失贡献。" : "读数来自当前线路的实际计算。更换样本，观察相同设备的响应。"); Save();
            }
            catch (Exception e) { SetStatus("线路检查：" + e.Message); instrument.text = "信号未到达"; }
        }
        Dictionary<string, double> InputSample()
        {
            if (averageTab) return new Dictionary<string, double> { { "L1", 1 }, { "L2", 2 }, { "L3", 3 }, { "L4", 4 } };
            if (level.id == "B02" || level.id == "B03") return new Dictionary<string, double> { { "a", sampleIndex % 2 == 0 ? 2 : -2 }, { "b", sampleIndex % 3 == 0 ? 3 : 0 }, { "g", sampleIndex % 2 == 0 ? 1 : -.5 } };
            if (level.id == "B06") return new Dictionary<string, double> { { "w", 2 }, { "g", sampleIndex % 2 == 0 ? 3 : -2 }, { "eta", .1 } };
            var samples = Missions.Samples().Concat(Missions.Samples(true)).ToList(); return samples[sampleIndex % samples.Count].Inputs();
        }
        double AverageLoss()
        {
            var engine = new GraphEngine(profile.modules); var inputs = new Dictionary<string, double>(); var samples = Missions.Samples();
            for (int i = 0; i < 4; i++) inputs["L" + (i + 1)] = engine.Evaluate(graph, samples[i].Inputs()).Output("loss");
            return new GraphEngine().Evaluate(profile.average, inputs).Output("mean");
        }
        void ToggleTraining()
        {
            SetDebugOpen(true);
            if (running) { running = false; Save(); SetStatus("训练暂停。"); return; }
            try { EnsureTraining(); if (training.step >= trainingBudget) trainingBudget += 120; running = true; SetStatus("CPU 正在执行你的计算图，每轮都会更新实际参数。"); }
            catch (Exception e) { SetStatus(e.Message); }
        }
        void EnsureTraining()
        {
            if (training != null) return;
            if (level.id == "P00" && !beforeObserved) throw new InvalidOperationException("先送入样本，保留训练前的结果。");
            if (level.id == "B09" && (!Missions.CheckRule(profile.addRule, false).passed || !Missions.CheckRule(profile.multiplyRule, true).passed || !Missions.CheckOptimizer(profile.optimizer).passed)) throw new InvalidOperationException("保存的反向规则或更新器不能通过检查，请回去修复。");
            training = new TrainingSession(graph, profile.modules, level.id == "P00" ? null : profile.Rules(), level.id == "P00" ? null : profile.optimizer, Missions.Samples(), level.id == "P00" ? null : profile.average);
            training.learningRate = level.id == "P00" ? .2 : eta;
            if (level.id == "B09") { training.phases = profile.phases; training.reverseOrder = profile.reverseOrder; training.accumulate = profile.accumulate; }
            trace.Clear(); trace.Add(training.loss); if (level.id == "P00") trainingBudget = 80;
        }
        void OneStep()
        { running = false; try { EnsureTraining(); training.Tick(); trace.Add(training.loss); UpdateTrainingUI(); Save(); } catch (Exception e) { SetStatus(e.Message); } }
        void UpdateTrainingUI()
        {
            SetDebugOpen(true);
            instrument.color = Hardware.Phosphor; instrument.text = "平均损失  " + Style.Scalar(training.loss) + "\n训练步骤  " + training.step;
            var engine = new GraphEngine(profile.modules); var e = engine.Evaluate(graph, InputSample()); board.Show(e); plot.series = "训练平均损失"; plot.domain = "训练步"; plot.Set(trace);
            resultTable.text = string.Join("\n", engine.Parameters(graph).Take(3).Select(p => p.name + "  " + p.Value.ToString("0.####")));
        }
        void Check()
        {
            SetDebugOpen(true);
            running = false; CheckResult result;
            try
            {
                if (averageTab)
                {
                    result = Missions.CheckAverage(profile.average); SetStatus(result.message);
                    if (result.passed) { Save(); Modal("平均器通过校准", result.message + "\n\n同一组四件样本的损失会送进你搭好的平均器。", "装配完整机器 →", () => { averageTab = false; undo.Clear(); redo.Clear(); BuildWorkshop(); }); }
                    return;
                }
                switch (level.id)
                {
                    case "P00": result = new CheckResult { passed = beforeObserved && afterObserved && training != null && training.loss < .02, message = "需要观察同一机器训练前后，并送入训练后的样本。训练后平均损失应 < 0.02。" }; break;
                    case "B01": result = new CheckResult { passed = probeRecorded.Count >= 3, message = "需要在三个不同旋钮状态分别记录正确的局部变化率。" }; break;
                    case "B04": result = CheckGradients(false); break;
                    case "B05": result = CheckGradients(true); break;
                    case "B07": result = new CheckResult { passed = unstableObserved && stableObserved, message = "比较一次不稳定轨迹，并让稳定步长在 20 步内将参考损失降到 0.0001。" }; break;
                    case "B08": result = LoopExperiment(true); break;
                    default:
                        result = Missions.Check(level.id, graph, profile);
                        if (level.id == "B09" && (training == null || training.step < 8 || training.loss >= .002)) result = CheckResult.Fail("需要用当前设备实际训练至少 8 步，并将训练平均损失降到 0.002 以下。");
                        break;
                }
                resultTable.text = string.Join("\n", result.rows.Take(5)); SetStatus(result.message);
                if (!result.passed) { instrument.text = "验收未通过\n检查输出与线路"; if (result.rows.Count == 0) resultTable.text = result.message; instrument.color = new Color(1, .65f, .46f); return; }
                if (level.id == "B02") profile.addRule = ProfileStore.Copy(graph);
                if (level.id == "B03") profile.multiplyRule = ProfileStore.Copy(graph);
                if (level.id == "B06") profile.optimizer = ProfileStore.Copy(graph);
                if (!profile.completed.Contains(level.id)) profile.completed.Add(level.id); Save();
                var next = campaign.levels.FirstOrDefault(l => Unlocked(l) && !profile.completed.Contains(l.id));
                string text = "知识点：" + level.concept + "\n\n设备通过实际数值验收。\n解锁：" + level.reward;
                if (level.id == "B09") text += "\n\n学习引擎已修复。后续将扩展到非线性网络、张量、卷积与手写数字分类。当前可玩切片到此结束。";
                Modal("维修完成 ✓", text, next == null ? "查看工坊路线" : "下一份委托 · " + next.id, () => { if (next == null) Map(); else OpenLevel(next.id); });
            }
            catch (Exception e) { SetStatus("验收停止：" + e.Message); }
        }
        CheckResult CheckGradients(bool shared)
        {
            var engine = new GraphEngine(profile.modules, profile.Rules()); var result = new CheckResult { passed = true };
            var graphs = shared ? new[] { Recipes.Shared(), Recipes.Shared(true) } : new[] { graph };
            foreach (var g in graphs) foreach (var sample in Missions.Samples())
            {
                var args = sample.Inputs(); var e = engine.Evaluate(g, args); e.accumulate = shared ? profile.accumulate : true; e.Backward("goal", shared || profile.reverseOrder);
                foreach (var slot in engine.Parameters(g))
                {
                    double old = slot.Value, delta = .00001, plus, minus;
                    try { slot.Value = old + delta; plus = engine.Evaluate(g, args).Output("goal"); slot.Value = old - delta; minus = engine.Evaluate(g, args).Output("goal"); }
                    finally { slot.Value = old; }
                    double expected = (plus - minus) / (2 * delta), actual = e.parameters[slot.key].gradient; bool good = Math.Abs(expected - actual) < .0001;
                    result.passed &= good; result.rows.Add(slot.name + "  回流 " + actual.ToString("0.###") + " / 数值 " + expected.ToString("0.###") + (good ? " ✓" : " ×"));
                }
            }
            result.message = result.passed ? "梯度与数值变化率一致。" : "回流丢失了影响。检查运算依赖顺序或贡献收集方式。"; return result;
        }
        void ProbeControls()
        {
            Style.Button(inspector, "旋钮状态 " + (probeState + 1) + " / 3", 20, 403, 248, 38, () => { probeState = (probeState + 1) % 3; Instruments(); ProbeMeasure(); });
            var epsilonLabel = Style.Text(inspector, "epsilon = " + epsilon.ToString("0.0000"), 20, 452, 248, 27, 15, Style.Muted);
            Style.Slider(inspector, 20, 483, 248, -4, -1, (float)Math.Log10(epsilon), v => { epsilon = Math.Pow(10, v); epsilonLabel.text = "epsilon = " + epsilon.ToString("0.0000"); ProbeMeasure(); });
            Style.Text(inspector, "你的变化率估计", 20, 523, 248, 27, 15, Style.Muted);
            Style.Field(inspector, estimate.ToString("0.###", CultureInfo.InvariantCulture), 20, 555, 120, s => { if (Parse(s, out var value)) estimate = value; });
            Style.Button(inspector, "记录", 153, 555, 115, 37, RecordProbe);
            Style.Text(inspector, "已记录 " + probeRecorded.Count + " / 3 状态\n变化率 ≈ (新损失−原损失) / epsilon", 20, 612, 258, 53, 14, Style.Muted);
        }
        double ProbeMeasure()
        {
            var engine = new GraphEngine(); var parameter = engine.Parameters(graph)[0]; parameter.Value = new[] { -1.0, 0.0, 2.0 }[probeState];
            double baseValue = parameter.Value; var s = Missions.Samples()[0]; var e = engine.Evaluate(graph, s.Inputs()); double loss = e.Output("loss"), shifted;
            try { parameter.Value = baseValue + epsilon; shifted = engine.Evaluate(graph, s.Inputs()).Output("loss"); }
            finally { parameter.Value = baseValue; }
            board.Show(e); instrument.text = "原损失 " + loss.ToString("0.00000") + "\n新损失 " + shifted.ToString("0.00000");
            resultTable.text = "旋钮 w1 = " + baseValue.ToString("0.###") + "\nΔw = " + epsilon.ToString("0.0000") + "\nΔloss = " + (shifted - loss).ToString("0.000000") + "\n计算比值，再在下面记录。";
            return (shifted - loss) / epsilon;
        }
        void RecordProbe()
        {
            double expected = ProbeMeasure();
            if (Math.Abs(estimate - expected) < .035) { probeRecorded.Add(probeState); SetStatus("这个状态的局部变化率记录正确。继续检查另外两个状态。"); Instruments(); ProbeMeasure(); }
            else SetStatus("估计尚未吻合。用损失变化除以旋钮位移，注意符号；可减小 epsilon 再试。");
        }
        void RateExperiment(double rate)
        {
            SetDebugOpen(true); quadraticW = -2; trace.Clear(); trace.Add(8);
            var engine = new GraphEngine(); Evaluation last = null;
            for (int i = 0; i < 20; i++)
            {
                last = engine.Evaluate(profile.optimizer, new Dictionary<string, double> { { "w", quadraticW }, { "g", quadraticW - 2 }, { "eta", rate } }); quadraticW = last.Output("newW"); trace.Add(.5 * Math.Pow(quadraticW - 2, 2));
            }
            if (trace.Last() > trace[0] * 1.1) unstableObserved = true;
            if (trace.Last() < .0001) stableObserved = true;
            Instruments(); board.Show(last); instrument.text = "eta " + rate + "  /  20 步\n损失 " + trace.Last().ToString("0.000000");
            resultTable.text = "w: −2 → " + quadraticW.ToString("0.####") + "\n目标 w=2\n" + (trace.Last() > 8 ? "越走越远：步长不稳定。" : "观察示波器中的下降速度。"); plot.Set(trace);
        }
        CheckResult LoopExperiment(bool checking)
        {
            SetDebugOpen(true); var result = new CheckResult();
            try
            {
                var current = ProfileStore.Copy(graph); var reference = ProfileStore.Copy(graph);
                foreach (var p in new GraphEngine().Parameters(current)) p.Value = 0;
                foreach (var p in new GraphEngine().Parameters(reference)) p.Value = 0;
                var player = new TrainingSession(current, null, profile.Rules(), profile.optimizer, Missions.Samples(), profile.average) { phases = profile.phases, reverseOrder = profile.reverseOrder, accumulate = profile.accumulate, learningRate = .1 };
                var wanted = new TrainingSession(reference, null, profile.Rules(), profile.optimizer, Missions.Samples(), profile.average) { learningRate = .1 };
                bool pass = true; trace.Clear(); trace.Add(player.loss);
                for (int i = 0; i < 12; i++) { player.Tick(); wanted.Tick(); trace.Add(player.loss); pass &= Math.Abs(player.loss - wanted.loss) < 1e-9; }
                var playerParams = new GraphEngine().Parameters(current); var wantedParams = new GraphEngine().Parameters(reference);
                for (int i = 0; i < playerParams.Count; i++) pass &= Math.Abs(playerParams[i].Value - wantedParams[i].Value) < 1e-8;
                result.passed = pass; result.message = pass ? "连续 12 轮结果与参考流程一致。每步都使用当前正向值，并清空旧梯度。" : "连续更新与参考不一致。清空旧梯度必须先于反向，更新必须等待当前反向。";
                instrument.text = "12 轮平均损失\n" + player.loss.ToString("0.00000"); resultTable.text = "参考 " + wanted.loss.ToString("0.00000") + "\n差值 " + Math.Abs(player.loss - wanted.loss).ToString("0.000000"); plot.Set(trace);
                board.Show(new GraphEngine().Evaluate(current, Missions.Samples()[0].Inputs()));
            }
            catch (Exception e) { result.message = "流程停止：" + e.Message; }
            if (!checking) SetStatus(result.message); return result;
        }
        static bool Parse(string s, out double value) { return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value) || double.TryParse(s, NumberStyles.Float, CultureInfo.CurrentCulture, out value); }
        void SetStatus(string text) { message = text; if (status) status.text = text; }
        RectTransform Overlay()
        {
            var r = Style.Box(canvas.transform, "Modal Scrim", 0, 0, 1600, 900, new Color(.08f, .10f, .10f, .72f), false).rectTransform; activeOverlay = r; return r;
        }
        void Modal(string title, string body, string button, Action action, bool cancel = false)
        {
            var overlay = Overlay(); var box = Hardware.Plate(overlay, "Message Plate", 410, 180, 780, 535, Style.Panel);
            Style.Text(box.transform, title, 34, 29, 710, 56, 32, Style.Brass);
            Style.Text(box.transform, body, 36, 109, 704, 324, 21);
            Style.Button(box.transform, button, cancel ? 382 : 35, 457, cancel ? 357 : 708, 49, () => { Destroy(overlay.gameObject); action(); });
            if (cancel) Style.Button(box.transform, "继续保留", 35, 457, 327, 49, () => Destroy(overlay.gameObject));
        }
        void Controls()
        {
            Modal("工作台操作手册", "零件：点托盘或数字键拿取，在空位左键安装。\n接线：点源模块右侧插孔，带着预览线点目标插孔。\n移动：拖动模块的实体面板。滚轮移动工作台，＋/− 缩放。\n调参：拖动实体旋钮；Shift 微调。点部件可输入精确值。\n拆线：选模块，点‘断开输入’。\n撤销 / 重做：Ctrl+Z / Ctrl+Y。Esc 取消接线并暂停。\n送入样本：读数来自当前线路；下一样本检查新输入。\n验收：运行多组数值规格，成功后解锁下一委托。\n工作台自动保存；训练可暂停、单步或继续。", "回到工作台", () => { });
        }
        void Settings()
        {
            var overlay = Overlay(); var box = Hardware.Plate(overlay, "Settings Plate", 485, 233, 630, 400, Style.Panel);
            Style.Text(box.transform, "工坊设置", 31, 29, 560, 47, 32, Style.Brass);
            Style.Text(box.transform, "继电器音量", 33, 103, 240, 31, 21); Style.Slider(box.transform, 286, 100, 292, 0, 1, profile.volume, v => { profile.volume = v; Sound.Volume = v; });
            Style.Button(box.transform, profile.reduceMotion ? "减少动画：开启" : "减少动画：关闭", 32, 162, 563, 45, () => { profile.reduceMotion = !profile.reduceMotion; Save(); Destroy(overlay.gameObject); Settings(); });
            Style.Button(box.transform, Screen.fullScreen ? "切换为窗口" : "切换为全屏", 32, 225, 563, 45, () => Screen.fullScreen = !Screen.fullScreen);
            Style.Button(box.transform, "保存并返回", 32, 323, 563, 45, () => { Save(); Destroy(overlay.gameObject); });
        }
        IEnumerator SmokeCapture()
        {
            var args = Environment.GetCommandLineArgs(); string output = null;
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "--capture-output") output = args[i + 1];
            if (output == null) output = Path.Combine(Application.persistentDataPath, "smoke"); Directory.CreateDirectory(output);
            yield return new WaitForSecondsRealtime(1);
            yield return new WaitForEndOfFrame(); CaptureNative(Path.Combine(output, "01-main-menu.png"));
            yield return new WaitForSecondsRealtime(.5f);
            Map(); yield return new WaitForEndOfFrame(); CaptureNative(Path.Combine(output, "02-campaign.png"));
            yield return new WaitForSecondsRealtime(.5f);
            OpenLevel("P00"); Observe(); EnsureTraining(); for (int i = 0; i < 80; i++) training.Tick(); UpdateTrainingUI(); Observe(); Check();
            bool tutorialFlow = profile.completed.Contains("P00");
            var nextJob = FindObjectsOfType<Button>().First(b => b.gameObject.name == "下一份委托 · A01"); nextJob.onClick.Invoke();
            yield return null;
            // Exercise both actual pin callbacks, rather than injecting the connection into the graph.
            var sourceNode = boardRoot.Find(graph.nodes.First(n => n.kind == NodeKind.Input).name);
            var targetNode = boardRoot.Find(graph.Output("y").name);
            sourceNode.GetComponentsInChildren<Button>().First(b => b.gameObject.name == "").onClick.Invoke();
            targetNode.GetComponentsInChildren<Button>().First(b => b.gameObject.name == "").onClick.Invoke(); Observe();
            bool pinConnection = Missions.Check("A01", graph, profile).passed;
            yield return new WaitForEndOfFrame(); CaptureNative(Path.Combine(output, "04-first-circuit.png"));
            Check(); tutorialFlow &= profile.completed.Contains("A01");
            FindObjectsOfType<Button>().First(b => b.gameObject.name == "下一份委托 · A02").onClick.Invoke(); yield return null;
            Check(); yield return null;
            bool inlineFailure = !FindObjectsOfType<RectTransform>().Any(r => r.name == "Modal Scrim") && instrument.text.Contains("验收未通过");
            yield return new WaitForEndOfFrame(); CaptureNative(Path.Combine(output, "05-inline-failure.png"));
            int initialCount = graph.nodes.Count;
            AddNode(NodeKind.Parameter); yield return null;
            bool takeBeforeInstall = board.Placing && graph.nodes.Count == initialCount;
            ClickBoard(new Vector2(graph.nodes[0].x + 84, graph.nodes[0].y + 62));
            bool overlapBlocked = board.Placing && graph.nodes.Count == initialCount;
            board.Cancel(); bool cancelledWithoutMutation = !board.Placing && graph.nodes.Count == initialCount;
            AddNode(NodeKind.Parameter); yield return new WaitForEndOfFrame();
            board.PreviewPlacement(new Vector2(564, 462)); CaptureNative(Path.Combine(output, "08-placement-preview.png"));
            ClickBoard(new Vector2(564, 462)); yield return null;
            var installed = graph.nodes.FirstOrDefault(n => n.kind == NodeKind.Parameter);
            bool installAtPointer = installed != null && graph.nodes.Count == initialCount + 1 && installed.x == 480 && installed.y == 400;
            double priorValue = installed == null ? 0 : installed.value;
            var knob = boardRoot.GetComponentsInChildren<RotaryKnob>().FirstOrDefault(k => !k.locked);
            if (knob != null)
            {
                var pointer = new PointerEventData(EventSystem.current) { delta = new Vector2(0, 20) };
                ExecuteEvents.Execute<IBeginDragHandler>(knob.gameObject, pointer, ExecuteEvents.beginDragHandler);
                ExecuteEvents.Execute<IDragHandler>(knob.gameObject, pointer, ExecuteEvents.dragHandler);
                ExecuteEvents.Execute<IEndDragHandler>(knob.gameObject, pointer, ExecuteEvents.endDragHandler);
            }
            bool rotaryEditsRealParameter = installed != null && installed.value > priorValue;
            if (installed != null) Undo(false); yield return null;
            bool rotaryUndo = graph.nodes.FirstOrDefault(n => n.kind == NodeKind.Parameter)?.value == priorValue;
            inspector.gameObject.SetActive(false); yield return null;
            bool hiddenBoxHasNoShadow = !screen.Find("Portable Control Box Shadow").gameObject.activeSelf;
            // A diagnostic fixture is never loaded into a real player's save. It exercises the actual workshop and trainer.
            foreach (var id in Missions.Implemented.Take(16)) if (!profile.completed.Contains(id)) profile.completed.Add(id);
            profile.addRule = Recipes.Rule(false); profile.multiplyRule = Recipes.Rule(true); profile.optimizer = Recipes.Optimizer(); profile.reverseOrder = profile.accumulate = true; profile.phases = new[] { "forward", "clear", "backward", "update" };
            profile.average = Recipes.Average(); profile.workspaces.Add(new Workspace { levelId = "A07", graph = Recipes.Affine(1.75, .5, -.25, true) });
            OpenLevel("B09");
            var positions = new[] {
                new Vector2(48, 62), new Vector2(48, 214), new Vector2(235, 350), new Vector2(426, 350), new Vector2(648, 350),
                new Vector2(252, 62), new Vector2(252, 214), new Vector2(443, 134), new Vector2(650, 134), new Vector2(854, 60),
                new Vector2(846, 350), new Vector2(1040, 490), new Vector2(1430, 350), new Vector2(1040, 350), new Vector2(1040, 134),
                new Vector2(1230, 134), new Vector2(1430, 134), new Vector2(1630, 134)
            };
            for (int i = 0; i < graph.nodes.Count && i < positions.Length; i++) { graph.nodes[i].x = positions[i].x; graph.nodes[i].y = positions[i].y; }
            board.Build(); boardZoom = .73f; boardRoot.localScale = Vector3.one * boardZoom;
            inspector.anchoredPosition = new Vector2(1246, -314); meterDock.anchoredPosition = new Vector2(366, -579);
            EnsureTraining(); for (int i = 0; i < 180; i++) { training.Tick(); trace.Add(training.loss); } UpdateTrainingUI();
            yield return new WaitForEndOfFrame(); CaptureNative(Path.Combine(output, "03-workshop.png"));
            string measured = instrument.text; Vector2 priorPosition = boardRoot.anchoredPosition;
            Select(graph.nodes.First(n => n.kind == NodeKind.Parameter)); yield return null;
            bool selectionKeepsReadout = instrument.text == measured;
            SetDebugOpen(false); SetDebugOpen(true);
            bool dockKeepsCanvas = boardRoot.anchoredPosition == priorPosition;
            Notebook(); yield return null;
            yield return new WaitForEndOfFrame(); CaptureNative(Path.Combine(output, "07-technical-manual.png"));
            FindObjectsOfType<Button>().First(b => b.gameObject.name == "回到装配").onClick.Invoke(); yield return null;
            bool manualKeepsCanvas = boardRoot.anchoredPosition == priorPosition && instrument.text == measured;
            yield return new WaitForEndOfFrame(); CaptureNative(Path.Combine(output, "06-object-inspector.png"));
            yield return new WaitForSecondsRealtime(.5f);
            bool good = takeBeforeInstall && overlapBlocked && cancelledWithoutMutation && installAtPointer && rotaryEditsRealParameter && rotaryUndo && hiddenBoxHasNoShadow && tutorialFlow && pinConnection && inlineFailure && selectionKeepsReadout && dockKeepsCanvas && manualKeepsCanvas && training.loss < .002 && Missions.Check("B09", graph, profile).passed;
            File.WriteAllText(Path.Combine(output, "player-smoke.json"), "{\"passed\":" + (good ? "true" : "false") + ",\"tutorialFlow\":" + (tutorialFlow ? "true" : "false") + ",\"pinConnection\":" + (pinConnection ? "true" : "false") + ",\"inlineFailure\":" + (inlineFailure ? "true" : "false") + ",\"selectionKeepsReadout\":" + (selectionKeepsReadout ? "true" : "false") + ",\"dockKeepsCanvas\":" + (dockKeepsCanvas ? "true" : "false") + ",\"manualKeepsCanvas\":" + (manualKeepsCanvas ? "true" : "false") + ",\"steps\":180,\"loss\":" + training.loss.ToString("R", CultureInfo.InvariantCulture) + ",\"ui\":\"Unity uGUI native player\"}");
            File.WriteAllText(Path.Combine(output, "interaction-smoke.json"), JsonUtility.ToJson(new InteractionReport {
                passed = good, takeBeforeInstall = takeBeforeInstall, overlapBlocked = overlapBlocked, cancelledWithoutMutation = cancelledWithoutMutation,
                installAtPointer = installAtPointer, rotaryEditsRealParameter = rotaryEditsRealParameter, rotaryUndo = rotaryUndo, hiddenBoxHasNoShadow = hiddenBoxHasNoShadow
            }, true));
            Debug.Log("LEARNING_FOUNDRY_PLAYER_SMOKE " + good); Application.Quit(good ? 0 : 1);
        }
        [Serializable] sealed class InteractionReport
        {
            public bool passed, takeBeforeInstall, overlapBlocked, cancelledWithoutMutation, installAtPointer, rotaryEditsRealParameter, rotaryUndo, hiddenBoxHasNoShadow;
        }
        void ClickBoard(Vector2 position)
        {
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left,
                pointerPressRaycast = new RaycastResult { module = canvas.GetComponent<GraphicRaycaster>() },
                position = RectTransformUtility.WorldToScreenPoint(uiCamera, boardRoot.TransformPoint(new Vector3(position.x, -position.y, 0))) };
            ExecuteEvents.Execute<IPointerClickHandler>(boardRoot.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }
        void CaptureNative(string path)
        {
            // Render the real Unity camera to a texture so a hidden/minimized test window still yields valid pixels.
            Canvas.ForceUpdateCanvases(); var target = new RenderTexture(1600, 900, 24); var previous = RenderTexture.active;
            uiCamera.targetTexture = target; uiCamera.Render(); RenderTexture.active = target;
            var image = new Texture2D(1600, 900, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG()); uiCamera.targetTexture = null; RenderTexture.active = previous; target.Release(); Destroy(target); Destroy(image);
        }
    }
}
