using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Framework-neutral BT result. Adapters can map Running/Success/Failure to the
/// equivalent status in Unity Behavior, Behavior Designer, NodeCanvas, etc.
/// </summary>
public enum DialogueBTStatus { Running, Success, Failure }

[Serializable]
public abstract class DialogueBTActionNode
{
    public abstract DialogueBTStatus Tick();
    public virtual void ResetNode() { }
}

static class DialogueBTUtility
{
    public static string NormalizePath(string path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? ""
            : path.Replace('\\', '/').Trim();
    }

    public static HashSet<string> ParseEventNames(string raw)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(raw)) return set;

        string[] parts = raw.Split(new[] { ',', ';', '|', '\n', '\r' },
            StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            string value = parts[i].Trim();
            if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
                value = value.Substring(1, value.Length - 2).Trim();
            if (!string.IsNullOrEmpty(value)) set.Add(value);
        }
        return set;
    }

    public static List<DialogueEventRecord> GetAllRows(Dialogue_Engine engine,
        string normalizedPath)
    {
        return engine != null && engine.RuntimeDatabase != null
            ? engine.RuntimeDatabase.QueryEvents(normalizedPath, null, 0)
            : null;
    }

    public static long FindLatestPlayStartSequence(IList<DialogueEventRecord> rows)
    {
        long latestStart = 0;
        if (rows == null) return latestStart;

        for (int i = 0; i < rows.Count; i++)
        {
            DialogueEventRecord row = rows[i];
            if (row == null) continue;
            if (row.Status == DialogueRuntimeStatus.Transitioning &&
                string.Equals(row.Detail, "Dialogue started",
                    StringComparison.OrdinalIgnoreCase))
                latestStart = row.Sequence;
        }
        return latestStart;
    }

    public static List<DialogueEventRecord> GetLatestPlayRows(IList<DialogueEventRecord> rows)
    {
        var result = new List<DialogueEventRecord>();
        if (rows == null || rows.Count == 0) return result;

        long latestStart = FindLatestPlayStartSequence(rows);
        if (latestStart <= 0)
        {
            for (int i = 0; i < rows.Count; i++)
                if (rows[i] != null) result.Add(rows[i]);
            return result;
        }

        for (int i = 0; i < rows.Count; i++)
        {
            DialogueEventRecord row = rows[i];
            if (row == null || row.Sequence < latestStart) continue;
            result.Add(row);
        }
        return result;
    }

    public static DialogueEventRecord GetLastRow(IList<DialogueEventRecord> rows)
    {
        return rows != null && rows.Count > 0 ? rows[rows.Count - 1] : null;
    }

    public static string GetLastEmittedEvent(IList<DialogueEventRecord> rows)
    {
        if (rows == null) return "";
        for (int i = rows.Count - 1; i >= 0; i--)
        {
            DialogueEventRecord row = rows[i];
            if (row != null && !string.IsNullOrEmpty(row.EmittedEvent))
                return row.EmittedEvent;
        }
        return "";
    }

    public static bool IsDiscarded(DialogueEventRecord row)
    {
        return row != null && row.Status == DialogueRuntimeStatus.Interrupted &&
            row.Detail != null && row.Detail.IndexOf("discarded",
                StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static bool IsSuspended(DialogueEventRecord row)
    {
        return row != null && row.Status == DialogueRuntimeStatus.Interrupted &&
            row.Detail != null && row.Detail.IndexOf("resume",
                StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static bool IsTerminal(DialogueEventRecord row)
    {
        return row != null &&
            (row.Status == DialogueRuntimeStatus.Completed || IsDiscarded(row));
    }

    public static bool SnapshotMatches(DialogueLiveSnapshot snapshot,
        string normalizedPath)
    {
        return snapshot != null && snapshot.IsPlaying &&
            string.Equals(NormalizePath(snapshot.DialoguePath), normalizedPath,
                StringComparison.OrdinalIgnoreCase);
    }

    public static DialogueLiveSnapshot BuildStoredSnapshot(string normalizedPath,
        IList<DialogueEventRecord> latestRows, Dialogue_Engine engine)
    {
        DialogueEventRecord lastRow = GetLastRow(latestRows);
        long latestSequence = engine != null && engine.RuntimeDatabase != null
            ? engine.RuntimeDatabase.LatestSequence : 0;

        return new DialogueLiveSnapshot
        {
            IsPlaying = lastRow != null && !IsTerminal(lastRow),
            DialogueId = normalizedPath,
            DialoguePath = normalizedPath,
            SectionId = "",
            TextName = lastRow != null ? lastRow.TextName : "",
            Text = lastRow != null ? lastRow.Text : "",
            LastEvent = GetLastEmittedEvent(latestRows),
            Status = lastRow != null ? lastRow.Status : DialogueRuntimeStatus.Idle,
            Detail = lastRow != null ? lastRow.Detail : "",
            LatestSequence = latestSequence
        };
    }
}

/// <summary>Plays one DSL and remains Running until that play completes.</summary>
[Serializable]
public sealed class DialoguePlayActionNode : DialogueBTActionNode
{
    [Tooltip("Path passed to Dialogue_Engine.Play(path).")]
    public string DslPath;
    [Tooltip("Allow a later Play node/call to interrupt this dialogue.")]
    public bool Interruptible;
    [Tooltip("Only used when Interruptible is enabled. Push interrupted playback onto the resume stack.")]
    public bool SaveState;

    bool started;
    long checkpoint;

    public override DialogueBTStatus Tick()
    {
        Dialogue_Engine engine = Dialogue_Engine.Instance;
        if (engine == null || string.IsNullOrWhiteSpace(DslPath))
            return DialogueBTStatus.Failure;

        if (!started)
        {
            checkpoint = engine.RuntimeDatabase != null
                ? engine.RuntimeDatabase.LatestSequence : 0;
            if (!Dialogue_Engine.Play(DslPath, Interruptible,
                    Interruptible && SaveState))
                return DialogueBTStatus.Failure;
            started = true;

            // In an interruptible flow, starting the DSL is the action's job.
            // Return Success immediately so the BT can advance to branches that
            // may monitor events or start another DSL. The engine continues the
            // dialogue independently and enforces SaveState interruption rules.
            if (Interruptible)
                return DialogueBTStatus.Success;
        }

        List<DialogueEventRecord> rows = engine.RuntimeDatabase.QueryEvents(
            DslPath.Replace('\\', '/'), null, checkpoint);
        foreach (DialogueEventRecord row in rows)
        {
            if (row.Status == DialogueRuntimeStatus.Completed)
                return DialogueBTStatus.Success;
            if (DialogueBTUtility.IsDiscarded(row))
                return DialogueBTStatus.Failure;
        }
        return DialogueBTStatus.Running;
    }

    public override void ResetNode()
    {
        started = false;
        checkpoint = 0;
    }
}

/// <summary>Immediate condition: did one DSL emit one event?</summary>
[Serializable]
public sealed class DialogueHasEventActionNode : DialogueBTActionNode
{
    public string DslPath;
    public string EventName;
    [Tooltip("Only match rows after this sequence. Zero searches the whole Play session.")]
    public long SinceSequence;
    [NonSerialized] public bool Result;
    [NonSerialized] public List<DialogueEventRecord> Matches;

    public override DialogueBTStatus Tick()
    {
        DialogueResponse response = Dialogue_Engine.SendRequest(new DialogueRequest
        {
            Type = DialogueRequestType.HasEvent,
            DialoguePath = DslPath,
            EventName = EventName,
            SinceSequence = SinceSequence
        });
        Result = response != null && response.Matched;
        Matches = response != null ? response.Events : null;
        return Result ? DialogueBTStatus.Success : DialogueBTStatus.Failure;
    }
}

/// <summary>
/// Listener action for one DSL and many target events. It returns Running while
/// that DSL is still alive and none of the targets have been emitted. It returns
/// Success with MatchedEvent set to the FIRST emitted target event from the
/// latest play of that DSL. If the DSL ends without emitting any target event,
/// it returns Success with MatchedEvent empty. Invalid input/service state
/// returns Failure.
/// </summary>
[Serializable]
public sealed class DialogueListenForMultipleEventsActionNode : DialogueBTActionNode
{
    [Tooltip("DSL path to inspect.")]
    public string DslPath;
    [Tooltip("Target event names separated by comma, semicolon, pipe, or newline.")]
    public string TargetEvents;
    [NonSerialized] public string MatchedEvent;
    [NonSerialized] public long MatchedSequence;

    public override DialogueBTStatus Tick()
    {
        MatchedEvent = "";
        MatchedSequence = 0;

        Dialogue_Engine engine = Dialogue_Engine.Instance;
        if (engine == null || engine.RuntimeDatabase == null ||
            string.IsNullOrWhiteSpace(DslPath))
            return DialogueBTStatus.Failure;

        HashSet<string> targets = DialogueBTUtility.ParseEventNames(TargetEvents);
        if (targets.Count == 0) return DialogueBTStatus.Failure;

        string normalizedPath = DialogueBTUtility.NormalizePath(DslPath);
        List<DialogueEventRecord> allRows =
            DialogueBTUtility.GetAllRows(engine, normalizedPath);
        if (allRows == null || allRows.Count == 0)
            return DialogueBTStatus.Failure;

        List<DialogueEventRecord> latestRows =
            DialogueBTUtility.GetLatestPlayRows(allRows);
        for (int i = 0; i < latestRows.Count; i++)
        {
            DialogueEventRecord row = latestRows[i];
            if (row == null || string.IsNullOrEmpty(row.EmittedEvent)) continue;
            if (!targets.Contains(row.EmittedEvent)) continue;
            MatchedEvent = row.EmittedEvent;
            MatchedSequence = row.Sequence;
            return DialogueBTStatus.Success;
        }

        DialogueEventRecord lastRow = DialogueBTUtility.GetLastRow(latestRows);
        if (DialogueBTUtility.IsTerminal(lastRow))
            return DialogueBTStatus.Success;

        DialogueResponse snapshotResponse = Dialogue_Engine.SendRequest(
            DialogueRequest.Snapshot());
        DialogueLiveSnapshot snapshot = snapshotResponse != null
            ? snapshotResponse.Snapshot : null;
        if (DialogueBTUtility.SnapshotMatches(snapshot, normalizedPath) ||
            DialogueBTUtility.IsSuspended(lastRow) ||
            lastRow != null)
            return DialogueBTStatus.Running;

        return DialogueBTStatus.Failure;
    }
}

/// <summary>Immediate action that copies the engine's current live snapshot.</summary>
[Serializable]
public sealed class DialogueLiveSnapshotActionNode : DialogueBTActionNode
{
    [NonSerialized] public DialogueLiveSnapshot Snapshot;
    [NonSerialized] public string Message;

    public override DialogueBTStatus Tick()
    {
        DialogueResponse response = Dialogue_Engine.SendRequest(
            DialogueRequest.Snapshot());
        Snapshot = response != null ? response.Snapshot : null;
        Message = response != null ? response.Message : "";
        return response != null && response.IsSuccess
            ? DialogueBTStatus.Success : DialogueBTStatus.Failure;
    }
}

/// <summary>
/// Blocking snapshot watcher for one DSL. While that DSL is alive, it returns
/// Running and updates Snapshot to the most current available state. When the
/// DSL reaches end-of-life without service/input errors, it returns Success.
/// Invalid input/service state returns Failure.
/// </summary>
[Serializable]
public sealed class DialogueBlockingLiveSnapshotActionNode : DialogueBTActionNode
{
    [Tooltip("DSL path to monitor until it reaches end-of-life.")]
    public string DslPath;
    [NonSerialized] public DialogueLiveSnapshot Snapshot;
    [NonSerialized] public string Message;

    public override DialogueBTStatus Tick()
    {
        Snapshot = null;
        Message = "";

        Dialogue_Engine engine = Dialogue_Engine.Instance;
        if (engine == null || engine.RuntimeDatabase == null ||
            string.IsNullOrWhiteSpace(DslPath))
            return DialogueBTStatus.Failure;

        string normalizedPath = DialogueBTUtility.NormalizePath(DslPath);
        List<DialogueEventRecord> allRows =
            DialogueBTUtility.GetAllRows(engine, normalizedPath);
        if (allRows == null || allRows.Count == 0)
            return DialogueBTStatus.Failure;

        List<DialogueEventRecord> latestRows =
            DialogueBTUtility.GetLatestPlayRows(allRows);
        DialogueEventRecord lastRow = DialogueBTUtility.GetLastRow(latestRows);
        if (lastRow == null) return DialogueBTStatus.Failure;

        DialogueResponse response = Dialogue_Engine.SendRequest(
            DialogueRequest.Snapshot());
        DialogueLiveSnapshot live = response != null ? response.Snapshot : null;

        if (DialogueBTUtility.SnapshotMatches(live, normalizedPath))
        {
            Snapshot = live;
            Message = response != null ? response.Message : "";
            return DialogueBTStatus.Running;
        }

        Snapshot = DialogueBTUtility.BuildStoredSnapshot(normalizedPath,
            latestRows, engine);
        Message = Snapshot != null ? Snapshot.ToMessage() : "";
        return DialogueBTUtility.IsTerminal(lastRow)
            ? DialogueBTStatus.Success
            : DialogueBTStatus.Running;
    }
}

/// <summary>Immediate action returning all matching status/event rows.</summary>
[Serializable]
public sealed class DialogueGetEventsActionNode : DialogueBTActionNode
{
    public string DslPath;
    public string EventName;
    public long SinceSequence;
    [NonSerialized] public List<DialogueEventRecord> Events;

    public override DialogueBTStatus Tick()
    {
        DialogueResponse response = Dialogue_Engine.SendRequest(new DialogueRequest
        {
            Type = DialogueRequestType.GetEvents,
            DialoguePath = DslPath,
            EventName = EventName,
            SinceSequence = SinceSequence
        });
        Events = response != null ? response.Events : null;
        return response != null && response.IsSuccess
            ? DialogueBTStatus.Success : DialogueBTStatus.Failure;
    }
}

/// <summary>Immediate action returning the unique DSL table row.</summary>
[Serializable]
public sealed class DialogueGetDslActionNode : DialogueBTActionNode
{
    public string DslPath;
    [NonSerialized] public DialogueScriptRecord Dialogue;

    public override DialogueBTStatus Tick()
    {
        DialogueResponse response = Dialogue_Engine.SendRequest(new DialogueRequest
        {
            Type = DialogueRequestType.GetDialogue,
            DialoguePath = DslPath
        });
        Dialogue = response != null ? response.Dialogue : null;
        return Dialogue != null ? DialogueBTStatus.Success : DialogueBTStatus.Failure;
    }
}

/// <summary>Escape hatch exposing every request type as one BT action.</summary>
[Serializable]
public sealed class DialogueQueryActionNode : DialogueBTActionNode
{
    public DialogueRequest Request = new DialogueRequest();
    [NonSerialized] public DialogueResponse Response;

    public override DialogueBTStatus Tick()
    {
        Response = Dialogue_Engine.SendRequest(Request);
        if (Response == null) return DialogueBTStatus.Failure;
        if (Response.Code == DialogueResponseCode.Pending)
            return DialogueBTStatus.Running;
        return Response.IsSuccess ? DialogueBTStatus.Success : DialogueBTStatus.Failure;
    }
}
