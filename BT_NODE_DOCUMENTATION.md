# Dialogue Engine Behavior Tree Node Documentation

Complete reference for the framework-neutral Behavior Tree nodes and their native Unity Behavior wrappers.

---

## 1. Overview

The Behavior Tree integration has two layers:

```text
Dialogue Engine code/service API
        |
        v
Framework-neutral BT nodes
DialogueBehaviorTreeNodes.cs
        |
        v
Native Unity Behavior actions
DialogueUnityBehaviorActions.cs
```

The framework-neutral layer can be adapted to any Behavior Tree implementation. The Unity Behavior layer makes those operations available visually through Unity's Behavior Graph.

All layers use the same:

- `Dialogue_Engine`
- `DialogueRuntimeDatabase`
- Emitted-event history
- Live snapshot service
- Interruption and SaveState stack

The BT integration is a client of the Dialogue Engine. The core Dialogue Engine does not require Unity Behavior.

---

# Part I — Installation and files

## 2. Framework-neutral files

```text
DIALOGUE_SYSTEM_CORE/DialogueBehaviorTreeNodes.cs
DIALOGUE_SYSTEM_CORE/DialogueService.cs
DIALOGUE_SYSTEM_CORE/Dialogue_Engine.cs
```

`DialogueBehaviorTreeNodes.cs` depends on the Dialogue Engine and service models, but it does not reference `Unity.Behavior`.

## 3. Native Unity Behavior file

```text
UNITY_BEHAVIOR_INTEGRATION/DialogueUnityBehaviorActions.cs
```

It requires:

- Unity 6-compatible Unity Behavior 1.x
- `com.unity.behavior`
- `DialogueBehaviorTreeNodes.cs`
- `DialogueService.cs`
- `Dialogue_Engine.cs`

It imports:

```csharp
using Unity.Behavior;
using Unity.Properties;
```

## 4. Unity Behavior node menu

Native nodes appear under:

```text
Add > Action > Dialogue
Add > Action > Dialogue > Query
```

If a node does not appear:

1. Ensure `com.unity.behavior` is installed.
2. Resolve all Console compile errors.
3. Save and reopen the Behavior Graph.
4. Remove stale node instances after changing a node class definition.
5. Add a fresh copy from the Add menu.

---

# Part II — Execution statuses

## 5. Framework-neutral status

```csharp
public enum DialogueBTStatus
{
    Running,
    Success,
    Failure
}
```

| Status | Meaning |
|---|---|
| `Running` | Operation has not resolved; tick again later |
| `Success` | Operation completed successfully |
| `Failure` | Operation was rejected, timed out, or could not produce its required result |

## 6. Framework-neutral base node

```csharp
public abstract class DialogueBTActionNode
{
    public abstract DialogueBTStatus Tick();
    public virtual void ResetNode() { }
}
```

A custom BT adapter calls `Tick()` whenever the node is active and maps the returned value to its own status enum.

Call `ResetNode()` whenever the BT framework restarts or reuses a stateful node.

## 7. Unity Behavior status mapping

The native wrapper maps:

```text
DialogueBTStatus.Running -> Node.Status.Running
DialogueBTStatus.Success -> Node.Status.Success
DialogueBTStatus.Failure -> Node.Status.Failure
```

Unity Behavior calls:

- `OnStart()` once when the action begins.
- `OnUpdate()` while it remains `Running`.
- `OnEnd()` when it finishes or is stopped.

---

# Part III — Framework-neutral nodes

## 8. `DialoguePlayActionNode`

Starts one DSL and optionally waits for it to finish.

```csharp
var node = new DialoguePlayActionNode
{
    DslPath = "Assets/Dialogues/intro.txt",
    Interruptible = true,
    SaveState = true
};

DialogueBTStatus status = node.Tick();
```

### Fields

| Field | Type | Purpose |
|---|---|---|
| `DslPath` | `string` | Path passed to `Dialogue_Engine.Play` |
| `Interruptible` | `bool` | Allows a later Play call to replace this DSL |
| `SaveState` | `bool` | Saves interrupted playback for later restoration |

### Non-interruptible behavior

```text
First tick:
  Play rejected/compile failed -> Failure
  Play accepted                -> Running

Later ticks:
  DSL still active             -> Running
  DSL completed                -> Success
  DSL interrupted and discarded -> Failure
```

### Interruptible behavior

```text
First tick:
  Play rejected/compile failed -> Failure
  Play accepted                -> Success immediately
```

