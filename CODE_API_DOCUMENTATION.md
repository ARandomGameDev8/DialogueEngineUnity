# Dialogue Engine Code API

Complete guide to playing Dialogue DSL files, receiving live emissions, requesting runtime information, querying historical records, waiting for events, and interrupting or resuming dialogue from C#.

---

## 1. Architecture at a glance

```text
Dialogue DSL (.txt)
        |
        v
Compiler_S -> DialogueGraph / SyntaxTokens
        |
        v
Dialogue_Engine
  |-- UI and input
  |-- traversal and choices
  |-- OnEmit live C# event
  |-- volatile DialogueRuntimeDatabase
  `-- IDialogueService request/response API
        |
        +-- ordinary C# systems
        +-- quests, combat, AI, state machines
        +-- custom Behavior Trees
        `-- optional Unity Behavior wrappers
```

The DSL owns narrative content. Gameplay systems remain in C# or another architecture and communicate with the engine through a small API.

The service is **in-process**. It resembles a client/server request API, but it does not use sockets, IPC, worker threads, or network serialization.

Compatibility notes:
- Unity-object client keys use the modern `GetEntityId()` path on newer Unity versions, with fallback to `GetInstanceID()` on older editors.
- Runtime input handling supports legacy Input Manager, new Input System, or Both project modes.

---

## 2. Required scene setup

A scene must contain one active `Dialogue_Engine` component with its required UI settings configured.

At runtime it becomes available through:

```csharp
Dialogue_Engine.Instance
```

Check availability before using instance APIs:

```csharp
if (Dialogue_Engine.Instance == null)
{
    Debug.LogError("No Dialogue_Engine exists in this scene.");
    return;
}
```

The engine uses a singleton policy. A duplicate instance destroys itself.

---

## 3. Minimal DSL example

```text
START
@ENTRY INTRO

SECTION INTRO
[NARRATOR]: "Welcome to the station.";
@EMIT "station_intro_seen";
[NARRATOR]: "Choose where to go.";

CHOICE:
OPTION_0: "Visit engineering"; goto ENGINEERING; @EMIT "engineering_selected";
OPTION_1: "Visit the bridge"; goto BRIDGE; @EMIT "bridge_selected";
;
END_SECTION

SECTION ENGINEERING
[ENGINEER]: "The reactor is stable.";
@EMIT "engineering_finished";
END_SECTION

SECTION BRIDGE
[CAPTAIN]: "We are ready to depart.";
@EMIT "bridge_finished";
END_SECTION

END
```

Standalone `@EMIT` statements are valid inside sections. Inline option emissions remain supported.

---

# Part I — Starting and controlling dialogue

## 4. `Dialogue_Engine.Play`

```csharp
public static bool Play(
    string path,
    bool interruptible = false,
    bool saveState = false);
```

### Basic playback

```csharp
bool started = Dialogue_Engine.Play(
    "Assets/Dialogues/station_intro.txt");

if (!started)
    Debug.LogError("Dialogue failed to start.");
```

The method returns:

| Return | Meaning |
|---|---|
| `true` | File opened, DSL compiled, and playback was accepted |
| `false` | Missing engine/file, compile failure, invalid graph, or interruption was rejected |

Compilation and startup happen synchronously during the call.

## 5. Non-interruptible playback

```csharp
Dialogue_Engine.Play(
    "Assets/Dialogues/main_story.txt",
    interruptible: false);
```

While this dialogue is open, another `Play` request is rejected.

```csharp
bool accepted = Dialogue_Engine.Play("Assets/Dialogues/ambient.txt");
// false while main_story.txt is still open
```

## 6. Interruptible playback without state saving

```csharp
Dialogue_Engine.Play(
    "Assets/Dialogues/guard_conversation.txt",
    interruptible: true,
    saveState: false);

// Later:
Dialogue_Engine.Play("Assets/Dialogues/combat_warning.txt");
```

The guard conversation is discarded. It does not resume when the warning finishes.

A non-saving interruption also clears older suspended dialogue states so that the new playback chain is fresh.

## 7. Interruptible playback with state saving

```csharp
Dialogue_Engine.Play(
    "Assets/Dialogues/guard_conversation.txt",
    interruptible: true,
    saveState: true);

// Interrupt it:
Dialogue_Engine.Play("Assets/Dialogues/radio_message.txt");
```

