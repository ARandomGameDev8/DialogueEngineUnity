#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Dialogue_Engine))]
public sealed class DialogueEngineEditor : Editor
{
    public override void OnInspectorGUI()
    {
        Dialogue_Engine engine = (Dialogue_Engine)target;
        serializedObject.Update();

        EditorGUILayout.HelpBox(
            "Dialogue_Engine runtime UI is now driven primarily through the Dialogue Visual Editor workflow. Assign a DialogueLayoutAsset, open the editor, and author the layout there instead of editing the legacy field wall directly in this inspector.",
            MessageType.Info);

        engine.panelSettings = (UnityEngine.UIElements.PanelSettings)EditorGUILayout.ObjectField(
            "Panel Settings", engine.panelSettings,
            typeof(UnityEngine.UIElements.PanelSettings), false);

        EditorGUILayout.Space(6f);
        GUILayout.Label("Dialogue Visual Editor", EditorStyles.boldLabel);
        engine.useVisualLayoutAsset = EditorGUILayout.Toggle("Use Layout Asset", engine.useVisualLayoutAsset);
        engine.visualLayoutAsset = (DialogueLayoutAsset)EditorGUILayout.ObjectField(
            "Layout Asset", engine.visualLayoutAsset, typeof(DialogueLayoutAsset), false);

        EditorGUILayout.BeginHorizontal();
        GUI.enabled = engine.visualLayoutAsset != null;
        if (GUILayout.Button("Apply Layout Asset To Runtime Fields", GUILayout.Height(24f)))
        {
            DialogueVisualLayoutBridge.ApplyToEngine(engine, engine.visualLayoutAsset);
            EditorUtility.SetDirty(engine);
        }
        GUI.enabled = true;
        if (GUILayout.Button("Open Dialogue Visual Editor", GUILayout.Height(24f)))
            DialogueVisualEditorWindow.Open(engine);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUI.enabled = engine.visualLayoutAsset != null;
        if (GUILayout.Button("Open Visual Layout Preview", GUILayout.Height(22f)))
            DialogueVisualLayoutPreviewWindow.Open(engine);
        GUI.enabled = true;
        if (GUILayout.Button("Open Runtime Preview", GUILayout.Height(22f)))
            DialoguePreviewWindow.Open(engine);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6f);
        GUILayout.Label("Runtime Build / Preset", EditorStyles.boldLabel);
        engine.presetName = EditorGUILayout.TextField("Preset Name", engine.presetName);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Build Runtime Layout (UXML)", GUILayout.Height(24f)))
        {
            DialogueLayoutBuilder.Build(engine);
            EditorUtility.SetDirty(engine);
        }
        if (GUILayout.Button("Open Dialogue Visual Editor", GUILayout.Height(24f)))
            DialogueVisualEditorWindow.Open(engine);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4f);
        EditorGUILayout.HelpBox(
            "Recommended workflow:\n" +
            "1. Assign Panel Settings.\n" +
            "2. Create a DialogueLayoutAsset.\n" +
            "3. Enable 'Use Layout Asset'.\n" +
            "4. Open Dialogue Visual Editor.\n" +
            "5. Build runtime UXML when you want the current bridge applied to the Dialogue_Engine runtime layout.",
            MessageType.None);

        serializedObject.ApplyModifiedProperties();
        if (GUI.changed)
            EditorUtility.SetDirty(engine);
    }
}
#endif