The Dialogue Engine continues playing independently after the node succeeds.

This allows following nodes to:

- Monitor emitted events
- Run gameplay logic
- Start another DSL
- Interrupt the current dialogue

### SaveState behavior

```text
Interruptible=false
  SaveState is ignored.

Interruptible=true, SaveState=false
  A later dialogue discards this one.

Interruptible=true, SaveState=true
  A later dialogue pushes this one's playback state onto the resume stack.
```

### Internal completion tracking

For non-interruptible playback, the node stores the database sequence before starting. It then searches newer rows for:

```text
DialogueRuntimeStatus.Completed
DialogueRuntimeStatus.Interrupted with "discarded"
```

### Reset

```csharp
node.ResetNode();
```

Clears its started flag and database checkpoint.

---

## 9. `DialogueHasEventActionNode`

Performs one immediate historical event query.

```csharp
var node = new DialogueHasEventActionNode
{
    DslPath = "Assets/Dialogues/intro.txt",
    EventName = "door_opened",
    SinceSequence = 0
};

DialogueBTStatus status = node.Tick();
```

### Fields

| Field | Type | Purpose |
|---|---|---|
| `DslPath` | `string` | DSL whose database rows are searched |
| `EventName` | `string` | Exact emitted string to find |
| `SinceSequence` | `long` | Ignore rows at or before this sequence |

### Outputs

| Output | Type | Meaning |
|---|---|---|
| `Result` | `bool` | Whether a matching event row exists |
| `Matches` | `List<DialogueEventRecord>` | Matching database rows |

### Return values

```text
Match found    -> Success, Result=true
No match/error -> Failure, Result=false
```

This framework-neutral node is an immediate condition. It does not wait for a currently playing DSL.

### Matching rules

- Dialogue paths are normalized to forward slashes by the database.
- Dialogue path comparisons are case-insensitive.
- Event-name comparisons are case-sensitive.
- `SinceSequence` is exclusive.

---

## 10. `DialogueLiveSnapshotActionNode`

Retrieves the engine's current live state.

```csharp
var node = new DialogueLiveSnapshotActionNode();

if (node.Tick() == DialogueBTStatus.Success)
{
    Debug.Log(node.Snapshot.SectionId);
    Debug.Log(node.Snapshot.Status);
}
```

### Outputs

| Output | Type | Meaning |
|---|---|---|
| `Snapshot` | `DialogueLiveSnapshot` | Strongly typed current state |
| `Message` | `string` | XML-like snapshot message |

### Return values

```text
Snapshot request succeeded -> Success
Service unavailable/error  -> Failure
```

### Snapshot data

Includes:

- Current DSL path and ID
- Current section
- Current speaker/text name
- Current text
- Current runtime status
- Detail message
- Last emitted event
- Latest database sequence
- Whether dialogue is playing

This is immediate and non-blocking.

---

## 11. `DialogueGetEventsActionNode`

Returns historical event/status rows.

```csharp
var node = new DialogueGetEventsActionNode
{
    DslPath = "Assets/Dialogues/intro.txt",
    EventName = "door_opened",
    SinceSequence = 0
};

DialogueBTStatus status = node.Tick();
List<DialogueEventRecord> rows = node.Events;
```

### Fields

| Field | Type | Purpose |
|---|---|---|
| `DslPath` | `string` | Optional dialogue filter |
| `EventName` | `string` | Optional event filter |
| `SinceSequence` | `long` | Exclusive sequence lower bound |

### Output

```csharp
List<DialogueEventRecord> Events;
```

### Return values

```text
Query executed -> Success
Service error  -> Failure
```

A valid query with zero results still returns `Success`.

When `EventName` is empty, the query returns all matching status and event rows for the DSL.

---

## 12. `DialogueGetDslActionNode`

Returns the unique DSL table record.

```csharp
var node = new DialogueGetDslActionNode
{
    DslPath = "Assets/Dialogues/intro.txt"
};

if (node.Tick() == DialogueBTStatus.Success)
{
    Debug.Log(node.Dialogue.PlayCount);
}
```

### Output

```csharp
DialogueScriptRecord Dialogue;
```

The record contains:

- Dialogue ID
- Path
- First start time
- Play count

### Return values

```text
DSL record found     -> Success
DSL record not found -> Failure
```

A DSL is registered only after it successfully compiles and starts in the current Play Mode session.

---

