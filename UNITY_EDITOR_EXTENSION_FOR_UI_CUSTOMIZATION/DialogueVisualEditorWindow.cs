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
        MoveMainCustom,
        ResizeMain,
        ResizeArea,
        AdjustAreaGap,
        MoveComponent,
        ResizeComponent
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
    SelectionState selection = SelectionState.None;
    DragMode dragMode = DragMode.None;
    Vector2 dragStartMouse;
    DialogueSizeUnit dragStartWidthUnit;
    DialogueSizeUnit dragStartHeightUnit;
    float dragStartWidthValue;
    float dragStartHeightValue;
    float dragStartGapValue;
    Vector2 dragStartOffset;

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

        GUILayout.Space(10f);
        toolMode = (ToolMode)GUILayout.Toolbar((int)toolMode,
            new[] { "Select", "Add Text", "Add Name", "Add Image" },
            EditorStyles.toolbarButton, GUILayout.Width(320f));

        GUILayout.FlexibleSpace();

        GUI.enabled = engine != null && layoutAsset != null;
        if (GUILayout.Button("Apply To Engine", EditorStyles.toolbarButton, GUILayout.Width(100f)))
            ApplyBridge();
        GUI.enabled = layoutAsset != null;
        if (GUILayout.Button("Open Preview", EditorStyles.toolbarButton, GUILayout.Width(90f)))
            DialogueVisualLayoutPreviewWindow.Open(engine);
        GUI.enabled = true;

        EditorGUILayout.EndHorizontal();
    }

    void DrawLeftSidebar()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(250f));
        GUILayout.Space(6f);

        GUILayout.Label("Palette", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Phase 3 MVP: select objects, add attached areas, add components to slots, resize or move selected elements, and edit the current selection on the right.",
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
        if (layout == null) return;

        EditorGUI.DrawRect(layout.MainPanelRect, new Color(0.16f, 0.20f, 0.30f, 0.88f));
        Handles.color = selection.Kind == SelectionKind.MainPanel
            ? new Color(1f, 1f, 0.45f, 1f)
            : new Color(0.70f, 0.82f, 1f, 1f);
        DrawOutline(layout.MainPanelRect, 2f);
        if (showLabels)
            GUI.Label(new Rect(layout.MainPanelRect.x + 6f, layout.MainPanelRect.y + 4f, 240f, 18f),
                "Main Panel", EditorStyles.whiteBoldLabel);

        for (int i = 0; i < layout.Areas.Count; i++)
        {
            ResolvedDialogueArea area = layout.Areas[i];
            Color fill = area.AreaKind == ResolvedDialogueAreaKind.MainInner
                ? new Color(0.22f, 0.26f, 0.30f, 0.30f)
                : new Color(0.16f, 0.36f, 0.24f, 0.40f);
            EditorGUI.DrawRect(area.Rect, fill);
            Handles.color = IsSelected(area)
                ? new Color(1f, 1f, 0.45f, 1f)
                : new Color(0.65f, 0.95f, 0.70f, 1f);
            DrawOutline(area.Rect, 1.5f);
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
                if (slotDef != null && slotDef.Background != null)
                {
                    Color fill = slotDef.Background.ColorA;
                    fill.a *= slotDef.Background.Opacity * 0.35f;
                    if (fill.a > 0f) EditorGUI.DrawRect(slot.Rect, fill);
                }
                Handles.color = IsSelected(slot)
                    ? new Color(1f, 1f, 0.45f, 1f)
                    : new Color(1f, 0.84f, 0.40f, 1f);
                DrawOutline(slot.Rect, 1.2f);
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
                EditorGUI.DrawRect(component.Rect, GetComponentFill(component.ComponentType));
                Handles.color = IsSelected(component)
                    ? new Color(1f, 1f, 0.45f, 1f)
                    : component.ClipToSlot
                        ? new Color(1f, 1f, 1f, 0.9f)
                        : new Color(1f, 0.45f, 0.45f, 1f);
                DrawOutline(component.Rect, 1.4f);
                if (!component.ClipToSlot)
                    DrawDashedRect(component.Rect, 6f);
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
        const float handle = 10f;
        switch (selection.Kind)
        {
            case SelectionKind.MainPanel:
                DrawHandleBox(GetResizeHandle(layout.MainPanelRect, handle), new Color(0.9f, 0.9f, 0.2f, 1f));
                break;
            case SelectionKind.Area:
                ResolvedDialogueArea area = FindSelectedArea(layout);
                if (area != null)
                {
                    DrawHandleBox(GetResizeHandle(area.Rect, handle), new Color(0.9f, 0.9f, 0.2f, 1f));
                    Rect gap = GetAreaGapHandle(area.Rect, layout.MainPanelRect, area.Side);
                    DrawHandleBox(gap, new Color(0.2f, 1f, 1f, 1f));
                }
                break;
            case SelectionKind.Component:
                ResolvedDialogueComponentRect component = FindSelectedComponent(layout);
                if (component != null)
                {
                    DrawHandleBox(GetResizeHandle(component.Rect, handle), new Color(0.9f, 0.9f, 0.2f, 1f));
                    DrawHandleBox(GetMoveHandle(component.Rect, handle), new Color(0.2f, 1f, 1f, 1f));
                }
                break;
        }
    }

    void HandleCanvasInput(UnityEngine.Event evt, Rect paddedCanvas)
    {
        if (evt == null || layoutAsset == null || resolved == null) return;

        if (evt.type == EventType.MouseDown && evt.button == 0 && paddedCanvas.Contains(evt.mousePosition))
        {
            if (editMode && TryBeginDrag(evt.mousePosition))
            {
                evt.Use();
                return;
            }

            SelectionState hit = HitTest(evt.mousePosition);
            if (toolMode == ToolMode.Select)
                selection = hit;
            else if (hit.Kind == SelectionKind.Slot)
                AddComponentAtSlot(hit, ModeToComponent(toolMode));
            else
                selection = hit;
            Repaint();
            evt.Use();
            return;
        }

        if (evt.type == EventType.MouseDrag && evt.button == 0 && dragMode != DragMode.None)
        {
            DragSelection(evt.mousePosition - dragStartMouse);
            evt.Use();
            return;
        }

        if (evt.type == EventType.MouseUp && evt.button == 0 && dragMode != DragMode.None)
        {
            dragMode = DragMode.None;
            evt.Use();
            return;
        }
    }

    bool TryBeginDrag(Vector2 mouse)
    {
        const float size = 10f;
        if (selection.Kind == SelectionKind.MainPanel)
        {
            if (GetResizeHandle(resolved.MainPanelRect, size).Contains(mouse))
            {
                BeginMainResize();
                return true;
            }
            if (layoutAsset != null && layoutAsset.MainPanel != null &&
                layoutAsset.MainPanel.AnchorPreset == DialogueAnchorPreset.Custom &&
                resolved.MainPanelRect.Contains(mouse))
            {
                BeginMainMove();
                return true;
            }
        }

        if (selection.Kind == SelectionKind.Area)
        {
            ResolvedDialogueArea area = FindSelectedArea(resolved);
            if (area != null)
            {
                if (GetAreaGapHandle(area.Rect, resolved.MainPanelRect, area.Side).Contains(mouse))
                {
                    BeginAreaGapDrag(area.AreaKind);
                    return true;
                }
                if (GetResizeHandle(area.Rect, size).Contains(mouse))
                {
                    BeginAreaResize(area.AreaKind);
                    return true;
                }
            }
        }

        if (selection.Kind == SelectionKind.Component)
        {
            ResolvedDialogueComponentRect component = FindSelectedComponent(resolved);
            if (component != null)
            {
                if (GetResizeHandle(component.Rect, size).Contains(mouse))
                {
                    BeginComponentResize(component);
                    return true;
                }
                if (GetMoveHandle(component.Rect, size).Contains(mouse))
                {
                    BeginComponentMove(component);
                    return true;
                }
            }
        }

        return false;
    }

    void BeginMainResize()
    {
        dragMode = DragMode.ResizeMain;
        dragStartMouse = UnityEngine.Event.current.mousePosition;
        dragStartWidthUnit = layoutAsset.MainPanel.Width.Unit;
        dragStartHeightUnit = layoutAsset.MainPanel.Height.Unit;
        dragStartWidthValue = layoutAsset.MainPanel.Width.Value;
        dragStartHeightValue = layoutAsset.MainPanel.Height.Value;
        DialogueVisualEditorUtility.RecordChange(layoutAsset, "Resize Main Panel");
    }

    void BeginMainMove()
    {
        dragMode = DragMode.MoveMainCustom;
        dragStartMouse = UnityEngine.Event.current.mousePosition;
        dragStartOffset = new Vector2(layoutAsset.MainPanel.CustomAnchor.OffsetX,
            layoutAsset.MainPanel.CustomAnchor.OffsetY);
        DialogueVisualEditorUtility.RecordChange(layoutAsset, "Move Main Panel");
    }

    void BeginAreaResize(ResolvedDialogueAreaKind kind)
    {
        DialogueAttachedAreaDefinition area = DialogueVisualEditorUtility.GetArea(layoutAsset, kind);
        if (area == null) return;
        dragMode = DragMode.ResizeArea;
        dragStartMouse = UnityEngine.Event.current.mousePosition;
        dragStartWidthUnit = area.Width.Unit;
        dragStartHeightUnit = area.Height.Unit;
        dragStartWidthValue = area.Width.Value;
        dragStartHeightValue = area.Height.Value;
        selection.AreaKind = kind;
        DialogueVisualEditorUtility.RecordChange(layoutAsset, "Resize Attached Area");
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

    void BeginComponentMove(ResolvedDialogueComponentRect component)
    {
        DialogueComponentDefinition def = DialogueVisualEditorUtility.GetComponent(layoutAsset,
            component.AreaKind, component.SlotIndex, component.ComponentIndex);
        if (def == null) return;
        dragMode = DragMode.MoveComponent;
        dragStartMouse = UnityEngine.Event.current.mousePosition;
        dragStartOffset = def.Offset;
        DialogueVisualEditorUtility.RecordChange(layoutAsset, "Move Component");
    }

    void BeginComponentResize(ResolvedDialogueComponentRect component)
    {
        DialogueComponentDefinition def = DialogueVisualEditorUtility.GetComponent(layoutAsset,
            component.AreaKind, component.SlotIndex, component.ComponentIndex);
        if (def == null) return;
        dragMode = DragMode.ResizeComponent;
        dragStartMouse = UnityEngine.Event.current.mousePosition;
        dragStartWidthUnit = def.Width.Unit;
        dragStartHeightUnit = def.Height.Unit;
        dragStartWidthValue = def.Width.Value;
        dragStartHeightValue = def.Height.Value;
        DialogueVisualEditorUtility.RecordChange(layoutAsset, "Resize Component");
    }

    void DragSelection(Vector2 delta)
    {
        switch (dragMode)
        {
            case DragMode.MoveMainCustom:
                if (layoutAsset.MainPanel != null && layoutAsset.MainPanel.CustomAnchor != null)
                {
                    layoutAsset.MainPanel.CustomAnchor.OffsetX = dragStartOffset.x + delta.x;
                    layoutAsset.MainPanel.CustomAnchor.OffsetY = dragStartOffset.y + delta.y;
                }
                break;
            case DragMode.ResizeMain:
                SetSizeAsPixels(layoutAsset.MainPanel.Width, dragStartWidthValue + delta.x);
                SetSizeAsPixels(layoutAsset.MainPanel.Height, dragStartHeightValue + delta.y);
                break;
            case DragMode.ResizeArea:
                DialogueAttachedAreaDefinition area = DialogueVisualEditorUtility.GetArea(layoutAsset, selection.AreaKind);
                if (area != null)
                {
                    SetSizeAsPixels(area.Width, dragStartWidthValue + delta.x);
                    SetSizeAsPixels(area.Height, dragStartHeightValue + delta.y);
                }
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
            case DragMode.MoveComponent:
                DialogueComponentDefinition comp = GetSelectedComponentDefinition();
                if (comp != null)
                    comp.Offset = dragStartOffset + delta;
                break;
            case DragMode.ResizeComponent:
                DialogueComponentDefinition resizeComp = GetSelectedComponentDefinition();
                if (resizeComp != null)
                {
                    SetSizeAsPixels(resizeComp.Width, dragStartWidthValue + delta.x);
                    SetSizeAsPixels(resizeComp.Height, dragStartHeightValue + delta.y);
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
        string label = enabled ? name : name + " (disabled)";
        DrawHierarchyButton(label, SelectionKind.Area, kind, -1, -1, 1);
        if (enabled) DrawHierarchySlots(kind, 2);
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
            panel.CustomAnchor.OffsetX = EditorGUILayout.FloatField("Offset X", panel.CustomAnchor.OffsetX);
            panel.CustomAnchor.OffsetY = EditorGUILayout.FloatField("Offset Y", panel.CustomAnchor.OffsetY);
        }

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
        area.DisplayName = EditorGUILayout.TextField("Display Name", area.DisplayName);
        area.Enabled = EditorGUILayout.Toggle("Enabled", area.Enabled);
        DialogueVisualEditorUtility.SetAreaEnabled(layoutAsset, selection.AreaKind, area.Enabled);
        area.GapFromMainPanel = EditorGUILayout.FloatField("Gap From Main Panel", area.GapFromMainPanel);
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
            "Slots are the final partition pieces. They cannot be partitioned further. They inherit their parent region's visual settings by default, but you can override their own size, spacing-after, and visual styling here.",
            MessageType.None);
        DrawSizeField("Width", slot.Width);
        DrawSizeField("Height", slot.Height);
        slot.GapAfter = EditorGUILayout.FloatField("Gap To Next Slot (-1 uses parent)", slot.GapAfter);
        DrawPaddingField("Padding", slot.Padding);
        slot.ZLayer = EditorGUILayout.IntSlider("Z Layer", slot.ZLayer, -10, 10);
        DrawBackgroundStyle(slot.Background);
        DrawBorderStyle(slot.Border);
        DrawShadowStyle(slot.Shadow);
        DrawOpacity(slot.Opacity);
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
        component.BaseAnimationProfile = (TextAnimationProfile)EditorGUILayout.ObjectField("Base Animation Profile", component.BaseAnimationProfile, typeof(TextAnimationProfile), false);
        component.OverlayAnimationProfile = (TextAnimationProfile)EditorGUILayout.ObjectField("Overlay Animation Profile", component.OverlayAnimationProfile, typeof(TextAnimationProfile), false);
    }

    void DrawNamePanelInspector(DialogueNamePanelDefinition component)
    {
        if (component == null) return;
        component.TypewriterEnabled = EditorGUILayout.Toggle("Typewriter Enabled", component.TypewriterEnabled);
        component.CharactersPerSecond = EditorGUILayout.FloatField("Characters Per Second", component.CharactersPerSecond);
        component.StartDelay = EditorGUILayout.FloatField("Start Delay", component.StartDelay);
        DrawTextStyle(component.TextStyle);
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
            component.UniformScale = EditorGUILayout.FloatField("Uniform Scale", component.UniformScale);
            component.InnerPadding = EditorGUILayout.FloatField("Inner Padding", component.InnerPadding);
            DrawImageStyle(component.ImageStyle);
        }
        else
        {
            component.FigureScaleMode = (DialogueFigureScaleMode)EditorGUILayout.EnumPopup("Scale Mode", component.FigureScaleMode);
            component.FlipHorizontal = EditorGUILayout.Toggle("Flip Horizontal", component.FlipHorizontal);
            component.ImageSourceKey = EditorGUILayout.TextField("Image Source Key", component.ImageSourceKey);
        }
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
        for (int i = 0; i < layout.Areas.Count; i++)
            if (layout.Areas[i].AreaKind == selection.AreaKind)
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

    static void SetSizeAsPixels(DialogueSizeValue size, float value)
    {
        if (size == null) return;
        size.Unit = DialogueSizeUnit.Pixels;
        size.Value = Mathf.Max(0f, value);
    }

    static Rect GetResizeHandle(Rect rect, float size)
    {
        return new Rect(rect.xMax - size, rect.yMax - size, size, size);
    }

    static Rect GetMoveHandle(Rect rect, float size)
    {
        return new Rect(rect.center.x - size * 0.5f, rect.center.y - size * 0.5f, size, size);
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
