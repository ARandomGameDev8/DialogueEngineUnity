#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public sealed class DialogueVisualLayoutPreviewWindow : EditorWindow
{
    Dialogue_Engine engine;
    DialogueLayoutAsset layoutAsset;
    Vector2 scroll;
    bool showLabels = true;
    bool showSlots = true;
    bool showComponents = true;

    public static void Open(Dialogue_Engine targetEngine)
    {
        var window = GetWindow<DialogueVisualLayoutPreviewWindow>("Visual Layout Preview");
        window.engine = targetEngine;
        window.layoutAsset = targetEngine != null ? targetEngine.visualLayoutAsset : null;
        window.minSize = new Vector2(620, 520);
        window.Show();
    }

    [MenuItem("Tools/Dialogue/Open Visual Layout Preview")]
    static void OpenFromMenu()
    {
        Open(Object.FindFirstObjectByType<Dialogue_Engine>());
    }

    void OnGUI()
    {
        DrawToolbar();
        scroll = EditorGUILayout.BeginScrollView(scroll);

        Rect canvas = GUILayoutUtility.GetRect(position.width - 24f, 420f,
            GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(canvas, new Color(0.10f, 0.10f, 0.11f, 1f));

        if (layoutAsset == null)
        {
            EditorGUILayout.HelpBox(
                "Assign a DialogueLayoutAsset on a Dialogue_Engine, or drag one directly into this window.",
                MessageType.Info);
            HandleDragAndDrop(canvas);
            EditorGUILayout.EndScrollView();
            return;
        }

        Rect padded = new Rect(canvas.x + 10f, canvas.y + 10f,
            canvas.width - 20f, canvas.height - 20f);
        ResolvedDialogueLayout resolved = DialogueVisualLayoutResolver.Resolve(layoutAsset, padded);
        DrawResolved(resolved);
        HandleDragAndDrop(canvas);

        EditorGUILayout.Space(8);
        if (engine != null)
        {
            EditorGUILayout.HelpBox(
                "This phase-2 preview is read-only. It resolves the DialogueLayoutAsset into screen rectangles and shows the resulting main panel, attached areas, slots, and components. Use 'Apply Layout Asset To Runtime Fields' on the Dialogue_Engine inspector to bridge supported fields into the current runtime UI.",
                MessageType.None);
        }

        EditorGUILayout.EndScrollView();
    }

    void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        engine = (Dialogue_Engine)EditorGUILayout.ObjectField(engine, typeof(Dialogue_Engine), true, GUILayout.Width(220));
        if (engine != null && layoutAsset == null)
            layoutAsset = engine.visualLayoutAsset;
        layoutAsset = (DialogueLayoutAsset)EditorGUILayout.ObjectField(layoutAsset, typeof(DialogueLayoutAsset), false, GUILayout.Width(220));

        GUILayout.FlexibleSpace();
        showLabels = GUILayout.Toggle(showLabels, "Labels", EditorStyles.toolbarButton);
        showSlots = GUILayout.Toggle(showSlots, "Slots", EditorStyles.toolbarButton);
        showComponents = GUILayout.Toggle(showComponents, "Components", EditorStyles.toolbarButton);

        GUI.enabled = engine != null && layoutAsset != null;
        if (GUILayout.Button("Apply To Engine", EditorStyles.toolbarButton, GUILayout.Width(100)))
        {
            DialogueVisualLayoutBridge.ApplyToEngine(engine, layoutAsset);
            EditorUtility.SetDirty(engine);
        }
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();
    }

    void DrawResolved(ResolvedDialogueLayout layout)
    {
        if (layout == null) return;

        EditorGUI.DrawRect(layout.MainPanelRect,
            new Color(0.18f, 0.22f, 0.30f, 0.90f));
        Handles.color = new Color(0.70f, 0.82f, 1f, 1f);
        Handles.DrawAAPolyLine(2f,
            new Vector3(layout.MainPanelRect.xMin, layout.MainPanelRect.yMin),
            new Vector3(layout.MainPanelRect.xMax, layout.MainPanelRect.yMin),
            new Vector3(layout.MainPanelRect.xMax, layout.MainPanelRect.yMax),
            new Vector3(layout.MainPanelRect.xMin, layout.MainPanelRect.yMax),
            new Vector3(layout.MainPanelRect.xMin, layout.MainPanelRect.yMin));

        if (showLabels)
            GUI.Label(new Rect(layout.MainPanelRect.x + 6, layout.MainPanelRect.y + 4, 220, 18),
                "Main Panel", EditorStyles.whiteBoldLabel);

        for (int i = 0; i < layout.Areas.Count; i++)
        {
            ResolvedDialogueArea area = layout.Areas[i];
            if (area.Name == "Main Panel / Inner Region")
                EditorGUI.DrawRect(area.Rect, new Color(0.22f, 0.26f, 0.30f, 0.35f));
            else
                EditorGUI.DrawRect(area.Rect, new Color(0.16f, 0.36f, 0.24f, 0.45f));

            if (showLabels)
                GUI.Label(new Rect(area.Rect.x + 4, area.Rect.y + 2, 240, 18),
                    area.Name, EditorStyles.miniBoldLabel);
        }

        if (showSlots)
        {
            for (int i = 0; i < layout.Slots.Count; i++)
            {
                ResolvedDialogueSlot slot = layout.Slots[i];
                DialogueSlotDefinition slotDef = DialogueVisualEditorUtility.GetSlot(layoutAsset, slot.AreaKind, slot.SlotIndex);
                if (slotDef != null)
                {
                    Color fill = slotDef.Background != null ? slotDef.Background.ColorA : new Color(0f, 0f, 0f, 0f);
                    float opacity = slotDef.Background != null ? slotDef.Background.Opacity : 0f;
                    fill.a *= opacity * 0.4f;
                    if (fill.a > 0f) EditorGUI.DrawRect(slot.Rect, fill);
                }
                Handles.color = new Color(1f, 0.84f, 0.40f, 1f);
                Handles.DrawAAPolyLine(1.5f,
                    new Vector3(slot.Rect.xMin, slot.Rect.yMin),
                    new Vector3(slot.Rect.xMax, slot.Rect.yMin),
                    new Vector3(slot.Rect.xMax, slot.Rect.yMax),
                    new Vector3(slot.Rect.xMin, slot.Rect.yMax),
                    new Vector3(slot.Rect.xMin, slot.Rect.yMin));
                if (showLabels)
                    GUI.Label(new Rect(slot.Rect.x + 4, slot.Rect.y + 2, 120, 18),
                        slot.SlotId, EditorStyles.miniLabel);
            }
        }

        if (showComponents)
        {
            for (int i = 0; i < layout.Components.Count; i++)
            {
                ResolvedDialogueComponentRect component = layout.Components[i];
                Color fill = component.ComponentType == DialogueComponentType.ImagePanel
                    ? new Color(0.75f, 0.46f, 0.16f, 0.22f)
                    : component.ComponentType == DialogueComponentType.NamePanel
                        ? new Color(0.24f, 0.56f, 0.92f, 0.22f)
                        : new Color(0.92f, 0.92f, 0.92f, 0.12f);
                EditorGUI.DrawRect(component.Rect, fill);

                Handles.color = component.ClipToSlot
                    ? new Color(1f, 1f, 1f, 0.85f)
                    : new Color(1f, 0.4f, 0.4f, 0.95f);
                Handles.DrawAAPolyLine(1.5f,
                    new Vector3(component.Rect.xMin, component.Rect.yMin),
                    new Vector3(component.Rect.xMax, component.Rect.yMin),
                    new Vector3(component.Rect.xMax, component.Rect.yMax),
                    new Vector3(component.Rect.xMin, component.Rect.yMax),
                    new Vector3(component.Rect.xMin, component.Rect.yMin));

                if (!component.ClipToSlot)
                    DrawDashedRect(component.Rect, 6f);

                if (showLabels)
                    GUI.Label(new Rect(component.Rect.x + 3, component.Rect.y + 2, 180, 18),
                        component.DisplayName + "  z:" + component.ZLayer,
                        EditorStyles.whiteMiniLabel);
            }
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

    void HandleDragAndDrop(Rect rect)
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
            Repaint();
        }
        evt.Use();
    }
}
#endif