## 13. `DialogueWaitForEventActionNode`

Waits across BT ticks until one event is found or a timeout expires.

```csharp
var node = new DialogueWaitForEventActionNode
{
    DslPath = "Assets/Dialogues/intro.txt",
    EventName = "door_opened",
    SinceSequence = 0,
    TimeoutSeconds = 20f
};
```

Call every BT update:

```csharp
DialogueBTStatus status = node.Tick();
```

### Fields

| Field | Type | Purpose |
|---|---|---|
| `DslPath` | `string` | DSL event source |
| `EventName` | `string` | Required event |
| `SinceSequence` | `long` | Ignore older rows |
| `TimeoutSeconds` | `float` | Failure timeout; zero or less means no timeout |

### Output

```csharp
DialogueEventRecord Match;
```

### Return values

```text
Event absent, timeout not reached -> Running
Event found                       -> Success
Timeout reached                   -> Failure
```

This is blocking only in BT terms. It does not freeze Unity's main thread.

### Reset

```csharp
node.ResetNode();
```

Clears its timer and match.

---

## 14. `DialogueQueryActionNode`

Generic escape hatch for any service request.

```csharp
var node = new DialogueQueryActionNode
{
    Request = new DialogueRequest
    {
        Type = DialogueRequestType.GetEvents,
        DialoguePath = "Assets/Dialogues/intro.txt"
    }
};

DialogueBTStatus status = node.Tick();
DialogueResponse response = node.Response;
```

### Fields

| Field | Type | Purpose |
|---|---|---|
| `Request` | `DialogueRequest` | Complete request object |

### Output

```csharp
DialogueResponse Response;
```

### Return values

```text
Response.Code == Pending -> Running
Response.IsSuccess       -> Success
Null/error response      -> Failure
```

Use this when a dedicated node does not expose the request configuration you need.

---

# Part IV — Native Unity Behavior actions

## 15. Native node list

| Visual node | Framework-neutral implementation |
|---|---|
| Play Dialogue DSL | `DialoguePlayActionNode` |
| Has Dialogue Event | `DialogueHasEventActionNode` plus live snapshot behavior |
| Get Dialogue Live Snapshot | `DialogueLiveSnapshotActionNode` |
| Wait For Dialogue Event | `DialogueWaitForEventActionNode` |
| Get Dialogue Events | Direct `GetEvents` service request |
| Get Dialogue DSL Record | `DialogueGetDslActionNode` |
| Dialogue Service Query | Direct generic service request |

---

## 16. Play Dialogue DSL — Unity Behavior

Menu:

```text
Action > Dialogue > Play Dialogue DSL
```

### Blackboard fields

| Field | Direction | Purpose |
|---|---|---|
| `DslPath` | Input | DSL file path |
| `Interruptible` | Input | Allow replacement |
| `SaveState` | Input | Preserve interrupted state |

### Native status behavior

#### Interruptible off

```text
Compile/start failure -> Failure
Playing               -> Running
Completed             -> Success
```

#### Interruptible on

```text
Compile/start failure -> Failure
Successfully started  -> Success immediately
```

The wrapper explicitly guarantees immediate `Success` for accepted interruptible playback, even though dialogue continues independently.

### Recommended graph use

Non-interruptible cinematic:

```text
Sequence
├── Play Dialogue DSL (Interruptible=false)
└── Continue after full dialogue completion
```

Background/monitorable dialogue:

```text
Sequence
├── Play Dialogue DSL (Interruptible=true)
└── Has Dialogue Event
```

---

## 17. Has Dialogue Event — Unity Behavior

Menu:

```text
Action > Dialogue > Query > Has Dialogue Event
```

### Blackboard fields

| Field | Direction | Purpose |
|---|---|---|
| `DslPath` | Input | Target DSL |
| `EventName` | Input | Target emitted string |
| `SinceSequence` | Input | Ignore older rows |
| `Result` | Output | Whether event exists |
| `MatchCount` | Output | Number of matching rows |

### Native behavior

The native wrapper extends the immediate neutral query into a listener:

```text
Event found
  -> Result=true
  -> MatchCount > 0
  -> Success

Event absent and target DSL currently playing
  -> Result=false
  -> Running

Event absent and target DSL is no longer the active playback
  -> Result=false
  -> Success
```

The wrapper compares normalized snapshot `DialoguePath` with the requested path while waiting.

### Successful false result

