using System;
using System.Collections.Generic;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

// Native Unity Behavior wrappers. These nodes appear under Add > Action > Dialogue.
// They intentionally delegate to the framework-neutral nodes/service so code and
// visual graphs share exactly the same runtime semantics and database.

static class DialogueUnityBehaviorStatus
{
    public static Node.Status Map(DialogueBTStatus status)
    {
        switch (status)
        {
            case DialogueBTStatus.Success: return Node.Status.Success;
            case DialogueBTStatus.Failure: return Node.Status.Failure;
            default: return Node.Status.Running;
        }
    }
}

[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Play Dialogue DSL",
    story: "Play [DslPath] interruptible [Interruptible] save state [SaveState]",
    category: "Action/Dialogue",
    id: "2f1fd49010db46abb7cb3d6f06cf8251")]
public partial class UnityBehaviorPlayDialogueAction : Action
{
    [SerializeReference] public BlackboardVariable<string> DslPath = new BlackboardVariable<string>("");
    [SerializeReference] public BlackboardVariable<bool> Interruptible = new BlackboardVariable<bool>(false);
    [SerializeReference] public BlackboardVariable<bool> SaveState = new BlackboardVariable<bool>(false);

    [NonSerialized] DialoguePlayActionNode node;

    protected override Status OnStart()
    {
        bool canInterrupt = Interruptible != null && Interruptible.Value;
        node = new DialoguePlayActionNode
        {
            DslPath = DslPath != null ? DslPath.Value : "",
            Interruptible = canInterrupt,
            SaveState = canInterrupt && SaveState != null && SaveState.Value
        };
        DialogueBTStatus result = node.Tick();
        if (result == DialogueBTStatus.Failure) return Status.Failure;

        // An interruptible Play node owns only the launch operation. Once the
        // DSL compiled and Dialogue_Engine accepted it, the visual graph must
        // advance while dialogue playback continues independently.
        return canInterrupt ? Status.Success
            : DialogueUnityBehaviorStatus.Map(result);
    }

    protected override Status OnUpdate()
    {
        return node == null ? Status.Failure
            : DialogueUnityBehaviorStatus.Map(node.Tick());
    }

    protected override void OnEnd()
    {
        if (node != null) node.ResetNode();
        node = null;
    }
}

[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Has Dialogue Event",
    story: "Check if [DslPath] emitted [EventName] after [SinceSequence] into [Result] and [MatchCount]",
    category: "Action/Dialogue/Query",
    id: "48be7ca1c38649d38f3b7b66fe345879")]
public partial class UnityBehaviorHasDialogueEventAction : Action
{
    [SerializeReference] public BlackboardVariable<string> DslPath = new BlackboardVariable<string>("");
    [SerializeReference] public BlackboardVariable<string> EventName = new BlackboardVariable<string>("");
    [SerializeReference] public BlackboardVariable<int> SinceSequence = new BlackboardVariable<int>(0);
    [SerializeReference] public BlackboardVariable<bool> Result = new BlackboardVariable<bool>(false);
    [SerializeReference] public BlackboardVariable<int> MatchCount = new BlackboardVariable<int>(0);

    protected override Status OnStart()
    {
        return EvaluateEventOutcome();
    }

    protected override Status OnUpdate()
    {
        return EvaluateEventOutcome();
    }

    Status EvaluateEventOutcome()
    {
        string path = DslPath != null ? DslPath.Value : "";
        var node = new DialogueHasEventActionNode
        {
            DslPath = path,
            EventName = EventName != null ? EventName.Value : "",
            SinceSequence = SinceSequence != null ? SinceSequence.Value : 0
        };
        node.Tick();
        if (Result != null) Result.Value = node.Result;
        if (MatchCount != null) MatchCount.Value = node.Matches != null ? node.Matches.Count : 0;
        if (node.Result) return Status.Success;

        // In a visual graph this node doubles as a non-blocking listener: while
        // the requested DSL is still playing, keep returning Running and poll
        // its database rows on later graph updates. If the DSL finishes without
        // the event, finish successfully with Result=false so a following
        // Branch node can evaluate the output.
        DialogueResponse snapshotResponse = Dialogue_Engine.SendRequest(DialogueRequest.Snapshot());
        DialogueLiveSnapshot snapshot = snapshotResponse != null ? snapshotResponse.Snapshot : null;
        bool targetStillPlaying = snapshot != null && snapshot.IsPlaying &&
            string.Equals(NormalizePath(snapshot.DialoguePath), NormalizePath(path),
                StringComparison.OrdinalIgnoreCase);
        return targetStillPlaying ? Status.Running : Status.Success;
    }

