# DialogueEngineUnity

A Unity dialogue system built around a **simple narrative DSL**, a **runtime code/service API**, and **live monitoring/event hooks** that let dialogue plug into existing gameplay code fast.

Compatibility notes:
- uses Unity's modern object identity path on newer Unity versions (`GetEntityId`) with fallback for older editors
- supports legacy Input Manager, new Input System, or Both active input handling modes

## Core idea

```text
Simple dialogue text files
        +
Small event/query boundary
        +
Your existing gameplay systems
        =
Fast dialogue authoring without DSL bloat
```

The DSL handles:
- narrative lines
- sections
- choices
- section jumps
- emitted events

Your game code handles:
- quests
- AI
- combat
- UI logic
- world state
- save data
- any advanced gameplay rules

That boundary is the point of the package.

---

# What is included

## Runtime core
Located in `DIALOGUE_SYSTEM_CORE/`

Main files:
- `Dialogue_Engine.cs`
- `DialogueService.cs`
- `Compiler_S.cs`
- `File_S.cs`

## Editor/UI customization
Located in `UNITY_EDITOR_EXTENSION_FOR_UI_CUSTOMIZATION/`

Main files:
- `DialogueEngineEditor.cs`
- `DialogueLayoutBuilder.cs`
- `DialoguePreviewWindow.cs`

## Test DSLs
Located in `TEST_DSLS/`

Useful for quick validation of:
- standalone `@EMIT`
- choice event emission
- interruption / resume
- variable resolution

---

# Documentation files

## Start here
- `README.md` — package overview + practical API quick start
- `README_DSL_SYNTAX.md` — full DSL syntax guide

## Full API reference
- `CODE_API_DOCUMENTATION.md` — complete C# API/service/runtime reference

## Optional integration docs
If you are using the optional behavior/BT layer, also read:
- `README_BT.md`
- `README_UNITY_BEHAVIOR.md`
- `BT_NODE_DOCUMENTATION.md`

---

# Minimal scene setup

Your scene needs **one active `Dialogue_Engine` component**.

At runtime it is available through:

```csharp
Dialogue_Engine.Instance
```

Always check that the engine exists before using instance-only APIs:

```csharp
if (Dialogue_Engine.Instance == null)
{
    Debug.LogError("No Dialogue_Engine exists in this scene.");
    return;
}
```

---

# Minimal DSL example

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

See `README_DSL_SYNTAX.md` for the full syntax.

---

# Fast API map

## Start dialogue

```csharp
bool ok = Dialogue_Engine.Play(
    "Assets/Dialogues/station_intro.txt",
    interruptible: true,
    saveState: true);
```

Returns:
- `true` when file open + compile + playback start succeeded
- `false` when startup failed or playback was rejected

---

## Live emitted events

## Compatibility hook: `OnEmit`

```csharp
Dialogue_Engine.OnEmit += HandleDialogueEvent;
Dialogue_Engine.OnEmit -= HandleDialogueEvent;
```

`OnEmit` is still available as a **simple compatibility facade**.

Use it when you want:
- a quick global event hook
- one script receiving all emitted events
- old-school `+=` / `-=` usage

Example:

```csharp
void HandleDialogueEvent(string eventName)
{
    if (eventName == "door_opened")
        Debug.Log("Door opened.");
}
```

## Recommended scalable hook: `Subscribe(...)`

Use `Subscribe(...)` when you want:
- explicit live subscription management
- client identity
- event filtering
- priority dispatch

---

# One-shot query API

The engine supports **request/response** style access for current state and historical state.

## Immediate compatibility request

```csharp
DialogueResponse response = Dialogue_Engine.SendRequest(
    DialogueRequest.Snapshot());
```

Use this when you simply want an immediate result now.

## Preferred coalesced one-shot request for Unity objects

