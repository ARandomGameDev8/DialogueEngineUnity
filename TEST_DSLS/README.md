# Dialogue DSL test files

Copy this folder into your Unity project's `Assets` directory, for example:

```text
Assets/DialogueTests/
```

Then use paths such as:

```text
Assets/DialogueTests/01_standalone_events.txt
```

## Test 1 — standalone events

Play `01_standalone_events.txt`. Expected emission order:

```text
standalone_test_started
standalone_checkpoint_reached
standalone_test_finished
```

Check live notifications with `Dialogue_Engine.OnEmit`, and afterward verify any
of these with a Has Dialogue Event action using this DSL's path.

## Test 2 — choice events

Play `02_choice_events.txt`.

Forest route:

```text
choice_forest_selected
forest_section_entered
choice_test_finished
```

City route:

```text
choice_city_selected
city_section_entered
choice_test_finished
```

This verifies both inline OPTION `@EMIT` and standalone section `@EMIT`.
While the choice is visible, press Space, Return, and Numpad Enter. All three
must be ignored; only clicking an option may select it.

## Test 3 — variable events

Play `05_variable_event.txt`. Expected:

```text
variable_event_started
variable_event_finished
```

This confirms the compiler resolves event-name variables into EventToken strings.

## Test 4 — Unity Behavior interruption and resume

Create this Unity Behavior sequence:

```text
Play Dialogue DSL
  DslPath = Assets/DialogueTests/03_interruptible_primary.txt
  Interruptible = true
  SaveState = true

Wait For Dialogue Event
  DslPath = Assets/DialogueTests/03_interruptible_primary.txt
  EventName = primary_started
  TimeoutSeconds = 5

Play Dialogue DSL
  DslPath = Assets/DialogueTests/04_interrupting_dialogue.txt
  Interruptible = false

Wait For Dialogue Event
  DslPath = Assets/DialogueTests/03_interruptible_primary.txt
  EventName = primary_finished
  TimeoutSeconds = 60
```

Expected behavior:

1. The first Play action starts the primary DSL and immediately returns Success
   because `Interruptible` is enabled.
2. The Wait action finds `primary_started`.
3. The second Play action interrupts the primary DSL and returns Running because
   the interrupting DSL is non-interruptible.
4. Finish the interrupting dialogue through user input.
5. The engine resumes the saved primary dialogue.
6. Finish the primary dialogue; the final Wait action returns Success.

Expected database/event evidence includes:

```text
primary_started
interruption_started
interruption_finished
primary_resumed_checkpoint
primary_finished
```

The database also records `Interrupted`, `Resumed`, and `Completed` statuses.

## Test 5 — interruption without Save State

Use the same graph, but set the first Play action to:

```text
Interruptible = true
SaveState = false
```

After the interrupting dialogue finishes, the primary dialogue must not resume.
`Has Dialogue Event(primary_finished)` should return Failure.

## Play-action BT behavior

- `Interruptible = false`: the Play action returns Running until its DSL ends.
- `Interruptible = true`: the Play action returns Success as soon as the DSL is
  successfully compiled and started. Playback continues independently, allowing
  later BT actions to monitor or interrupt it.
- A rejected Play request returns Failure.
