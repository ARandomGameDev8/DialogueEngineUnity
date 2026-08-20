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
- The node returns `Running` while its DSL is active or suspended.
- It returns `Success` when that DSL completes.
- It returns `Failure` if playback is rejected or its state is interrupted and
  discarded.

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

## Live Snapshot action

```csharp
var node = new DialogueLiveSnapshotActionNode();
if (node.Tick() == DialogueBTStatus.Success)
    Debug.Log(node.Snapshot.SectionId);
```

## Wait For Event action

```csharp
var node = new DialogueWaitForEventActionNode
{
    DslPath = "Assets/Dialogues/intro.txt",
    EventName = "door_opened",
    TimeoutSeconds = 20f
};

// Call from the BT every update. Running does not block Unity's main thread.
DialogueBTStatus status = node.Tick();
```

It returns `Running` until the database contains the requested DSL/event pair,
then `Success`. It returns `Failure` on timeout.

## Other request nodes

- `DialogueGetEventsActionNode` returns matching history rows.
- `DialogueGetDslActionNode` returns the unique DSL table row.
- `DialogueQueryActionNode` accepts any `DialogueRequest` as an escape hatch.

Call `ResetNode()` when your BT framework restarts a node.
