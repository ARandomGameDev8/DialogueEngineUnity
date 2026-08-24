# Behavior Tree integration

The repository provides framework-neutral action-node logic in
`DialogueBehaviorTreeNodes.cs`. Map `DialogueBTStatus.Running`, `Success`, and
`Failure` to the equivalent values in your BT package.

## Play action

```csharp
var node = new DialoguePlayActionNode
{
    DslPath = "Assets/Dialogues/intro.txt",
    Interruptible = true,
    SaveState = true
};

DialogueBTStatus status = node.Tick();
```

- The first tick calls `Dialogue_Engine.Play`.
- With `Interruptible = false`, the node returns `Running` until its DSL ends,
  then `Success`.
- With `Interruptible = true`, the node returns `Success` immediately after the
  DSL is successfully compiled and started. The engine keeps playing it in the
  background, so following BT actions can monitor or interrupt it.
- It returns `Failure` when the Play request is rejected.

`Interruptible = false` means every later Play request is rejected until this
DSL completes.

`Interruptible = true, SaveState = false` means a later Play request discards
this DSL.

`Interruptible = true, SaveState = true` means a later Play request pushes this
DSL's line-level playback snapshot onto the engine's LIFO resume stack. When the
interrupting DSL completes, the engine pops and resumes it. Nested interruptions
are supported.

The custom Unity property drawer only displays `SaveState` when `Interruptible`
is enabled.

## Has Event condition

```csharp
var node = new DialogueHasEventActionNode
{
    DslPath = "Assets/Dialogues/intro.txt",
    EventName = "door_opened"
};

// Success = found in that DSL's play-session DB rows; Failure = not found.
DialogueBTStatus status = node.Tick();
```

## Listen for multiple targeted events

```csharp
var node = new DialogueListenForMultipleEventsActionNode
{
    DslPath = "Assets/Dialogues/intro.txt",
    TargetEvents = "asked_about_crew, insulted_captain, ended_conversation"
};

DialogueBTStatus status = node.Tick();
```

- `TargetEvents` accepts comma, semicolon, pipe, or newline separators.
- While the DSL is still alive and none of the targets were emitted, the node
  returns `Running`.
- If one of the target events is emitted, the node returns `Success` and writes
  the FIRST matched event to `MatchedEvent` and its sequence to
  `MatchedSequence`.
- If the DSL reaches end-of-life without emitting any target event, the node
  returns `Success` with `MatchedEvent = ""`.
- Invalid input or missing runtime state returns `Failure`.

## Live Snapshot action

```csharp
var node = new DialogueLiveSnapshotActionNode();
if (node.Tick() == DialogueBTStatus.Success)
    Debug.Log(node.Snapshot.SectionId);
```

## Blocking live snapshot watcher

```csharp
var node = new DialogueBlockingLiveSnapshotActionNode
{
    DslPath = "Assets/Dialogues/intro.txt"
};

DialogueBTStatus status = node.Tick();
```

- While the requested DSL is alive, the node returns `Running` and keeps
  updating `Snapshot`.
- When that DSL reaches end-of-life without input/service errors, it returns
  `Success`.
- Invalid input or missing runtime state returns `Failure`.

## Other request nodes

- `DialogueGetEventsActionNode` returns matching history rows.
- `DialogueGetDslActionNode` returns the unique DSL table row.
- `DialogueQueryActionNode` accepts any `DialogueRequest` as an escape hatch.

Call `ResetNode()` when your BT framework restarts a node.