The engine captures the interrupted playback state and pushes it onto a LIFO stack. When the interrupting dialogue finishes, the saved dialogue resumes.

Captured state includes:

- Compiled graph
- Current section and token index
- Nested traversal stack
- Current line and text state
- Current choice
- Dialogue history
- Portrait assignments and active slots
- Dialogue path and ID
- Runtime status
- Interruption flags

Nested interruptions resume in stack order:

```text
A interrupted by B
B interrupted by C
C finishes -> resume B
B finishes -> resume A
```

A partially typed line resumes at line granularity as a fully rendered line waiting for input.

## 8. Useful playback properties

```csharp
Dialogue_Engine engine = Dialogue_Engine.Instance;

bool playing = engine.IsPlaying;
bool canBeInterrupted = engine.CurrentDialogueInterruptible;
int savedDialogueCount = engine.SuspendedDialogueCount;
```

## 9. Fresh UI behavior

Every independent `Play` starts from Inspector-configured UI defaults. Transient state from the previous DSL is cleared, including:

- Old text and speaker names
- Choices and highlights
- Portrait images, frames, tint, and opacity
- Character image panels
- History UI
- Typewriter state
- Traversal state
- Toolbar state

The play-session database is intentionally **not** cleared. Saved interruption state is restored only when `saveState: true` explicitly requested it.

---

# Part II — Live emitted events

## 10. `Dialogue_Engine.OnEmit`

```csharp
public static event Action<string> OnEmit;
```

Subscribe to receive an emitted string immediately when the engine reaches an `EventToken`.

```csharp
using UnityEngine;

public class DialogueEventReceiver : MonoBehaviour
{
    void OnEnable()
    {
        Dialogue_Engine.OnEmit += HandleDialogueEvent;
    }

    void OnDisable()
    {
        Dialogue_Engine.OnEmit -= HandleDialogueEvent;
    }

    void HandleDialogueEvent(string eventName)
    {
        switch (eventName)
        {
            case "open_security_door":
                OpenSecurityDoor();
                break;

            case "start_combat":
                StartCombat();
                break;
        }
    }

    void OpenSecurityDoor() { }
    void StartCombat() { }
}
```

When this DSL executes:

```text
@EMIT "open_security_door";
```

the engine performs these operations:

1. Records an `EventEmitted` row in the database.
2. Updates the snapshot's `LastEvent`.
3. Invokes `OnEmit` with the resolved string.
4. Continues to the next DSL token.

### Important properties of `OnEmit`

- It is a live push notification.
- It is invoked synchronously on Unity's main thread.
- It does not wait for a request.
- It does not replay events that happened before subscription.
- Always unsubscribe when the receiver is disabled or destroyed.
- Keep handlers fast; offload expensive work if necessary.

Use the database query API when a listener may have missed the live emission.

## 10A. Dedicated live subscription APIs

For continuous monitoring, use the engine's dedicated subscription servers
instead of repeatedly querying one-shot requests in loops.

### Live snapshot subscriptions

```csharp
SnaphotSubID snapshotSubId = Dialogue_Engine.SubscribeLiveSnapshots(
    this,
    snapshot => Debug.Log(snapshot.ToMessage()),
    dialoguePathFilter: "",
    onlyOnChange: true,
    minIntervalSeconds: 0f);
```

Unsubscribe later:

```csharp
Dialogue_Engine.UnsubscribeLiveSnapshots(snapshotSubId);
```

### Live event subscriptions

The simplest public API is closure-friendly: your callback can capture any
fields or local variables from the surrounding script.

```csharp
EventMonitorID eventMonitorId = Dialogue_Engine.Subscribe(
    this,
    "quest_accepted",
    () =>
    {
        questSystem.Accept(currentQuestId);
        ui.ShowAccepted();
    });
```

If you want the emitted event string too:

```csharp
EventMonitorID eventMonitorId = Dialogue_Engine.Subscribe(
    this,
    eventName => Debug.Log(eventName));
```

Unsubscribe later:

```csharp
Dialogue_Engine.UnsubscribeLiveEvents(eventMonitorId);
```

Non-Unity callers can pass a stable string client id instead of `this`. Advanced
filtering by path/event remains available through
`SubscribeLiveEvents(clientId, ...)`.

### Priority live event subscriptions

```csharp
PriorityEventMonitorID priorityEventMonitorId = Dialogue_Engine.Subscribe(
    this,
    100,
    "quest_accepted",
    () =>
    {
        if (questSystem.CanClaim(currentQuestId))
            return DialoguePriorityDispatchResult.CullLowerPriorities;
        return DialoguePriorityDispatchResult.Continue;
    });
```