```csharp
DialogueResponse response = Dialogue_Engine.SendRequest(
    this,
    DialogueRequest.Snapshot());

if (response.IsSuccess)
    Debug.Log(response.Snapshot.SectionId);
else if (response.IsPending)
    Debug.Log("The query server will retry automatically next frame.");
else if (response.IsFail)
    Debug.LogError(response.Message);
```

Use this when many systems may issue one-shot requests in the same frame and you want:
- latest-wins behavior per caller
- bounded server work per frame
- no request-spam hitching

## Preferred coalesced one-shot request for plain C# systems

```csharp
DialogueResponse response = Dialogue_Engine.SendRequest(
    "quest-system",
    DialogueRequest.HasEvent("door_opened"));
```

---

# Live monitoring subscriptions

There are now **dedicated live monitoring APIs**.

Important rule:
- **live subscriptions require an explicit client**
- if the listener is a Unity object, pass `this`
- if the listener is a plain C# system, pass a stable string client id

---

## Live snapshot monitoring

### Unity object client

```csharp
int subscriptionId = Dialogue_Engine.SubscribeLiveSnapshots(
    this,
    snapshot => Debug.Log(snapshot.ToMessage()),
    dialoguePathFilter: "",
    onlyOnChange: true,
    minIntervalSeconds: 0f);

Dialogue_Engine.UnsubscribeLiveSnapshots(subscriptionId);
```

### Plain C# client

```csharp
int subscriptionId = Dialogue_Engine.SubscribeLiveSnapshots(
    "debug-panel",
    snapshot => Debug.Log(snapshot.ToMessage()),
    dialoguePathFilter: "",
    onlyOnChange: true,
    minIntervalSeconds: 0f);
```

Use this for:
- live debug panels
- quest/UI state mirrors
- AI state monitors
- monitoring current text/section/status over time

Warning:
- `GetLiveSnapshot()` and `LiveSnapshot` requests are **one-shot reads**
- for continuous monitoring, use `SubscribeLiveSnapshots(...)`

---

## Live event monitoring (non-priority)

### Unity object client

```csharp
int subscriptionId = Dialogue_Engine.Subscribe(
    this,
    "quest_accepted",
    () =>
    {
        questSystem.Accept(currentQuestId);
        ui.ShowAccepted();
    });

Dialogue_Engine.UnsubscribeLiveEvents(subscriptionId);
```

If you want the emitted event string:

```csharp
int subscriptionId = Dialogue_Engine.Subscribe(
    this,
    eventName => Debug.Log(eventName));
```

### Plain C# client

```csharp
int subscriptionId = Dialogue_Engine.Subscribe(
    "quest-system",
    "quest_accepted",
    () => questSystem.Accept(currentQuestId));
```

Advanced filtering by path/event is still available through:

```csharp
Dialogue_Engine.SubscribeLiveEvents(
    "quest-system",
    eventName => Debug.Log(eventName),
    dialoguePathFilter: "Assets/Dialogues/quest_offer.txt",
    eventNameFilter: "quest_accepted");
```

---

## Live event monitoring (priority)

### Unity object client

```csharp
int subscriptionId = Dialogue_Engine.Subscribe(
    this,
    100,
    "quest_accepted",
    () =>
    {
        if (questSystem.CanClaim(currentQuestId))
            return DialoguePriorityDispatchResult.CullLowerPriorities;

        return DialoguePriorityDispatchResult.Continue;
    });

Dialogue_Engine.UnsubscribePriorityLiveEvents(subscriptionId);
```

With emitted event string:

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

### Plain C# client

```csharp
int subscriptionId = Dialogue_Engine.Subscribe(
    "npc-arbiter",
    100,
    "quest_accepted",
    () => DialoguePriorityDispatchResult.DeregisterLowerPriorities);
```

Priority results:
- `Continue` → keep dispatching normally
- `CullLowerPriorities` → suppress lower priorities for **this dispatch only**
- `DeregisterLowerPriorities` → permanently remove lower-priority subscribers