`Success + Result=false` means the event question resolved because the target DSL is no longer playing, but no matching event row exists.

### Conditional continuation

Connect the action's successful continuation to Unity Behavior's built-in Conditional Branch:

```text
Has Dialogue Event
└── Conditional Branch: Result == true
    ├── True  -> event-found subtree
    └── False -> no-event subtree
```

The child continuation executes only after the action returns `Success`.

### Parallel listener

To keep other logic running, place the listener flow under one branch of a Parallel node:

```text
Run In Parallel
├── Has Dialogue Event
│   └── Conditional Branch using Result
└── Other independent logic
```

`Running` blocks only the event-listener branch, not Unity's main thread or sibling parallel branches.

### Important caveat

The restored working implementation determines the valid false outcome from the current live snapshot. If a saved DSL is temporarily behind another interrupting DSL, the requested DSL is no longer the current snapshot and can resolve false. For historical-only checks, use the immediate code API or `Get Dialogue Events`.

---

## 18. Get Dialogue Live Snapshot — Unity Behavior

Menu:

```text
Action > Dialogue > Query > Get Dialogue Live Snapshot
```

### Blackboard outputs

| Output | Meaning |
|---|---|
| `DialoguePath` | Active DSL path |
| `Section` | Current section ID |
| `TextName` | Current speaker/choice name |
| `Text` | Current text |
| `IOStatus` | Runtime status string |
| `LastEvent` | Last emitted event |
| `IsPlaying` | Whether dialogue is active |
| `LatestSequence` | Latest DB sequence, clamped to `int` |
| `Message` | XML-like service response |

### Status

```text
Snapshot retrieved -> Success
Request failed     -> Failure
```

It runs once in `OnStart()` and is non-blocking.

---

## 19. Wait For Dialogue Event — Unity Behavior

Menu:

```text
Action > Dialogue > Wait For Dialogue Event
```

### Blackboard fields

| Field | Direction | Purpose |
|---|---|---|
| `DslPath` | Input | Event-producing DSL |
| `EventName` | Input | Event to wait for |
| `SinceSequence` | Input | Ignore older rows |
| `TimeoutSeconds` | Input | Timeout; zero or less means no timeout |
| `MatchedTimestamp` | Output | Matching row timestamp |
| `MatchedSequence` | Output | Matching row sequence, clamped to `int` |

### Status

```text
Waiting       -> Running
Event matched -> Success
Timed out     -> Failure
```

The wrapper resets its internal timer and match in `OnEnd()`.

### Difference from Has Dialogue Event

| Has Dialogue Event | Wait For Dialogue Event |
|---|---|
| Can resolve success with `Result=false` | Only succeeds when event is found |
| Intended for true/false outcome flow | Intended for a required event |
| Exposes `Result` and count | Exposes matched row timing |

---

## 20. Get Dialogue Events — Unity Behavior

Menu:

```text
Action > Dialogue > Query > Get Dialogue Events
```

### Blackboard fields

| Field | Direction | Purpose |
|---|---|---|
| `DslPath` | Input | DSL filter |
| `EventName` | Input | Optional event filter |
| `SinceSequence` | Input | Sequence lower bound |
| `EventCount` | Output | Number of emitted events in the matching rows |
| `HistoryRowCount` | Output | Total number of matching history rows |
| `ResponseMessage` | Output | XML-like rows response |

### Status

```text
Valid query, including zero rows -> Success
Request/service error            -> Failure
```

This is an immediate historical query. `EventCount` counts actual emitted
`@EMIT` rows, while `HistoryRowCount` includes all matching status/history rows
such as `TypingText`, `WaitingForInput`, `Transitioning`, and `Completed`.

---

## 21. Get Dialogue DSL Record — Unity Behavior

Menu:

```text
Action > Dialogue > Query > Get Dialogue DSL Record
```

### Blackboard fields

| Field | Direction | Purpose |
|---|---|---|
| `DslPath` | Input | DSL to look up |
| `Found` | Output | Whether record exists |
| `DialogueId` | Output | Normalized path/ID |
| `PlayCount` | Output | Number of starts this session |

### Status

```text
Record found     -> Success
Record not found -> Failure
```

---

## 22. Dialogue Service Query — Unity Behavior

Menu:

```text
Action > Dialogue > Query > Dialogue Service Query
```

### Blackboard fields

