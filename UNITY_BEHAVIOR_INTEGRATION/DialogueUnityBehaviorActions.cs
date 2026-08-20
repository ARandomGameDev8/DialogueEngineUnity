using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

// Native Unity Behavior wrappers. These nodes appear under Add > Action > Dialogue.
// They call Dialogue_Engine and its request service directly, so this single
// integration file does not require DialogueBehaviorTreeNodes.cs.

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

    [NonSerialized] long checkpoint;
    [NonSerialized] string normalizedPath;
    [NonSerialized] bool started;

    protected override Status OnStart()
    {
        Dialogue_Engine engine = Dialogue_Engine.Instance;
        normalizedPath = DslPath != null ? DslPath.Value : "";
        if (engine == null || string.IsNullOrWhiteSpace(normalizedPath))
            return Status.Failure;

        checkpoint = engine.RuntimeDatabase != null
            ? engine.RuntimeDatabase.LatestSequence : 0;
        bool canInterrupt = Interruptible != null && Interruptible.Value;
        started = Dialogue_Engine.Play(normalizedPath, canInterrupt,
            canInterrupt && SaveState != null && SaveState.Value);
        return started ? EvaluatePlayback(engine) : Status.Failure;
    }

    protected override Status OnUpdate()
    {
        return started && Dialogue_Engine.Instance != null
            ? EvaluatePlayback(Dialogue_Engine.Instance) : Status.Failure;
    }

    Status EvaluatePlayback(Dialogue_Engine engine)
    {
        var rows = engine.RuntimeDatabase.QueryEvents(
            normalizedPath.Replace('\\', '/'), null, checkpoint);
        foreach (DialogueEventRecord row in rows)
        {
            if (row.Status == DialogueRuntimeStatus.Completed)
                return Status.Success;
            if (row.Status == DialogueRuntimeStatus.Interrupted &&
                row.Detail != null && row.Detail.IndexOf("discarded",
                    StringComparison.OrdinalIgnoreCase) >= 0)
                return Status.Failure;
        }
        return Status.Running;
    }

    protected override void OnEnd()
    {
        started = false;
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
        DialogueResponse response = Dialogue_Engine.SendRequest(new DialogueRequest
        {
            Type = DialogueRequestType.HasEvent,
            DialoguePath = DslPath != null ? DslPath.Value : "",
            EventName = EventName != null ? EventName.Value : "",
            SinceSequence = SinceSequence != null ? SinceSequence.Value : 0
        });
        bool matched = response != null && response.Matched;
        if (Result != null) Result.Value = matched;
        if (MatchCount != null) MatchCount.Value = response != null && response.Events != null
            ? response.Events.Count : 0;
        return matched ? Status.Success : Status.Failure;
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
        DialogueResponse response = Dialogue_Engine.SendRequest(DialogueRequest.Snapshot());
        DialogueLiveSnapshot snapshot = response != null ? response.Snapshot : null;
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
        if (Message != null) Message.Value = response != null ? response.Message : "";
        return response != null && response.IsSuccess ? Status.Success : Status.Failure;
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

    [NonSerialized] float startedAt;
    [NonSerialized] bool waiting;

    protected override Status OnStart()
    {
        waiting = true;
        startedAt = Time.realtimeSinceStartup;
        return TickRequest();
    }

    protected override Status OnUpdate()
    {
        return TickRequest();
    }

    Status TickRequest()
    {
        if (!waiting) return Status.Failure;
        DialogueResponse response = Dialogue_Engine.SendRequest(new DialogueRequest
        {
            Type = DialogueRequestType.HasEvent,
            DialoguePath = DslPath != null ? DslPath.Value : "",
            EventName = EventName != null ? EventName.Value : "",
            SinceSequence = SinceSequence != null ? SinceSequence.Value : 0
        });
        if (response != null && response.Matched)
        {
            DialogueEventRecord match = response.Events != null && response.Events.Count > 0
                ? response.Events[0] : null;
            if (match != null)
            {
                if (MatchedTimestamp != null) MatchedTimestamp.Value = match.Timestamp;
                if (MatchedSequence != null) MatchedSequence.Value =
                    (int)Math.Min(int.MaxValue, match.Sequence);
            }
            return Status.Success;
        }

        float timeout = TimeoutSeconds != null ? TimeoutSeconds.Value : 10f;
        if (timeout > 0f && Time.realtimeSinceStartup - startedAt >= timeout)
            return Status.Failure;
        return Status.Running;
    }

    protected override void OnEnd()
    {
        waiting = false;
        startedAt = 0f;
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
        DialogueResponse response = Dialogue_Engine.SendRequest(new DialogueRequest
        {
            Type = DialogueRequestType.GetDialogue,
            DialoguePath = DslPath != null ? DslPath.Value : ""
        });
        DialogueScriptRecord dialogue = response != null ? response.Dialogue : null;
        bool found = dialogue != null;
        if (Found != null) Found.Value = found;
        if (DialogueId != null) DialogueId.Value = found ? dialogue.DialogueId : "";
        if (PlayCount != null) PlayCount.Value = found ? dialogue.PlayCount : 0;
        return found ? Status.Success : Status.Failure;
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
