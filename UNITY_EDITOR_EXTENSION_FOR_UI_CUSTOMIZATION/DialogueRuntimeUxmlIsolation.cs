#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Play-mode UI isolation for the dialogue system.
///
/// While playing, the engine instantiates a disposable copy of the current
/// UXML (Dialogue_Engine.RUNTIME_UXML_PATH) instead of the source layout, so
/// the generated file / presets are never modified at runtime.
///
/// When play mode ends this hook:
///   • deletes the runtime UXML copy (and its .meta), and
///   • clears every dialogue-UI-carried state (current speaker, section,
///     traversal stack, typewriter, history, suspended dialogues) on all
///     engines — which also covers setups with domain reload disabled.
/// </summary>
[InitializeOnLoad]
public static class DialogueRuntimeUxmlIsolation
{
    static DialogueRuntimeUxmlIsolation()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        // ExitingPlayMode fires while the play-mode objects still exist, so the
        // engines can still be found and their state cleared.
        if (state != PlayModeStateChange.ExitingPlayMode) return;

        DiscardRuntimeUxmlCopy();
        ClearDialogueUiStates();
    }

    static void DiscardRuntimeUxmlCopy()
    {
        string path = Dialogue_Engine.RUNTIME_UXML_PATH;
        if (!File.Exists(path)) return;

        // AssetDatabase.DeleteAsset removes the asset and its .meta in one go.
        if (!AssetDatabase.DeleteAsset(path))
        {
            File.Delete(path);
            string meta = path + ".meta";
            if (File.Exists(meta)) File.Delete(meta);
        }
        AssetDatabase.Refresh();
        Debug.Log("DialogueRuntimeUxmlIsolation: Runtime UXML copy discarded — the source layout was never touched during play.");
    }

    static void ClearDialogueUiStates()
    {
        Dialogue_Engine[] engines = Object.FindObjectsByType<Dialogue_Engine>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < engines.Length; i++)
        {
            if (engines[i] != null)
                engines[i].ClearDialogueUiRuntimeState();
        }
    }
}
#endif