If you want the emitted event string inside the priority callback:

```csharp
PriorityEventMonitorID priorityEventMonitorId = Dialogue_Engine.Subscribe(
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

Same-priority subscribers remain eligible in either lower-priority case.

Unsubscribe later:

```csharp
Dialogue_Engine.UnsubscribePriorityLiveEvents(priorityEventMonitorId);
```

Use `CullLowerPriorities` for temporary per-dispatch suppression and
`DeregisterLowerPriorities` only when permanent removal is actually intended.

Non-Unity callers can pass a stable string client id instead of `this`. Advanced
filtering by client/path/event remains available through
`SubscribePriorityLiveEvents(clientId, ...)`.

### Client-wide cleanup

```csharp
Dialogue_Engine.UnsubscribeAllClientSubscriptions("quest-bridge");
```

The live subscription APIs are pushed by dedicated internal servers. They do not
travel through the one-shot async request queue.

---

# Part III — Service requests and responses

## 11. Service interface

`Dialogue_Engine` implements:

```csharp
public interface IDialogueService
{
    DialogueResponse Send(DialogueRequest request);

    void SendAsync(
        DialogueRequest request,
        Action<DialogueResponse> completed);

    IEnumerator SendBlocking(
        DialogueRequest request,
        Action<DialogueResponse> completed);
}
```

Access it through:

```csharp
IDialogueService service = Dialogue_Engine.Service;
```

Or use the static convenience methods:

```csharp
// Immediate compatibility path
DialogueResponse response = Dialogue_Engine.SendRequest(request);

// Preferred coalesced one-shot path for Unity objects
DialogueResponse coalesced = Dialogue_Engine.SendRequest(this, request);

// Preferred coalesced one-shot path for plain C# systems
DialogueResponse plain = Dialogue_Engine.SendRequest("quest-system", request);
```

## 12. `DialogueRequest`

```csharp
public sealed class DialogueRequest
{
    public string RequestId;
    public string ClientId;
    public DialogueRequestType Type;
    public string DialogueId;
    public string DialoguePath;
    public string EventName;
    public long SinceSequence;
    public float TimeoutSeconds;
}
```

### Fields

| Field | Purpose |
|---|---|
| `RequestId` | Correlates a response with its request; generated automatically |
| `ClientId` | Optional coalescing key for async or caller-managed one-shot requests |
| `Type` | Operation to execute |
| `DialogueId` | Optional DSL table key/filter |
| `DialoguePath` | Optional DSL path/filter |
| `EventName` | Event string used by event requests |
| `SinceSequence` | Exclusive lower sequence bound |
| `TimeoutSeconds` | Timeout used by coroutine waiting |

If neither `DialogueId` nor `DialoguePath` is supplied, the service generally uses the currently active dialogue ID.

Prefer explicit `DialoguePath` for DSL-specific historical queries.

## 13. Request types

```csharp
public enum DialogueRequestType
{
    LiveSnapshot,
    GetDialogue,
    GetEvents,
    HasEvent,
    WaitForEvent
}
```

| Type | Purpose |
|---|---|
| `LiveSnapshot` | Current engine state |
| `GetDialogue` | One DSL table record |
| `GetEvents` | Historical status/event rows |
| `HasEvent` | Whether matching event rows exist |
| `WaitForEvent` | Pending until matching event exists; intended for coroutine waiting |

## 14. `DialogueResponse`

```csharp
public sealed class DialogueResponse
{
    public string RequestId;
    public DialogueResponseCode Code;
    public string Message;
    public DialogueLiveSnapshot Snapshot;
    public DialogueScriptRecord Dialogue;
    public List<DialogueEventRecord> Events;
    public bool Matched;