    static string NormalizePath(string path)
    {
        return string.IsNullOrEmpty(path) ? "" : path.Replace('\\', '/');
    }
}

[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Listen For Dialogue Events",
    story: "Listen on [DslPath] for [TargetEvents] into [MatchedEvent] [MatchedSequence]",
    category: "Action/Dialogue",
    id: "f9ef20f7f405432fa7237b2551eb1736")]
public partial class UnityBehaviorListenForDialogueEventsAction : Action
{
    [SerializeReference] public BlackboardVariable<string> DslPath = new BlackboardVariable<string>("");
    [SerializeReference] public BlackboardVariable<string> TargetEvents = new BlackboardVariable<string>("");
    [SerializeReference] public BlackboardVariable<string> MatchedEvent = new BlackboardVariable<string>("");
    [SerializeReference] public BlackboardVariable<int> MatchedSequence = new BlackboardVariable<int>(0);

    protected override Status OnStart()
    {
        return Evaluate();
    }

    protected override Status OnUpdate()
    {
        return Evaluate();
    }

    Status Evaluate()
    {
        var node = new DialogueListenForMultipleEventsActionNode
        {
            DslPath = DslPath != null ? DslPath.Value : "",
            TargetEvents = TargetEvents != null ? TargetEvents.Value : ""
        };
        DialogueBTStatus status = node.Tick();
        if (MatchedEvent != null) MatchedEvent.Value = node.MatchedEvent ?? "";
        if (MatchedSequence != null)
            MatchedSequence.Value = (int)Math.Min(int.MaxValue, Math.Max(0L, node.MatchedSequence));
        return DialogueUnityBehaviorStatus.Map(status);
    }
}

[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Get Dialogue Live Snapshot",
    story: "Get dialogue snapshot into [DialoguePath] [Section] [TextName] [Text] [IOStatus] [LastEvent] [IsPlaying] [LatestSequence] [Message]",
    category: "Action/Dialogue/Query",
    id: "078744323f894f78b43ea0399d000e33")]
public partial class UnityBehaviorGetDialogueSnapshotAction : Action
{
    [SerializeReference] public BlackboardVariable<string> DialoguePath = new BlackboardVariable<string>("");
    [SerializeReference] public BlackboardVariable<string> Section = new BlackboardVariable<string>("");
    [SerializeReference] public BlackboardVariable<string> TextName = new BlackboardVariable<string>("");
    [SerializeReference] public BlackboardVariable<string> Text = new BlackboardVariable<string>("");
    [SerializeReference] public BlackboardVariable<string> IOStatus = new BlackboardVariable<string>("");
    [SerializeReference] public BlackboardVariable<string> LastEvent = new BlackboardVariable<string>("");
    [SerializeReference] public BlackboardVariable<bool> IsPlaying = new BlackboardVariable<bool>(false);
    [SerializeReference] public BlackboardVariable<int> LatestSequence = new BlackboardVariable<int>(0);
    [SerializeReference] public BlackboardVariable<string> Message = new BlackboardVariable<string>("");

    protected override Status OnStart()
    {
        var node = new DialogueLiveSnapshotActionNode();
        DialogueBTStatus result = node.Tick();
        ApplySnapshot(node.Snapshot, node.Message);
        return DialogueUnityBehaviorStatus.Map(result);
    }

    void ApplySnapshot(DialogueLiveSnapshot snapshot, string message)
    {
        if (snapshot != null)
        {
            if (DialoguePath != null) DialoguePath.Value = snapshot.DialoguePath;
            if (Section != null) Section.Value = snapshot.SectionId;
            if (TextName != null) TextName.Value = snapshot.TextName;
            if (Text != null) Text.Value = snapshot.Text;
            if (IOStatus != null) IOStatus.Value = snapshot.Status.ToString();
            if (LastEvent != null) LastEvent.Value = snapshot.LastEvent;
            if (IsPlaying != null) IsPlaying.Value = snapshot.IsPlaying;
            if (LatestSequence != null) LatestSequence.Value = (int)Math.Min(int.MaxValue, snapshot.LatestSequence);
        }
        if (Message != null) Message.Value = message ?? "";
    }
}

