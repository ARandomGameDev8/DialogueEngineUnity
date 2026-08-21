# Native Unity Behavior actions

`UNITY_BEHAVIOR_INTEGRATION/DialogueUnityBehaviorActions.cs` contains native
custom actions for Unity Behavior 1.x (`com.unity.behavior`). They inherit from
`Unity.Behavior.Action`, use Blackboard variables, and appear in the graph under:

```text
Add > Action > Dialogue
Add > Action > Dialogue > Query
```

The Unity Behavior package must be installed. Put the integration script in an
`Assets` runtime folder alongside the Dialogue Engine scripts.

## Actions

### Play Dialogue DSL

Inputs:

- `DSL Path`
- `Interruptible`
- `Save State` (only applied when Interruptible is true)

With `Interruptible` disabled, returns Running until the selected play completes.
With `Interruptible` enabled, returns Success immediately after playback starts,
allowing the visual graph to advance to monitoring or interruption actions.
Returns Failure when the Play request is rejected.

### Has Dialogue Event

Inputs: DSL Path, Event Name, Since Sequence.
Outputs: Result and Match Count.

This is now a branching Composite listener rather than a leaf-only action. Attach
`Dialogue Event TRUE Block` as its first child and `Dialogue Event FALSE Block`
as its second child. While unresolved it returns Running, so only its own branch
waits; sibling branches under a Parallel node continue normally.

- Event found: writes Result=true and starts child 1 (TRUE).
- Target DSL completed or was discarded without the event: writes Result=false
  and starts child 2 (FALSE).
- Still unresolved: returns Running and continues checking database history.

Each TRUE/FALSE block is itself a sequence container and may contain multiple
actions. A missing outcome child is treated as a successful no-op.

### Get Dialogue Live Snapshot

Outputs the current DSL path, section, text name, text, IO status, last event,
playing flag, latest sequence, and XML-like service message to Blackboard fields.

### Wait For Dialogue Event

Returns Running until the specific DSL emits the requested event. Returns
Success on a match and Failure on timeout. Unity's main thread is never blocked.

### Get Dialogue Events

Queries status/event history by DSL, optional event name, and sequence checkpoint.
Outputs event count and the service response message.

### Get Dialogue DSL Record

Queries the unique DSL table. Outputs Found, Dialogue ID, and Play Count.

### Dialogue Service Query

Generic visual wrapper around every `DialogueRequestType`. It exposes request
fields and writes Matched, Response Code, and Response Message.

## Example graphs

Use the Has Dialogue Event composite as a listener branch in parallel with other
main-thread graph logic:

```text
On Start
  -> Sequence
      -> Play Dialogue DSL (A, Interruptible=true, SaveState=true)
      -> Run In Parallel Until Any Completes
          -> Has Dialogue Event (A, asked_about_crew)
              -> Dialogue Event TRUE Block       // child 1
                  -> Play Dialogue DSL B2
              -> Dialogue Event FALSE Block      // child 2
                  -> Play Dialogue DSL B1
          -> [other main graph logic]
```

The listener is "blocking" only in Behavior Tree terms: its own branch remains
Running. It never blocks Unity's main thread, the Dialogue Engine, or sibling
branches in the Parallel node. Do not place an ordinary Branch node beside the
listener; the listener now owns and starts its TRUE/FALSE child itself.

For an event that must occur (with timeout failure), use:

```text
On Start
  -> Play Dialogue DSL (intro.txt, Interruptible=true, SaveState=true)
  -> Wait For Dialogue Event (intro.txt, door_opened)
  -> [your Open Door action]
  -> Play Dialogue DSL (entered_room.txt)
```

All native actions delegate to the same framework-neutral nodes and internal
service used by C# code. Code-driven and visually-authored dialogue therefore
share the same database, interruption stack, event history, and snapshots.