    public bool IsSuccess;
    public bool IsPending;
    public bool IsFail;
}
```

Only fields relevant to the request are populated.

| Request | Main populated fields |
|---|---|
| `LiveSnapshot` | `Snapshot`, `Message` |
| `GetDialogue` | `Dialogue`, `Message` |
| `GetEvents` | `Events`, `Matched`, `Message` |
| `HasEvent` | `Events`, `Matched`, `Message` |
| `WaitForEvent` | `Events`, `Matched`, `Message` |

## 15. Response codes

```csharp
public enum DialogueResponseCode
{
    Ok = 200,
    Pending = 202,
    InvalidRequest = 400,
    NotFound = 404,
    Timeout = 408
}
```

| Code | Meaning |
|---|---|
| `Ok` | Request resolved successfully |
| `Pending` | Event condition has not matched yet, or a coalesced one-shot request was deferred |
| `InvalidRequest` | Missing/unsupported request information |
| `NotFound` | Engine service or requested DSL unavailable |
| `Timeout` | Blocking coroutine wait exceeded its timeout, or a coalesced one-shot request exceeded its retry limit |

`response.IsSuccess` is true only when `Code == Ok`.
`response.IsPending` is true only when `Code == Pending`.
`response.IsFail` is true for every non-success, non-pending outcome.

## 16. Immediate request

```csharp
DialogueResponse response = Dialogue_Engine.SendRequest(request);
```

This compatibility form returns during the same method call. It does not wait
across frames and does not use caller-based latest-wins coalescing.

## 16A. Preferred coalesced one-shot request

For code that may issue many one-shot queries from many callers in the same
frame, prefer the caller-aware overloads:

```csharp
DialogueResponse response = Dialogue_Engine.SendRequest(
    this,
    DialogueRequest.Snapshot());
```

Or, from a plain C# system:

```csharp
DialogueResponse response = Dialogue_Engine.SendRequest(
    "quest-system",
    DialogueRequest.HasEvent("door_opened"));
```

Behavior:

- each caller/client keeps only its latest queued one-shot request
- the query server resolves at most `maxAsyncQueryClientsPerFrame` clients per frame
- if your caller's slot is processed immediately, `IsSuccess` is true
- if not, `IsPending` is returned and the request retries automatically next frame
- if the coalesced retry limit is exceeded, `IsFail` becomes true

## 17. Update-queued asynchronous request

```csharp
var request = DialogueRequest.Snapshot();
request.ClientId = "debug-panel";

Dialogue_Engine.Service.SendAsync(
    request,
    response =>
    {
        Debug.Log(response.Message);
    });
```

The async query server keeps only the latest pending one-shot request per
`ClientId`. `Dialogue_Engine.Update()` resolves a bounded number of distinct
clients each frame and invokes callbacks on Unity's main thread.

If `ClientId` is empty, the request falls back to its own `RequestId`, which
preserves one callback per call but disables request coalescing.

This is asynchronous by scheduling, not by a worker thread.

## 18. Coroutine-based blocking request

```csharp
Dialogue_Engine.Instance.StartBlockingRequest(
    request,
    response =>
    {
        Debug.Log(response.Code);
    });
```

“Blocking” means the coroutine remains active and yields each frame. Unity's main thread is not frozen.

---

# Part IV — Live snapshots

## 19. Requesting a snapshot

Convenience request:

```csharp
DialogueResponse response = Dialogue_Engine.SendRequest(
    DialogueRequest.Snapshot());

if (response.IsSuccess)
{
    DialogueLiveSnapshot snapshot = response.Snapshot;
    Debug.Log(snapshot.SectionId);
}
```

Direct instance call:

```csharp
DialogueLiveSnapshot snapshot =
    Dialogue_Engine.Instance.GetLiveSnapshot();
```

Warning: `GetLiveSnapshot()` and `LiveSnapshot` requests are one-shot reads.
Do not build tight per-frame monitoring loops around them when live snapshot
subscriptions are available.

## 20. Snapshot fields

```csharp
public sealed class DialogueLiveSnapshot
{
    public bool IsPlaying;
    public string DialogueId;
    public string DialoguePath;
    public string SectionId;
    public string TextName;
    public string Text;
    public string LastEvent;
    public DialogueRuntimeStatus Status;
    public string Detail;
    public long LatestSequence;
}
```

| Field | Meaning |
|---|---|
| `IsPlaying` | UI/dialogue is currently active |
| `DialogueId` | Current normalized database ID |
| `DialoguePath` | Path passed to Play |
| `SectionId` | Current section |
| `TextName` | Current speaker, choice name, or text identifier |
| `Text` | Current dialogue/choice text |
| `LastEvent` | Most recent event emitted by current playback |
| `Status` | Current execution/IO state |
| `Detail` | Human-readable status explanation |
| `LatestSequence` | Latest database row sequence |

## 21. Runtime statuses

```csharp
public enum DialogueRuntimeStatus
{
    Idle,
    TypingText,
    WaitingForInput,
    TakingChoice,
    ChoiceSelected,
    EventEmitted,
    Transitioning,
    Interrupted,
    Resumed,
    Completed
}
```

Typical checks:

```csharp
DialogueLiveSnapshot snapshot =
    Dialogue_Engine.Instance.GetLiveSnapshot();

