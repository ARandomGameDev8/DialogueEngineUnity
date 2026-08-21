using System;
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
    story: "Listen until [DslPath] resolves [EventName] after [SinceSequence] into [Result] and [MatchCount] then run True child 1 or False child 2",
    category: "Action/Dialogue/Query",
    id: "48be7ca1c38649d38f3b7b66fe345879")]
public partial class UnityBehaviorHasDialogueEventAction : Composite
{
    [SerializeReference] public BlackboardVariable<string> DslPath = new BlackboardVariable<string>("");
    [SerializeReference] public BlackboardVariable<string> EventName = new BlackboardVariable<string>("");
    [SerializeReference] public BlackboardVariable<int> SinceSequence = new BlackboardVariable<int>(0);
    [SerializeReference] public BlackboardVariable<bool> Result = new BlackboardVariable<bool>(false);
    [SerializeReference] public BlackboardVariable<int> MatchCount = new BlackboardVariable<int>(0);

    [NonSerialized] Node selectedChild;

    protected override Status OnStart()
    {
        selectedChild = null;
        return EvaluateOrRunBranch();
    }

    protected override Status OnUpdate()
    {
        if (selectedChild == null)
            return EvaluateOrRunBranch();

        switch (selectedChild.CurrentStatus)
        {
            case Status.Success: return Status.Success;
            case Status.Failure:
            case Status.Interrupted: return Status.Failure;
            default: return Status.Waiting;
        }
    }

    Status EvaluateOrRunBranch()
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

        if (node.Result)
            return StartOutcomeChild(true);

        // This composite acts as an independent listener when placed under a
        // Unity Behavior parallel branch. It remains Running without blocking
        // sibling branches. FALSE resolves only when this specific DSL actually
        // completes (or is explicitly interrupted and discarded), not merely
        // because another saved/interruption DSL is currently in front of it.
        Dialogue_Engine engine = Dialogue_Engine.Instance;
        if (engine == null || engine.RuntimeDatabase == null)
            return Status.Failure;

        var statusRows = engine.RuntimeDatabase.QueryEvents(
            NormalizePath(path), null,
            SinceSequence != null ? SinceSequence.Value : 0);
        foreach (DialogueEventRecord row in statusRows)
        {
            if (row.Status == DialogueRuntimeStatus.Completed)
                return StartOutcomeChild(false);
            if (row.Status == DialogueRuntimeStatus.Interrupted &&
                row.Detail != null && row.Detail.IndexOf("discarded",
                    StringComparison.OrdinalIgnoreCase) >= 0)
                return StartOutcomeChild(false);
        }
        return Status.Running;
    }

    Status StartOutcomeChild(bool eventWasFound)
    {
        // Child ordering is explicit: first child is TRUE, second child is FALSE.
        // Missing outcome children are allowed and count as a successful no-op.
        int childIndex = eventWasFound ? 0 : 1;
        if (Children == null || childIndex >= Children.Count || Children[childIndex] == null)
            return Status.Success;

        selectedChild = Children[childIndex];
        Status childStatus = StartNode(selectedChild);
        return childStatus == Status.Running || childStatus == Status.Waiting
            ? Status.Waiting : childStatus;
    }

    protected override void OnEnd()
    {
        if (selectedChild != null &&
            (selectedChild.CurrentStatus == Status.Running ||
             selectedChild.CurrentStatus == Status.Waiting))
            EndNode(selectedChild);
        selectedChild = null;
    }

    static string NormalizePath(string path)
    {
        return string.IsNullOrEmpty(path) ? "" : path.Replace('\\', '/');
    }
}

[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Dialogue Event TRUE Block",
    story: "TRUE event branch",
    category: "Flow/Dialogue",
    id: "a9e3d9cdb32f47909f19726b41a17da1")]
public partial class UnityBehaviorDialogueEventTrueBlock : Composite
{
    [NonSerialized] int childIndex;

    protected override Status OnStart()
    {
        childIndex = 0;
        return StartCurrentChild();
    }

    protected override Status OnUpdate()
    {
        if (Children == null || childIndex >= Children.Count) return Status.Success;
        Status status = Children[childIndex].CurrentStatus;
        if (status == Status.Success) { childIndex++; return StartCurrentChild(); }
        if (status == Status.Failure || status == Status.Interrupted) return Status.Failure;
        return Status.Waiting;
    }

    Status StartCurrentChild()
    {
        while (Children != null && childIndex < Children.Count)
        {
            Node child = Children[childIndex];
            if (child == null) { childIndex++; continue; }
            Status status = StartNode(child);
            if (status == Status.Success) { childIndex++; continue; }
            if (status == Status.Running || status == Status.Waiting) return Status.Waiting;
            return Status.Failure;
        }
        return Status.Success;
    }