| Field | Direction | Purpose |
|---|---|---|
| `RequestType` | Input | Service operation |
| `DslPath` | Input | Dialogue filter |
| `EventName` | Input | Event filter |
| `SinceSequence` | Input | Sequence lower bound |
| `Matched` | Output | Whether rows matched |
| `ResponseCode` | Output | Numeric response code |
| `ResponseMessage` | Output | XML-like response |

### Status

```text
Response Pending -> Running
Response Ok      -> Success
Other response   -> Failure
```

### Request types

```text
LiveSnapshot
GetDialogue
GetEvents
HasEvent
WaitForEvent
```

Use dedicated actions when possible; use this node for low-level or experimental request flows.

---

# Part V — Blackboard and sequence checkpoints

## 23. Blackboard linking

Any Unity Behavior input/output field can be linked to a compatible Blackboard variable.

Typical Blackboard:

```text
DialoguePath       String
TargetEvent        String
EventResult        Boolean
EventMatchCount    Int32
Checkpoint         Int32
CurrentSection     String
CurrentText        String
CurrentStatus      String
```

## 24. `SinceSequence`

`SinceSequence` prevents older rows from matching.

```text
row.Sequence <= SinceSequence -> ignored
row.Sequence > SinceSequence  -> included
```

A common flow:

```text
Get Dialogue Live Snapshot
  -> save LatestSequence into Checkpoint

Play/continue dialogue

Has Dialogue Event
  -> SinceSequence = Checkpoint
```

This is useful when:

- A DSL is replayed
- The same event can occur multiple times
- A BT node should only react to future emissions

## 25. Event-name rules

Event matching is case-sensitive:

```text
door_opened != Door_Opened
```

Do not include quote characters in Blackboard values:

```text
Correct:   door_opened
Incorrect: "door_opened"
```

---

# Part VI — Graph recipes

## 26. Play a full blocking dialogue

```text
Sequence
├── Play Dialogue DSL
│   ├── Interruptible = false
│   └── SaveState ignored
└── Continue after completion
```

## 27. Start dialogue and continue graph immediately

```text
Sequence
├── Play Dialogue DSL
│   └── Interruptible = true
└── Gameplay action
```

The Dialogue Engine continues playback independently.

## 28. Start dialogue and branch on an event outcome

```text
Play Dialogue DSL
  Interruptible = true

Has Dialogue Event
  DslPath = same path
  EventName = asked_about_crew
  Result = eventResult

Conditional Branch: eventResult == true
├── True  -> Play crew_response.txt
└── False -> Play fallback.txt
```

## 29. Required event with timeout

```text
Sequence
├── Play Dialogue DSL (Interruptible=true)
├── Wait For Dialogue Event
│   ├── EventName = door_opened
│   └── TimeoutSeconds = 20
└── Open Door gameplay action
```

## 30. Parallel listener

```text
Run In Parallel
├── Has Dialogue Event
│   └── Conditional Branch using Result
└── Other main logic
```

The listener's `Running` state does not block the sibling branch.

## 31. Interrupt and resume

```text
Play A
  Interruptible = true
  SaveState = true

Play B
  Interruptible = false
```

B interrupts A. When B completes, A resumes from its captured state.

## 32. Interrupt and discard

```text
Play A
  Interruptible = true
  SaveState = false

Play B
```

A is discarded and never resumes.

## 33. Historical analytics/debug query

```text
Get Dialogue Events
  DslPath = target DSL
  EventName = empty
  SinceSequence = 0
  -> EventCount
  -> HistoryRowCount
  -> ResponseMessage
```

---

# Part VII — Framework adapters

## 34. Adapting to another Behavior Tree package

Map the neutral status:

```csharp
YourTaskStatus Map(DialogueBTStatus status)
{
    switch (status)
    {
        case DialogueBTStatus.Success:
            return YourTaskStatus.Success;

        case DialogueBTStatus.Failure:
            return YourTaskStatus.Failure;

        default:
            return YourTaskStatus.Running;
    }
}
```

Wrap a neutral node:

```csharp
DialogueWaitForEventActionNode node;

void OnStart()
{
    node = new DialogueWaitForEventActionNode
    {
        DslPath = path,
        EventName = eventName,
        TimeoutSeconds = timeout
    };
}

YourTaskStatus OnUpdate()
{
    return Map(node.Tick());
}

void OnReset()
{
    node.ResetNode();
}
```

This pattern works for Behavior Designer, NodeCanvas, or a custom BT implementation.

---

# Part VIII — Troubleshooting

