using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using LearningFoundry.Core;

namespace LearningFoundry.UI
{
    // Original procedural artwork. No game sprites or source code are imported.
    public static class Hardware
    {
        static Sprite raised, recessed, wood, mat;
        public static readonly Color Metal = new Color(.73f, .75f, .67f);
        public static readonly Color Enamel = new Color(.78f, .77f, .65f);
        public static readonly Color Phosphor = new Color(.69f, .86f, .64f);
        public static readonly Color Dark = new Color(.10f, .15f, .13f);
        public static Sprite Bevel(bool down = false)
        {
            if (down ? recessed : raised) return down ? recessed : raised;
            const int size = 48;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                int edge = Math.Min(Math.Min(x, size - 1 - x), Math.Min(y, size - 1 - y));
                bool top = y > x && y > size - 1 - x;
                bool left = x < y && x < size - 1 - y;
                float value = edge == 0 ? .16f : edge == 1 ? .32f : edge < 4 ? ((top || left) != down ? 1f : .48f) : .84f;
                texture.SetPixel(x, y, new Color(value, value, value, 1));
            }
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(6, 6, 6, 6));
            if (down) recessed = sprite; else raised = sprite;
            return sprite;
        }
        static Sprite Surface(bool cuttingMat)
        {
            if (cuttingMat ? mat : wood) return cuttingMat ? mat : wood;
            const int size = 512;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Repeat;
            var random = new System.Random(cuttingMat ? 16 : 41);
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float grain = (float)random.NextDouble() * .013f;
                Color color;
                if (cuttingMat) color = new Color(.145f + grain, .205f + grain, .175f + grain);
                else
                {
                    float wave = Mathf.Sin(y * .49f + Mathf.Sin(x * .015f) * 2) * .012f + Mathf.Sin(y * .063f) * .025f;
                    float seam = y % 128 < 2 ? -.055f : 0;
                    color = new Color(.32f + wave + grain + seam, .255f + wave * .75f + grain + seam, .185f + wave * .6f + grain + seam);
                }
                texture.SetPixel(x, y, color);
            }
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100);
            if (cuttingMat) mat = sprite; else wood = sprite;
            return sprite;
        }
        public static Sprite MatSprite { get { return Surface(true); } }
        public static Image Surface(Transform parent, string name, float x, float y, float w, float h, bool cuttingMat)
        {
            var image = Style.Box(parent, name, x, y, w, h, Color.white);
            image.sprite = Surface(cuttingMat); image.type = Image.Type.Tiled; image.pixelsPerUnitMultiplier = 1;
            return image;
        }
        public static Image Plate(Transform parent, string name, float x, float y, float w, float h, Color color, bool screws = true)
        {
            var shadow = Style.Box(parent, name + " Shadow", x + 3, y + 5, w, h, new Color(0, 0, 0, .3f)); shadow.raycastTarget = false;
            var image = Style.Box(parent, name, x, y, w, h, color);
            var follower = image.gameObject.AddComponent<PanelShadow>(); follower.shadow = shadow.rectTransform;
            image.sprite = Bevel(); image.type = Image.Type.Sliced;
            if (screws) Style.Rivets(image.transform, w, h);
            return image;
        }
        public static HardwareGraphic Shape(Transform parent, string name, float x, float y, float w, float h, HardwareShape shape, Color color)
        {
            var graphic = Style.Rect(parent, name, x, y, w, h).gameObject.AddComponent<HardwareGraphic>();
            graphic.shape = shape; graphic.color = color; graphic.raycastTarget = false; return graphic;
        }
        public static void Miniature(Transform parent, NodeKind kind, float x, float y, float w, float h)
        {
            if (kind == NodeKind.Parameter)
            {
                Shape(parent, "Dial Preview", x + w * .25f, y, w * .5f, h, HardwareShape.Knob, Enamel);
            }
            else if (kind == NodeKind.Module)
            {
                Shape(parent, "Cassette Preview", x, y + 3, w, h - 6, HardwareShape.Chip, new Color(.40f, .53f, .51f));
                for (int i = 0; i < 4; i++) Style.Box(parent, "Vent", x + w * .58f, y + 11 + i * 5, w * .24f, 2, Dark).raycastTarget = false;
            }
            else
            {
                Shape(parent, "Part Preview", x + w * .13f, y + 3, w * .74f, h - 6, HardwareShape.Chip, Enamel);
                string glyph = kind == NodeKind.Add ? "+" : kind == NodeKind.Multiply ? "×" : kind == NodeKind.Constant ? "1" : "•";
                Style.Number(parent, glyph, x, y - 4, w, h + 8, 31, Dark, TextAnchor.MiddleCenter);
            }
        }
    }
    public enum HardwareShape { Chip, Socket, Knob, Lamp, Tape }
    public sealed class PanelShadow : MonoBehaviour
    {
        public RectTransform shadow;
        void OnEnable() { if (shadow) shadow.gameObject.SetActive(true); }
        void OnDisable() { if (shadow) shadow.gameObject.SetActive(false); }
        void LateUpdate()
        {
            if (shadow) shadow.anchoredPosition = ((RectTransform)transform).anchoredPosition + new Vector2(3, -5);
        }
        void OnDestroy() { if (shadow) Destroy(shadow.gameObject); }
    }
    public sealed class HardwareGraphic : MaskableGraphic
    {
        public HardwareShape shape; public float dialValue;
        public void Refresh() { SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); float w = rectTransform.rect.width, h = rectTransform.rect.height;
            var center = new Vector2(w / 2, -h / 2);
            if (shape == HardwareShape.Chip || shape == HardwareShape.Tape)
            {
                float cut = shape == HardwareShape.Chip ? 9 : 3;
                Polygon(vh, new[] { new Vector2(cut, 0), new Vector2(w - cut, 0), new Vector2(w, -cut), new Vector2(w, -h + cut), new Vector2(w - cut, -h), new Vector2(cut, -h), new Vector2(0, -h + cut), new Vector2(0, -cut) }, color * .45f);
                Polygon(vh, new[] { new Vector2(cut, -3), new Vector2(w - cut, -3), new Vector2(w - 3, -cut), new Vector2(w - 3, -h + cut), new Vector2(w - cut, -h + 4), new Vector2(cut, -h + 4), new Vector2(3, -h + cut), new Vector2(3, -cut) }, color);
                Stroke(vh, new Vector2(cut, -4), new Vector2(w - cut, -4), 2, color * 1.15f);
                return;
            }
            float radius = Mathf.Min(w, h) / 2;
            Circle(vh, center + new Vector2(1, -2), radius, new Color(0, 0, 0, .5f));
            Circle(vh, center, radius - 1, color * .55f);
            Circle(vh, center + new Vector2(-1, 1), radius - 3, color);
            if (shape == HardwareShape.Socket)
            {
                Circle(vh, center, radius * .52f, Hardware.Dark);
                Circle(vh, center + new Vector2(-1, 1), radius * .22f, color * .65f);
            }
            else if (shape == HardwareShape.Knob)
            {
                for (int i = 0; i < 24; i++)
                {
                    float angle = i * Mathf.PI / 12;
                    var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    Stroke(vh, center + direction * (radius - 4), center + direction * (radius - 8), 1.5f, color * .38f);
                }
                Circle(vh, center, radius * .65f, Hardware.Dark);
                Circle(vh, center + new Vector2(-1, 1), radius * .55f, new Color(.25f, .28f, .24f));
                float a = (90 - Mathf.Clamp(dialValue, -4, 4) * 30) * Mathf.Deg2Rad;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Stroke(vh, center + d * radius * .22f, center + d * radius * .53f, 3, Hardware.Phosphor);
            }
            else Circle(vh, center + new Vector2(-2, 2), radius * .3f, color * 1.3f);
        }
        static void Polygon(VertexHelper vh, Vector2[] points, Color color)
        {
            int start = vh.currentVertCount;
            foreach (var p in points) vh.AddVert(p, color, Vector2.zero);
            for (int i = 2; i < points.Length; i++) vh.AddTriangle(start, start + i - 1, start + i);
        }
        static void Circle(VertexHelper vh, Vector2 center, float radius, Color color)
        {
            int start = vh.currentVertCount; vh.AddVert(center, color, Vector2.zero);
            const int count = 36;
            for (int i = 0; i <= count; i++) { float a = i * 2 * Mathf.PI / count; vh.AddVert(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, color, Vector2.zero); }
            for (int i = 1; i <= count; i++) vh.AddTriangle(start, start + i, start + i + 1);
        }
        static void Stroke(VertexHelper vh, Vector2 a, Vector2 b, float width, Color color)
        {
            var d = (b - a).normalized; var n = new Vector2(-d.y, d.x) * width * .5f;
            Polygon(vh, new[] { a - n, a + n, b + n, b - n }, color);
        }
    }
    public sealed class RotaryKnob : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public GraphNode node; public Action before, changed, end; public HardwareGraphic graphic;
        public bool locked;
        public void OnBeginDrag(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left && !locked) before?.Invoke(); }
        public void OnDrag(PointerEventData e)
        {
            if (locked || e.button != PointerEventData.InputButton.Left) return;
            node.value += e.delta.y * (Input.GetKey(KeyCode.LeftShift) ? .0025 : .025);
            graphic.dialValue = (float)node.value; graphic.Refresh(); changed?.Invoke();
        }
        public void OnEndDrag(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left && !locked) end?.Invoke(); }
    }
    public sealed class PanelDrag : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        public RectTransform panel;
        Vector2 offset;
        public void OnBeginDrag(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)panel.parent, e.position, e.pressEventCamera, out var p);
            offset = panel.anchoredPosition - p; panel.SetAsLastSibling();
        }
        public void OnDrag(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)panel.parent, e.position, e.pressEventCamera, out var p);
            var size = ((RectTransform)panel.parent).rect.size;
            panel.anchoredPosition = new Vector2(Mathf.Clamp(p.x + offset.x, 8, size.x - panel.rect.width - 8), Mathf.Clamp(p.y + offset.y, -size.y + panel.rect.height + 8, -8));
        }
    }
    public sealed class BoardInput : MonoBehaviour, IPointerClickHandler
    {
        public Action<Vector2> place;
        public void OnPointerClick(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, e.position, e.pressEventCamera, out var p);
            place?.Invoke(new Vector2(p.x, -p.y));
        }
    }
}