    protected override void OnEnd()
    {
        if (Children != null && childIndex < Children.Count)
        {
            Node child = Children[childIndex];
            if (child != null && (child.CurrentStatus == Status.Running || child.CurrentStatus == Status.Waiting))
                EndNode(child);
        }
    }
}

[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Dialogue Event FALSE Block",
    story: "FALSE event branch",
    category: "Flow/Dialogue",
    id: "4eb0bd92d35a499e84b3407161c9ea84")]
public partial class UnityBehaviorDialogueEventFalseBlock : Composite
{
    [NonSerialized] int childIndex;

    protected override Status OnStart()
    {
        childIndex = 0;
        return StartCurrentChild();
    }

    protected override Status OnUpdate()
    {
        if (Children == null || childIndex >= Children.Count) return Status.Success;
        Status status = Children[childIndex].CurrentStatus;
        if (status == Status.Success) { childIndex++; return StartCurrentChild(); }
        if (status == Status.Failure || status == Status.Interrupted) return Status.Failure;
        return Status.Waiting;
    }

    Status StartCurrentChild()
    {
        while (Children != null && childIndex < Children.Count)
        {
            Node child = Children[childIndex];
            if (child == null) { childIndex++; continue; }
            Status status = StartNode(child);
            if (status == Status.Success) { childIndex++; continue; }
            if (status == Status.Running || status == Status.Waiting) return Status.Waiting;
            return Status.Failure;
        }
        return Status.Success;
    }

    protected override void OnEnd()
    {
        if (Children != null && childIndex < Children.Count)
        {
            Node child = Children[childIndex];
            if (child != null && (child.CurrentStatus == Status.Running || child.CurrentStatus == Status.Waiting))
                EndNode(child);
        }
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
        DialogueLiveSnapshot snapshot = node.Snapshot;
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
        if (Message != null) Message.Value = node.Message ?? "";
        return DialogueUnityBehaviorStatus.Map(result);
    }
}

[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Wait For Dialogue Event",
    story: "Wait until [DslPath] emits [EventName] after [SinceSequence] timeout [TimeoutSeconds] into [MatchedTimestamp] [MatchedSequence]",
    category: "Action/Dialogue",
    id: "fc79a42933a248ac859e53dba0686f26")]
public partial class UnityBehaviorWaitForDialogueEventAction : Action
{
    [SerializeReference] public BlackboardVariable<string> DslPath = new BlackboardVariable<string>("");
    [SerializeReference] public BlackboardVariable<string> EventName = new BlackboardVariable<string>("");
    [SerializeReference] public BlackboardVariable<int> SinceSequence = new BlackboardVariable<int>(0);
    [SerializeReference] public BlackboardVariable<float> TimeoutSeconds = new BlackboardVariable<float>(10f);
    [SerializeReference] public BlackboardVariable<string> MatchedTimestamp = new BlackboardVariable<string>("");
    [SerializeReference] public BlackboardVariable<int> MatchedSequence = new BlackboardVariable<int>(0);

    [NonSerialized] DialogueWaitForEventActionNode node;

    protected override Status OnStart()
    {
        node = new DialogueWaitForEventActionNode
        {
            DslPath = DslPath != null ? DslPath.Value : "",
            EventName = EventName != null ? EventName.Value : "",
            SinceSequence = SinceSequence != null ? SinceSequence.Value : 0,
            TimeoutSeconds = TimeoutSeconds != null ? TimeoutSeconds.Value : 10f
        };
        return TickNode();
    }

    protected override Status OnUpdate()
    {
        return TickNode();
    }

    Status TickNode()
    {
        if (node == null) return Status.Failure;
        DialogueBTStatus status = node.Tick();
        if (node.Match != null)
        {
            if (MatchedTimestamp != null) MatchedTimestamp.Value = node.Match.Timestamp;
            if (MatchedSequence != null) MatchedSequence.Value =
                (int)Math.Min(int.MaxValue, node.Match.Sequence);
        }
        return DialogueUnityBehaviorStatus.Map(status);
    }

    protected override void OnEnd()
    {
        if (node != null) node.ResetNode();
        node = null;
    }
}

[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Get Dialogue Events",
    story: "Get events for [DslPath] named [EventName] after [SinceSequence] into [EventCount] [ResponseMessage]",
    category: "Action/Dialogue/Query",
    id: "bd741fa37f7b4b7496024736456284c4")]
public partial class UnityBehaviorGetDialogueEventsAction : Action
{
    [SerializeReference] public BlackboardVariable<string> DslPath = new BlackboardVariable<string>("");
    [SerializeReference] public BlackboardVariable<string> EventName = new BlackboardVariable<string>("");
    [SerializeReference] public BlackboardVariable<int> SinceSequence = new BlackboardVariable<int>(0);
    [SerializeReference] public BlackboardVariable<int> EventCount = new BlackboardVariable<int>(0);
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
        if (EventCount != null) EventCount.Value = response != null && response.Events != null
            ? response.Events.Count : 0;
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