Same-priority subscribers remain eligible.

Advanced filtering by path/event is still available through:

```csharp
Dialogue_Engine.SubscribePriorityLiveEvents(
    "npc-arbiter",
    eventName => DialoguePriorityDispatchResult.Continue,
    priority: 100,
    dialoguePathFilter: "Assets/Dialogues/quest_offer.txt",
    eventNameFilter: "quest_accepted");
```

---

# Request/response service API

`Dialogue_Engine` implements `IDialogueService`.

```csharp
IDialogueService service = Dialogue_Engine.Service;
```

## Immediate send

```csharp
DialogueResponse response = service.Send(request);
```

## Async send

```csharp
var request = DialogueRequest.Snapshot();
request.ClientId = "debug-panel";

service.SendAsync(
    request,
    response => Debug.Log(response.Message));
```

## Coroutine wait

```csharp
var request = new DialogueRequest
{
    Type = DialogueRequestType.WaitForEvent,
    EventName = "door_opened",
    TimeoutSeconds = 15f
};

Dialogue_Engine.Instance.StartBlockingRequest(
    request,
    response => Debug.Log(response.Code));
```

---

# Main request types

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

## Use these for
- `LiveSnapshot` → current engine state once
- `GetDialogue` → one DSL record
- `GetEvents` → historical rows
- `HasEvent` → event already occurred?
- `WaitForEvent` → coroutine-style waiting across frames

---

# Main response helpers

`DialogueResponse` exposes:
- `IsSuccess`
- `IsPending`
- `IsFail`

Typical handling:

```csharp
DialogueResponse response = Dialogue_Engine.SendRequest(this, request);

if (response.IsSuccess)
{
    // use response
}
else if (response.IsPending)
{
    // caller-aware one-shot request was deferred; engine retries automatically
}
else if (response.IsFail)
{
    Debug.LogError(response.Message);
}
```

---

# Runtime database

Each engine owns an in-memory `DialogueRuntimeDatabase`.

It stores:
- one `DialogueScriptRecord` per DSL path
- many `DialogueEventRecord` rows
- sequence ordering
- emitted events
- runtime status history

Important:
- this database is **volatile**
- it is **not** a save-game system
- it resets when Play Mode stops or the engine is destroyed

Use your own save architecture for permanent state.

---

# Important usage rules

## 1) Use the right API for the job

### One-shot read
Use:
- `SendRequest(request)`
- `SendRequest(this, request)`
- `GetLiveSnapshot()`
- `HasEvent`
- `GetEvents`

### Continuous monitoring
Use:
- `SubscribeLiveSnapshots(...)`
- `Subscribe(...)`
- `Subscribe(priority, ...)`

Do **not** build generic monitoring loops by spamming one-shot requests every frame when a live subscription exists.

## 2) Always unsubscribe
Especially for live subscriptions:
- subscribe in `OnEnable()`
- unsubscribe in `OnDisable()`

## 3) Keep live callbacks fast
They run on Unity’s main thread.

## 4) Keep your DSL narrative-only
Let the DSL emit signals.
Let code decide gameplay consequences.

---

# Suggested lifecycle patterns

## MonoBehaviour listener
- subscribe in `OnEnable()`
- unsubscribe in `OnDisable()`
- pass `this`

## Plain C# system listener
- subscribe in your init/setup method
- unsubscribe in your shutdown/dispose method
- pass a stable string client id

---

# Optional files you may also want

- `CODE_API_DOCUMENTATION.md` — complete detailed API reference
- `README_DSL_SYNTAX.md` — complete DSL syntax guide
- `TEST_DSLS/README.md` — test dialogue pack notes

---

# Summary

This package is strongest when you use it like this:

```text
DSL for narrative
+
C# for gameplay logic
+
Event/query/subscription boundary for integration
=
Fast dialogue authoring that still scales into complex game systems
```