if (snapshot.Status == DialogueRuntimeStatus.WaitingForInput)
    Debug.Log("Waiting for player advance input.");

if (snapshot.Status == DialogueRuntimeStatus.TakingChoice)
    Debug.Log("A clickable choice is active.");
```

## 22. Snapshot message format

```csharp
string message = snapshot.ToMessage();
```

Example:

```xml
<dialogue-snapshot>
  <playing>True</playing>
  <dialogue id="Assets/Dialogues/station_intro.txt">Assets/Dialogues/station_intro.txt</dialogue>
  <section>INTRO</section>
  <text name="NARRATOR">Welcome to the station.</text>
  <io-status>WaitingForInput</io-status>
  <detail>Waiting for Enter/Space</detail>
  <last-event>station_intro_seen</last-event>
  <sequence>12</sequence>
</dialogue-snapshot>
```

The message is XML-like text intended for simple transport/logging. Prefer the strongly typed `Snapshot` object in C#.

---

# Part V — Event queries

## 23. Immediate `HasEvent` query

```csharp
var request = new DialogueRequest
{
    Type = DialogueRequestType.HasEvent,
    DialoguePath = "Assets/Dialogues/station_intro.txt",
    EventName = "engineering_selected",
    SinceSequence = 0
};

DialogueResponse response = Dialogue_Engine.SendRequest(request);

if (response.Matched)
{
    Debug.Log("The event occurred.");
}
else
{
    Debug.Log("The event is not in the database yet.");
}
```

This checks database history. It does not only inspect the current token.

Warning: `HasEvent` is a one-shot query, not a live event stream. For
continuous monitoring, use the dedicated live event subscription APIs instead of
spamming `HasEvent` inside loops.

### Unmatched behavior

For `HasEvent`, no match produces:

```text
Code = Pending
Matched = false
Events.Count = 0
```

A match produces:

```text
Code = Ok
Matched = true
Events.Count > 0
```

## 24. Convenience `HasEvent` request

```csharp
DialogueResponse response = Dialogue_Engine.SendRequest(
    DialogueRequest.HasEvent("engineering_selected"));
```

This helper omits a DSL path, so the service defaults to the current dialogue. For explicit cross-dialogue queries, construct the request and set `DialoguePath`.

## 25. Querying one specific DSL

```csharp
var request = new DialogueRequest
{
    Type = DialogueRequestType.HasEvent,
    DialoguePath = "Assets/Dialogues/guard.txt",
    EventName = "guard_warned_player"
};

DialogueResponse response = Dialogue_Engine.SendRequest(request);
```

The database filters on both:

```text
DialogueId/path + EmittedEvent
```

## 26. Event-name matching rules

Event matching uses ordinal, case-sensitive comparison.

These are different:

```text
guard_warned_player
Guard_Warned_Player
```

In C#, do not include DSL quote characters:

```csharp
EventName = "guard_warned_player";     // correct
EventName = "\"guard_warned_player\""; // incorrect
```

## 27. Sequence checkpoints

Every database row has a monotonically increasing `Sequence`.

Capture a checkpoint:

```csharp
long checkpoint = Dialogue_Engine
    .SendRequest(DialogueRequest.Snapshot())
    .Snapshot.LatestSequence;
```

Query only rows created afterward:

```csharp
var request = new DialogueRequest
{
    Type = DialogueRequestType.HasEvent,
    DialoguePath = path,
    EventName = "door_opened",
    SinceSequence = checkpoint
};
```

`SinceSequence` is exclusive. A row is included only when:

```text
row.Sequence > SinceSequence
```

Use checkpoints when the same event may be emitted multiple times or when an older play-session event must not satisfy a new condition.

## 28. Reading matching rows

```csharp
foreach (DialogueEventRecord row in response.Events)
{
    Debug.Log(
        $"#{row.Sequence} [{row.Timestamp}] " +
        $"{row.DialogueId} emitted {row.EmittedEvent}");
}
```

---

# Part VI — Waiting for events

## 29. Coroutine wait

```csharp
var request = new DialogueRequest
{
    Type = DialogueRequestType.WaitForEvent,
    DialoguePath = "Assets/Dialogues/guard.txt",
    EventName = "guard_warned_player",
    SinceSequence = 0,
    TimeoutSeconds = 20f
};

