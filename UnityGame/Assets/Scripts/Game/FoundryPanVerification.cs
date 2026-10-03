using System;
using System.Collections;
using System.IO;
using System.Linq;
using LearningFoundry.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LearningFoundry.Game
{
    public sealed partial class FoundryGame
    {
        IEnumerator SmokePanning(string output, Action<bool> complete)
        {
            var report = new PanReport();
            Vector2 origin = boardRoot.anchoredPosition, inspectorOrigin = inspector.anchoredPosition, meterOrigin = meterDock.anchoredPosition;
            Vector3 originalScale = boardRoot.localScale;
            string originalGraph = JsonUtility.ToJson(graph); int undoCount = undo.Count;
            Canvas.ForceUpdateCanvases();
            Vector2 start = CanvasPoint(450, 395), end = CanvasPoint(590, 479);
            SimulatePan(start, end); yield return null;
            report.backgroundMovesBothAxes = Near(boardRoot.anchoredPosition, origin + ViewDelta(start, end));
            report.freePastOriginalBounds = boardRoot.anchoredPosition.x > 100 && boardRoot.anchoredPosition.y < -50;
            var released = boardRoot.anchoredPosition;
            boardPan.ProcessPointer(CanvasPoint(790, 250), false, false, false, uiCamera); yield return null;
            report.releaseStopsImmediately = Near(boardRoot.anchoredPosition, released) && !boardPan.Dragging;
            boardRoot.anchoredPosition = origin;

            var node = boardRoot.GetComponentsInChildren<NodeDrag>().First();
            start = RectTransformUtility.WorldToScreenPoint(uiCamera, node.transform.TransformPoint(new Vector3(48, -30, 0)));
            end = start + new Vector2(82, -46);
            RightDragEvents(node.gameObject, start, end); SimulatePan(start, end);
            report.overNodeMovesViewNotPart = Near(boardRoot.anchoredPosition, origin + ViewDelta(start, end)) && originalGraph == JsonUtility.ToJson(graph);
            boardRoot.anchoredPosition = origin;
            var knob = boardRoot.GetComponentsInChildren<RotaryKnob>().First(k => !k.locked);
            start = RectTransformUtility.WorldToScreenPoint(uiCamera, knob.transform.TransformPoint(new Vector3(27, -27, 0)));
            end = start + new Vector2(56, 32);
            RightDragEvents(knob.gameObject, start, end); SimulatePan(start, end);
            report.overKnobKeepsParameterAndUndo = Near(boardRoot.anchoredPosition, origin + ViewDelta(start, end)) && originalGraph == JsonUtility.ToJson(graph) && undo.Count == undoCount;
            boardRoot.anchoredPosition = origin;

            start = CanvasPoint(450, 395); end = CanvasPoint(530, 440);
            boardRoot.localScale = Vector3.one * .55f; Canvas.ForceUpdateCanvases(); SimulatePan(start, end);
            Vector2 atSmallZoom = boardRoot.anchoredPosition - origin;
            boardRoot.anchoredPosition = origin; boardRoot.localScale = Vector3.one * 1.1f; Canvas.ForceUpdateCanvases(); SimulatePan(start, end);
            report.zoomKeepsHandSpeed = Near(boardRoot.anchoredPosition - origin, atSmallZoom) && Near(atSmallZoom, ViewDelta(start, end));
            boardRoot.anchoredPosition = origin; boardRoot.localScale = originalScale; Canvas.ForceUpdateCanvases();

            bool wasInspectorOpen = inspector.gameObject.activeSelf; inspector.gameObject.SetActive(true); yield return null;
            start = RectTransformUtility.WorldToScreenPoint(uiCamera, inspector.TransformPoint(new Vector3(120, -23, 0)));
            end = start + new Vector2(-92, -50);
            RightDragEvents(inspector.GetComponentInChildren<PanelDrag>().gameObject, start, end); SimulatePan(start, end);
            report.floatingControlsKeepTheirPlace = Near(boardRoot.anchoredPosition, origin) && Near(inspector.anchoredPosition, inspectorOrigin) && Near(meterDock.anchoredPosition, meterOrigin);
            inspector.gameObject.SetActive(wasInspectorOpen); yield return null;

            start = CanvasPoint(450, 395); end = CanvasPoint(580, 480);
            boardPan.ProcessPointer(CanvasPoint(1000, 825), true, true, false, uiCamera);
            boardPan.ProcessPointer(start, false, true, false, uiCamera);
            boardPan.ProcessPointer(start, false, false, true, uiCamera);
            report.pressOutsideCannotCapture = Near(boardRoot.anchoredPosition, origin);
            Notebook(); yield return null;
            boardPan.ProcessPointer(start, true, true, false, uiCamera, true);
            boardPan.ProcessPointer(end, false, true, false, uiCamera, true);
            boardPan.ProcessPointer(end, false, false, true, uiCamera, true);
            report.modalBlocksPanning = Near(boardRoot.anchoredPosition, origin); CloseCurrentOverlay(); yield return null;

            boardPan.ProcessPointer(start, true, true, false, uiCamera);
            boardPan.ProcessPointer(end, false, true, false, uiCamera);
            var beforeBlocking = boardRoot.anchoredPosition;
            boardPan.ProcessPointer(end + Vector2.one * 120, false, true, false, uiCamera, true);
            boardPan.ProcessPointer(end + Vector2.one * 200, false, true, false, uiCamera);
            report.interruptionStopsCapture = Near(boardRoot.anchoredPosition, beforeBlocking) && !boardPan.Dragging;
            boardPan.ProcessPointer(end, false, false, true, uiCamera);
            boardRoot.anchoredPosition = origin;

            AddNode(LearningFoundry.Core.NodeKind.Multiply); yield return null;
            SimulatePan(start, end); yield return null;
            report.panKeepsPartInHand = board.Placing && Near(boardRoot.anchoredPosition, origin + ViewDelta(start, end)) && originalGraph == JsonUtility.ToJson(graph);
            boardRoot.anchoredPosition = origin;
            boardPan.ProcessPointer(start, true, true, false, uiCamera);
            boardPan.ProcessPointer(start + Vector2.one, false, false, true, uiCamera);
            report.rightClickStillPutsPartBack = !board.Placing && originalGraph == JsonUtility.ToJson(graph);

            var source = boardRoot.Find(graph.nodes.First(n => n.kind == LearningFoundry.Core.NodeKind.Input).name);
            source.GetComponentsInChildren<Button>().First(b => b.name == "").onClick.Invoke();
            SimulatePan(start, end); yield return null;
            report.panKeepsCableInHand = board.PendingCable && originalGraph == JsonUtility.ToJson(graph);
            boardRoot.anchoredPosition = origin;
            boardPan.ProcessPointer(start, true, true, false, uiCamera);
            boardPan.ProcessPointer(start, false, false, true, uiCamera);
            report.rightClickStillPutsCableBack = !board.PendingCable && originalGraph == JsonUtility.ToJson(graph);

            var scroll = boardViewport.GetComponent<ScrollRect>();
            var wheel = new PointerEventData(EventSystem.current) { scrollDelta = new Vector2(0, -1) };
            ExecuteEvents.Execute<IScrollHandler>(boardViewport.gameObject, wheel, ExecuteEvents.scrollHandler);
            report.wheelStillMovesView = !Near(boardRoot.anchoredPosition, origin);
            boardRoot.anchoredPosition = origin;
            var left = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, position = start,
                pointerPressRaycast = new RaycastResult { module = canvas.GetComponent<GraphicRaycaster>() } };
            ExecuteEvents.Execute<IBeginDragHandler>(boardViewport.gameObject, left, ExecuteEvents.beginDragHandler);
            left.position = end; ExecuteEvents.Execute<IDragHandler>(boardViewport.gameObject, left, ExecuteEvents.dragHandler);
            ExecuteEvents.Execute<IEndDragHandler>(boardViewport.gameObject, left, ExecuteEvents.endDragHandler);
            report.leftBlankDragStillWorks = Near(boardRoot.anchoredPosition, origin + ViewDelta(start, end));
            boardPan.CancelGesture(); board.Cancel(); boardRoot.anchoredPosition = origin; boardRoot.localScale = originalScale; scroll.StopMovement();
            report.noModelOrUndoMutation = JsonUtility.ToJson(graph) == originalGraph && undo.Count == undoCount;
            report.passed = report.backgroundMovesBothAxes && report.freePastOriginalBounds && report.releaseStopsImmediately && report.overNodeMovesViewNotPart && report.overKnobKeepsParameterAndUndo && report.zoomKeepsHandSpeed && report.floatingControlsKeepTheirPlace && report.pressOutsideCannotCapture && report.modalBlocksPanning && report.interruptionStopsCapture && report.panKeepsPartInHand && report.rightClickStillPutsPartBack && report.panKeepsCableInHand && report.rightClickStillPutsCableBack && report.wheelStillMovesView && report.leftBlankDragStillWorks && report.noModelOrUndoMutation;
            File.WriteAllText(Path.Combine(output, "panning-smoke.json"), JsonUtility.ToJson(report, true)); complete(report.passed);
        }

        Vector2 CanvasPoint(float x, float y)
        { return RectTransformUtility.WorldToScreenPoint(uiCamera, screen.TransformPoint(new Vector3(x, -y, 0))); }
        Vector2 ViewDelta(Vector2 a, Vector2 b)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(boardViewport, a, uiCamera, out var start);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(boardViewport, b, uiCamera, out var end);
            return end - start;
        }
        void SimulatePan(Vector2 start, Vector2 end)
        {
            boardPan.ProcessPointer(start, true, true, false, uiCamera);
            boardPan.ProcessPointer(end, false, true, false, uiCamera);
            boardPan.ProcessPointer(end, false, false, true, uiCamera);
        }
        void RightDragEvents(GameObject target, Vector2 start, Vector2 end)
        {
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Right, position = start,
                pointerPressRaycast = new RaycastResult { module = canvas.GetComponent<GraphicRaycaster>() } };
            ExecuteEvents.Execute<IBeginDragHandler>(target, pointer, ExecuteEvents.beginDragHandler);
            pointer.position = end; pointer.delta = end - start;
            ExecuteEvents.Execute<IDragHandler>(target, pointer, ExecuteEvents.dragHandler);
            ExecuteEvents.Execute<IEndDragHandler>(target, pointer, ExecuteEvents.endDragHandler);
        }
        static bool Near(Vector2 a, Vector2 b) { return Vector2.Distance(a, b) < .05f; }
        [Serializable] sealed class PanReport
        {
            public bool passed, backgroundMovesBothAxes, freePastOriginalBounds, releaseStopsImmediately,
                overNodeMovesViewNotPart, overKnobKeepsParameterAndUndo, zoomKeepsHandSpeed, floatingControlsKeepTheirPlace,
                pressOutsideCannotCapture, modalBlocksPanning, interruptionStopsCapture, panKeepsPartInHand,
                rightClickStillPutsPartBack, panKeepsCableInHand, rightClickStillPutsCableBack, wheelStillMovesView,
                leftBlankDragStillWorks, noModelOrUndoMutation;
        }
    }
}
