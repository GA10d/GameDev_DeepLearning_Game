using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using LearningFoundry.Core;

namespace LearningFoundry.UI
{
    public static class Style
    {
        public static readonly Color Ink = new Color(.145f, .205f, .175f);
        public static readonly Color Panel = new Color(.89f, .86f, .75f);
        public static readonly Color Steel = new Color(.57f, .61f, .54f);
        public static readonly Color Cream = new Color(.16f, .20f, .17f);
        public static readonly Color Muted = new Color(.18f, .23f, .19f);
        public static readonly Color Cyan = new Color(.21f, .38f, .28f);
        public static readonly Color Brass = new Color(.71f, .38f, .19f);
        public static readonly Color Red = new Color(.647f, .247f, .208f);
        public static readonly Color Frame = new Color(.10f, .15f, .13f);
        public static readonly Color Paper = new Color(.89f, .86f, .75f);
        static Font font, mono; static Sprite rounded;
        public static Font Font
        {
            get
            {
                if (!font) font = Resources.Load<Font>("Fonts/SourceHanSansCN-Regular");
                if (!font) font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Arial" }, 20);
                return font;
            }
        }
        public static Text Number(Transform parent, string value, float x, float y, float w, float h, int size = 18, Color? color = null, TextAnchor align = TextAnchor.UpperLeft)
        {
            if (!mono) mono = Resources.Load<Font>("Fonts/IBMPlexMono-Regular");
            var text = Text(parent, value, x, y, w, h, size, color, align);
            if (mono) text.font = mono;
            return text;
        }
        static Sprite Rounded
        {
            get
            {
                if (rounded) return rounded;
                var t = new Texture2D(32, 32, TextureFormat.RGBA32, false);
                t.wrapMode = TextureWrapMode.Clamp; t.filterMode = FilterMode.Bilinear;
                for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
                {
                    float dx = Mathf.Max(0, Mathf.Abs(x - 15.5f) - 9.5f), dy = Mathf.Max(0, Mathf.Abs(y - 15.5f) - 9.5f);
                    t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(6.5f - Mathf.Sqrt(dx * dx + dy * dy))));
                }
                t.Apply(); rounded = Sprite.Create(t, new Rect(0, 0, 32, 32), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(8, 8, 8, 8)); return rounded;
            }
        }
        public static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
        {
            var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 1); r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h); return r;
        }
        public static Image Box(Transform parent, string name, float x, float y, float w, float h, Color color, bool round = false)
        {
            var r = Rect(parent, name, x, y, w, h); var i = r.gameObject.AddComponent<Image>(); i.color = color;
            if (round) { i.sprite = Rounded; i.type = Image.Type.Sliced; } return i;
        }
        public static Text Text(Transform parent, string value, float x, float y, float w, float h, int size = 18, Color? color = null, TextAnchor align = TextAnchor.UpperLeft)
        {
            // CJK font ascent / descent is taller than the old system-font layout.
            var r = Rect(parent, "Text", x, y, w, Mathf.Max(h, size * 1.65f)); var t = r.gameObject.AddComponent<Text>(); t.font = Font; t.text = value; t.fontSize = size; t.color = color ?? Cream;
            t.alignment = align; t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate; t.raycastTarget = false; t.lineSpacing = 1.12f; return t;
        }
        public static Button Button(Transform parent, string title, float x, float y, float w, float h, Action action, Color? color = null)
        {
            var fill = color ?? Steel;
            var plate = Box(parent, title, x, y, w, h, fill); var b = plate.gameObject.AddComponent<Button>();
            if (fill.a > .01f)
            {
                plate.sprite = Hardware.Bevel(); plate.type = Image.Type.Sliced;
                b.transition = Selectable.Transition.SpriteSwap;
                var state = b.spriteState; state.highlightedSprite = Hardware.Bevel(); state.pressedSprite = Hardware.Bevel(true); state.selectedSprite = Hardware.Bevel(); state.disabledSprite = Hardware.Bevel(true); b.spriteState = state;
                Box(plate.transform, "Key Lip", 7, h - 6, w - 14, 1, new Color(0, 0, 0, .25f)).raycastTarget = false;
            }
            Color label = fill.r * .2126f + fill.g * .7152f + fill.b * .0722f < .5f ? Paper : Cream;
            Text(plate.transform, title, 6, 0, w - 12, h, 17, label, TextAnchor.MiddleCenter);
            b.onClick.AddListener(() => { Sound.Click(); action(); }); return b;
        }
        public static InputField Field(Transform parent, string value, float x, float y, float w, Action<string> onEnd)
        {
            var box = Box(parent, "Field", x, y, w, 36, Frame); box.sprite = Hardware.Bevel(true); box.type = Image.Type.Sliced; var field = box.gameObject.AddComponent<InputField>();
            field.textComponent = Text(box.transform, "", 8, 0, w - 16, 36, 18, Hardware.Phosphor, TextAnchor.MiddleLeft);
            field.text = value; field.onEndEdit.AddListener(s => onEnd(s)); return field;
        }
        public static Slider Slider(Transform parent, float x, float y, float width, float min, float max, float value, Action<float> action)
        {
            var r = Rect(parent, "Slider", x, y, width, 28); var s = r.gameObject.AddComponent<Slider>();
            Box(r, "Track", 0, 10, width, 6, Steel); var handleArea = Rect(r, "Handle Area", 8, 0, width - 16, 28);
            var handle = Box(handleArea, "Handle", 0, 4, 16, 20, Brass);
            handle.rectTransform.anchorMin = new Vector2(0, 0); handle.rectTransform.anchorMax = new Vector2(0, 1); handle.rectTransform.pivot = new Vector2(.5f, .5f);
            s.handleRect = handle.rectTransform; s.targetGraphic = handle; s.minValue = min; s.maxValue = max; s.value = value;
            s.onValueChanged.AddListener(v => action(v)); return s;
        }
        public static ScrollRect Scroll(Transform parent, float x, float y, float w, float h, float contentW, float contentH, out RectTransform content)
        {
            var outer = Box(parent, "Viewport", x, y, w, h, Color.white); outer.sprite = Hardware.MatSprite; outer.type = Image.Type.Tiled; outer.gameObject.AddComponent<RectMask2D>();
            var scroll = outer.gameObject.AddComponent<ScrollRect>(); scroll.viewport = outer.rectTransform;
            content = Rect(outer.transform, "Content", 0, 0, contentW, contentH); scroll.content = content; scroll.horizontal = contentW > w; scroll.vertical = contentH > h; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 50;
            if (contentH > h)
            {
                var track = Box(outer.transform, "Scroll Track", w - 9, 5, 6, h - 10, Steel);
                var bar = track.gameObject.AddComponent<Scrollbar>();
                var handle = Box(track.transform, "Scroll Handle", 0, 0, 6, h - 10, Muted);
                handle.rectTransform.anchorMin = Vector2.zero; handle.rectTransform.anchorMax = Vector2.one;
                handle.rectTransform.offsetMin = handle.rectTransform.offsetMax = Vector2.zero;
                bar.handleRect = handle.rectTransform; bar.targetGraphic = handle; bar.direction = Scrollbar.Direction.BottomToTop;
                scroll.verticalScrollbar = bar; scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            }
            return scroll;
        }
        public static void Rivets(Transform parent, float w, float h)
        {
            foreach (var p in new[] { new Vector2(8, 8), new Vector2(w - 12, 8), new Vector2(8, h - 12), new Vector2(w - 12, h - 12) })
            { Hardware.Shape(parent, "Screw", p.x - 1, p.y - 1, 7, 7, HardwareShape.Socket, Steel); }
        }
        public static string Scalar(double value)
        {
            double magnitude = Math.Abs(value);
            return value.ToString(magnitude > 0 && (magnitude < .0001 || magnitude >= 1000000) ? "0.###E+0" : "0.#####", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
    public static class Sound
    {
        static AudioSource source; static AudioClip click;
        public static float Volume = .25f;
        public static void Click()
        {
            if (Volume <= 0) return;
            if (!source) { var go = new GameObject("Workshop Audio"); source = go.AddComponent<AudioSource>(); UnityEngine.Object.DontDestroyOnLoad(go); }
            if (!click)
            {
                const int rate = 22050; var data = new float[1102];
                for (int i = 0; i < data.Length; i++) { float t = (float)i / rate; data[i] = Mathf.Sin(2 * Mathf.PI * 750 * t) * Mathf.Exp(-100 * t) * .18f; }
                click = AudioClip.Create("original-relay-click", data.Length, 1, rate, false); click.SetData(data, 0);
            }
            source.PlayOneShot(click, Volume);
        }
    }
    public class Lines : MaskableGraphic
    {
        public readonly List<Vector2[]> paths = new List<Vector2[]>();
        public bool grid, cables; public float spacing = 32, thickness = 2;
        public void Refresh() { SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (grid)
            {
                for (float x = 0; x < rectTransform.rect.width; x += spacing) for (float y = 0; y < rectTransform.rect.height; y += spacing)
                    Segment(vh, new Vector2(x - 1, -y), new Vector2(x + 1, -y), 1, new Color(.68f, .72f, .58f, .22f));
            }
            foreach (var path in paths)
                for (int i = 1; i < path.Length; i++)
                {
                    if (cables)
                    {
                        Segment(vh, path[i - 1] + new Vector2(2, -3), path[i] + new Vector2(2, -3), 9, new Color(0, 0, 0, .32f));
                        Segment(vh, path[i - 1], path[i], 6, color * .65f);
                        Segment(vh, path[i - 1] + new Vector2(0, 1), path[i] + new Vector2(0, 1), 2, color);
                    }
                    else Segment(vh, path[i - 1], path[i], thickness, color);
                }
        }
        static void Segment(VertexHelper vh, Vector2 a, Vector2 b, float width, Color c)
        {
            var d = (b - a).normalized; var normal = new Vector2(-d.y, d.x) * width * .5f; int start = vh.currentVertCount;
            vh.AddVert(a - normal, c, Vector2.zero); vh.AddVert(a + normal, c, Vector2.zero); vh.AddVert(b + normal, c, Vector2.zero); vh.AddVert(b - normal, c, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
        }
        public static Vector2[] Cable(Vector2 from, Vector2 to)
        {
            var result = new Vector2[25]; float bend = Mathf.Max(60, Mathf.Abs(to.x - from.x) * .45f);
            var a = from + new Vector2(bend, 0); var b = to - new Vector2(bend, 0);
            for (int i = 0; i < result.Length; i++) { float t = (float)i / (result.Length - 1), u = 1 - t; result[i] = u * u * u * from + 3 * u * u * t * a + 3 * u * t * t * b + t * t * t * to; }
            return result;
        }
    }
    public class NodeDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public GraphNode node; public RectTransform board; public Action before, changed, end;
        Vector2 offset;
        public void OnBeginDrag(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            before?.Invoke(); RectTransformUtility.ScreenPointToLocalPointInRectangle(board, e.position, e.pressEventCamera, out var p); offset = new Vector2(node.x, -node.y) - p;
        }
        public void OnDrag(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(board, e.position, e.pressEventCamera, out var p); p += offset;
            node.x = Mathf.Round(Mathf.Clamp(p.x, 16, board.rect.width - 200) / 16) * 16; node.y = Mathf.Round(Mathf.Clamp(-p.y, 16, board.rect.height - 150) / 16) * 16;
            ((RectTransform)transform).anchoredPosition = new Vector2(node.x, -node.y); changed?.Invoke();
        }
        public void OnEndDrag(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) end?.Invoke(); }
    }
    public sealed class GraphBoard
    {
        readonly RectTransform root; readonly GraphEngine engine; readonly List<ModuleSpec> modules;
        readonly Action edited, before; readonly Action<GraphNode> select, tuned; readonly Action<string> status;
        readonly bool readOnly;
        readonly Dictionary<string, Text> readouts = new Dictionary<string, Text>();
        readonly Dictionary<string, HardwareGraphic> lamps = new Dictionary<string, HardwareGraphic>(), knobs = new Dictionary<string, HardwareGraphic>();
        readonly Dictionary<string, Image> plates = new Dictionary<string, Image>();
        readonly List<string> signalOrder = new List<string>();
        readonly List<HardwareGraphic> inputSockets = new List<HardwareGraphic>();
        GraphSpec graph; Lines wires, previewWire; string pending; bool gradientDisplay;
        RectTransform ghost; NodeKind? placing; Action<Vector2> placeAction; Vector2 candidate; bool placementValid;
        public bool Placing { get { return placing.HasValue; } }
        public bool PendingCable { get { return !string.IsNullOrEmpty(pending); } }
        public bool PlacementValid { get { return placementValid; } }
        public GraphBoard(RectTransform root, GraphSpec graph, List<ModuleSpec> modules, bool readOnly, Action before, Action edited, Action<GraphNode> select, Action<string> status, Action<GraphNode> tuned = null)
        {
            this.root = root; this.graph = graph; this.modules = modules; engine = new GraphEngine(modules);
            this.readOnly = readOnly; this.before = before; this.edited = edited; this.select = select; this.status = status; this.tuned = tuned; Build();
        }
        Vector2 Size(GraphNode node)
        {
            if (node.kind == NodeKind.Module) return new Vector2(190, Mathf.Max(124, 55 + engine.Arity(node) * 30));
            if (node.kind == NodeKind.Parameter) return new Vector2(168, 124);
            if (node.kind == NodeKind.Add || node.kind == NodeKind.Multiply) return new Vector2(148, 114);
            return new Vector2(node.kind == NodeKind.Output ? 180 : 154, node.kind == NodeKind.Output ? 116 : 100);
        }
        public void Build()
        {
            foreach (Transform child in root) UnityEngine.Object.Destroy(child.gameObject);
            Cancel(); readouts.Clear(); lamps.Clear(); knobs.Clear(); plates.Clear(); signalOrder.Clear(); inputSockets.Clear();
            var background = root.GetComponent<Image>() ?? root.gameObject.AddComponent<Image>(); background.color = Color.clear;
            var input = root.GetComponent<BoardInput>() ?? root.gameObject.AddComponent<BoardInput>(); input.place = PlaceAt;
            var grid = Style.Rect(root, "Board Registration Marks", 0, 0, root.rect.width, root.rect.height).gameObject.AddComponent<Lines>(); grid.grid = true; grid.raycastTarget = false;
            wires = Style.Rect(root, "Physical Patch Cables", 0, 0, root.rect.width, root.rect.height).gameObject.AddComponent<Lines>(); wires.cables = true; wires.color = new Color(.68f, .66f, .40f); wires.raycastTarget = false;
            foreach (var n in graph.nodes)
            {
                var node = n; var size = Size(node); float w = size.x, h = size.y; int arity = engine.Arity(node);
                var rim = Style.Box(root, node.name, node.x, node.y, w, h, Color.clear);
                Style.Box(rim.transform, "Cast Shadow", 4, 6, w, h, new Color(0, 0, 0, .36f)).raycastTarget = false;
                var outline = Style.Box(rim.transform, "Selection Rim", -3, -3, w + 6, h + 6, Color.clear);
                outline.sprite = Hardware.Bevel(); outline.type = Image.Type.Sliced; outline.raycastTarget = false; plates[node.id] = outline;
                if (node.kind == NodeKind.Add || node.kind == NodeKind.Multiply || node.kind == NodeKind.Module)
                {
                    Hardware.Shape(rim.transform, "Cast Housing", 0, 0, w, h, HardwareShape.Chip, node.kind == NodeKind.Module ? new Color(.52f, .65f, .60f) : Hardware.Enamel);
                }
                else
                {
                    var face = Style.Box(rim.transform, "Enamel Housing", 0, 0, w, h, Hardware.Enamel);
                    face.sprite = Hardware.Bevel(); face.type = Image.Type.Sliced; face.raycastTarget = false;
                }
                Style.Rivets(rim.transform, w, h);
                Style.Number(rim.transform, Serial(node), 14, 9, w - 28, 21, 10, Style.Muted);
                if (node.kind == NodeKind.Parameter)
                {
                    Style.Text(rim.transform, node.name, 15, 34, 44, 30, 17);
                    var knob = Hardware.Shape(rim.transform, "Rotary Dial", 66, 30, 54, 54, HardwareShape.Knob, Hardware.Metal);
                    knob.raycastTarget = true; knob.dialValue = (float)node.value; knobs[node.id] = knob;
                    var control = knob.gameObject.AddComponent<RotaryKnob>(); control.node = node; control.graphic = knob; control.locked = readOnly;
                    control.before = before; control.changed = () => { UpdateParameterReadout(node); tuned?.Invoke(node); }; control.end = edited;
                    AddDisplay(rim.transform, node, 15, 90, w - 30, 25, 16);
                }
                else if (node.kind == NodeKind.Output)
                {
                    Style.Text(rim.transform, "监测 / " + node.name, 15, 24, w - 32, 25, 16);
                    AddDisplay(rim.transform, node, 17, 54, w - 34, 39, 23);
                    Style.Number(rim.transform, "SIGNAL MONITOR", 18, 97, w - 35, 16, 9, Style.Muted);
                }
                else if (node.kind == NodeKind.Add || node.kind == NodeKind.Multiply)
                {
                    Style.Text(rim.transform, node.kind == NodeKind.Add ? "加法器" : "乘法器", 16, 26, 80, 24, 15);
                    Style.Number(rim.transform, node.kind == NodeKind.Add ? "+" : "×", 75, 23, 57, 51, 42, Style.Cream, TextAnchor.MiddleCenter);
                    AddDisplay(rim.transform, node, 39, h - 33, w - 54, 24, 15);
                }
                else if (node.kind == NodeKind.Module)
                {
                    Style.Text(rim.transform, node.name, 20, 28, w - 35, 26, 16);
                    for (int i = 0; i < 4; i++) Style.Box(rim.transform, "Cooling Slot", w - 46, 61 + i * 6, 24, 2, Hardware.Dark).raycastTarget = false;
                    AddDisplay(rim.transform, node, 58, h - 33, w - 74, 24, 15);
                }
                else
                {
                    Style.Text(rim.transform, (node.kind == NodeKind.Input ? "输入 " : "常量 ") + node.name, 16, 28, w - 32, 28, 17);
                    AddDisplay(rim.transform, node, 15, 63, w - 30, 26, 16);
                }
                var hit = rim.gameObject.AddComponent<Button>(); hit.transition = Selectable.Transition.None;
                hit.onClick.AddListener(() => { Highlight(node); select(node); });
                var drag = rim.gameObject.AddComponent<NodeDrag>(); drag.node = node; drag.board = root; drag.before = before; drag.changed = RefreshWires; drag.end = edited;
                for (int i = 0; i < arity; i++)
                {
                    int port = i; float py = InputY(node, i);
                    var pin = Style.Button(rim.transform, "", -18, py - 18, 36, 36, () => Connect(node, port), Color.clear);
                    var socket = Hardware.Shape(pin.transform, "Input Socket", 8, 8, 20, 20, HardwareShape.Socket, new Color(.66f, .43f, .22f)); inputSockets.Add(socket);
                    if (node.kind != NodeKind.Output) Style.Number(rim.transform, PortName(node, port), 17, py - 11, 27, 22, 12, Style.Cream);
                    pin.interactable = !readOnly;
                }
                if (node.kind != NodeKind.Output)
                {
                    var pin = Style.Button(rim.transform, "", w - 18, h * .5f - 18, 36, 36, () => StartCable(node), Color.clear);
                    Hardware.Shape(pin.transform, "Output Socket", 8, 8, 20, 20, HardwareShape.Socket, new Color(.40f, .58f, .52f)); pin.interactable = !readOnly;
                }
                lamps[node.id] = Hardware.Shape(rim.transform, "Signal Lamp", w - 29, 13, 8, 8, HardwareShape.Lamp, Style.Muted);
                if (node.kind == NodeKind.Parameter || node.kind == NodeKind.Constant) UpdateParameterReadout(node);
            }
            previewWire = Style.Rect(root, "Cable In Hand", 0, 0, root.rect.width, root.rect.height).gameObject.AddComponent<Lines>(); previewWire.cables = true; previewWire.raycastTarget = false; previewWire.color = new Color(.88f, .67f, .36f);
            RefreshWires();
        }
        void AddDisplay(Transform parent, GraphNode node, float x, float y, float w, float h, int fontSize)
        {
            var bezel = Style.Box(parent, "Readout Bezel", x - 2, y - 2, w + 4, h + 4, Style.Cream); bezel.raycastTarget = false;
            var display = Style.Box(parent, "Phosphor Window", x, y, w, h, Hardware.Dark); display.raycastTarget = false;
            readouts[node.id] = Style.Number(parent, "—", x + 4, y, w - 8, h, fontSize, Hardware.Phosphor, TextAnchor.MiddleRight);
        }
        void UpdateParameterReadout(GraphNode node)
        {
            if (readouts.ContainsKey(node.id)) readouts[node.id].text = Style.Scalar(node.value);
            if (knobs.TryGetValue(node.id, out var knob)) { knob.dialValue = (float)node.value; knob.Refresh(); }
        }
        string Serial(GraphNode n) { return n.kind == NodeKind.Input ? "SOURCE / " + n.name : n.kind == NodeKind.Output ? "READOUT" : n.kind == NodeKind.Parameter ? "CALIBRATION" : n.kind == NodeKind.Constant ? "FIXED VALUE" : n.kind == NodeKind.Module ? "MODEL CASSETTE" : "ALU / " + (n.kind == NodeKind.Add ? "01" : "02"); }
        float InputY(GraphNode node, int index) { return node.kind == NodeKind.Output ? 58 : 54 + index * 30; }
        string PortName(GraphNode n, int port)
        {
            if (n.kind == NodeKind.Module) { var spec = modules.Find(m => m.id == n.moduleId); return spec != null && port < spec.inputNames.Count ? spec.inputNames[port] : "?"; }
            return port == 0 ? "a" : "b";
        }
        void StartCable(GraphNode node)
        {
            Cancel(); pending = node.id; Highlight(node);
            foreach (var socket in inputSockets) { socket.color = new Color(.88f, .63f, .30f); socket.Refresh(); }
            status("拿起接线头：把预览线路插入目标左侧插孔。右键 / Esc 放下。");
        }
        void Connect(GraphNode node, int port)
        {
            if (string.IsNullOrEmpty(pending)) { status("先拿起源模块右侧的接线头，再插入这里。"); return; }
            if (node.id == pending) { status("同一个模块不能接回自身，请换一个插孔。"); return; }
            before(); while (node.inputs.Count <= port) node.inputs.Add(""); node.inputs[port] = pending; Cancel();
            RefreshWires(); edited(); status("插接完成。送入样本，检查设备的实际读数。");
        }
        public void Cancel()
        {
            pending = null; placing = null; placementValid = false; placeAction = null;
            if (ghost) UnityEngine.Object.Destroy(ghost.gameObject); ghost = null;
            if (previewWire) { previewWire.paths.Clear(); previewWire.Refresh(); }
            foreach (var socket in inputSockets) if (socket) { socket.color = new Color(.66f, .43f, .22f); socket.Refresh(); }
        }
        public void BeginPlacement(NodeKind kind, Action<Vector2> action)
        {
            Cancel(); placing = kind; placeAction = action;
            ghost = Style.Rect(root, "Part In Hand", 0, 0, 168, 124);
            var group = ghost.gameObject.AddComponent<CanvasGroup>(); group.alpha = .68f; group.blocksRaycasts = false;
            Hardware.Shape(ghost, "Placement Housing", 0, 0, 168, 124, HardwareShape.Chip, Hardware.Enamel);
            Hardware.Miniature(ghost, kind, 26, 25, 116, 56);
            Style.Text(ghost, "左键放置", 12, 88, 144, 29, 17, Style.Cream, TextAnchor.MiddleCenter);
            status("零件已拿起。移动到空位，左键安装；红色表示位置重叠。右键 / Esc 放回。");
        }
        bool Valid(Vector2 p)
        {
            var size = placing == NodeKind.Module ? new Vector2(190, 124) : placing == NodeKind.Add || placing == NodeKind.Multiply ? new Vector2(148, 114) : placing == NodeKind.Parameter ? new Vector2(168, 124) : new Vector2(154, 100);
            var rect = new Rect(p, size);
            if (p.x < 16 || p.y < 16 || rect.xMax > root.rect.width - 16 || rect.yMax > root.rect.height - 16) return false;
            foreach (var n in graph.nodes) if (rect.Overlaps(new Rect(new Vector2(n.x - 8, n.y - 8), Size(n) + new Vector2(16, 16)))) return false;
            return true;
        }
        void PlaceAt(Vector2 position)
        {
            if (!placing.HasValue) return;
            var p = new Vector2(Mathf.Round((position.x - 84) / 16) * 16, Mathf.Round((position.y - 62) / 16) * 16);
            if (!Valid(p)) { status("这里放不下，换一个空位。部件不能压住已有设备。"); return; }
            var action = placeAction; Cancel(); action?.Invoke(p); Sound.Click();
        }
        public void TickPointer(Camera camera)
        {
            if (!placing.HasValue && !PendingCable) return;
            bool inside = RectTransformUtility.RectangleContainsScreenPoint((RectTransform)root.parent, Input.mousePosition, camera);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, Input.mousePosition, camera, out var local);
            if (placing.HasValue && ghost)
            {
                PreviewPlacement(new Vector2(local.x, -local.y), inside);
            }
            if (PendingCable && previewWire)
            {
                previewWire.paths.Clear(); var source = graph.Node(pending); var size = Size(source);
                if (inside) previewWire.paths.Add(Lines.Cable(new Vector2(source.x + size.x, -source.y - size.y / 2), local));
                previewWire.Refresh();
            }
        }
        public void PreviewPlacement(Vector2 pointer, bool inside = true)
        {
            if (!ghost || !placing.HasValue) return;
            ghost.gameObject.SetActive(inside);
            candidate = new Vector2(Mathf.Round((pointer.x - 84) / 16) * 16, Mathf.Round((pointer.y - 62) / 16) * 16);
            ghost.anchoredPosition = new Vector2(candidate.x, -candidate.y); placementValid = inside && Valid(candidate);
            var face = ghost.GetComponentInChildren<HardwareGraphic>(); face.color = placementValid ? Hardware.Enamel : new Color(.82f, .31f, .20f); face.Refresh();
        }
        public void Highlight(GraphNode selected)
        { foreach (var p in plates) p.Value.color = p.Key == selected.id ? new Color(.85f, .59f, .27f) : Color.clear; }
        public void RefreshWires()
        {
            if (!wires) return; wires.paths.Clear();
            foreach (var n in graph.nodes) for (int i = 0; i < n.inputs.Count; i++)
            {
                var source = graph.Node(n.inputs[i]); if (source == null) continue; var size = Size(source);
                wires.paths.Add(Lines.Cable(new Vector2(source.x + size.x, -source.y - size.y / 2), new Vector2(n.x, -n.y - InputY(n, i))));
            }
            wires.Refresh();
        }
        public void Show(Evaluation e, bool gradients = false)
        {
            signalOrder.Clear(); gradientDisplay = gradients; wires.color = gradients ? new Color(.78f, .47f, .27f) : new Color(.68f, .66f, .40f); wires.Refresh();
            var ordered = new List<KeyValuePair<string, int>>();
            foreach (var n in graph.nodes)
            {
                bool active = e.values.TryGetValue(n.id, out var value); lamps[n.id].color = active ? Hardware.Phosphor : Style.Muted; lamps[n.id].Refresh();
                readouts[n.id].text = active ? Style.Scalar(gradients ? value.gradient : value.value) : "—";
                if (knobs.TryGetValue(n.id, out var knob)) { knob.dialValue = (float)n.value; knob.Refresh(); }
                if (active) ordered.Add(new KeyValuePair<string, int>(n.id, e.tape.IndexOf(value)));
            }
            ordered.Sort((a, b) => a.Value.CompareTo(b.Value)); foreach (var pair in ordered) signalOrder.Add(pair.Key);
            if (gradients) signalOrder.Reverse();
        }
        public void Pulse(float time, bool reduced)
        {
            if (signalOrder.Count == 0 || reduced) return; int active = Mathf.FloorToInt(time * 4) % signalOrder.Count;
            for (int i = 0; i < signalOrder.Count; i++) { lamps[signalOrder[i]].color = i == active ? Hardware.Phosphor : (gradientDisplay ? Style.Brass : Hardware.Phosphor) * .7f; lamps[signalOrder[i]].Refresh(); }
        }
    }

    public sealed class TracePlot
    {
        readonly Lines lines; readonly Text range, domainLabel; readonly float width, height;
        public string series = "实际测量";
        public string domain = "记录序号";
        public TracePlot(Transform parent, float x, float y, float w, float h)
        {
            width = w; height = h;
            var box = Style.Box(parent, "Oscilloscope", x, y, w, h, Style.Frame);
            lines = Style.Rect(box.transform, "Trace", 10, 26, w - 20, h - 40).gameObject.AddComponent<Lines>(); lines.color = new Color(.64f, .78f, .69f); lines.raycastTarget = false;
            range = Style.Text(box.transform, "等待测量", 12, 7, w - 24, 21, 14, Style.Paper);
            domainLabel = Style.Text(box.transform, "", 12, h - 25, w - 24, 21, 12, Style.Paper, TextAnchor.MiddleRight);
        }
        public void Set(List<double> data)
        {
            lines.paths.Clear();
            domainLabel.text = data.Count > 1 ? domain + " 0 … " + (data.Count - 1) : "";
            if (data.Count < 2) { range.text = data.Count == 1 ? series + "  " + data[0].ToString("0.###") + " · 1 个读数" : "等待测量"; lines.Refresh(); return; }
            double min = 0, max = .0001; foreach (var value in data) if (GraphEngine.Finite(value)) { min = Math.Min(min, value); max = Math.Max(max, value); }
            var points = new Vector2[data.Count];
            for (int i = 0; i < points.Length; i++) points[i] = new Vector2((width - 20) * i / (data.Count - 1), -(height - 62) * (1 - (float)Math.Min(1, Math.Max(0, (data[i] - min) / (max - min)))));
            lines.paths.Add(points); lines.Refresh(); range.text = series + "  " + Style.Scalar(min) + " … " + Style.Scalar(max) + " · " + data.Count + " 点";
        }
    }
}