Dialogue_Engine.Instance.StartBlockingRequest(
    request,
    response =>
    {
        switch (response.Code)
        {
            case DialogueResponseCode.Ok:
                Debug.Log("Event received.");
                break;

            case DialogueResponseCode.Timeout:
                Debug.Log("Event was not received before timeout.");
                break;

            default:
                Debug.LogError(response.Message);
                break;
        }
    });
```

## 30. Waiting without a practical timeout

`SendBlocking` clamps timeout to at least `0.01` seconds. Therefore, use a sufficiently large timeout rather than zero for an effectively long wait.

For custom indefinite waiting, poll `HasEvent` from your own coroutine or state machine.

## 31. Manual non-blocking polling

```csharp
void Update()
{
    DialogueResponse response = Dialogue_Engine.SendRequest(
        new DialogueRequest
        {
            Type = DialogueRequestType.HasEvent,
            DialoguePath = path,
            EventName = targetEvent,
            SinceSequence = checkpoint
        });

    if (response.Matched)
        HandleMatch(response.Events);
}

void HandleMatch(List<DialogueEventRecord> rows) { }
```

Each call returns immediately. The surrounding `Update` loop provides repeated polling.

---

# Part VII — Historical database queries

## 32. `GetEvents`

Query all status and event rows for one DSL:

```csharp
var request = new DialogueRequest
{
    Type = DialogueRequestType.GetEvents,
    DialoguePath = "Assets/Dialogues/guard.txt",
    SinceSequence = 0
};

DialogueResponse response = Dialogue_Engine.SendRequest(request);

foreach (DialogueEventRecord row in response.Events)
{
    Debug.Log(
        $"{row.Timestamp} | {row.Status} | " +
        $"{row.TextName} | {row.EmittedEvent}");
}
```

Filter to one event:

```csharp
request.EventName = "guard_warned_player";
```

`GetEvents` returns `Ok` even when the result list is empty. `Matched` indicates whether at least one row was found.

## 33. `DialogueEventRecord`

```csharp
public sealed class DialogueEventRecord
{
    public string PrimaryKey;
    public long Sequence;
    public string DialogueId;
    public string Timestamp;
    public string TextName;
    public string Text;
    public string EmittedEvent;
    public DialogueRuntimeStatus Status;
    public string Detail;
}
```

| Field | Meaning |
|---|---|
| `PrimaryKey` | Collision-safe `timestamp + text name` key |
| `Sequence` | Global play-session ordering number |
| `DialogueId` | Foreign-key-style link to the DSL table |
| `Timestamp` | Session-relative `mm:ss.fff` |
| `TextName` | Speaker/choice/current text identifier |
| `Text` | Associated text content |
| `EmittedEvent` | Event string; empty for normal status rows |
| `Status` | Runtime state recorded for this row |
| `Detail` | Human-readable transition detail |

## 34. Direct database queries

Advanced code can access the database directly:

```csharp
DialogueRuntimeDatabase db =
    Dialogue_Engine.Instance.RuntimeDatabase;

List<DialogueEventRecord> rows = db.QueryEvents(
    dialogueId: "Assets/Dialogues/guard.txt",
    eventName: "guard_warned_player",
    sinceSequence: 0);
```

Other direct methods:

```csharp
DialogueScriptRecord row = db.FindDialogue(path);
List<DialogueScriptRecord> all = db.GetDialogues();
long latest = db.LatestSequence;
DateTime sessionStart = db.SessionStartedUtc;
```

Prefer service requests for looser coupling. Direct database access is useful for debugging, tooling, and performance-sensitive local integrations.

---

# Part VIII — DSL table queries

## 35. `GetDialogue`

```csharp
var request = new DialogueRequest
{
    Type = DialogueRequestType.GetDialogue,
    DialoguePath = "Assets/Dialogues/guard.txt"
};

DialogueResponse response = Dialogue_Engine.SendRequest(request);

