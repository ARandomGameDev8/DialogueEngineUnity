using UnityEngine;
using UnityEngine.UIElements;
using System.IO;

#if UNITY_EDITOR
using UnityEditor;

public static class DialogueLayoutBuilder
{
    // ─── Menu ──────────────────────────────────────────────────────────────────
    [MenuItem("Tools/Dialogue/Build Layout")]
    public static void BuildLayoutFromEditor()
    {
        var engine = Object.FindFirstObjectByType<Dialogue_Engine>();
        if (engine == null)
        {
            Debug.LogError("DialogueLayoutBuilder: No Dialogue_Engine instance found in active scene.");
            return;
        }
        Build(engine);
    }

    [MenuItem("Tools/Dialogue/Save As Preset…")]
    public static void SaveAsPresetFromEditor()
    {
        var engine = FindEngine();
        if (engine == null) return;

        string dir = Dialogue_Engine.PRESETS_PATH;
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        string picked = EditorUtility.SaveFilePanel("Save Dialogue Preset", dir, "dialogue_preset.uxml", "uxml");
        if (string.IsNullOrEmpty(picked)) return;

        SaveAsPreset(engine, picked);
    }

    [MenuItem("Tools/Dialogue/Load Preset…")]
    public static void LoadPresetFromEditor()
    {
        var engine = FindEngine();
        if (engine == null) return;

        string dir = Dialogue_Engine.PRESETS_PATH;
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        string picked = EditorUtility.OpenFilePanel("Load Dialogue Preset", dir, "uxml");
        if (string.IsNullOrEmpty(picked)) return;

        LoadPreset(engine, picked);
    }

    static Dialogue_Engine FindEngine()
    {
        var engine = Object.FindFirstObjectByType<Dialogue_Engine>();
        if (engine == null)
            Debug.LogError("DialogueLayoutBuilder: No Dialogue_Engine instance found in active scene.");
        return engine;
    }

    // ─── Build (generated layout) ─────────────────────────────────────────────
    public static void Build(Dialogue_Engine engine)
    {
        Build(engine, Dialogue_Engine.UXML_PATH);
    }

    public static void Build(Dialogue_Engine engine, string assetPath)
    {
        if (engine != null && engine.useVisualLayoutAsset && engine.visualLayoutAsset != null)
            DialogueVisualLayoutBridge.ApplyToEngine(engine, engine.visualLayoutAsset);

        string dir = Path.GetDirectoryName(assetPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

        File.WriteAllText(assetPath, BuildUxml(engine));
        AssetDatabase.Refresh();

        Debug.Log($"DialogueLayoutBuilder: Successfully generated layout to {assetPath}");
    }

    // ─── Presets ───────────────────────────────────────────────────────────────
    public static string NormalizePresetFileName(string name)
    {
        string n = string.IsNullOrEmpty(name) ? "dialogue_preset" : Path.GetFileNameWithoutExtension(name).Trim();
        if (n.Length == 0) n = "dialogue_preset";
        if (!n.EndsWith(".uxml", System.StringComparison.OrdinalIgnoreCase)) n += ".uxml";
        return n;
    }

    /// <summary>Writes the preset UXML + a JSON sidecar (sprites/fonts/animations).</summary>
    public static void SaveAsPreset(Dialogue_Engine engine, string filePathOrName)
    {
        string dir = Dialogue_Engine.PRESETS_PATH;
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        string fileName = NormalizePresetFileName(filePathOrName);
        string uxmlPath = Path.Combine(dir, fileName);
        string jsonPath = Path.Combine(dir, Path.GetFileNameWithoutExtension(fileName) + ".json");

        File.WriteAllText(uxmlPath, BuildUxml(engine));
        File.WriteAllText(jsonPath, JsonUtility.ToJson(engine.BuildPresetDTO(), true));
        AssetDatabase.Refresh();

        Debug.Log($"DialogueLayoutBuilder: Preset saved to {uxmlPath} (+ sidecar {Path.GetFileName(jsonPath)}).");
    }

    /// <summary>
    /// Loads a preset into the engine's inspector fields, so the inspector is
    /// the source of truth again (presetName is cleared). Set presetName
    /// manually if you want a preset file to be used live at play time instead.
    /// </summary>
    public static void LoadPreset(Dialogue_Engine engine, string filePathOrName)
    {
        string dir = Dialogue_Engine.PRESETS_PATH;
        string fileName = NormalizePresetFileName(filePathOrName);
        string uxmlPath = Path.Combine(dir, fileName);

        if (!File.Exists(uxmlPath))
        {
            Debug.LogError($"DialogueLayoutBuilder: Preset not found at {uxmlPath}");
            return;
        }

        string jsonPath = Path.Combine(dir, Path.GetFileNameWithoutExtension(fileName) + ".json");
        if (File.Exists(jsonPath))
        {
            var dto = JsonUtility.FromJson<DialoguePresetDTO>(File.ReadAllText(jsonPath));
            engine.ApplyPreset(dto);
        }

        engine.presetName = "";
        EditorUtility.SetDirty(engine);
        AssetDatabase.Refresh();

        Debug.Log($"DialogueLayoutBuilder: Preset \"{fileName}\" loaded into the inspector.");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // UXML GENERATION — delegated to the engine.
    //
    // Dialogue_Engine owns the generation code (editor-guarded, inside the
    // runtime assembly) so that it can rebuild its own layout in Awake()
    // without ever referencing this editor-only class. This file only handles
    // file IO, menu items and presets.
    // ══════════════════════════════════════════════════════════════════════════
    public static string BuildUxml(Dialogue_Engine e)
    {
        return Dialogue_Engine.GenerateUxml(e);
    }
}
#endif

