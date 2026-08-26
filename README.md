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

This compatibility form resolves immediately in the same call. It is still the
most direct choice for simple code and the existing BT / Unity Behavior wrappers.

Warning: this is a one-shot snapshot query. Do not build tight monitoring loops
around it when live snapshot subscriptions are available.

### Preferred coalesced one-shot query

Use the caller-aware overload when many systems may issue one-shot queries in the
same frame and you want latest-wins coalescing per caller.

```csharp
DialogueResponse response = Dialogue_Engine.SendRequest(
    this,
    DialogueRequest.Snapshot());

if (response.IsSuccess)
    Debug.Log(response.Snapshot.SectionId);
else if (response.IsPending)
    Debug.Log("Query server will retry automatically next frame.");
else if (response.IsFail)
    Debug.LogError(response.Message);
```

For plain C# systems that do not inherit from `UnityEngine.Object`, use a stable
string key instead of `this`:

```csharp
DialogueResponse response = Dialogue_Engine.SendRequest(
    "quest-system",
    DialogueRequest.HasEvent("door_opened"));
```

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
    this,
    snapshot => Debug.Log(snapshot.ToMessage()),
    dialoguePathFilter: "",
    onlyOnChange: true,
    minIntervalSeconds: 0f);

// Later:
Dialogue_Engine.UnsubscribeLiveSnapshots(subscriptionId);
```

Use a one-shot snapshot query to get the current state immediately, then keep a
live subscription for ongoing monitoring.

### Live event subscription

The simplest API is intentionally closure-friendly, so the callback can capture
any fields or local variables from your script.

```csharp
int subscriptionId = Dialogue_Engine.Subscribe(
    this,
    "quest_accepted",
    () =>
    {
        questSystem.Accept(currentQuestId);
        ui.ShowAccepted();
    });

// Later:
Dialogue_Engine.UnsubscribeLiveEvents(subscriptionId);
```

If you also want the emitted event string:

```csharp
int subscriptionId = Dialogue_Engine.Subscribe(
    this,
    eventName => Debug.Log(eventName));
```

Non-Unity callers can pass a stable string client id instead of `this`. Advanced
filtering by path/event is still available through
`SubscribeLiveEvents(clientId, ...)`.

### Priority live event subscription

```csharp
int subscriptionId = Dialogue_Engine.Subscribe(
    this,
    100,
    "quest_accepted",
    () =>
    {
        questSystem.Accept(currentQuestId);
        return DialoguePriorityDispatchResult.CullLowerPriorities;
    });

// Later:
Dialogue_Engine.UnsubscribePriorityLiveEvents(subscriptionId);
```

If you want the emitted event string inside the priority callback:

```csharp
int subscriptionId = Dialogue_Engine.Subscribe(
    this,
    100,
    eventName =>
    {
        Debug.Log(eventName);
        return DialoguePriorityDispatchResult.Continue;
    });
```

Priority callbacks support three explicit outcomes:

- `Continue` → keep dispatching normally
- `CullLowerPriorities` → suppress lower-priority subscribers for THIS dispatch only
- `DeregisterLowerPriorities` → permanently remove lower-priority subscribers

Same-priority subscribers always remain eligible in either lower-priority case.
Non-Unity callers can pass a stable string client id instead of `this`. Advanced
client/path/event filtering remains available through
`SubscribePriorityLiveEvents(clientId, ...)`.

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
