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
interface without OS sockets or threads.

### Immediate non-blocking snapshot

```csharp
DialogueResponse response = Dialogue_Engine.SendRequest(
    DialogueRequest.Snapshot());

Debug.Log(response.Message); // <dialogue-snapshot>...</dialogue-snapshot>
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

### Async request handled on the next Update

```csharp
Dialogue_Engine.Service.SendAsync(
    DialogueRequest.Snapshot(),
    response => Debug.Log(response.Message));
```

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

Requests support live snapshots, dialogue lookup, event queries, event polling,
and conditional event waits. These APIs can be called by ordinary game code or
wrapped by a Behavior Tree action node.
