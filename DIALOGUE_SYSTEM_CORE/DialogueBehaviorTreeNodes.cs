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
        }

        List<DialogueEventRecord> rows = engine.RuntimeDatabase.QueryEvents(
            DslPath.Replace('\\', '/'), null, checkpoint);
        foreach (DialogueEventRecord row in rows)
        {
            if (row.Status == DialogueRuntimeStatus.Completed)
                return DialogueBTStatus.Success;
            if (row.Status == DialogueRuntimeStatus.Interrupted &&
                row.Detail != null && row.Detail.IndexOf("discarded",
                    StringComparison.OrdinalIgnoreCase) >= 0)
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

/// <summary>
/// Blocking BT action in BT terms: returns Running every tick until the event is
/// found, then Success. It never blocks Unity's main thread.
/// </summary>
[Serializable]
public sealed class DialogueWaitForEventActionNode : DialogueBTActionNode
{
    public string DslPath;
    public string EventName;
    public long SinceSequence;
    [Tooltip("Zero or less means no timeout.")]
    public float TimeoutSeconds = 10f;
    [NonSerialized] public DialogueEventRecord Match;

    bool waiting;
    float startedAt;

    public override DialogueBTStatus Tick()
    {
        if (!waiting)
        {
            waiting = true;
            startedAt = Time.realtimeSinceStartup;
        }

        DialogueResponse response = Dialogue_Engine.SendRequest(new DialogueRequest
        {
            Type = DialogueRequestType.HasEvent,
            DialoguePath = DslPath,
            EventName = EventName,
            SinceSequence = SinceSequence
        });
        if (response != null && response.Matched)
        {
            Match = response.Events != null && response.Events.Count > 0
                ? response.Events[0] : null;
            return DialogueBTStatus.Success;
        }

        if (TimeoutSeconds > 0f &&
            Time.realtimeSinceStartup - startedAt >= TimeoutSeconds)
            return DialogueBTStatus.Failure;

        return DialogueBTStatus.Running;
    }

    public override void ResetNode()
    {
        waiting = false;
        startedAt = 0f;
        Match = null;
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