if (response.Code == DialogueResponseCode.Ok)
{
    DialogueScriptRecord script = response.Dialogue;
    Debug.Log(script.PlayCount);
}
```

## 36. `DialogueScriptRecord`

```csharp
public sealed class DialogueScriptRecord
{
    public string DialogueId;
    public string Path;
    public DateTime StartedAtUtc;
    public int PlayCount;
}
```

One unique record is kept per normalized DSL path. Starting the same path again increments `PlayCount`.

Path IDs normalize backslashes to forward slashes and are looked up case-insensitively.

---

# Part IX — Complete integration examples

## 37. Quest integration with live event delivery

```csharp
using UnityEngine;

public class QuestDialogueBridge : MonoBehaviour
{
    void OnEnable()
    {
        Dialogue_Engine.OnEmit += OnDialogueEmit;
    }

    void OnDisable()
    {
        Dialogue_Engine.OnEmit -= OnDialogueEmit;
    }

    void OnDialogueEmit(string eventName)
    {
        if (eventName == "quest_accepted")
        {
            // questSystem.Accept("station_repair");
        }
        else if (eventName == "quest_refused")
        {
            // questSystem.Refuse("station_repair");
        }
    }
}
```

## 38. Start dialogue and inspect its current location

```csharp
bool started = Dialogue_Engine.Play(
    "Assets/Dialogues/station_intro.txt",
    interruptible: true,
    saveState: true);

if (started)
{
    DialogueLiveSnapshot snapshot =
        Dialogue_Engine.Instance.GetLiveSnapshot();

    Debug.Log($"{snapshot.DialoguePath} / {snapshot.SectionId}");
}
```

## 39. Historical fallback when a live event was missed

```csharp
bool WasQuestAccepted()
{
    DialogueResponse response = Dialogue_Engine.SendRequest(
        new DialogueRequest
        {
            Type = DialogueRequestType.HasEvent,
            DialoguePath = "Assets/Dialogues/quest_offer.txt",
            EventName = "quest_accepted"
        });

    return response.Matched;
}
```

## 40. Monitor dialogue status

```csharp
void Update()
{
    DialogueLiveSnapshot snapshot =
        Dialogue_Engine.Instance.GetLiveSnapshot();

    switch (snapshot.Status)
    {
        case DialogueRuntimeStatus.TypingText:
            break;

        case DialogueRuntimeStatus.WaitingForInput:
            break;

        case DialogueRuntimeStatus.TakingChoice:
            break;

        case DialogueRuntimeStatus.Completed:
            break;
    }
}
```

## 41. Interrupt and resume

```csharp
void StartConversation()
{
    Dialogue_Engine.Play(
        "Assets/Dialogues/crew_conversation.txt",
        interruptible: true,
        saveState: true);
}

void ReceiveEmergencyTransmission()
{
    Dialogue_Engine.Play(
        "Assets/Dialogues/emergency_transmission.txt",
        interruptible: false);

    // When the emergency transmission finishes, the saved crew conversation
    // resumes automatically.
}
```

## 42. Request/response logging

```csharp
var request = new DialogueRequest
{
    Type = DialogueRequestType.GetEvents,
    DialoguePath = "Assets/Dialogues/crew_conversation.txt"
};

DialogueResponse response = Dialogue_Engine.SendRequest(request);

Debug.Log($"Request: {response.RequestId}");
Debug.Log($"Code: {(int)response.Code}");
Debug.Log(response.Message);
```

---

# Part X — Input and interaction behavior

## 43. Dialogue input

During ordinary dialogue:

- Space advances or completes the typewriter.
- Numpad Enter advances or completes the typewriter.
- Clicking the dialogue panel advances when `clickToAdvance` is enabled.
- Holding Ctrl speeds up the typewriter.

## 44. Choice input

While a choice is active:

- Space is ignored.
- Return is ignored.
- Numpad Enter is ignored.
- A choice must be clicked.
- The unanswered choice cannot accidentally advance or close the DSL.

Choice selection may emit an inline event before transitioning to its target section.

---

# Part XI — Error handling and diagnostics

## 45. Always inspect the Play return value

```csharp
if (!Dialogue_Engine.Play(path))
{
    Debug.LogError("Could not play dialogue: " + path);
}
```

Possible causes:

- No engine instance
- Empty path
- Missing file
- Compiler error
- Missing entry section
- Current dialogue is non-interruptible
- UI document failed to initialize

## 46. Always inspect response codes

```csharp
DialogueResponse response = Dialogue_Engine.SendRequest(request);