[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Get Dialogue Live Snapshot Blocking",
    story: "Watch [DslPath] snapshot into [DialoguePath] [Section] [TextName] [Text] [IOStatus] [LastEvent] [IsPlaying] [LatestSequence] [Message]",
    category: "Action/Dialogue",
    id: "9a0d65d5a0b145a7ae25fe308d4b9711")]
public partial class UnityBehaviorGetDialogueSnapshotBlockingAction : Action
{
    [SerializeReference] public BlackboardVariable<string> DslPath = new BlackboardVariable<string>("");
    [SerializeReference] public BlackboardVariable<string> DialoguePath = new BlackboardVariable<string>("");
    [SerializeReference] public BlackboardVariable<string> Section = new BlackboardVariable<string>("");
    [SerializeReference] public BlackboardVariable<string> TextName = new BlackboardVariable<string>("");
    [SerializeReference] public BlackboardVariable<string> Text = new BlackboardVariable<string>("");
    [SerializeReference] public BlackboardVariable<string> IOStatus = new BlackboardVariable<string>("");
    [SerializeReference] public BlackboardVariable<string> LastEvent = new BlackboardVariable<string>("");
    [SerializeReference] public BlackboardVariable<bool> IsPlaying = new BlackboardVariable<bool>(false);
    [SerializeReference] public BlackboardVariable<int> LatestSequence = new BlackboardVariable<int>(0);
    [SerializeReference] public BlackboardVariable<string> Message = new BlackboardVariable<string>("");

    protected override Status OnStart()
    {
        return Evaluate();
    }

    protected override Status OnUpdate()
    {
        return Evaluate();
    }

    Status Evaluate()
    {
        var node = new DialogueBlockingLiveSnapshotActionNode
        {
            DslPath = DslPath != null ? DslPath.Value : ""
        };
        DialogueBTStatus status = node.Tick();
        ApplySnapshot(node.Snapshot, node.Message);
        return DialogueUnityBehaviorStatus.Map(status);
    }

    void ApplySnapshot(DialogueLiveSnapshot snapshot, string message)
    {
        if (snapshot != null)
        {
            if (DialoguePath != null) DialoguePath.Value = snapshot.DialoguePath;
            if (Section != null) Section.Value = snapshot.SectionId;
            if (TextName != null) TextName.Value = snapshot.TextName;
            if (Text != null) Text.Value = snapshot.Text;
            if (IOStatus != null) IOStatus.Value = snapshot.Status.ToString();
            if (LastEvent != null) LastEvent.Value = snapshot.LastEvent;
            if (IsPlaying != null) IsPlaying.Value = snapshot.IsPlaying;
            if (LatestSequence != null) LatestSequence.Value = (int)Math.Min(int.MaxValue, snapshot.LatestSequence);
        }
        if (Message != null) Message.Value = message ?? "";
    }
}

[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Get Dialogue Events",
    story: "Get events for [DslPath] named [EventName] after [SinceSequence] into [EventCount] emitted / [HistoryRowCount] rows [ResponseMessage]",
    category: "Action/Dialogue/Query",
    id: "bd741fa37f7b4b7496024736456284c4")]
public partial class UnityBehaviorGetDialogueEventsAction : Action
{
    [SerializeReference] public BlackboardVariable<string> DslPath = new BlackboardVariable<string>("");
    [SerializeReference] public BlackboardVariable<string> EventName = new BlackboardVariable<string>("");
    [SerializeReference] public BlackboardVariable<int> SinceSequence = new BlackboardVariable<int>(0);
    [SerializeReference] public BlackboardVariable<int> EventCount = new BlackboardVariable<int>(0);
    [SerializeReference] public BlackboardVariable<int> HistoryRowCount = new BlackboardVariable<int>(0);
    [SerializeReference] public BlackboardVariable<string> ResponseMessage = new BlackboardVariable<string>("");

