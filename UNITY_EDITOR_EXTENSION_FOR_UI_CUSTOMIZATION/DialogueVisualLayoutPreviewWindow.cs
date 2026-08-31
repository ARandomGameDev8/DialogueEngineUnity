#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// TRUE runtime preview. This window does not imitate the layout — it clones
/// the EXACT UXML file the visual editor builds and the engine instantiates at
/// Play (<see cref="DialogueVisualEditorUxml"/>). What you see here is
/// byte-for-byte the runtime tree, rendered by UI Toolkit at the Panel
/// Settings reference resolution, scaled to fit the window.
/// </summary>
public sealed class DialogueVisualLayoutPreviewWindow : EditorWindow
{
    Dialogue_Engine engine;
    DialogueLayoutAsset layoutAsset;

    VisualElement previewViewport;
    VisualElement previewStage;
    DateTime lastBuildWrite;

    [MenuItem("Tools/Dialogue/Open Visual Layout Preview")]
    static void OpenFromMenu()
    {
        Open(Object.FindFirstObjectByType<Dialogue_Engine>());
    }

    public static void Open(Dialogue_Engine targetEngine)
    {
        var window = GetWindow<DialogueVisualLayoutPreviewWindow>("True Runtime Preview");
        window.engine = targetEngine;
        window.layoutAsset = targetEngine != null ? targetEngine.visualLayoutAsset : null;
        window.minSize = new Vector2(560, 420);
        window.Show();
    }

    void OnEnable()
    {
        BuildInterface();
        EditorApplication.delayCall += RefreshPreview;
    }

    void OnFocus()
    {
        RefreshPreview();
    }

    void BuildInterface()
    {
        var root = rootVisualElement;
        root.Clear();
        root.style.backgroundColor = new Color(0.10f, 0.10f, 0.11f, 1f);

        var toolbar = new VisualElement();
        toolbar.style.flexDirection = FlexDirection.Row;
        toolbar.style.paddingTop = 4;
        toolbar.style.paddingBottom = 4;
        toolbar.style.paddingLeft = 6;
        toolbar.style.paddingRight = 6;

        var engineField = new ObjectField("Engine")
        {
            objectType = typeof(Dialogue_Engine),
            value = engine,
            allowSceneObjects = true
        };
        engineField.RegisterValueChangedCallback(evt =>
        {
            engine = (Dialogue_Engine)evt.newValue;
            if (engine != null && layoutAsset == null)
                layoutAsset = engine.visualLayoutAsset;
            RefreshPreview();
        });
        engineField.style.flexGrow = 1f;
        toolbar.Add(engineField);

        var assetField = new ObjectField("Layout Asset")
        {
            objectType = typeof(DialogueLayoutAsset),
            value = layoutAsset,
            allowSceneObjects = false
        };
        assetField.RegisterValueChangedCallback(evt =>
        {
            layoutAsset = (DialogueLayoutAsset)evt.newValue;
            RefreshPreview();
        });
        assetField.style.flexGrow = 1f;
        toolbar.Add(assetField);

        var refreshButton = new Button(RefreshPreview) { text = "Refresh" };
        toolbar.Add(refreshButton);

        root.Add(toolbar);

        previewViewport = new VisualElement
        {
            name = "PreviewViewport",
            style =
            {
                flexGrow = 1f,
                justifyContent = Justify.Center,
                alignItems = Align.Center,
                overflow = Overflow.Hidden
            }
        };
        previewStage = new VisualElement { name = "PreviewStage" };
        previewStage.style.backgroundColor = new Color(0.07f, 0.07f, 0.08f, 1f);
        previewViewport.Add(previewStage);
        root.Add(previewViewport);

        var help = new Label(
            "TRUE PREVIEW — this is the exact UXML the visual editor builds and the engine instantiates at Play. " +
            "It refreshes automatically; keep the Panel Settings reference resolution in mind when comparing sizes.");
        help.style.whiteSpace = WhiteSpace.Normal;
        help.style.paddingTop = 4;
        help.style.paddingBottom = 4;
        help.style.paddingLeft = 6;
        help.style.paddingRight = 6;
        help.style.unityFontStyleAndWeight = FontStyle.Italic;
        help.style.color = new Color(0.7f, 0.7f, 0.72f, 1f);
        root.Add(help);

        // Keep the stage scaled/centered as the window resizes.
        previewViewport.RegisterCallback<GeometryChangedEvent>(_ => ApplyStageScale());
        // Poll the canonical file so edits made in the visual editor show up here.
        schedule.Execute(CheckForRebuild).Every(800);
    }

    void CheckForRebuild()
    {
        if (layoutAsset == null) return;
        string path = DialogueVisualEditorUxml.BuildPathFor(layoutAsset);
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
        DateTime written = File.GetLastWriteTimeUtc(path);
        if (written != lastBuildWrite)
            RefreshPreview();
    }

    void RefreshPreview()
    {
        if (previewStage == null) return;
        previewStage.Clear();
        previewStage.style.width = 0;
        previewStage.style.height = 0;

        if (layoutAsset == null)
        {
            previewStage.Add(MakeMessage(
                "Assign a DialogueLayoutAsset (drop one on the field above, or set it on a Dialogue_Engine)."));
            return;
        }

        if (engine == null)
            engine = Object.FindFirstObjectByType<Dialogue_Engine>();

        Vector2 reference = engine != null && engine.panelSettings != null
            ? new Vector2(engine.panelSettings.referenceResolution.x, engine.panelSettings.referenceResolution.y)
            : new Vector2(1920f, 1080f);

        string path;
        try
        {
            path = DialogueVisualEditorUxml.EnsureBuilt(layoutAsset, engine, reference);
        }
        catch (System.Exception ex)
        {
            previewStage.Add(MakeMessage("Failed to build the runtime UXML: " + ex.Message));
            return;
        }

        lastBuildWrite = File.GetLastWriteTimeUtc(path);

        var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
        if (tree == null)
        {
            previewStage.Add(MakeMessage("The built UXML could not be imported: " + path));
            return;
        }

        previewStage.style.width = reference.x;
        previewStage.style.height = reference.y;
        tree.Clone(previewStage);
        ApplyStageScale();
    }

    void ApplyStageScale()
    {
        if (previewStage == null || previewViewport == null) return;
        float w = previewViewport.resolvedStyle.width;
        float h = previewViewport.resolvedStyle.height;
        if (float.IsNaN(w) || float.IsNaN(h) || w <= 8f || h <= 8f)
        {
            previewViewport.schedule.Execute(ApplyStageScale).StartingIn(50);
            return;
        }
        float stageW = previewStage.resolvedStyle.width;
        float stageH = previewStage.resolvedStyle.height;
        if (float.IsNaN(stageW) || stageW <= 1f || float.IsNaN(stageH) || stageH <= 1f)
        {
            previewViewport.schedule.Execute(ApplyStageScale).StartingIn(50);
            return;
        }
        float scale = Mathf.Min(w / stageW, h / stageH);
        previewStage.style.scale = new Scale(new Vector3(scale, scale, 1f));
    }

    static Label MakeMessage(string message)
    {
        var label = new Label(message)
        {
            style =
            {
                whiteSpace = WhiteSpace.Normal,
                color = new Color(0.85f, 0.85f, 0.88f, 1f),
                paddingLeft = 10, paddingRight = 10, paddingTop = 10, paddingBottom = 10
            }
        };
        return label;
    }
}
#endif