switch (response.Code)
{
    case DialogueResponseCode.Ok:
        break;

    case DialogueResponseCode.Pending:
        break;

    case DialogueResponseCode.InvalidRequest:
    case DialogueResponseCode.NotFound:
    case DialogueResponseCode.Timeout:
        Debug.LogError(response.Message);
        break;
}
```

Do not treat `Matched == false` as an engine error. For event requests it normally means the event has not occurred yet.

## 47. Path consistency

For reliable filtering, use the same path string when playing and querying:

```csharp
const string GuardDialogue = "Assets/Dialogues/guard.txt";

Dialogue_Engine.Play(GuardDialogue);

var request = new DialogueRequest
{
    Type = DialogueRequestType.HasEvent,
    DialoguePath = GuardDialogue,
    EventName = "guard_finished"
};
```

Centralized constants reduce path and event-name mistakes.

## 48. Event constants

```csharp
public static class DialogueEvents
{
    public const string QuestAccepted = "quest_accepted";
    public const string QuestRefused = "quest_refused";
    public const string CombatStarted = "combat_started";
}
```

Use the same names in DSL files and C# handlers.

---

# Part XII — Database lifetime and persistence

## 49. Volatile lifetime

`DialogueRuntimeDatabase` is created in `Dialogue_Engine.Awake()`.

It is not written to disk. Records disappear when:

- Play Mode stops
- The engine is destroyed
- The owning scene unloads without preserving the engine

## 50. What survives between DSL plays

During one Play Mode session:

- DSL table records remain
- Event/status history remains
- Sequence numbers continue increasing
- Play counts remain

Transient UI and traversal variables are reset for each independent dialogue.

## 51. Permanent save games

The internal database is not a permanent save system. If permanent narrative state is needed:

1. Query relevant events/statuses.
2. Copy them into the game's save model.
3. Restore gameplay consequences through the game's own persistence architecture.

Do not rely on the volatile database after exiting Play Mode or unloading its engine.

---

# Part XIII — API decision table

| Need | Recommended API |
|---|---|
| Start a DSL | `Dialogue_Engine.Play` |
| Know whether startup worked | Play return value |
| React immediately to `@EMIT` | `Dialogue_Engine.OnEmit` |
| Monitor live snapshots continuously | `Dialogue_Engine.SubscribeLiveSnapshots` |
| Monitor live emitted events continuously | `Dialogue_Engine.Subscribe(...)` |
| Prioritize live emitted-event consumers | `Dialogue_Engine.Subscribe(priority, ...)` |
| Issue coalesced one-shot queries from gameplay code | `Dialogue_Engine.SendRequest(this, request)` |
| Inspect current section/text/status once | `GetLiveSnapshot` or `LiveSnapshot` request |
| Determine whether an event occurred earlier | `HasEvent` request |
| Query one DSL and one event | Explicit `DialoguePath + EventName` request |
| Detect only new records | `SinceSequence` checkpoint |
| Retrieve full history | `GetEvents` request |
| Retrieve DSL registration/play count | `GetDialogue` request |
| Wait across frames for an event | `StartBlockingRequest` with `WaitForEvent` |
| Queue a one-shot callback for later Updates | `Service.SendAsync` with `ClientId` |
| Interrupt and discard | Play current with `interruptible:true, saveState:false` |
| Interrupt and later resume | Play current with `interruptible:true, saveState:true` |
| Build a custom integration | `IDialogueService` plus live subscription APIs |

---

# Part XIV — Recommended integration pattern

For most projects:

1. Use `Dialogue_Engine.Play` to launch narrative files.
2. Use `OnEmit` or live event subscriptions for immediate gameplay reactions.
3. Use explicit DSL/event database queries for historical conditions.
4. Use one-shot snapshots for single reads and live snapshot subscriptions for continuous monitoring.
5. Use sequence checkpoints for repeated events.
6. Set `ClientId` on async one-shot queries when you want latest-request coalescing per client.
7. Keep permanent gameplay state in the game's own save architecture.
8. Treat Unity Behavior and other BT adapters as optional clients of the same code API.

This preserves the intended architecture:

```text
Simple narrative DSL
       +
Small request/event boundary
       +
Existing gameplay architecture
       =
Fast basic dialogue that can scale into complex interactions
```
ll request/event boundary
       +
Existing gameplay architecture
       =
Fast basic dialogue that can scale into complex interactions
```
ay architecture
       =
Fast basic dialogue that can scale into complex interactions
```
tecture
       =
Fast basic dialogue that can scale into complex interactions
```
ns
```
```
