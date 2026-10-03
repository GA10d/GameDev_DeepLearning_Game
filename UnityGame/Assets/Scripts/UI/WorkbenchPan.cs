using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LearningFoundry.UI
{
    /// <summary>Right-button navigation captured at the workbench, independent of the part under the pointer.</summary>
    public sealed class WorkbenchPan : MonoBehaviour
    {
        public ScrollRect scroll;
        public Action rightClick;
        public bool Dragging { get; private set; }
        bool captured;
        Vector2 pressedScreen, lastLocal;
        readonly List<RaycastResult> hits = new List<RaycastResult>();
        PointerEventData pointer;
        EventSystem pointerSystem;

        public void Tick(Camera camera, bool blocked)
        {
            ProcessPointer(Input.mousePosition, Input.GetMouseButtonDown(1), Input.GetMouseButton(1), Input.GetMouseButtonUp(1), camera, blocked);
        }

        // The runtime and native verification use the same screen-coordinate input path.
        public void ProcessPointer(Vector2 position, bool pressed, bool held, bool released, Camera camera, bool blocked = false)
        {
            if (blocked || !scroll || !scroll.content || !scroll.isActiveAndEnabled) { CancelGesture(); return; }
            if (pressed)
            {
                CancelGesture();
                if (!CanStart(position, camera)) return;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(scroll.viewport, position, camera, out lastLocal)) return;
                captured = true; pressedScreen = position; scroll.StopMovement();
            }
            if (!captured) return;
            int threshold = EventSystem.current ? Mathf.Max(1, EventSystem.current.pixelDragThreshold) : 5;
            if (!Dragging && (position - pressedScreen).sqrMagnitude >= threshold * threshold) Dragging = true;
            if (Dragging && RectTransformUtility.ScreenPointToLocalPointInRectangle(scroll.viewport, position, camera, out var local))
            {
                // Viewport coordinates account for CanvasScaler; model zoom must not change hand speed.
                scroll.content.anchoredPosition += local - lastLocal;
                lastLocal = local; scroll.StopMovement();
            }
            if (released || !held)
            {
                bool click = released && !Dragging;
                CancelGesture();
                if (click) rightClick?.Invoke();
            }
        }

        bool CanStart(Vector2 position, Camera camera)
        {
            if (!RectTransformUtility.RectangleContainsScreenPoint(scroll.viewport, position, camera) || !EventSystem.current) return false;
            if (pointer == null || pointerSystem != EventSystem.current) { pointerSystem = EventSystem.current; pointer = new PointerEventData(pointerSystem); }
            pointer.Reset(); pointer.position = position; hits.Clear(); EventSystem.current.RaycastAll(pointer, hits);
            if (hits.Count == 0 || !hits[0].gameObject) return false;
            var hit = hits[0].gameObject.transform;
            // A control box, oscilloscope, or tutorial note in front of the mat owns its input.
            return hit == scroll.viewport || hit.IsChildOf(scroll.viewport);
        }

        public void CancelGesture()
        {
            captured = false; Dragging = false;
            if (scroll) scroll.StopMovement();
        }
        void OnDisable() { CancelGesture(); }
    }
}
