#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public sealed class DialogueVisualEditorWindow : EditorWindow
{
    enum ToolMode
    {
        Select,
        AddTextPanel,
        AddNamePanel,
        AddImagePanel
    }

    enum EditTool
    {
        Select,
        MoveRoot,
        ScaleRoot,
        Width,
        Height,
        Size
    }

    enum SelectionKind
    {
        None,
        MainPanel,
        Area,
        Slot,
        Component
    }

    enum DragMode
    {
        None,
        MoveSelection,
        ScaleMainSymmetric,
        ResizeWidthLeft,
        ResizeWidthRight,
        ResizeHeightTop,
        ResizeHeightBottom,
        ResizeSymmetric,
        AdjustAreaGap
    }

    struct SelectionState
    {
        public SelectionKind Kind;
        public ResolvedDialogueAreaKind AreaKind;
        public int SlotIndex;
        public int ComponentIndex;

        public static SelectionState None
        {
            get
            {
                return new SelectionState
                {
                    Kind = SelectionKind.None,
                    AreaKind = ResolvedDialogueAreaKind.MainInner,
                    SlotIndex = -1,
                    ComponentIndex = -1
                };
            }
        }
    }

    Dialogue_Engine engine;
    DialogueLayoutAsset layoutAsset;
    Vector2 hierarchyScroll;
    Vector2 inspectorScroll;

    bool editMode = true;
    bool autoApplyToEngine = true;
    bool showLabels = true;
    bool showSlotBounds = true;
    bool showComponents = true;

    ToolMode toolMode = ToolMode.Select;
    EditTool editTool = EditTool.Select;
    SelectionState selection = SelectionState.None;
    DragMode dragMode = DragMode.None;
    Vector2 dragStartMouse;
    float dragStartGapValue;
    Rect dragStartRect;
    Rect dragParentRect;
    Vector2 dragHandleDirection = Vector2.one;
    Vector2 dragStartSelectionOffset;

    static readonly int CanvasInputControlHash = "DialogueVisualEditorCanvasInput".GetHashCode();

    Rect canvasRect;
    ResolvedDialogueLayout resolved;

    [MenuItem("Tools/Dialogue Editor")]
    static void OpenWindow()
    {
        Open(Object.FindFirstObjectByType<Dialogue_Engine>());
    }

    public static void Open(Dialogue_Engine targetEngine)
    {
        var window = GetWindow<DialogueVisualEditorWindow>("Dialogue Visual Editor");
        window.engine = targetEngine;
        window.layoutAsset = targetEngine != null ? targetEngine.visualLayoutAsset : null;
        window.minSize = new Vector2(1120f, 640f);
        window.Show();
    }

    void OnGUI()
    {
        DrawToolbar();

        EditorGUILayout.BeginHorizontal();
        DrawLeftSidebar();
        DrawCanvasPanel();
        DrawInspectorPanel();
        EditorGUILayout.EndHorizontal();
    }

    void DrawToolbar()
    {
        EditorGUILayout.BeginVertical();

        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        engine = (Dialogue_Engine)EditorGUILayout.ObjectField(engine, typeof(Dialogue_Engine), true, GUILayout.Width(220f));
        if (engine != null && layoutAsset == null)
            layoutAsset = engine.visualLayoutAsset;
        layoutAsset = (DialogueLayoutAsset)EditorGUILayout.ObjectField(layoutAsset, typeof(DialogueLayoutAsset), false, GUILayout.Width(240f));

        GUILayout.Space(6f);
        editMode = GUILayout.Toggle(editMode, "Edit Mode", EditorStyles.toolbarButton, GUILayout.Width(78f));
        showLabels = GUILayout.Toggle(showLabels, "Labels", EditorStyles.toolbarButton, GUILayout.Width(60f));
        showSlotBounds = GUILayout.Toggle(showSlotBounds, "Slots", EditorStyles.toolbarButton, GUILayout.Width(52f));
        showComponents = GUILayout.Toggle(showComponents, "Components", EditorStyles.toolbarButton, GUILayout.Width(90f));
        autoApplyToEngine = GUILayout.Toggle(autoApplyToEngine, "Auto Apply", EditorStyles.toolbarButton, GUILayout.Width(82f));

        GUILayout.FlexibleSpace();

        GUI.enabled = engine != null && layoutAsset != null;
        if (GUILayout.Button("Apply To Engine", EditorStyles.toolbarButton, GUILayout.Width(100f)))
            ApplyBridge();
        GUI.enabled = layoutAsset != null;
        if (GUILayout.Button("Open Preview", EditorStyles.toolbarButton, GUILayout.Width(90f)))
            DialogueVisualLayoutPreviewWindow.Open(engine);
        GUI.enabled = true;

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("Edit", GUILayout.Width(28f));
        editTool = (EditTool)GUILayout.Toolbar((int)editTool,
            new[] { "Select", "Move Root", "Scale Root", "Width", "Height", "Size" },
            EditorStyles.toolbarButton, GUILayout.Width(470f));
        GUILayout.Space(8f);
        GUILayout.Label("Add", GUILayout.Width(28f));
        toolMode = (ToolMode)GUILayout.Toolbar((int)toolMode,
            new[] { "Select", "Add Text", "Add Name", "Add Image" },
            EditorStyles.toolbarButton, GUILayout.Width(320f));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    void DrawLeftSidebar()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(250f));
        GUILayout.Space(6f);

        GUILayout.Label("Palette", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Keep the current visual workflow: Move Root drags the current selection while keeping it inside its parent container, the main panel still auto-updates its anchor when moved, attached areas stay locked outside the main panel and only slide along their side while Gap From Main Panel controls their distance, Scale Root symmetrically scales only the main panel, Width/Height/Size resize the current selection, and attached areas on the same edge as the main-panel anchor are auto-hidden until that anchor changes.",
            MessageType.None);

        EditorGUILayout.BeginHorizontal();
        DrawAreaAddButton("Top", ResolvedDialogueAreaKind.Top);
        DrawAreaAddButton("Bottom", ResolvedDialogueAreaKind.Bottom);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        DrawAreaAddButton("Left", ResolvedDialogueAreaKind.Left);
        DrawAreaAddButton("Right", ResolvedDialogueAreaKind.Right);
        EditorGUILayout.EndHorizontal();

        GUI.enabled = selection.Kind == SelectionKind.Area;
        if (GUILayout.Button("Remove Selected Area", GUILayout.Height(24f)))
            RemoveSelectedArea();
        GUI.enabled = selection.Kind == SelectionKind.Component;
        if (GUILayout.Button("Remove Selected Component", GUILayout.Height(24f)))
            RemoveSelectedComponent();
        GUI.enabled = true;

        GUILayout.Space(10f);
        GUILayout.Label("Add Component To Selected Slot", EditorStyles.boldLabel);
        GUI.enabled = selection.Kind == SelectionKind.Slot;
        if (GUILayout.Button("Add Text Panel", GUILayout.Height(24f)))
            AddComponentToSelectedSlot(DialogueComponentType.TextPanel);
        if (GUILayout.Button("Add Name Panel", GUILayout.Height(24f)))
            AddComponentToSelectedSlot(DialogueComponentType.NamePanel);
        if (GUILayout.Button("Add Image Panel", GUILayout.Height(24f)))
            AddComponentToSelectedSlot(DialogueComponentType.ImagePanel);
        GUI.enabled = true;

        GUILayout.Space(10f);
        GUILayout.Label("Hierarchy", EditorStyles.boldLabel);
        hierarchyScroll = EditorGUILayout.BeginScrollView(hierarchyScroll, GUILayout.ExpandHeight(true));
        DrawHierarchy();
        EditorGUILayout.EndScrollView();

        EditorGUILayout.EndVertical();
    }

    void DrawCanvasPanel()
    {
        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        GUILayout.Space(6f);

        canvasRect = GUILayoutUtility.GetRect(position.width - 540f, position.height - 70f,
            GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        EditorGUI.DrawRect(canvasRect, new Color(0.09f, 0.09f, 0.10f, 1f));

        if (layoutAsset == null)
        {
            EditorGUI.HelpBox(new Rect(canvasRect.x + 12f, canvasRect.y + 12f,
                canvasRect.width - 24f, 42f),
                "Assign a DialogueLayoutAsset to start editing, or drag one into this canvas.",
                MessageType.Info);
            HandleCanvasDragAndDrop(canvasRect);
            EditorGUILayout.EndVertical();
            return;
        }

        DialogueVisualEditorUtility.EnsureSlotArrays(layoutAsset);
        Rect padded = new Rect(canvasRect.x + 12f, canvasRect.y + 12f,
            canvasRect.width - 24f, canvasRect.height - 24f);
        resolved = DialogueVisualLayoutResolver.Resolve(layoutAsset, padded);
        DrawCanvas(resolved);
        HandleCanvasInput(UnityEngine.Event.current, padded);
        HandleCanvasDragAndDrop(canvasRect);

        EditorGUILayout.EndVertical();
    }

    void DrawInspectorPanel()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(300f));
        GUILayout.Space(6f);
        GUILayout.Label("Selection Inspector", EditorStyles.boldLabel);

        if (layoutAsset == null)
        {
            EditorGUILayout.HelpBox("No DialogueLayoutAsset selected.", MessageType.Info);
            EditorGUILayout.EndVertical();
            return;
        }

        // Recording during an active canvas drag would snapshot mid-drag state after
        // the canvas already mutated the asset, corrupting the undo entry.
        if (dragMode == DragMode.None)
            Undo.RecordObject(layoutAsset, "Dialogue Layout Change");
        inspectorScroll = EditorGUILayout.BeginScrollView(inspectorScroll, GUILayout.ExpandHeight(true));

        switch (selection.Kind)
        {
            case SelectionKind.MainPanel:
                DrawMainPanelInspector();
                break;
            case SelectionKind.Area:
                DrawAreaInspector();
                break;
            case SelectionKind.Slot:
                DrawSlotInspector();
                break;
            case SelectionKind.Component:
                DrawComponentInspector();
                break;
            default:
                DrawLayoutRootInspector();
                break;
        }

        EditorGUILayout.EndScrollView();
        if (GUI.changed)
            CommitLayoutMutation();
        EditorGUILayout.EndVertical();
    }

    void DrawCanvas(ResolvedDialogueLayout layout)
    {
        if (layout == null || layoutAsset == null) return;

        DialogueMainPanelDefinition mainPanel = layoutAsset.MainPanel;
        DialogueInnerRegionDefinition innerRegion = mainPanel != null ? mainPanel.InnerRegion : null;

        DialogueVisualStylePreviewUtility.DrawStyledElement(
            layout.MainPanelRect,
            mainPanel != null ? mainPanel.Background : null,
            mainPanel != null ? mainPanel.Border : null,
            mainPanel != null ? mainPanel.Shadow : null,
            mainPanel != null ? mainPanel.Opacity : null,
            new Color(0.16f, 0.20f, 0.30f, 0.88f),
            new Color(0.70f, 0.82f, 1f, 1f),
            2f);
        if (selection.Kind == SelectionKind.MainPanel)
            DialogueVisualStylePreviewUtility.DrawSelectionOutline(
                layout.MainPanelRect,
                mainPanel != null ? mainPanel.Border : null,
                new Color(1f, 1f, 0.45f, 1f),
                2.5f);
        if (showLabels)
            GUI.Label(new Rect(layout.MainPanelRect.x + 6f, layout.MainPanelRect.y + 4f, 240f, 18f),
                "Main Panel", EditorStyles.whiteBoldLabel);

        for (int i = 0; i < layout.Areas.Count; i++)
        {
            ResolvedDialogueArea area = layout.Areas[i];
            DialogueBackgroundStyle background = null;
            DialogueBorderStyle border = null;
            DialogueShadowStyle shadow = null;
            DialogueOpacitySettings opacity = null;
            Color fallbackFill = area.AreaKind == ResolvedDialogueAreaKind.MainInner
                ? new Color(0.22f, 0.26f, 0.30f, 0.30f)
                : new Color(0.16f, 0.36f, 0.24f, 0.40f);

            if (area.AreaKind == ResolvedDialogueAreaKind.MainInner)
            {
                background = innerRegion != null ? innerRegion.Background : null;
                border = innerRegion != null ? innerRegion.Border : null;
                shadow = innerRegion != null ? innerRegion.Shadow : null;
                opacity = innerRegion != null ? innerRegion.Opacity : null;
            }
            else
            {
                DialogueAttachedAreaDefinition areaDef = DialogueVisualEditorUtility.GetArea(layoutAsset, area.AreaKind);
                background = areaDef != null ? areaDef.Background : null;
                border = areaDef != null ? areaDef.Border : null;
                shadow = areaDef != null ? areaDef.Shadow : null;
                opacity = areaDef != null ? areaDef.Opacity : null;
            }

            DialogueVisualStylePreviewUtility.DrawStyledElement(
                area.Rect,
                background,
                border,
                shadow,
                opacity,
                fallbackFill,
                new Color(0.65f, 0.95f, 0.70f, 1f),
                1.5f);
            if (IsSelected(area))
                DialogueVisualStylePreviewUtility.DrawSelectionOutline(
                    area.Rect,
                    border,
                    new Color(1f, 1f, 0.45f, 1f),
                    2f);
            if (showLabels)
                GUI.Label(new Rect(area.Rect.x + 4f, area.Rect.y + 2f, 220f, 18f),
                    area.Name, EditorStyles.miniBoldLabel);
        }

        if (showSlotBounds)
        {
            for (int i = 0; i < layout.Slots.Count; i++)
            {
                ResolvedDialogueSlot slot = layout.Slots[i];
                DialogueSlotDefinition slotDef = DialogueVisualEditorUtility.GetSlot(layoutAsset, slot.AreaKind, slot.SlotIndex);
                DialogueVisualStylePreviewUtility.DrawStyledElement(
                    slot.Rect,
                    slotDef != null ? slotDef.Background : null,
                    slotDef != null ? slotDef.Border : null,
                    slotDef != null ? slotDef.Shadow : null,
                    slotDef != null ? slotDef.Opacity : null,
                    new Color(1f, 0.84f, 0.40f, 0.08f),
                    new Color(1f, 0.84f, 0.40f, 1f),
                    1.2f);
                if (IsSelected(slot))
                    DialogueVisualStylePreviewUtility.DrawSelectionOutline(
                        slot.Rect,
                        slotDef != null ? slotDef.Border : null,
                        new Color(1f, 1f, 0.45f, 1f),
                        2f);
                if (showLabels)
                    GUI.Label(new Rect(slot.Rect.x + 4f, slot.Rect.y + 2f, 120f, 18f),
                        slot.SlotId, EditorStyles.miniLabel);
            }
        }

        if (showComponents)
        {
            for (int i = 0; i < layout.Components.Count; i++)
            {
                ResolvedDialogueComponentRect component = layout.Components[i];
                DialogueComponentDefinition componentDef = DialogueVisualEditorUtility.GetComponent(
                    layoutAsset,
                    component.AreaKind,
                    component.SlotIndex,
                    component.ComponentIndex);
                DialogueVisualStylePreviewUtility.DrawStyledElement(
                    component.Rect,
                    componentDef != null ? componentDef.Background : null,
                    componentDef != null ? componentDef.Border : null,
                    componentDef != null ? componentDef.Shadow : null,
                    componentDef != null ? componentDef.Opacity : null,
                    GetComponentFill(component.ComponentType),
                    component.ClipToSlot
                        ? new Color(1f, 1f, 1f, 0.9f)
                        : new Color(1f, 0.45f, 0.45f, 1f),
                    1.4f);
                if (IsSelected(component))
                    DialogueVisualStylePreviewUtility.DrawSelectionOutline(
                        component.Rect,
                        componentDef != null ? componentDef.Border : null,
                        new Color(1f, 1f, 0.45f, 1f),
                        2f);
                if (!component.ClipToSlot)
                {
                    Handles.color = new Color(1f, 0.45f, 0.45f, 1f);
                    DrawDashedRect(component.Rect, 6f);
                }
                if (showLabels)
                    GUI.Label(new Rect(component.Rect.x + 3f, component.Rect.y + 2f, 180f, 18f),
                        component.DisplayName + "  z:" + component.ZLayer,
                        EditorStyles.whiteMiniLabel);
            }
        }

        if (editMode)
            DrawSelectionHandles(layout);
    }

    void DrawSelectionHandles(ResolvedDialogueLayout layout)
    {
        Rect selectedRect;
        if (!TryGetSelectedRect(layout, out selectedRect))
            return;

        const float handle = 10f;
        Color sizeHandleColor = new Color(0.9f, 0.9f, 0.2f, 1f);
        Color moveHandleColor = new Color(0.2f, 1f, 1f, 1f);

        switch (editTool)
        {
            case EditTool.MoveRoot:
                if (selection.Kind != SelectionKind.None)
                    DrawHandleBox(GetMoveHandle(selectedRect, handle), moveHandleColor);
                break;

            case EditTool.ScaleRoot:
                if (selection.Kind == SelectionKind.MainPanel)
                    DrawCornerHandles(selectedRect, handle, sizeHandleColor);
                break;

            case EditTool.Width:
                DrawEdgeBars(selectedRect, true, sizeHandleColor);
                DrawHandleBox(GetLeftEdgeHandle(selectedRect, handle), sizeHandleColor);
                DrawHandleBox(GetRightEdgeHandle(selectedRect, handle), sizeHandleColor);
                break;

            case EditTool.Height:
                DrawEdgeBars(selectedRect, false, sizeHandleColor);
                DrawHandleBox(GetTopEdgeHandle(selectedRect, handle), sizeHandleColor);
                DrawHandleBox(GetBottomEdgeHandle(selectedRect, handle), sizeHandleColor);
                break;

            case EditTool.Size:
                DrawCornerHandles(selectedRect, handle, sizeHandleColor);
                break;

            default:
                if (selection.Kind == SelectionKind.Area && selection.AreaKind != ResolvedDialogueAreaKind.MainInner)
                {
                    ResolvedDialogueArea area = FindSelectedArea(layout);
                    if (area != null)
                    {
                        Rect gap = GetAreaGapHandle(area.Rect, layout.MainPanelRect, area.Side);
                        DrawHandleBox(gap, moveHandleColor);
                    }
                }
                break;
        }
    }

    void HandleCanvasInput(UnityEngine.Event evt, Rect paddedCanvas)
    {
        if (evt == null || layoutAsset == null || resolved == null) return;

        int controlId = GUIUtility.GetControlID(CanvasInputControlHash, FocusType.Passive);
        bool dragActive = dragMode != DragMode.None;

        if (evt.type == EventType.Repaint && editMode && toolMode == ToolMode.Select)
            UpdateCanvasCursor();

        if (evt.type == EventType.MouseDown)
        {
            // Safety net: a press that never saw its mouse-up leaves a stale drag behind.
            if (dragActive && evt.button == 0)
                EndCanvasDrag();

            // Use the full canvas rect (not the padded one): the edge drag zones of a
            // rect sitting on the canvas border stick out past the padding.
            if (evt.button != 0 || !canvasRect.Contains(evt.mousePosition)) return;

            if (editMode && toolMode == ToolMode.Select && TryBeginDrag(evt.mousePosition))
            {
                // Capture the mouse so scroll views / other windows cannot steal the
                // drag events, and so the release is always delivered to this window.
                GUIUtility.hotControl = controlId;
                GUIUtility.keyboardControl = 0;
                EditorGUIUtility.SetWantsMouseJumping(1);
                evt.Use();
                return;
            }

            SelectionState hit = HitTest(evt.mousePosition);
            if (toolMode != ToolMode.Select && hit.Kind == SelectionKind.Slot)
            {
                AddComponentAtSlot(hit, ModeToComponent(toolMode));
                evt.Use();
                return;
            }

            selection = hit;
            Repaint();
            evt.Use();
            return;
        }

        if (evt.type == EventType.MouseDrag)
        {
            if (!dragActive || evt.button != 0) return;
            // The drag keeps running no matter where the cursor travels, and the
            // geometry is always derived from the fixed drag-start snapshot, so the
            // result is identical for every pixel along the way.
            DragSelection(evt.mousePosition - dragStartMouse);
            evt.Use();
            return;
        }

        if (evt.type == EventType.MouseUp)
        {
            if (dragActive || GUIUtility.hotControl == controlId)
            {
                EndCanvasDrag();
                evt.Use();
            }
            return;
        }
    }

    void EndCanvasDrag()
    {
        if (dragMode == DragMode.None && GUIUtility.hotControl == 0) return;
        dragMode = DragMode.None;
        GUIUtility.hotControl = 0;
        EditorGUIUtility.SetWantsMouseJumping(0);
        Repaint();
    }

    void UpdateCanvasCursor()
    {
        Rect selectedRect;
        if (!TryGetSelectedRect(resolved, out selectedRect)) return;

        const float size = 10f;
        switch (editTool)
        {
            case EditTool.Width:
                EditorGUIUtility.AddCursorRect(GetLeftEdgeDragZone(selectedRect, size), MouseCursor.ResizeHorizontal);
                EditorGUIUtility.AddCursorRect(GetRightEdgeDragZone(selectedRect, size), MouseCursor.ResizeHorizontal);
                break;

            case EditTool.Height:
                EditorGUIUtility.AddCursorRect(GetTopEdgeDragZone(selectedRect, size), MouseCursor.ResizeVertical);
                EditorGUIUtility.AddCursorRect(GetBottomEdgeDragZone(selectedRect, size), MouseCursor.ResizeVertical);
                break;

            case EditTool.Size:
            case EditTool.ScaleRoot:
                if (editTool == EditTool.ScaleRoot && selection.Kind != SelectionKind.MainPanel)
                    return;
                EditorGUIUtility.AddCursorRect(GetTopRightCornerHandle(selectedRect, size), MouseCursor.ResizeUpRight);
                EditorGUIUtility.AddCursorRect(GetBottomLeftCornerHandle(selectedRect, size), MouseCursor.ResizeUpRight);
                EditorGUIUtility.AddCursorRect(GetTopLeftCornerHandle(selectedRect, size), MouseCursor.ResizeUpLeft);
                EditorGUIUtility.AddCursorRect(GetBottomRightCornerHandle(selectedRect, size), MouseCursor.ResizeUpLeft);
                break;
        }
    }

    bool TryBeginDrag(Vector2 mouse)
    {
        const float size = 10f;
        Rect selectedRect;
        if (!TryGetSelectedRect(resolved, out selectedRect))
            return false;

        switch (editTool)
        {
            case EditTool.MoveRoot:
                if (selection.Kind != SelectionKind.None && selectedRect.Contains(mouse))
                {
                    BeginMoveSelection(selectedRect);
                    return true;
                }
                break;

            case EditTool.ScaleRoot:
                if (selection.Kind == SelectionKind.MainPanel)
                {
                    if (TryHitCornerHandle(selectedRect, mouse, size, out dragHandleDirection))
                    {
                        BeginSizedDrag(DragMode.ScaleMainSymmetric, selectedRect, GetSelectedParentRect(), "Scale Main Panel");
                        return true;
                    }
                }
                break;

            case EditTool.Width:
                if (GetLeftEdgeDragZone(selectedRect, size).Contains(mouse))
                {
                    BeginSizedDrag(DragMode.ResizeWidthLeft, selectedRect, GetSelectedParentRect(), "Adjust Width");
                    return true;
                }
                if (GetRightEdgeDragZone(selectedRect, size).Contains(mouse))
                {
                    BeginSizedDrag(DragMode.ResizeWidthRight, selectedRect, GetSelectedParentRect(), "Adjust Width");
                    return true;
                }
                break;

            case EditTool.Height:
                if (GetTopEdgeDragZone(selectedRect, size).Contains(mouse))
                {
                    BeginSizedDrag(DragMode.ResizeHeightTop, selectedRect, GetSelectedParentRect(), "Adjust Height");
                    return true;
                }
                if (GetBottomEdgeDragZone(selectedRect, size).Contains(mouse))
                {
                    BeginSizedDrag(DragMode.ResizeHeightBottom, selectedRect, GetSelectedParentRect(), "Adjust Height");
                    return true;
                }
                break;

            case EditTool.Size:
                if (TryHitCornerHandle(selectedRect, mouse, size, out dragHandleDirection))
                {
                    BeginSizedDrag(DragMode.ResizeSymmetric, selectedRect, GetSelectedParentRect(), "Adjust Size");
                    return true;
                }
                break;

            default:
                if (selection.Kind == SelectionKind.Area && selection.AreaKind != ResolvedDialogueAreaKind.MainInner)
                {
                    ResolvedDialogueArea area = FindSelectedArea(resolved);
                    if (area != null && GetAreaGapHandle(area.Rect, resolved.MainPanelRect, area.Side).Contains(mouse))
                    {
                        BeginAreaGapDrag(area.AreaKind);
                        return true;
                    }
                }
                break;
        }

        return false;
    }

    void BeginMoveSelection(Rect currentRect)
    {
        if (layoutAsset == null)
            return;

        if (selection.Kind == SelectionKind.MainPanel && layoutAsset.MainPanel != null &&
            layoutAsset.MainPanel.CustomAnchor == null)
            layoutAsset.MainPanel.CustomAnchor = new DialogueCustomAnchorDefinition();

        dragMode = DragMode.MoveSelection;
        dragStartMouse = UnityEngine.Event.current.mousePosition;
        dragStartRect = currentRect;
        dragParentRect = GetSelectedParentRect();
        CaptureDragStartSelectionOffset();
        DialogueVisualEditorUtility.RecordChange(layoutAsset, "Move Selection");
    }

    void BeginSizedDrag(DragMode mode, Rect currentRect, Rect parentRect, string actionName)
    {
        dragMode = mode;
        dragStartMouse = UnityEngine.Event.current.mousePosition;
        dragStartRect = currentRect;
        dragParentRect = parentRect;
        CaptureDragStartSelectionOffset();
        DialogueVisualEditorUtility.RecordChange(layoutAsset, actionName);
    }

    void CaptureDragStartSelectionOffset()
    {
        dragStartSelectionOffset = Vector2.zero;
        if (layoutAsset == null) return;

        switch (selection.Kind)
        {
            case SelectionKind.Area:
                if (selection.AreaKind == ResolvedDialogueAreaKind.MainInner)
                {
                    DialogueInnerRegionDefinition region = layoutAsset.MainPanel != null
                        ? layoutAsset.MainPanel.InnerRegion : null;
                    if (region != null) dragStartSelectionOffset = region.Offset;
                }
                else
                {
                    DialogueAttachedAreaDefinition area = DialogueVisualEditorUtility.GetArea(layoutAsset, selection.AreaKind);
                    if (area != null) dragStartSelectionOffset = area.Offset;
                }
                break;

            case SelectionKind.Slot:
                DialogueSlotDefinition slot = DialogueVisualEditorUtility.GetSlot(layoutAsset,
                    selection.AreaKind, selection.SlotIndex);
                if (slot != null) dragStartSelectionOffset = slot.Offset;
                break;

            case SelectionKind.Component:
                DialogueComponentDefinition component = GetSelectedComponentDefinition();
                if (component != null) dragStartSelectionOffset = component.Offset;
                break;
        }
    }

    void BeginAreaGapDrag(ResolvedDialogueAreaKind kind)
    {
        DialogueAttachedAreaDefinition area = DialogueVisualEditorUtility.GetArea(layoutAsset, kind);
        if (area == null) return;
        dragMode = DragMode.AdjustAreaGap;
        dragStartMouse = UnityEngine.Event.current.mousePosition;
        dragStartGapValue = area.GapFromMainPanel;
        selection.AreaKind = kind;
        DialogueVisualEditorUtility.RecordChange(layoutAsset, "Adjust Area Gap");
    }

    void DragSelection(Vector2 delta)
    {
        switch (dragMode)
        {
            case DragMode.MoveSelection:
                Rect movedRect = ClampRectInside(new Rect(
                    dragStartRect.x + delta.x,
                    dragStartRect.y + delta.y,
                    dragStartRect.width,
                    dragStartRect.height), dragParentRect);
                if (selection.Kind == SelectionKind.MainPanel)
                    ApplyMainPanelRect(movedRect, false, true);
                else
                    ApplyRectToCurrentSelection(movedRect);
                break;

            case DragMode.ScaleMainSymmetric:
            case DragMode.ResizeSymmetric:
                ApplyRectToCurrentSelection(ClampRectInside(
                    CreateSymmetricResizedRect(dragStartRect, delta, dragHandleDirection),
                    dragParentRect));
                break;

            case DragMode.ResizeWidthLeft:
                ApplyRectToCurrentSelection(ClampRectInside(
                    CreateWidthAdjustedRect(dragStartRect, delta.x, true),
                    dragParentRect));
                break;

            case DragMode.ResizeWidthRight:
                ApplyRectToCurrentSelection(ClampRectInside(
                    CreateWidthAdjustedRect(dragStartRect, delta.x, false),
                    dragParentRect));
                break;

            case DragMode.ResizeHeightTop:
                ApplyRectToCurrentSelection(ClampRectInside(
                    CreateHeightAdjustedRect(dragStartRect, delta.y, true),
                    dragParentRect));
                break;

            case DragMode.ResizeHeightBottom:
                ApplyRectToCurrentSelection(ClampRectInside(
                    CreateHeightAdjustedRect(dragStartRect, delta.y, false),
                    dragParentRect));
                break;

            case DragMode.AdjustAreaGap:
                DialogueAttachedAreaDefinition gapArea = DialogueVisualEditorUtility.GetArea(layoutAsset, selection.AreaKind);
                if (gapArea != null)
                {
                    float axisDelta = (selection.AreaKind == ResolvedDialogueAreaKind.Left || selection.AreaKind == ResolvedDialogueAreaKind.Right)
                        ? Mathf.Abs(delta.x) : Mathf.Abs(delta.y);
                    gapArea.GapFromMainPanel = Mathf.Max(0f, dragStartGapValue + axisDelta * Mathf.Sign(ProjectGapSign(selection.AreaKind, delta)));
                }
                break;
        }

        CommitLayoutMutation();
    }

    static float ProjectGapSign(ResolvedDialogueAreaKind kind, Vector2 delta)
    {
        switch (kind)
        {
            case ResolvedDialogueAreaKind.Top: return -delta.y;
            case ResolvedDialogueAreaKind.Bottom: return delta.y;
            case ResolvedDialogueAreaKind.Left: return -delta.x;
            default: return delta.x;
        }
    }

    // Axis locking: a Width drag may only change horizontal geometry and a Height drag
    // may only change vertical geometry. Nothing else in the drag pipeline is allowed
    // to move or resize the perpendicular axis.
    static bool DragChangesWidth(DragMode mode)
    {
        switch (mode)
        {
            case DragMode.MoveSelection:
            case DragMode.ScaleMainSymmetric:
            case DragMode.ResizeSymmetric:
            case DragMode.ResizeWidthLeft:
            case DragMode.ResizeWidthRight:
                return true;
            default:
                return false;
        }
    }

    static bool DragChangesHeight(DragMode mode)
    {
        switch (mode)
        {
            case DragMode.MoveSelection:
            case DragMode.ScaleMainSymmetric:
            case DragMode.ResizeSymmetric:
            case DragMode.ResizeHeightTop:
            case DragMode.ResizeHeightBottom:
                return true;
            default:
                return false;
        }
    }

    // Partitioned regions/areas derive their size from their visible slots, so a parent
    // resize must scale the explicit slot sizes along with it. Otherwise the resolver
    // ignores the size the handle wrote and the drag degrades into a slide. Scaling is
    // incremental (current resolved size -> target size) so repeated drag events can
    // never compound.
    static void ScalePartitionedSlotsForParentResize(List<DialogueSlotDefinition> slots,
        int visibleCount, bool horizontalParent, float defaultSpacing,
        float currentPrimary, float targetPrimary,
        float currentSecondary, float targetSecondary)
    {
        if (slots == null || visibleCount <= 1) return;
        int visible = Mathf.Min(visibleCount, slots.Count);

        float gapTotal = 0f;
        for (int i = 0; i < visible; i++)
        {
            DialogueSlotDefinition slot = slots[i];
            if (slot == null) continue;
            if (i < visible - 1)
                gapTotal += slot.GapAfter >= 0f ? slot.GapAfter : defaultSpacing;
        }

        float currentPrimarySpan = currentPrimary - gapTotal;
        if (!Mathf.Approximately(targetPrimary, currentPrimary) && currentPrimarySpan > 1f)
        {
            float primaryScale = Mathf.Max(0f, (targetPrimary - gapTotal) / currentPrimarySpan);
            for (int i = 0; i < visible; i++)
            {
                DialogueSlotDefinition slot = slots[i];
                if (slot == null || !slot.Enabled) continue;
                DialogueSizeValue primary = horizontalParent ? slot.Width : slot.Height;
                if (primary != null && primary.Unit == DialogueSizeUnit.Pixels && primary.Value > 0f)
                    primary.Value = Mathf.Max(1f, primary.Value * primaryScale);
            }
        }

        if (!Mathf.Approximately(targetSecondary, currentSecondary) && currentSecondary > 1f)
        {
            float secondaryScale = Mathf.Clamp(targetSecondary / currentSecondary, 0f, 4f);
            if (secondaryScale < 0.999f || secondaryScale > 1.001f)
            {
                for (int i = 0; i < visible; i++)
                {
                    DialogueSlotDefinition slot = slots[i];
                    if (slot == null || !slot.Enabled) continue;
                    DialogueSizeValue secondary = horizontalParent ? slot.Height : slot.Width;
                    if (secondary != null && secondary.Unit == DialogueSizeUnit.Pixels && secondary.Value > 0f)
                        secondary.Value = Mathf.Max(1f, Mathf.Min(targetSecondary, secondary.Value * secondaryScale));
                }
            }
        }
    }

    void DrawHierarchy()
    {
        DrawHierarchyButton("Main Panel", SelectionKind.MainPanel, ResolvedDialogueAreaKind.MainInner, -1, -1, 0);
        DrawHierarchyButton("Inner Region", SelectionKind.Area, ResolvedDialogueAreaKind.MainInner, -1, -1, 1);
        DrawHierarchySlots(ResolvedDialogueAreaKind.MainInner, 2);
        DrawHierarchyArea(ResolvedDialogueAreaKind.Top, "Top Area");
        DrawHierarchyArea(ResolvedDialogueAreaKind.Bottom, "Bottom Area");
        DrawHierarchyArea(ResolvedDialogueAreaKind.Left, "Left Area");
        DrawHierarchyArea(ResolvedDialogueAreaKind.Right, "Right Area");
    }

    void DrawHierarchyArea(ResolvedDialogueAreaKind kind, string name)
    {
        bool enabled = DialogueVisualEditorUtility.IsAreaEnabled(layoutAsset, kind);
        bool autoHidden = enabled && DialogueVisualLayoutResolver.IsAreaSuppressedByMainPanelAnchor(layoutAsset, kind);
        string label = !enabled
            ? name + " (disabled)"
            : autoHidden
                ? name + " (auto-hidden by main anchor)"
                : name;
        DrawHierarchyButton(label, SelectionKind.Area, kind, -1, -1, 1);
        if (enabled && !autoHidden) DrawHierarchySlots(kind, 2);
    }

    void DrawHierarchySlots(ResolvedDialogueAreaKind kind, int indent)
    {
        List<DialogueSlotDefinition> slots = DialogueVisualEditorUtility.GetSlots(layoutAsset, kind);
        if (slots == null) return;
        int visibleCount = GetVisibleSlotCount(kind);
        for (int i = 0; i < visibleCount && i < slots.Count; i++)
        {
            DialogueSlotDefinition slot = slots[i];
            if (slot == null) continue;
            DrawHierarchyButton(slot.DisplayName, SelectionKind.Slot, kind, i, -1, indent);
            if (slot.Components == null) continue;
            for (int c = 0; c < slot.Components.Count; c++)
            {
                DialogueComponentDefinition component = slot.Components[c];
                if (component == null) continue;
                DrawHierarchyButton(component.DisplayName, SelectionKind.Component, kind, i, c, indent + 1);
            }
        }
    }

    void DrawHierarchyButton(string label, SelectionKind kind,
        ResolvedDialogueAreaKind areaKind, int slotIndex, int componentIndex,
        int indent)
    {
        Rect row = EditorGUILayout.GetControlRect(false, 20f);
        row.xMin += indent * 16f;
        bool selected = selection.Kind == kind && selection.AreaKind == areaKind &&
                        selection.SlotIndex == slotIndex &&
                        selection.ComponentIndex == componentIndex;
        if (selected) EditorGUI.DrawRect(row, new Color(0.25f, 0.42f, 0.75f, 0.45f));
        if (GUI.Button(row, label, EditorStyles.label))
        {
            selection = new SelectionState
            {
                Kind = kind,
                AreaKind = areaKind,
                SlotIndex = slotIndex,
                ComponentIndex = componentIndex
            };
            Repaint();
        }
    }

    void DrawLayoutRootInspector()
    {
        layoutAsset.LayoutName = EditorGUILayout.TextField("Layout Name", layoutAsset.LayoutName);
        layoutAsset.DataVersion = EditorGUILayout.IntField("Data Version", layoutAsset.DataVersion);
        EditorGUILayout.Space(6f);
        EditorGUILayout.HelpBox(
            "Select the Main Panel, an Attached Area, a Slot, or a Component from the canvas or hierarchy to edit it. The phase-3 MVP keeps a strict one-selection model.",
            MessageType.Info);
        if (engine != null)
        {
            engine.useVisualLayoutAsset = EditorGUILayout.Toggle("Engine Uses This Layout", engine.useVisualLayoutAsset);
            engine.visualLayoutAsset = (DialogueLayoutAsset)EditorGUILayout.ObjectField("Engine Layout Asset", engine.visualLayoutAsset, typeof(DialogueLayoutAsset), false);
        }

        EditorGUILayout.Space(8f);
        DrawSpeakerEmphasisInspector();
    }

    void DrawSpeakerEmphasisInspector()
    {
        DialogueSpeakerEmphasisSettings emphasis = layoutAsset.SpeakerEmphasis;
        if (emphasis == null)
        {
            emphasis = new DialogueSpeakerEmphasisSettings();
            layoutAsset.SpeakerEmphasis = emphasis;
        }

        GUILayout.Label("Speaker Emphasis", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "The current speaker's name and image stay fully visible. When a new speaker interrupts, the previous one stays on screen greyed out.",
            MessageType.None);
        emphasis.GreyOutPastSpeakers = EditorGUILayout.Toggle(
            new GUIContent("Grey Out Past Speakers", "Keep the most recently interrupted speaker visible, greyed out."),
            emphasis.GreyOutPastSpeakers);
        emphasis.ActiveOpacity = EditorGUILayout.Slider("Active Opacity", emphasis.ActiveOpacity, 0f, 1f);
        emphasis.InactiveOpacity = EditorGUILayout.Slider("Greyed Opacity", emphasis.InactiveOpacity, 0f, 1f);
        emphasis.InactiveTint = EditorGUILayout.ColorField("Greyed Tint", emphasis.InactiveTint);
    }

    void DrawMainPanelInspector()
    {
        DialogueMainPanelDefinition panel = layoutAsset.MainPanel;
        if (panel == null) return;
        panel.DisplayName = EditorGUILayout.TextField("Display Name", panel.DisplayName);
        panel.Enabled = EditorGUILayout.Toggle("Enabled", panel.Enabled);
        panel.AnchorPreset = (DialogueAnchorPreset)EditorGUILayout.EnumPopup("Anchor", panel.AnchorPreset);
        panel.FillMode = (DialoguePanelFillMode)EditorGUILayout.EnumPopup("Fill Mode", panel.FillMode);
        DrawSizeField("Width", panel.Width);
        DrawSizeField("Height", panel.Height);
        DrawPaddingField("Padding", panel.Padding);
        panel.ZLayer = EditorGUILayout.IntSlider("Z Layer", panel.ZLayer, -10, 10);

        if (panel.CustomAnchor == null)
            panel.CustomAnchor = new DialogueCustomAnchorDefinition();
        if (panel.AnchorPreset == DialogueAnchorPreset.Custom)
        {
            panel.CustomAnchor.HorizontalReference = (DialogueAnchorReferenceEdge)EditorGUILayout.EnumPopup("Horizontal Reference", panel.CustomAnchor.HorizontalReference);
            panel.CustomAnchor.VerticalReference = (DialogueAnchorReferenceEdge)EditorGUILayout.EnumPopup("Vertical Reference", panel.CustomAnchor.VerticalReference);
        }
        panel.CustomAnchor.OffsetX = EditorGUILayout.FloatField("Anchor Offset X", panel.CustomAnchor.OffsetX);
        panel.CustomAnchor.OffsetY = EditorGUILayout.FloatField("Anchor Offset Y", panel.CustomAnchor.OffsetY);

        EditorGUILayout.Space(8f);
        GUILayout.Label("Main Panel Style", EditorStyles.boldLabel);
        DrawBackgroundStyle(panel.Background);
        DrawBorderStyle(panel.Border);
        DrawShadowStyle(panel.Shadow);
        DrawOpacity(panel.Opacity);

        EditorGUILayout.Space(8f);
        EditorGUILayout.HelpBox(
            "Inner Region is edited directly by selecting 'Inner Region' in the hierarchy or canvas. Partition level 0 keeps one slot, level 1 creates two slots, and level 2 creates three slots. Slots are terminal containers and cannot be partitioned further.",
            MessageType.None);
        EditorGUILayout.HelpBox(
            "Attached areas that share the same screen edge as the main-panel anchor are auto-hidden in the editor preview and come back automatically when the anchor changes.",
            MessageType.None);
    }

    void DrawAreaInspector()
    {
        if (selection.AreaKind == ResolvedDialogueAreaKind.MainInner)
        {
            DrawInnerRegionInspector();
            return;
        }
        DialogueAttachedAreaDefinition area = DialogueVisualEditorUtility.GetArea(layoutAsset, selection.AreaKind);
        if (area == null) return;
        if (DialogueVisualLayoutResolver.IsAreaSuppressedByMainPanelAnchor(layoutAsset, selection.AreaKind))
            EditorGUILayout.HelpBox(
                "This attached area is currently auto-hidden because the main panel is anchored to the same screen edge. Change the main-panel anchor and it will reappear automatically.",
                MessageType.Warning);
        EditorGUILayout.HelpBox(
            "Attached areas always stay outside the main panel. Use Gap From Main Panel for the distance away from the main panel, and use the slide field or Move Root tool to slide them along their current side without crossing into the main panel.",
            MessageType.None);
        area.DisplayName = EditorGUILayout.TextField("Display Name", area.DisplayName);
        area.Enabled = EditorGUILayout.Toggle("Enabled", area.Enabled);
        DialogueVisualEditorUtility.SetAreaEnabled(layoutAsset, selection.AreaKind, area.Enabled);
        area.GapFromMainPanel = Mathf.Max(0f, EditorGUILayout.FloatField("Gap From Main Panel", area.GapFromMainPanel));
        DrawAreaSlideOffsetField(area);
        DrawSizeField("Width", area.Width);
        DrawSizeField("Height", area.Height);
        int oldPartition = area.PartitionLevel;
        area.PartitionLevel = EditorGUILayout.IntSlider("Partition Level", area.PartitionLevel, 0, 2);
        area.InterSlotSpacing = EditorGUILayout.FloatField("Default Inter-Slot Spacing", area.InterSlotSpacing);
        area.ZLayer = EditorGUILayout.IntSlider("Z Layer", area.ZLayer, -10, 10);
        if (area.PartitionLevel != oldPartition && area.PartitionLevel > oldPartition)
            DialogueVisualEditorUtility.SyncVisibleSlotsFromArea(area);
        if (area.PartitionLevel > 0 && GUILayout.Button("Sync Visible Slots From Parent Area", GUILayout.Height(22f)))
            DialogueVisualEditorUtility.SyncVisibleSlotsFromArea(area);
        ResolvedDialogueAreaKind oppositeAreaKind;
        if (DialogueVisualEditorUtility.TryGetOppositeAreaKind(selection.AreaKind, out oppositeAreaKind) &&
            GUILayout.Button("Copy This Area To " + DialogueVisualEditorUtility.GetAreaKindDisplayName(oppositeAreaKind), GUILayout.Height(22f)))
        {
            DialogueVisualEditorUtility.RecordChange(layoutAsset, "Copy Area To Opposite Side");
            DialogueVisualEditorUtility.CopyAreaToOpposite(layoutAsset, selection.AreaKind);
            CommitLayoutMutation();
            return;
        }
        DrawBackgroundStyle(area.Background);
        DrawBorderStyle(area.Border);
        DrawShadowStyle(area.Shadow);
        DrawOpacity(area.Opacity);
    }

    void DrawSlotInspector()
    {
        DialogueSlotDefinition slot = DialogueVisualEditorUtility.GetSlot(layoutAsset, selection.AreaKind, selection.SlotIndex);
        if (slot == null) return;
        slot.DisplayName = EditorGUILayout.TextField("Display Name", slot.DisplayName);
        slot.Enabled = EditorGUILayout.Toggle("Enabled", slot.Enabled);
        EditorGUILayout.HelpBox(
            "Slots are the final partition pieces. They cannot be partitioned further. They inherit their parent region's visual settings by default, but you can override their own size, position, spacing-after, and visual styling here.",
            MessageType.None);
        slot.Offset = EditorGUILayout.Vector2Field("Offset", slot.Offset);
        DrawSizeField("Width", slot.Width);
        DrawSizeField("Height", slot.Height);
        slot.GapAfter = EditorGUILayout.FloatField("Gap To Next Slot (-1 uses parent)", slot.GapAfter);
        DrawPaddingField("Padding", slot.Padding);
        slot.ZLayer = EditorGUILayout.IntSlider("Z Layer", slot.ZLayer, -10, 10);
        DrawBackgroundStyle(slot.Background);
        DrawBorderStyle(slot.Border);
        DrawShadowStyle(slot.Shadow);
        DrawOpacity(slot.Opacity);
        ResolvedDialogueAreaKind oppositeSlotAreaKind;
        if (selection.AreaKind != ResolvedDialogueAreaKind.MainInner &&
            DialogueVisualEditorUtility.TryGetOppositeAreaKind(selection.AreaKind, out oppositeSlotAreaKind) &&
            GUILayout.Button("Copy This Slot To Matching Slot On " + DialogueVisualEditorUtility.GetAreaKindDisplayName(oppositeSlotAreaKind), GUILayout.Height(22f)))
        {
            DialogueVisualEditorUtility.RecordChange(layoutAsset, "Copy Slot To Opposite Side");
            DialogueVisualEditorUtility.CopySlotToOpposite(layoutAsset, selection.AreaKind, selection.SlotIndex);
            CommitLayoutMutation();
            return;
        }
        EditorGUILayout.Space(6f);
        GUILayout.Label("Components", EditorStyles.boldLabel);
        if (slot.Components != null)
        {
            for (int i = 0; i < slot.Components.Count; i++)
            {
                DialogueComponentDefinition component = slot.Components[i];
                if (component == null) continue;
                EditorGUILayout.BeginHorizontal("box");
                if (GUILayout.Button(component.DisplayName, EditorStyles.label))
                {
                    selection = new SelectionState
                    {
                        Kind = SelectionKind.Component,
                        AreaKind = selection.AreaKind,
                        SlotIndex = selection.SlotIndex,
                        ComponentIndex = i
                    };
                    Repaint();
                }
                if (GUILayout.Button("X", GUILayout.Width(22f)))
                {
                    slot.Components.RemoveAt(i);
                    selection = SelectionState.None;
                    CommitLayoutMutation();
                    return;
                }
                EditorGUILayout.EndHorizontal();
            }
        }
        EditorGUILayout.Space(4f);
        if (GUILayout.Button("Add Text Panel")) AddComponentToSelectedSlot(DialogueComponentType.TextPanel);
        if (GUILayout.Button("Add Name Panel")) AddComponentToSelectedSlot(DialogueComponentType.NamePanel);
        if (GUILayout.Button("Add Image Panel")) AddComponentToSelectedSlot(DialogueComponentType.ImagePanel);
    }

    void DrawInnerRegionInspector()
    {
        DialogueInnerRegionDefinition region = layoutAsset != null && layoutAsset.MainPanel != null
            ? layoutAsset.MainPanel.InnerRegion : null;
        if (region == null) return;

        region.DisplayName = EditorGUILayout.TextField("Display Name", region.DisplayName);
        region.Enabled = EditorGUILayout.Toggle("Enabled", region.Enabled);
        DrawSizeField("Width", region.Width);
        DrawSizeField("Height", region.Height);
        region.Offset = EditorGUILayout.Vector2Field("Offset", region.Offset);
        int oldPartition = region.PartitionLevel;
        region.PartitionLevel = EditorGUILayout.IntSlider("Partition Level", region.PartitionLevel, 0, 2);
        region.InterSlotSpacing = EditorGUILayout.FloatField("Default Inter-Slot Spacing", region.InterSlotSpacing);
        region.ZLayer = EditorGUILayout.IntSlider("Z Layer", region.ZLayer, -10, 10);
        if (region.PartitionLevel != oldPartition && region.PartitionLevel > 0)
            DialogueVisualEditorUtility.SyncVisibleSlotsFromRegion(region);
        if (region.PartitionLevel > 0 && GUILayout.Button("Sync Visible Slots From Parent Region", GUILayout.Height(22f)))
            DialogueVisualEditorUtility.SyncVisibleSlotsFromRegion(region);
        DrawBackgroundStyle(region.Background);
        DrawBorderStyle(region.Border);
        DrawShadowStyle(region.Shadow);
        DrawOpacity(region.Opacity);
    }

    int GetVisibleSlotCount(ResolvedDialogueAreaKind kind)
    {
        if (layoutAsset == null) return 0;
        if (kind == ResolvedDialogueAreaKind.MainInner)
        {
            DialogueInnerRegionDefinition region = layoutAsset.MainPanel != null ? layoutAsset.MainPanel.InnerRegion : null;
            return DialogueVisualEditorUtility.GetVisibleSlotCount(region);
        }
        DialogueAttachedAreaDefinition area = DialogueVisualEditorUtility.GetArea(layoutAsset, kind);
        return DialogueVisualEditorUtility.GetVisibleSlotCount(area);
    }

    void DrawComponentInspector()
    {
        DialogueComponentDefinition component = GetSelectedComponentDefinition();
        if (component == null) return;
        component.DisplayName = EditorGUILayout.TextField("Display Name", component.DisplayName);
        component.Enabled = EditorGUILayout.Toggle("Enabled", component.Enabled);
        component.HorizontalAlignment = (DialogueHorizontalAlignment)EditorGUILayout.EnumPopup("Horizontal Alignment", component.HorizontalAlignment);
        component.VerticalAlignment = (DialogueVerticalAlignment)EditorGUILayout.EnumPopup("Vertical Alignment", component.VerticalAlignment);
        component.Offset = EditorGUILayout.Vector2Field("Offset", component.Offset);
        DrawSizeField("Width", component.Width);
        DrawSizeField("Height", component.Height);
        DrawPaddingField("Padding", component.Padding);
        component.ClipToSlot = EditorGUILayout.Toggle("Clip To Slot", component.ClipToSlot);
        component.ZLayer = EditorGUILayout.IntSlider("Z Layer", component.ZLayer, -10, 10);

        DrawBackgroundStyle(component.Background);
        DrawBorderStyle(component.Border);
        DrawShadowStyle(component.Shadow);
        DrawOpacity(component.Opacity);

        EditorGUILayout.Space(8f);
        switch (component.ComponentType)
        {
            case DialogueComponentType.TextPanel:
                DrawTextPanelInspector(component as DialogueTextPanelDefinition);
                break;
            case DialogueComponentType.NamePanel:
                DrawNamePanelInspector(component as DialogueNamePanelDefinition);
                break;
            case DialogueComponentType.ImagePanel:
                DrawImagePanelInspector(component as DialogueImagePanelDefinition);
                break;
        }
    }

    void DrawTextPanelInspector(DialogueTextPanelDefinition component)
    {
        if (component == null) return;
        component.TypewriterEnabled = EditorGUILayout.Toggle("Typewriter Enabled", component.TypewriterEnabled);
        component.CharactersPerSecond = EditorGUILayout.FloatField("Characters Per Second", component.CharactersPerSecond);
        component.StartDelay = EditorGUILayout.FloatField("Start Delay", component.StartDelay);
        component.CharacterAudioKey = EditorGUILayout.TextField("Character Audio Key", component.CharacterAudioKey);
        DrawTextStyle(component.TextStyle);
        DrawLetterEffectField("Letter Behaviour", component.LetterEffect, component.BaseAnimationProfile);
        component.BaseAnimationProfile = (TextAnimationProfile)EditorGUILayout.ObjectField("Base Animation Profile", component.BaseAnimationProfile, typeof(TextAnimationProfile), false);
        component.OverlayAnimationProfile = (TextAnimationProfile)EditorGUILayout.ObjectField("Overlay Animation Profile", component.OverlayAnimationProfile, typeof(TextAnimationProfile), false);
    }

    void DrawNamePanelInspector(DialogueNamePanelDefinition component)
    {
        if (component == null) return;
        component.TypewriterEnabled = EditorGUILayout.Toggle("Typewriter Enabled", component.TypewriterEnabled);
        component.CharactersPerSecond = EditorGUILayout.FloatField("Characters Per Second", component.CharactersPerSecond);
        component.StartDelay = EditorGUILayout.FloatField("Start Delay", component.StartDelay);
        component.Uppercase = EditorGUILayout.Toggle("Uppercase", component.Uppercase);
        DrawTextStyle(component.TextStyle);
        DrawLetterEffectField("Letter Behaviour", component.LetterEffect, component.BaseAnimationProfile);
        component.BaseAnimationProfile = (TextAnimationProfile)EditorGUILayout.ObjectField("Base Animation Profile", component.BaseAnimationProfile, typeof(TextAnimationProfile), false);
        component.OverlayAnimationProfile = (TextAnimationProfile)EditorGUILayout.ObjectField("Overlay Animation Profile", component.OverlayAnimationProfile, typeof(TextAnimationProfile), false);
    }

    void DrawImagePanelInspector(DialogueImagePanelDefinition component)
    {
        if (component == null) return;
        component.Mode = (DialogueImagePanelMode)EditorGUILayout.EnumPopup("Mode", component.Mode);
        if (component.Mode == DialogueImagePanelMode.Icon)
        {
            component.Shape = (DialogueIconShape)EditorGUILayout.EnumPopup("Shape", component.Shape);
            component.UniformScale = EditorGUILayout.Slider("Uniform Scale", component.UniformScale, 0.25f, 4f);
            component.InnerPadding = EditorGUILayout.FloatField("Inner Padding", component.InnerPadding);
            component.HideWhenEmpty = EditorGUILayout.Toggle(
                new GUIContent("Hide When Empty", "Hide the shape frame entirely while no image is loaded."),
                component.HideWhenEmpty);
            DrawImageStyle(component.ImageStyle);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Character Figure: the panel hugs the loaded image (never larger than its parent container) and is invisible while no image is loaded.",
                MessageType.None);
            component.FigureScaleMode = (DialogueFigureScaleMode)EditorGUILayout.EnumPopup("Scale Mode", component.FigureScaleMode);
            component.FlipHorizontal = EditorGUILayout.Toggle("Flip Horizontal", component.FlipHorizontal);
            component.FitToImage = EditorGUILayout.Toggle(
                new GUIContent("Fit To Image", "Size the panel to the image's aspect ratio instead of a fixed square."),
                component.FitToImage);
            component.HideWhenEmpty = EditorGUILayout.Toggle("Hide When Empty", component.HideWhenEmpty);
            component.MaxSizePercent = EditorGUILayout.Slider(
                new GUIContent("Max Size % Of Parent", "Upper bound as a percentage of the parent container."),
                component.MaxSizePercent, 10f, 100f);
            component.ImageSourceKey = EditorGUILayout.TextField("Image Source Key", component.ImageSourceKey);
        }
    }

    void DrawLetterEffectField(string label, DialogueLetterEffectSettings effect,
        TextAnimationProfile profileOverride)
    {
        if (effect == null) return;
        GUILayout.Label(label, EditorStyles.boldLabel);
        effect.EffectType = (DialogueTextEffectType)EditorGUILayout.EnumPopup("Effect", effect.EffectType);
        if (profileOverride != null)
            EditorGUILayout.HelpBox(
                "A Base Animation Profile is assigned and overrides these inline values at runtime.",
                MessageType.Info);
        if (effect.EffectType == DialogueTextEffectType.None) return;

        effect.Amplitude = EditorGUILayout.Slider("Amplitude", effect.Amplitude, 0f, 48f);
        effect.Frequency = EditorGUILayout.Slider("Frequency", effect.Frequency, 0.05f, 3f);
        effect.PhaseOffset = EditorGUILayout.Slider("Phase Offset", effect.PhaseOffset, 0f, 6.28f);
        effect.AnimationSpeed = EditorGUILayout.Slider("Animation Speed", effect.AnimationSpeed, 0.1f, 8f);
        effect.Loop = EditorGUILayout.Toggle("Loop", effect.Loop);
    }

    void DrawTextStyle(DialogueTextStyle style)
    {
        if (style == null) return;
        EditorGUILayout.Space(4f);
        GUILayout.Label("Text Style", EditorStyles.boldLabel);
        style.FontSourceKey = EditorGUILayout.TextField("Font Source Key", style.FontSourceKey);
        style.FontSize = EditorGUILayout.FloatField("Font Size", style.FontSize);
        style.FontWeight = (DialogueFontWeight)EditorGUILayout.EnumPopup("Font Weight", style.FontWeight);
        style.Color = EditorGUILayout.ColorField("Color", style.Color);
        style.LineHeight = EditorGUILayout.FloatField("Line Height", style.LineHeight);
        style.LetterSpacing = EditorGUILayout.FloatField("Letter Spacing", style.LetterSpacing);
        style.HorizontalAlignment = (DialogueHorizontalAlignment)EditorGUILayout.EnumPopup("Text Horizontal Alignment", style.HorizontalAlignment);
        style.VerticalAlignment = (DialogueVerticalAlignment)EditorGUILayout.EnumPopup("Text Vertical Alignment", style.VerticalAlignment);
    }

    void DrawImageStyle(DialogueImageStyle style)
    {
        if (style == null) return;
        style.ImageSourceKey = EditorGUILayout.TextField("Image Source Key", style.ImageSourceKey);
        style.Opacity = EditorGUILayout.Slider("Image Opacity", style.Opacity, 0f, 1f);
        style.PreserveAspect = EditorGUILayout.Toggle("Preserve Aspect", style.PreserveAspect);
        style.Tint = EditorGUILayout.ColorField("Tint", style.Tint);
    }

    void DrawBackgroundStyle(DialogueBackgroundStyle style)
    {
        if (style == null) return;
        EditorGUILayout.Space(4f);
        GUILayout.Label("Background", EditorStyles.boldLabel);
        style.Mode = (DialogueBackgroundMode)EditorGUILayout.EnumPopup("Mode", style.Mode);
        style.ColorA = EditorGUILayout.ColorField("Color A", style.ColorA);
        style.ColorB = EditorGUILayout.ColorField("Color B", style.ColorB);
        style.Opacity = EditorGUILayout.Slider("Opacity", style.Opacity, 0f, 1f);
        style.SpriteSourceKey = EditorGUILayout.TextField("Sprite Source Key", style.SpriteSourceKey);
        style.GradientDirection = (DialogueGradientDirection)EditorGUILayout.EnumPopup("Gradient Direction", style.GradientDirection);
    }

    void DrawBorderStyle(DialogueBorderStyle style)
    {
        if (style == null) return;
        EditorGUILayout.Space(4f);
        GUILayout.Label("Border", EditorStyles.boldLabel);
        style.Enabled = EditorGUILayout.Toggle("Enabled", style.Enabled);
        style.LeftThickness = EditorGUILayout.FloatField("Left Thickness", style.LeftThickness);
        style.RightThickness = EditorGUILayout.FloatField("Right Thickness", style.RightThickness);
        style.TopThickness = EditorGUILayout.FloatField("Top Thickness", style.TopThickness);
        style.BottomThickness = EditorGUILayout.FloatField("Bottom Thickness", style.BottomThickness);
        style.BorderColor = EditorGUILayout.ColorField("Border Color", style.BorderColor);
        style.CornerRadiusTopLeft = EditorGUILayout.FloatField("Radius TL", style.CornerRadiusTopLeft);
        style.CornerRadiusTopRight = EditorGUILayout.FloatField("Radius TR", style.CornerRadiusTopRight);
        style.CornerRadiusBottomLeft = EditorGUILayout.FloatField("Radius BL", style.CornerRadiusBottomLeft);
        style.CornerRadiusBottomRight = EditorGUILayout.FloatField("Radius BR", style.CornerRadiusBottomRight);
        style.BorderSpriteSourceKey = EditorGUILayout.TextField("Border Sprite Source Key", style.BorderSpriteSourceKey);
        style.Opacity = EditorGUILayout.Slider("Opacity", style.Opacity, 0f, 1f);
    }

    void DrawShadowStyle(DialogueShadowStyle style)
    {
        if (style == null) return;
        EditorGUILayout.Space(4f);
        GUILayout.Label("Shadow", EditorStyles.boldLabel);
        style.Enabled = EditorGUILayout.Toggle("Enabled", style.Enabled);
        style.Offset = EditorGUILayout.Vector2Field("Offset", style.Offset);
        style.Blur = EditorGUILayout.FloatField("Blur", style.Blur);
        style.Color = EditorGUILayout.ColorField("Color", style.Color);
        style.Opacity = EditorGUILayout.Slider("Opacity", style.Opacity, 0f, 1f);
    }

    void DrawOpacity(DialogueOpacitySettings opacity)
    {
        if (opacity == null) return;
        EditorGUILayout.Space(4f);
        opacity.Opacity = EditorGUILayout.Slider("Overall Opacity", opacity.Opacity, 0f, 1f);
    }

    void DrawPaddingField(string label, DialoguePadding padding)
    {
        if (padding == null) return;
        GUILayout.Label(label, EditorStyles.boldLabel);
        padding.Left = EditorGUILayout.FloatField("Left", padding.Left);
        padding.Right = EditorGUILayout.FloatField("Right", padding.Right);
        padding.Top = EditorGUILayout.FloatField("Top", padding.Top);
        padding.Bottom = EditorGUILayout.FloatField("Bottom", padding.Bottom);
    }

    void DrawSizeField(string label, DialogueSizeValue size)
    {
        if (size == null) return;
        GUILayout.Label(label, EditorStyles.boldLabel);
        size.Unit = (DialogueSizeUnit)EditorGUILayout.EnumPopup("Unit", size.Unit);
        size.Value = EditorGUILayout.FloatField("Value", size.Value);
    }

    void DrawAreaAddButton(string label, ResolvedDialogueAreaKind kind)
    {
        GUI.enabled = layoutAsset != null;
        if (GUILayout.Button(label, GUILayout.Height(24f)))
            AddArea(kind);
        GUI.enabled = true;
    }

    void AddArea(ResolvedDialogueAreaKind kind)
    {
        if (layoutAsset == null || kind == ResolvedDialogueAreaKind.MainInner) return;
        DialogueVisualEditorUtility.RecordChange(layoutAsset, "Enable Attached Area");
        DialogueVisualEditorUtility.SetAreaEnabled(layoutAsset, kind, true);
        selection = new SelectionState { Kind = SelectionKind.Area, AreaKind = kind, SlotIndex = -1, ComponentIndex = -1 };
        CommitLayoutMutation();
    }

    void RemoveSelectedArea()
    {
        if (layoutAsset == null || selection.Kind != SelectionKind.Area || selection.AreaKind == ResolvedDialogueAreaKind.MainInner) return;
        DialogueVisualEditorUtility.RecordChange(layoutAsset, "Disable Attached Area");
        DialogueVisualEditorUtility.SetAreaEnabled(layoutAsset, selection.AreaKind, false);
        selection = SelectionState.None;
        CommitLayoutMutation();
    }

    void AddComponentToSelectedSlot(DialogueComponentType type)
    {
        if (selection.Kind != SelectionKind.Slot) return;
        AddComponentAtSlot(selection, type);
    }

    void AddComponentAtSlot(SelectionState slotSelection, DialogueComponentType type)
    {
        DialogueSlotDefinition slot = DialogueVisualEditorUtility.GetSlot(layoutAsset,
            slotSelection.AreaKind, slotSelection.SlotIndex);
        if (slot == null) return;
        DialogueVisualEditorUtility.RecordChange(layoutAsset, "Add Dialogue Component");
        DialogueComponentDefinition component = DialogueVisualEditorUtility.CreateComponent(type);
        slot.Components.Add(component);
        selection = new SelectionState
        {
            Kind = SelectionKind.Component,
            AreaKind = slotSelection.AreaKind,
            SlotIndex = slotSelection.SlotIndex,
            ComponentIndex = slot.Components.Count - 1
        };
        toolMode = ToolMode.Select;
        CommitLayoutMutation();
    }

    void RemoveSelectedComponent()
    {
        DialogueSlotDefinition slot = DialogueVisualEditorUtility.GetSlot(layoutAsset,
            selection.AreaKind, selection.SlotIndex);
        if (slot == null || slot.Components == null ||
            selection.ComponentIndex < 0 || selection.ComponentIndex >= slot.Components.Count) return;
        DialogueVisualEditorUtility.RecordChange(layoutAsset, "Remove Dialogue Component");
        slot.Components.RemoveAt(selection.ComponentIndex);
        selection = SelectionState.None;
        CommitLayoutMutation();
    }

    SelectionState HitTest(Vector2 mouse)
    {
        if (resolved == null) return SelectionState.None;
        for (int i = resolved.Components.Count - 1; i >= 0; i--)
        {
            if (resolved.Components[i].Rect.Contains(mouse))
            {
                return new SelectionState
                {
                    Kind = SelectionKind.Component,
                    AreaKind = resolved.Components[i].AreaKind,
                    SlotIndex = resolved.Components[i].SlotIndex,
                    ComponentIndex = resolved.Components[i].ComponentIndex
                };
            }
        }
        for (int i = resolved.Slots.Count - 1; i >= 0; i--)
        {
            if (resolved.Slots[i].Rect.Contains(mouse))
            {
                return new SelectionState
                {
                    Kind = SelectionKind.Slot,
                    AreaKind = resolved.Slots[i].AreaKind,
                    SlotIndex = resolved.Slots[i].SlotIndex,
                    ComponentIndex = -1
                };
            }
        }
        for (int i = resolved.Areas.Count - 1; i >= 0; i--)
        {
            if (resolved.Areas[i].Rect.Contains(mouse))
            {
                return new SelectionState
                {
                    Kind = SelectionKind.Area,
                    AreaKind = resolved.Areas[i].AreaKind,
                    SlotIndex = -1,
                    ComponentIndex = -1
                };
            }
        }
        if (resolved.MainPanelRect.Contains(mouse))
        {
            return new SelectionState
            {
                Kind = SelectionKind.MainPanel,
                AreaKind = ResolvedDialogueAreaKind.MainInner,
                SlotIndex = -1,
                ComponentIndex = -1
            };
        }
        return SelectionState.None;
    }

    DialogueComponentType ModeToComponent(ToolMode mode)
    {
        switch (mode)
        {
            case ToolMode.AddNamePanel: return DialogueComponentType.NamePanel;
            case ToolMode.AddImagePanel: return DialogueComponentType.ImagePanel;
            default: return DialogueComponentType.TextPanel;
        }
    }

    DialogueComponentDefinition GetSelectedComponentDefinition()
    {
        return DialogueVisualEditorUtility.GetComponent(layoutAsset,
            selection.AreaKind, selection.SlotIndex, selection.ComponentIndex);
    }

    ResolvedDialogueArea FindSelectedArea(ResolvedDialogueLayout layout)
    {
        if (layout == null || selection.Kind != SelectionKind.Area) return null;
        return FindAreaByKind(layout, selection.AreaKind);
    }

    ResolvedDialogueArea FindAreaByKind(ResolvedDialogueLayout layout,
        ResolvedDialogueAreaKind areaKind)
    {
        if (layout == null) return null;
        for (int i = 0; i < layout.Areas.Count; i++)
            if (layout.Areas[i].AreaKind == areaKind)
                return layout.Areas[i];
        return null;
    }

    ResolvedDialogueComponentRect FindSelectedComponent(ResolvedDialogueLayout layout)
    {
        if (layout == null || selection.Kind != SelectionKind.Component) return null;
        for (int i = 0; i < layout.Components.Count; i++)
        {
            ResolvedDialogueComponentRect component = layout.Components[i];
            if (component.AreaKind == selection.AreaKind &&
                component.SlotIndex == selection.SlotIndex &&
                component.ComponentIndex == selection.ComponentIndex)
                return component;
        }
        return null;
    }

    bool IsSelected(ResolvedDialogueArea area)
    {
        return selection.Kind == SelectionKind.Area &&
               selection.AreaKind == area.AreaKind;
    }

    bool IsSelected(ResolvedDialogueSlot slot)
    {
        return selection.Kind == SelectionKind.Slot &&
               selection.AreaKind == slot.AreaKind &&
               selection.SlotIndex == slot.SlotIndex;
    }

    bool IsSelected(ResolvedDialogueComponentRect component)
    {
        return selection.Kind == SelectionKind.Component &&
               selection.AreaKind == component.AreaKind &&
               selection.SlotIndex == component.SlotIndex &&
               selection.ComponentIndex == component.ComponentIndex;
    }

    ResolvedDialogueSlot FindSelectedSlot(ResolvedDialogueLayout layout)
    {
        if (layout == null || selection.Kind != SelectionKind.Slot)
            return null;

        for (int i = 0; i < layout.Slots.Count; i++)
        {
            ResolvedDialogueSlot slot = layout.Slots[i];
            if (slot.AreaKind == selection.AreaKind && slot.SlotIndex == selection.SlotIndex)
                return slot;
        }

        return null;
    }

    ResolvedDialogueSlot FindResolvedSlot(ResolvedDialogueLayout layout,
        ResolvedDialogueAreaKind areaKind, int slotIndex)
    {
        if (layout == null)
            return null;

        for (int i = 0; i < layout.Slots.Count; i++)
        {
            ResolvedDialogueSlot slot = layout.Slots[i];
            if (slot.AreaKind == areaKind && slot.SlotIndex == slotIndex)
                return slot;
        }

        return null;
    }

    bool TryGetSelectedRect(ResolvedDialogueLayout layout, out Rect rect)
    {
        rect = new Rect();
        if (layout == null)
            return false;

        switch (selection.Kind)
        {
            case SelectionKind.MainPanel:
                rect = layout.MainPanelRect;
                return rect.width > 0f && rect.height > 0f;

            case SelectionKind.Area:
                ResolvedDialogueArea area = FindSelectedArea(layout);
                if (area == null) return false;
                rect = area.Rect;
                return true;

            case SelectionKind.Slot:
                ResolvedDialogueSlot slot = FindSelectedSlot(layout);
                if (slot == null) return false;
                rect = slot.Rect;
                return true;

            case SelectionKind.Component:
                ResolvedDialogueComponentRect component = FindSelectedComponent(layout);
                if (component == null) return false;
                rect = component.Rect;
                return true;

            default:
                return false;
        }
    }

    Rect GetSelectedParentRect()
    {
        if (resolved == null)
            return new Rect(0f, 0f, 1f, 1f);

        switch (selection.Kind)
        {
            case SelectionKind.MainPanel:
                return resolved.CanvasRect;

            case SelectionKind.Area:
                if (selection.AreaKind == ResolvedDialogueAreaKind.MainInner)
                    return DialogueVisualLayoutResolver.ShrinkRect(resolved.MainPanelRect,
                        layoutAsset != null && layoutAsset.MainPanel != null ? layoutAsset.MainPanel.Padding : null);
                return resolved.CanvasRect;

            case SelectionKind.Slot:
                ResolvedDialogueArea slotArea = FindAreaByKind(resolved, selection.AreaKind);
                return slotArea != null ? slotArea.Rect : resolved.CanvasRect;

            case SelectionKind.Component:
                ResolvedDialogueSlot slot = FindResolvedSlot(resolved, selection.AreaKind, selection.SlotIndex);
                if (slot == null)
                    return resolved.CanvasRect;
                DialogueSlotDefinition slotDef = DialogueVisualEditorUtility.GetSlot(layoutAsset, selection.AreaKind, selection.SlotIndex);
                Rect slotContent = DialogueVisualLayoutResolver.ShrinkRect(slot.Rect, slotDef != null ? slotDef.Padding : null);
                DialogueComponentDefinition component = GetSelectedComponentDefinition();
                return DialogueVisualLayoutResolver.ShrinkRect(slotContent, component != null ? component.Padding : null);

            default:
                return resolved.CanvasRect;
        }
    }

    void ApplyRectToCurrentSelection(Rect targetRect)
    {
        switch (selection.Kind)
        {
            case SelectionKind.MainPanel:
                ApplyMainPanelRect(targetRect, true, false);
                break;
            case SelectionKind.Area:
                if (selection.AreaKind == ResolvedDialogueAreaKind.MainInner)
                    ApplyInnerRegionRect(targetRect);
                else
                    ApplyAttachedAreaRect(targetRect);
                break;
            case SelectionKind.Slot:
                ApplySlotRect(targetRect);
                break;
            case SelectionKind.Component:
                ApplyComponentRect(targetRect);
                break;
        }
    }

    void ApplyMainPanelRect(Rect targetRect, bool forceFixedFillMode,
        bool updateAnchorFromPosition)
    {
        if (layoutAsset == null || layoutAsset.MainPanel == null || resolved == null)
            return;

        DialogueMainPanelDefinition panel = layoutAsset.MainPanel;
        if (panel.CustomAnchor == null)
            panel.CustomAnchor = new DialogueCustomAnchorDefinition();

        targetRect = ClampRectInside(targetRect, resolved.CanvasRect);
        // Apply the same min/max clamping the resolver will apply before the anchor
        // offsets are derived; otherwise the dragged edge stops tracking the cursor
        // and the panel appears to drift or slide once a min/max limit is hit.
        targetRect = ConstrainRectToPanelMinMax(targetRect);
        float anchorWidth = targetRect.width;
        float anchorHeight = targetRect.height;

        bool rewriteSize = forceFixedFillMode || panel.FillMode != DialoguePanelFillMode.Fixed;
        if (rewriteSize)
        {
            panel.FillMode = DialoguePanelFillMode.Fixed;
            SetSizeAsPixels(panel.Width, targetRect.width, resolved.CanvasRect.width);
            SetSizeAsPixels(panel.Height, targetRect.height, resolved.CanvasRect.height);
            anchorWidth = panel.Width != null ? panel.Width.Value : targetRect.width;
            anchorHeight = panel.Height != null ? panel.Height.Value : targetRect.height;
        }

        DialogueAnchorPreset anchorPreset = panel.AnchorPreset;
        if (updateAnchorFromPosition)
        {
            anchorPreset = ResolveBestAnchorPreset(targetRect, resolved.CanvasRect);
            panel.AnchorPreset = anchorPreset;
        }

        Rect baseRect = GetMainPanelBaseRect(anchorPreset, panel.CustomAnchor,
            resolved.CanvasRect, anchorWidth, anchorHeight);
        panel.CustomAnchor.OffsetX = targetRect.x - baseRect.x;
        panel.CustomAnchor.OffsetY = targetRect.y - baseRect.y;
    }

    Rect ConstrainRectToPanelMinMax(Rect rect)
    {
        DialogueMainPanelDefinition panel = layoutAsset != null ? layoutAsset.MainPanel : null;
        if (panel == null || panel.MinMax == null) return rect;

        DialogueMinMaxSize minMax = panel.MinMax;
        float minWidth = minMax.MinWidth > 0f ? minMax.MinWidth : 0f;
        float minHeight = minMax.MinHeight > 0f ? minMax.MinHeight : 0f;
        float maxWidth = Mathf.Max(minWidth, minMax.MaxWidth > 0f ? minMax.MaxWidth : 100000f);
        float maxHeight = Mathf.Max(minHeight, minMax.MaxHeight > 0f ? minMax.MaxHeight : 100000f);

        float width = Mathf.Clamp(rect.width, minWidth, maxWidth);
        float height = Mathf.Clamp(rect.height, minHeight, maxHeight);
        if (Mathf.Approximately(width, rect.width) && Mathf.Approximately(height, rect.height))
            return rect;

        // Keep the stationary edge pinned while the size sits at its limit so a clamped
        // drag can never turn into a slide.
        float x = rect.x;
        float y = rect.y;
        switch (dragMode)
        {
            case DragMode.ResizeWidthLeft:
                x = Mathf.Min(rect.x, rect.xMax - width);
                break;
            case DragMode.ResizeHeightTop:
                y = Mathf.Min(rect.y, rect.yMax - height);
                break;
            case DragMode.ScaleMainSymmetric:
            case DragMode.ResizeSymmetric:
                x = rect.center.x - width * 0.5f;
                y = rect.center.y - height * 0.5f;
                break;
        }

        return new Rect(x, y, width, height);
    }

    void ApplyInnerRegionRect(Rect targetRect)
    {
        if (layoutAsset == null || layoutAsset.MainPanel == null || layoutAsset.MainPanel.InnerRegion == null ||
            resolved == null)
            return;

        DialogueInnerRegionDefinition region = layoutAsset.MainPanel.InnerRegion;
        Rect parentRect = DialogueVisualLayoutResolver.ShrinkRect(resolved.MainPanelRect,
            layoutAsset.MainPanel.Padding);
        targetRect = ClampRectInside(targetRect, parentRect);

        bool changeWidth = DragChangesWidth(dragMode);
        bool changeHeight = DragChangesHeight(dragMode);

        // Partitioned regions size themselves from their slots; scale the visible
        // slots with the drag so resizing the region resizes it instead of sliding it.
        if (region.PartitionLevel > 0)
        {
            ResolvedDialogueArea currentArea = FindAreaByKind(resolved, ResolvedDialogueAreaKind.MainInner);
            Rect currentRect = currentArea != null ? currentArea.Rect : dragStartRect;
            ScalePartitionedSlotsForParentResize(region.Slots,
                DialogueVisualEditorUtility.GetVisibleSlotCount(region), true,
                region.InterSlotSpacing,
                currentRect.width, targetRect.width,
                currentRect.height, targetRect.height);
        }

        if (changeWidth)
            SetSizeAsPixels(region.Width, targetRect.width, parentRect.width);
        if (changeHeight)
            SetSizeAsPixels(region.Height, targetRect.height, parentRect.height);

        // Axis locked: a width-only drag never shifts the region vertically and a
        // height-only drag never shifts it horizontally.
        Vector2 offset = dragStartSelectionOffset;
        if (changeWidth)
            offset.x = targetRect.center.x - parentRect.center.x;
        if (changeHeight)
            offset.y = targetRect.center.y - parentRect.center.y;
        region.Offset = offset;
    }

    void ApplyAttachedAreaRect(Rect targetRect)
    {
        DialogueAttachedAreaDefinition area = DialogueVisualEditorUtility.GetArea(layoutAsset, selection.AreaKind);
        if (area == null || resolved == null)
            return;

        float gap = Mathf.Max(0f, area.GapFromMainPanel);
        bool horizontal = area.Side == DialogueAttachedAreaSide.Top ||
                          area.Side == DialogueAttachedAreaSide.Bottom;
        float maxWidth = horizontal
            ? resolved.CanvasRect.width
            : Mathf.Max(1f, GetAvailableAttachedAreaSpace(area.Side, resolved.MainPanelRect, resolved.CanvasRect, gap));
        float maxHeight = horizontal
            ? Mathf.Max(1f, GetAvailableAttachedAreaSpace(area.Side, resolved.MainPanelRect, resolved.CanvasRect, gap))
            : resolved.CanvasRect.height;

        bool changeWidth = DragChangesWidth(dragMode);
        bool changeHeight = DragChangesHeight(dragMode);

        float clampedWidth = Mathf.Clamp(targetRect.width, 1f, maxWidth);
        float clampedHeight = Mathf.Clamp(targetRect.height, 1f, maxHeight);

        // Partitioned areas size themselves from their slots once those have explicit
        // sizes; scale the visible slots with the drag so the resize really applies.
        if (area.PartitionLevel > 0)
        {
            ResolvedDialogueArea currentArea = FindAreaByKind(resolved, selection.AreaKind);
            Rect currentRect = currentArea != null ? currentArea.Rect : dragStartRect;
            ScalePartitionedSlotsForParentResize(area.Slots,
                DialogueVisualEditorUtility.GetVisibleSlotCount(area), horizontal,
                area.InterSlotSpacing,
                currentRect.width, clampedWidth,
                currentRect.height, clampedHeight);
        }

        if (changeWidth)
            SetSizeAsPixels(area.Width, clampedWidth, maxWidth);
        if (changeHeight)
            SetSizeAsPixels(area.Height, clampedHeight, maxHeight);

        Rect baseRect = GetAttachedAreaBaseRect(area.Side, resolved.MainPanelRect,
            clampedWidth, clampedHeight, gap);
        Vector2 slide = dragStartSelectionOffset;

        if (horizontal)
        {
            // Only a drag that changes horizontal geometry may move the area along its
            // side; height-only drags keep the slide offset exactly as it was.
            if (changeWidth)
            {
                float pinnedX;
                switch (dragMode)
                {
                    case DragMode.ResizeWidthRight:
                        pinnedX = dragStartRect.x;
                        break;
                    case DragMode.ResizeSymmetric:
                    case DragMode.ScaleMainSymmetric:
                        pinnedX = dragStartRect.center.x - clampedWidth * 0.5f;
                        break;
                    case DragMode.ResizeWidthLeft:
                        // The right edge stays put; a clamped width must not slide it.
                        pinnedX = Mathf.Min(targetRect.x, dragStartRect.xMax - clampedWidth);
                        break;
                    default:
                        pinnedX = targetRect.x;
                        break;
                }
                slide.x = Mathf.Clamp(pinnedX, resolved.CanvasRect.xMin,
                    Mathf.Max(resolved.CanvasRect.xMin, resolved.CanvasRect.xMax - clampedWidth)) - baseRect.x;
            }
            slide.y = 0f;
        }
        else
        {
            if (changeHeight)
            {
                float pinnedY;
                switch (dragMode)
                {
                    case DragMode.ResizeHeightBottom:
                        pinnedY = dragStartRect.y;
                        break;
                    case DragMode.ResizeSymmetric:
                    case DragMode.ScaleMainSymmetric:
                        pinnedY = dragStartRect.center.y - clampedHeight * 0.5f;
                        break;
                    case DragMode.ResizeHeightTop:
                        // The bottom edge stays put; a clamped height must not slide it.
                        pinnedY = Mathf.Min(targetRect.y, dragStartRect.yMax - clampedHeight);
                        break;
                    default:
                        pinnedY = targetRect.y;
                        break;
                }
                slide.y = Mathf.Clamp(pinnedY, resolved.CanvasRect.yMin,
                    Mathf.Max(resolved.CanvasRect.yMin, resolved.CanvasRect.yMax - clampedHeight)) - baseRect.y;
            }
            slide.x = 0f;
        }
        area.Offset = slide;
    }

    static float GetAvailableAttachedAreaSpace(DialogueAttachedAreaSide side,
        Rect mainRect, Rect canvasRect, float gap)
    {
        switch (side)
        {
            case DialogueAttachedAreaSide.Top:
                return mainRect.yMin - canvasRect.yMin - gap;
            case DialogueAttachedAreaSide.Bottom:
                return canvasRect.yMax - mainRect.yMax - gap;
            case DialogueAttachedAreaSide.Left:
                return mainRect.xMin - canvasRect.xMin - gap;
            default:
                return canvasRect.xMax - mainRect.xMax - gap;
        }
    }

    static Rect GetAttachedAreaBaseRect(DialogueAttachedAreaSide side,
        Rect mainRect, float width, float height, float gap)
    {
        switch (side)
        {
            case DialogueAttachedAreaSide.Top:
                return new Rect(mainRect.center.x - width * 0.5f,
                    mainRect.yMin - gap - height, width, height);
            case DialogueAttachedAreaSide.Bottom:
                return new Rect(mainRect.center.x - width * 0.5f,
                    mainRect.yMax + gap, width, height);
            case DialogueAttachedAreaSide.Left:
                return new Rect(mainRect.xMin - gap - width,
                    mainRect.center.y - height * 0.5f, width, height);
            default:
                return new Rect(mainRect.xMax + gap,
                    mainRect.center.y - height * 0.5f, width, height);
        }
    }

    void DrawAreaSlideOffsetField(DialogueAttachedAreaDefinition area)
    {
        if (area == null)
            return;

        switch (area.Side)
        {
            case DialogueAttachedAreaSide.Top:
            case DialogueAttachedAreaSide.Bottom:
                area.Offset.x = EditorGUILayout.FloatField("Horizontal Slide", area.Offset.x);
                area.Offset.y = 0f;
                break;
            case DialogueAttachedAreaSide.Left:
            case DialogueAttachedAreaSide.Right:
                area.Offset.y = EditorGUILayout.FloatField("Vertical Slide", area.Offset.y);
                area.Offset.x = 0f;
                break;
        }
    }

    void ApplySlotRect(Rect targetRect)
    {
        DialogueSlotDefinition slot = DialogueVisualEditorUtility.GetSlot(layoutAsset, selection.AreaKind, selection.SlotIndex);
        if (slot == null || resolved == null)
            return;

        Rect parentRect = GetSelectedParentRect();
        targetRect = ClampRectInside(targetRect, parentRect);

        bool changeWidth = DragChangesWidth(dragMode);
        bool changeHeight = DragChangesHeight(dragMode);
        if (changeWidth)
            SetSizeAsPixels(slot.Width, targetRect.width, parentRect.width);
        if (changeHeight)
            SetSizeAsPixels(slot.Height, targetRect.height, parentRect.height);

        // The offset is applied absolutely from the slot's flow position inside its row
        // (captured at drag start). Accumulating deltas against the live, clamped
        // resolved position made offsets run away and slots teleport mid-drag.
        Vector2 flowBase = dragStartRect.position - dragStartSelectionOffset;
        Vector2 offset = dragStartSelectionOffset;
        if (changeWidth)
            offset.x = targetRect.x - flowBase.x;
        if (changeHeight)
            offset.y = targetRect.y - flowBase.y;
        slot.Offset = offset;
    }

    void ApplyComponentRect(Rect targetRect)
    {
        DialogueComponentDefinition component = GetSelectedComponentDefinition();
        if (component == null)
            return;

        Rect parentRect = GetSelectedParentRect();
        targetRect = ClampRectInside(targetRect, parentRect);

        bool changeWidth = DragChangesWidth(dragMode);
        bool changeHeight = DragChangesHeight(dragMode);

        // Stretch overrides explicit sizes, so release stretch on the axis being
        // resized only; the other alignment is left untouched.
        if (changeWidth && component.HorizontalAlignment == DialogueHorizontalAlignment.Stretch)
            component.HorizontalAlignment = DialogueHorizontalAlignment.Left;
        if (changeHeight && component.VerticalAlignment == DialogueVerticalAlignment.Stretch)
            component.VerticalAlignment = DialogueVerticalAlignment.Top;

        if (changeWidth)
            SetSizeAsPixels(component.Width, targetRect.width, parentRect.width);
        if (changeHeight)
            SetSizeAsPixels(component.Height, targetRect.height, parentRect.height);

        Rect alignedRect = ResolveAlignedComponentRect(component, parentRect, targetRect.width, targetRect.height);
        Vector2 offset = component.Offset;
        if (changeWidth)
            offset.x = targetRect.x - alignedRect.x;
        if (changeHeight)
            offset.y = targetRect.y - alignedRect.y;
        component.Offset = offset;
    }

    static DialogueAnchorPreset ResolveBestAnchorPreset(Rect rect, Rect canvas)
    {
        float leftBoundary = canvas.xMin + canvas.width / 3f;
        float rightBoundary = canvas.xMax - canvas.width / 3f;
        float topBoundary = canvas.yMin + canvas.height / 3f;
        float bottomBoundary = canvas.yMax - canvas.height / 3f;

        int horizontalZone = rect.center.x < leftBoundary ? -1 : rect.center.x > rightBoundary ? 1 : 0;
        int verticalZone = rect.center.y < topBoundary ? -1 : rect.center.y > bottomBoundary ? 1 : 0;

        if (verticalZone < 0)
            return horizontalZone < 0 ? DialogueAnchorPreset.TopLeft
                : horizontalZone > 0 ? DialogueAnchorPreset.TopRight
                : DialogueAnchorPreset.Top;
        if (verticalZone > 0)
            return horizontalZone < 0 ? DialogueAnchorPreset.BottomLeft
                : horizontalZone > 0 ? DialogueAnchorPreset.BottomRight
                : DialogueAnchorPreset.Bottom;

        return horizontalZone < 0 ? DialogueAnchorPreset.Left
            : horizontalZone > 0 ? DialogueAnchorPreset.Right
            : DialogueAnchorPreset.Center;
    }

    static Rect GetMainPanelBaseRect(DialogueAnchorPreset anchor,
        DialogueCustomAnchorDefinition customAnchor, Rect canvas,
        float width, float height)
    {
        float x = canvas.center.x - width * 0.5f;
        float y = canvas.center.y - height * 0.5f;

        switch (anchor)
        {
            case DialogueAnchorPreset.TopLeft:
                x = canvas.xMin;
                y = canvas.yMin;
                break;
            case DialogueAnchorPreset.Top:
                x = canvas.center.x - width * 0.5f;
                y = canvas.yMin;
                break;
            case DialogueAnchorPreset.TopRight:
                x = canvas.xMax - width;
                y = canvas.yMin;
                break;
            case DialogueAnchorPreset.Left:
                x = canvas.xMin;
                y = canvas.center.y - height * 0.5f;
                break;
            case DialogueAnchorPreset.Center:
                x = canvas.center.x - width * 0.5f;
                y = canvas.center.y - height * 0.5f;
                break;
            case DialogueAnchorPreset.Right:
                x = canvas.xMax - width;
                y = canvas.center.y - height * 0.5f;
                break;
            case DialogueAnchorPreset.BottomLeft:
                x = canvas.xMin;
                y = canvas.yMax - height;
                break;
            case DialogueAnchorPreset.Bottom:
                x = canvas.center.x - width * 0.5f;
                y = canvas.yMax - height;
                break;
            case DialogueAnchorPreset.BottomRight:
                x = canvas.xMax - width;
                y = canvas.yMax - height;
                break;
            case DialogueAnchorPreset.Custom:
                x = ResolveCustomAnchorBaseX(customAnchor, canvas, width);
                y = ResolveCustomAnchorBaseY(customAnchor, canvas, height);
                break;
        }

        return new Rect(x, y, width, height);
    }

    static float ResolveCustomAnchorBaseX(DialogueCustomAnchorDefinition custom,
        Rect canvas, float width)
    {
        if (custom == null)
            return canvas.center.x - width * 0.5f;

        switch (custom.HorizontalReference)
        {
            case DialogueAnchorReferenceEdge.Left:
                return canvas.xMin;
            case DialogueAnchorReferenceEdge.Right:
                return canvas.xMax - width;
            default:
                return canvas.center.x - width * 0.5f;
        }
    }

    static float ResolveCustomAnchorBaseY(DialogueCustomAnchorDefinition custom,
        Rect canvas, float height)
    {
        if (custom == null)
            return canvas.center.y - height * 0.5f;

        switch (custom.VerticalReference)
        {
            case DialogueAnchorReferenceEdge.Top:
                return canvas.yMin;
            case DialogueAnchorReferenceEdge.Bottom:
                return canvas.yMax - height;
            default:
                return canvas.center.y - height * 0.5f;
        }
    }

    static Rect ResolveAlignedComponentRect(DialogueComponentDefinition component,
        Rect parentRect, float width, float height)
    {
        float x = parentRect.xMin;
        switch (component.HorizontalAlignment)
        {
            case DialogueHorizontalAlignment.Center:
                x = parentRect.center.x - width * 0.5f;
                break;
            case DialogueHorizontalAlignment.Right:
                x = parentRect.xMax - width;
                break;
            case DialogueHorizontalAlignment.Stretch:
                x = parentRect.xMin;
                width = parentRect.width;
                break;
        }

        float y = parentRect.yMin;
        switch (component.VerticalAlignment)
        {
            case DialogueVerticalAlignment.Center:
                y = parentRect.center.y - height * 0.5f;
                break;
            case DialogueVerticalAlignment.Bottom:
                y = parentRect.yMax - height;
                break;
            case DialogueVerticalAlignment.Stretch:
                y = parentRect.yMin;
                height = parentRect.height;
                break;
        }

        return new Rect(x, y, width, height);
    }

    static Rect ClampRectInside(Rect rect, Rect parentRect)
    {
        if (parentRect.width <= 0f || parentRect.height <= 0f)
            return rect;

        rect.width = Mathf.Clamp(rect.width, 1f, parentRect.width);
        rect.height = Mathf.Clamp(rect.height, 1f, parentRect.height);
        rect.x = Mathf.Clamp(rect.x, parentRect.xMin, parentRect.xMax - rect.width);
        rect.y = Mathf.Clamp(rect.y, parentRect.yMin, parentRect.yMax - rect.height);
        return rect;
    }

    static Rect CreateWidthAdjustedRect(Rect startRect, float deltaX, bool adjustMinSide)
    {
        const float minSize = 1f;
        if (adjustMinSide)
        {
            float newX = Mathf.Min(startRect.xMax - minSize, startRect.x + deltaX);
            return new Rect(newX, startRect.y, Mathf.Max(minSize, startRect.xMax - newX), startRect.height);
        }

        float newWidth = Mathf.Max(minSize, startRect.width + deltaX);
        return new Rect(startRect.x, startRect.y, newWidth, startRect.height);
    }

    static Rect CreateHeightAdjustedRect(Rect startRect, float deltaY, bool adjustMinSide)
    {
        const float minSize = 1f;
        if (adjustMinSide)
        {
            float newY = Mathf.Min(startRect.yMax - minSize, startRect.y + deltaY);
            return new Rect(startRect.x, newY, startRect.width, Mathf.Max(minSize, startRect.yMax - newY));
        }

        float newHeight = Mathf.Max(minSize, startRect.height + deltaY);
        return new Rect(startRect.x, startRect.y, startRect.width, newHeight);
    }

    static Rect CreateSymmetricResizedRect(Rect startRect, Vector2 delta, Vector2 handleDirection)
    {
        const float minSize = 1f;
        float width = Mathf.Max(minSize, startRect.width + delta.x * handleDirection.x * 2f);
        float height = Mathf.Max(minSize, startRect.height + delta.y * handleDirection.y * 2f);
        return new Rect(
            startRect.center.x - width * 0.5f,
            startRect.center.y - height * 0.5f,
            width,
            height);
    }

    void ApplyBridge()
    {
        if (engine == null || layoutAsset == null) return;
        DialogueVisualLayoutBridge.ApplyToEngine(engine, layoutAsset);
        EditorUtility.SetDirty(engine);
    }

    void CommitLayoutMutation()
    {
        if (layoutAsset == null) return;
        EditorUtility.SetDirty(layoutAsset);
        if (autoApplyToEngine)
            ApplyBridge();
        Repaint();
    }

    static void SetSizeAsPixels(DialogueSizeValue size, float value, float maxValue)
    {
        if (size == null) return;
        size.Unit = DialogueSizeUnit.Pixels;
        size.Value = Mathf.Clamp(value, 1f, Mathf.Max(1f, maxValue));
    }

    static Rect GetMoveHandle(Rect rect, float size)
    {
        return new Rect(rect.center.x - size * 0.5f, rect.center.y - size * 0.5f, size, size);
    }

    static Rect GetLeftEdgeHandle(Rect rect, float size)
    {
        return new Rect(rect.xMin - size * 0.5f, rect.center.y - size * 0.5f, size, size);
    }

    static Rect GetRightEdgeHandle(Rect rect, float size)
    {
        return new Rect(rect.xMax - size * 0.5f, rect.center.y - size * 0.5f, size, size);
    }

    static Rect GetTopEdgeHandle(Rect rect, float size)
    {
        return new Rect(rect.center.x - size * 0.5f, rect.yMin - size * 0.5f, size, size);
    }

    static Rect GetBottomEdgeHandle(Rect rect, float size)
    {
        return new Rect(rect.center.x - size * 0.5f, rect.yMax - size * 0.5f, size, size);
    }

    // The whole edge is a live drag zone, so draw it as a visible grip bar rather than
    // just the little centre square.
    static void DrawEdgeBars(Rect rect, bool verticalEdges, Color color)
    {
        const float thickness = 4f;
        const float inset = 6f;
        Rect bar = verticalEdges
            ? new Rect(rect.xMin - thickness * 0.5f, rect.yMin + inset, thickness, Mathf.Max(4f, rect.height - inset * 2f))
            : new Rect(rect.xMin + inset, rect.yMin - thickness * 0.5f, Mathf.Max(4f, rect.width - inset * 2f), thickness);
        EditorGUI.DrawRect(bar, color * new Color(1f, 1f, 1f, 0.55f));

        Rect bar2 = verticalEdges
            ? new Rect(rect.xMax - thickness * 0.5f, rect.yMin + inset, thickness, Mathf.Max(4f, rect.height - inset * 2f))
            : new Rect(rect.xMin + inset, rect.yMax - thickness * 0.5f, Mathf.Max(4f, rect.width - inset * 2f), thickness);
        EditorGUI.DrawRect(bar2, color * new Color(1f, 1f, 1f, 0.55f));
    }

    static Rect GetLeftEdgeDragZone(Rect rect, float size)
    {
        float thickness = Mathf.Max(size, 18f);
        return new Rect(rect.xMin - thickness * 0.5f, rect.yMin - size,
            thickness, rect.height + size * 2f);
    }

    static Rect GetRightEdgeDragZone(Rect rect, float size)
    {
        float thickness = Mathf.Max(size, 18f);
        return new Rect(rect.xMax - thickness * 0.5f, rect.yMin - size,
            thickness, rect.height + size * 2f);
    }

    static Rect GetTopEdgeDragZone(Rect rect, float size)
    {
        float thickness = Mathf.Max(size, 18f);
        return new Rect(rect.xMin - size, rect.yMin - thickness * 0.5f,
            rect.width + size * 2f, thickness);
    }

    static Rect GetBottomEdgeDragZone(Rect rect, float size)
    {
        float thickness = Mathf.Max(size, 18f);
        return new Rect(rect.xMin - size, rect.yMax - thickness * 0.5f,
            rect.width + size * 2f, thickness);
    }

    static Rect GetTopLeftCornerHandle(Rect rect, float size)
    {
        return new Rect(rect.xMin - size * 0.5f, rect.yMin - size * 0.5f, size, size);
    }

    static Rect GetTopRightCornerHandle(Rect rect, float size)
    {
        return new Rect(rect.xMax - size * 0.5f, rect.yMin - size * 0.5f, size, size);
    }

    static Rect GetBottomLeftCornerHandle(Rect rect, float size)
    {
        return new Rect(rect.xMin - size * 0.5f, rect.yMax - size * 0.5f, size, size);
    }

    static Rect GetBottomRightCornerHandle(Rect rect, float size)
    {
        return new Rect(rect.xMax - size * 0.5f, rect.yMax - size * 0.5f, size, size);
    }

    static bool TryHitCornerHandle(Rect rect, Vector2 mouse, float size, out Vector2 direction)
    {
        if (GetTopLeftCornerHandle(rect, size).Contains(mouse))
        {
            direction = new Vector2(-1f, -1f);
            return true;
        }
        if (GetTopRightCornerHandle(rect, size).Contains(mouse))
        {
            direction = new Vector2(1f, -1f);
            return true;
        }
        if (GetBottomLeftCornerHandle(rect, size).Contains(mouse))
        {
            direction = new Vector2(-1f, 1f);
            return true;
        }
        if (GetBottomRightCornerHandle(rect, size).Contains(mouse))
        {
            direction = new Vector2(1f, 1f);
            return true;
        }

        direction = Vector2.one;
        return false;
    }

    static void DrawCornerHandles(Rect rect, float size, Color color)
    {
        DrawHandleBox(GetTopLeftCornerHandle(rect, size), color);
        DrawHandleBox(GetTopRightCornerHandle(rect, size), color);
        DrawHandleBox(GetBottomLeftCornerHandle(rect, size), color);
        DrawHandleBox(GetBottomRightCornerHandle(rect, size), color);
    }

    static Rect GetAreaGapHandle(Rect areaRect, Rect mainRect, DialogueAttachedAreaSide side)
    {
        const float size = 10f;
        switch (side)
        {
            case DialogueAttachedAreaSide.Top:
                return new Rect(mainRect.center.x - size * 0.5f, mainRect.yMin - size * 0.5f, size, size);
            case DialogueAttachedAreaSide.Bottom:
                return new Rect(mainRect.center.x - size * 0.5f, mainRect.yMax - size * 0.5f, size, size);
            case DialogueAttachedAreaSide.Left:
                return new Rect(mainRect.xMin - size * 0.5f, mainRect.center.y - size * 0.5f, size, size);
            default:
                return new Rect(mainRect.xMax - size * 0.5f, mainRect.center.y - size * 0.5f, size, size);
        }
    }

    static void DrawHandleBox(Rect rect, Color color)
    {
        EditorGUI.DrawRect(rect, color);
        Handles.color = Color.black;
        DrawOutline(rect, 1f);
    }

    static void DrawOutline(Rect rect, float thickness)
    {
        Handles.DrawAAPolyLine(thickness,
            new Vector3(rect.xMin, rect.yMin),
            new Vector3(rect.xMax, rect.yMin),
            new Vector3(rect.xMax, rect.yMax),
            new Vector3(rect.xMin, rect.yMax),
            new Vector3(rect.xMin, rect.yMin));
    }

    static Color GetComponentFill(DialogueComponentType type)
    {
        switch (type)
        {
            case DialogueComponentType.ImagePanel:
                return new Color(0.78f, 0.48f, 0.18f, 0.24f);
            case DialogueComponentType.NamePanel:
                return new Color(0.24f, 0.56f, 0.92f, 0.24f);
            default:
                return new Color(0.92f, 0.92f, 0.92f, 0.15f);
        }
    }

    void DrawDashedRect(Rect rect, float dash)
    {
        DrawDashedLine(new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMax, rect.yMin), dash);
        DrawDashedLine(new Vector2(rect.xMax, rect.yMin), new Vector2(rect.xMax, rect.yMax), dash);
        DrawDashedLine(new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMin, rect.yMax), dash);
        DrawDashedLine(new Vector2(rect.xMin, rect.yMax), new Vector2(rect.xMin, rect.yMin), dash);
    }

    void DrawDashedLine(Vector2 a, Vector2 b, float dash)
    {
        float distance = Vector2.Distance(a, b);
        Vector2 dir = (b - a).normalized;
        for (float p = 0f; p < distance; p += dash * 2f)
        {
            Vector2 start = a + dir * p;
            Vector2 end = a + dir * Mathf.Min(distance, p + dash);
            Handles.DrawLine(start, end);
        }
    }

    void HandleCanvasDragAndDrop(Rect rect)
    {
        UnityEngine.Event evt = UnityEngine.Event.current;
        if (!rect.Contains(evt.mousePosition)) return;
        if (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform) return;
        if (DragAndDrop.objectReferences == null || DragAndDrop.objectReferences.Length == 0) return;

        DialogueLayoutAsset dropped = DragAndDrop.objectReferences[0] as DialogueLayoutAsset;
        if (dropped == null) return;
        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
        if (evt.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            layoutAsset = dropped;
            if (engine != null)
                engine.visualLayoutAsset = dropped;
            Repaint();
        }
        evt.Use();
    }
}
#endif
