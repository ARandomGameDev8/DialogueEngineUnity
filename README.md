# DialogueEngineUnity

## Standalone events

`@EMIT` is a leaf `EventToken` and may appear anywhere inside a section. The
legacy inline option form is also supported.

```text
START
@ENTRY INTRO

SECTION INTRO
[NARRATOR]: "The door opens.";
@EMIT "door_opened";
[NARRATOR]: "Something approaches.";

CHOICE:
OPTION_0: "Run"; goto ESCAPE; @EMIT "player_ran";
OPTION_1: "Stay"; goto WAIT;
;
END_SECTION

SECTION ESCAPE
@EMIT "escape_started";
[NARRATOR]: "You run.";
END_SECTION

SECTION WAIT
[NARRATOR]: "You wait.";
END_SECTION
END
```

The compiler resolves the event to a string and the engine publishes it through:

```csharp
Dialogue_Engine.OnEmit += eventName => Debug.Log(eventName);
```

## Play-session database

Each `Dialogue_Engine` owns an in-memory `DialogueRuntimeDatabase`. It contains:

- one unique `DialogueScriptRecord` per DSL path;
- many `DialogueEventRecord` rows per DSL;
- a collision-safe `timestamp + text name` primary key;
- current statuses (`TypingText`, `WaitingForInput`, `TakingChoice`,
  `ChoiceSelected`, `EventEmitted`, and others);
- the emitted string, which is empty for ordinary status rows.

Nothing is written to disk. The database resets when the engine is destroyed or
Play Mode stops.

## In-process client/service API

The engine implements `IDialogueService`. This is an HTTP-like request/response
interface without OS sockets or worker threads. One-shot query requests are
resolved by a bounded query server, while dedicated live subscription servers
handle snapshot monitoring and live emitted events separately.

### Immediate non-blocking snapshot

```csharp
DialogueResponse response = Dialogue_Engine.SendRequest(
    DialogueRequest.Snapshot());

Debug.Log(response.Message); // <dialogue-snapshot>...</dialogue-snapshot>
```

Warning: this is a one-shot snapshot query. Do not build tight monitoring loops
around it when live snapshot subscriptions are available.

### Poll for an event (non-blocking)

```csharp
DialogueResponse response = Dialogue_Engine.SendRequest(
    DialogueRequest.HasEvent("door_opened", sinceSequence));

if (response.Matched)
    Debug.Log("The dialogue reached the door.");
```

Store `response.Snapshot.LatestSequence` (or an event row's `Sequence`) and pass
it as `SinceSequence` to only inspect newer records.

Warning: `HasEvent` is a query-style check, not a live event stream. For
continuous monitoring, register a live event subscription instead of spamming it
inside loops.

### Async request handled on later Updates

```csharp
var request = DialogueRequest.Snapshot();
request.ClientId = "debug-panel";

Dialogue_Engine.Service.SendAsync(
    request,
    response => Debug.Log(response.Message));
```

The async query server keeps only the latest pending one-shot request per
`ClientId`. If `ClientId` is left empty, the request falls back to its own
`RequestId`, which preserves one callback per call but disables coalescing.

### Live snapshot subscription

```csharp
int subscriptionId = Dialogue_Engine.SubscribeLiveSnapshots(
    snapshot => Debug.Log(snapshot.ToMessage()),
    clientId: "debug-panel",
    dialoguePathFilter: "",
    onlyOnChange: true,
    minIntervalSeconds: 0f);

// Later:
Dialogue_Engine.UnsubscribeLiveSnapshots(subscriptionId);
```

Use a one-shot snapshot query to get the current state immediately, then keep a
live subscription for ongoing monitoring.

### Live event subscription

```csharp
int subscriptionId = Dialogue_Engine.SubscribeLiveEvents(
    eventName => Debug.Log(eventName),
    clientId: "quest-bridge",
    dialoguePathFilter: "Assets/Dialogues/quest_offer.txt");

// Later:
Dialogue_Engine.UnsubscribeLiveEvents(subscriptionId);
```

### Priority live event subscription

```csharp
int subscriptionId = Dialogue_Engine.SubscribePriorityLiveEvents(
    eventName =>
    {
        if (eventName == "quest_accepted")
            return DialoguePriorityDispatchResult.CullLowerPriorities;
        return DialoguePriorityDispatchResult.Continue;
    },
    priority: 100,
    clientId: "quest-arbiter");

// Later:
Dialogue_Engine.UnsubscribePriorityLiveEvents(subscriptionId);
```

If a priority callback returns `CullLowerPriorities`, all lower-priority live
subscribers are removed while same-priority ones remain.

### Coroutine-blocking wait

"Blocking" is implemented as a coroutine, so it never freezes Unity's main
thread:

```csharp
var request = new DialogueRequest
{
    Type = DialogueRequestType.WaitForEvent,
    EventName = "door_opened",
    TimeoutSeconds = 15f
};

Dialogue_Engine.Instance.StartBlockingRequest(request, response =>
{
    if (response.Code == DialogueResponseCode.Ok)
        Debug.Log("Event received");
    else
        Debug.Log("Timed out");
});
```

Requests still support one-shot live snapshots, dialogue lookup, event queries,
event polling, and conditional event waits. Continuous monitoring now belongs to
the dedicated live subscription APIs rather than repeated loop polling.