    protected override Status OnStart()
    {
        DialogueResponse response = Dialogue_Engine.SendRequest(new DialogueRequest
        {
            Type = DialogueRequestType.GetEvents,
            DialoguePath = DslPath != null ? DslPath.Value : "",
            EventName = EventName != null ? EventName.Value : "",
            SinceSequence = SinceSequence != null ? SinceSequence.Value : 0
        });
        List<DialogueEventRecord> rows = response != null ? response.Events : null;
        if (EventCount != null) EventCount.Value = DialogueEventMetrics.CountEmittedEvents(rows);
        if (HistoryRowCount != null) HistoryRowCount.Value = DialogueEventMetrics.CountRows(rows);
        if (ResponseMessage != null) ResponseMessage.Value = response != null ? response.Message : "";
        return response != null && response.IsSuccess ? Status.Success : Status.Failure;
    }
}

[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Get Dialogue DSL Record",
    story: "Get DSL [DslPath] into [Found] [DialogueId] [PlayCount]",
    category: "Action/Dialogue/Query",
    id: "d7a1f6b6e9494d0894410aa14929f6a1")]
public partial class UnityBehaviorGetDialogueDslAction : Action
{
    [SerializeReference] public BlackboardVariable<string> DslPath = new BlackboardVariable<string>("");
    [SerializeReference] public BlackboardVariable<bool> Found = new BlackboardVariable<bool>(false);
    [SerializeReference] public BlackboardVariable<string> DialogueId = new BlackboardVariable<string>("");
    [SerializeReference] public BlackboardVariable<int> PlayCount = new BlackboardVariable<int>(0);

    protected override Status OnStart()
    {
        var node = new DialogueGetDslActionNode
        {
            DslPath = DslPath != null ? DslPath.Value : ""
        };
        DialogueBTStatus status = node.Tick();
        bool found = node.Dialogue != null;
        if (Found != null) Found.Value = found;
        if (DialogueId != null) DialogueId.Value = found ? node.Dialogue.DialogueId : "";
        if (PlayCount != null) PlayCount.Value = found ? node.Dialogue.PlayCount : 0;
        return DialogueUnityBehaviorStatus.Map(status);
    }
}

[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Dialogue Service Query",
    story: "Send dialogue [RequestType] for [DslPath] event [EventName] after [SinceSequence] into [Matched] [ResponseCode] [ResponseMessage]",
    category: "Action/Dialogue/Query",
    id: "7acc0263fb654d3181784a2f6fd95a24")]
public partial class UnityBehaviorDialogueQueryAction : Action
{
    [SerializeReference] public BlackboardVariable<DialogueRequestType> RequestType =
        new BlackboardVariable<DialogueRequestType>(DialogueRequestType.LiveSnapshot);
    [SerializeReference] public BlackboardVariable<string> DslPath = new BlackboardVariable<string>("");
    [SerializeReference] public BlackboardVariable<string> EventName = new BlackboardVariable<string>("");
    [SerializeReference] public BlackboardVariable<int> SinceSequence = new BlackboardVariable<int>(0);
    [SerializeReference] public BlackboardVariable<bool> Matched = new BlackboardVariable<bool>(false);
    [SerializeReference] public BlackboardVariable<int> ResponseCode = new BlackboardVariable<int>(0);
    [SerializeReference] public BlackboardVariable<string> ResponseMessage = new BlackboardVariable<string>("");

    protected override Status OnStart()
    {
        return Execute();
    }

    protected override Status OnUpdate()
    {
        return Execute();
    }

    Status Execute()
    {
        DialogueResponse response = Dialogue_Engine.SendRequest(new DialogueRequest
        {
            Type = RequestType != null ? RequestType.Value : DialogueRequestType.LiveSnapshot,
            DialoguePath = DslPath != null ? DslPath.Value : "",
            EventName = EventName != null ? EventName.Value : "",
            SinceSequence = SinceSequence != null ? SinceSequence.Value : 0
        });
        if (Matched != null) Matched.Value = response != null && response.Matched;
        if (ResponseCode != null) ResponseCode.Value = response != null ? (int)response.Code : 0;
        if (ResponseMessage != null) ResponseMessage.Value = response != null ? response.Message : "";
        if (response == null) return Status.Failure;
        if (response.Code == DialogueResponseCode.Pending) return Status.Running;
        return response.IsSuccess ? Status.Success : Status.Failure;
    }
}