## 35. Missing neutral node types

Errors such as:

```text
DialogueBTStatus could not be found
DialoguePlayActionNode could not be found
DialogueWaitForEventActionNode could not be found
```

mean this file is missing:

```text
DialogueBehaviorTreeNodes.cs
```

The native Unity Behavior wrapper currently depends on it.

## 36. Missing Play overload or Interrupted status

Errors such as:

```text
No overload for Play takes 3 arguments
Operator ! cannot be applied to void
DialogueRuntimeStatus has no Interrupted
```

mean project files are from mismatched versions. Replace these together:

```text
Dialogue_Engine.cs
DialogueService.cs
DialogueBehaviorTreeNodes.cs
DialogueUnityBehaviorActions.cs
```

The expected API is:

```csharp
public static bool Play(
    string path,
    bool interruptible = false,
    bool saveState = false);
```

And `DialogueRuntimeStatus` must include:

```text
Interrupted
Resumed
```

## 37. Has Dialogue Event never matches

Verify:

1. The Console logs the expected `@EMIT` string.
2. `DslPath` exactly corresponds to the path used by Play.
3. `EventName` has no quotes.
4. Event-name casing matches.
5. `SinceSequence` is not newer than the event.
6. `MatchCount` is linked to a Blackboard variable for debugging.

## 38. Has Dialogue Event resolves false too early

The native implementation uses the current live snapshot to decide whether the target DSL is still playing. Verify that:

- The path in Play and Has Event is the same.
- Another DSL has not replaced or interrupted the target.
- You are not querying a suspended DSL behind another active DSL.

For pure historical checks, use `Get Dialogue Events` or the code API.

## 39. Play Dialogue remains Running with Interruptible enabled

Ensure both files are current:

```text
DialogueBehaviorTreeNodes.cs
DialogueUnityBehaviorActions.cs
```

Both layers explicitly return success after accepted interruptible startup.

## 40. Existing graph node behaves incorrectly after code changes

Unity Behavior serializes node definitions into graph assets. If a node class changed significantly:

1. Delete the old node instance.
2. Save the graph.
3. Close and reopen it.
4. Add a fresh node from the Add menu.
5. Reconnect Blackboard variables and children.

Avoid changing a shipped node between `Action` and `Composite` while preserving the same node ID.

---

# Part IX — Node selection table

| Need | Framework-neutral node | Unity Behavior action |
|---|---|---|
| Play DSL | `DialoguePlayActionNode` | Play Dialogue DSL |
| Immediate historical event condition | `DialogueHasEventActionNode` | Use code/neutral adapter |
| Listen until event or target stops | Custom loop around HasEvent | Has Dialogue Event |
| Read current state | `DialogueLiveSnapshotActionNode` | Get Dialogue Live Snapshot |
| Wait for required event | `DialogueWaitForEventActionNode` | Wait For Dialogue Event |
| Query event/status rows | `DialogueGetEventsActionNode` | Get Dialogue Events |
| Query DSL table row | `DialogueGetDslActionNode` | Get Dialogue DSL Record |
| Send arbitrary request | `DialogueQueryActionNode` | Dialogue Service Query |

---

# Part X — Recommended practices

1. Keep DSL paths in shared constants or Blackboard variables.
2. Keep event names consistent and case-correct.
3. Use `SinceSequence` checkpoints for replayable dialogue.
4. Use Play return/status failures for compile/start problems.
5. Use Has Event when both true and false outcomes are valid.
6. Use Wait For Event when the event is mandatory.
7. Use Get Events for debugging and historical logic.
8. Use snapshots for current-state monitoring, not permanent history.
9. Reset stateful neutral nodes when reused.
10. Keep Unity Behavior optional; the code/service API remains the core integration.

---

## Summary

```text
Play Dialogue DSL
  -> starts or waits for a DSL

Has Dialogue Event
  -> Running while target plays without event
  -> Success + Result=true when emitted
  -> Success + Result=false when target stops without event

Get Dialogue Live Snapshot
  -> copies current engine state

Wait For Dialogue Event
  -> Running until match
  -> Success on match
  -> Failure on timeout

Get Dialogue Events
  -> retrieves historical rows

Get Dialogue DSL Record
  -> retrieves DSL registration/play count

Dialogue Service Query
  -> generic request wrapper
```

Together, these nodes let a simple narrative DSL participate in visual AI/gameplay orchestration without moving gameplay logic into the DSL itself.
