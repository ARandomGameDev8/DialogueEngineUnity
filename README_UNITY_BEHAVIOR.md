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

If the event already exists, the action immediately writes Result=true and
returns Success. If the target DSL is currently playing and has not emitted it
yet, the action returns Running and keeps polling on graph updates. If that DSL
finishes without the event, it writes Result=false and returns Success so a
following Branch node can evaluate the result.

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

## Parallel listener with arbitrary TRUE/FALSE subtrees

Unity Behavior 1.x does not expose a public API for third-party nodes to create
the same two named output ports used by its built-in Conditional Branch. A
custom Action/Composite therefore cannot safely add native `True` and `False`
ports without relying on internal editor APIs.

Use one Sequence as the listener thread under the Parallel node. Put Has Dialogue
Event first and Unity's built-in Conditional Branch second. Because Sequence
waits while Has Dialogue Event is Running, the Conditional Branch cannot evaluate
too early. Its native True and False sections accept arbitrary actions,
sequences, parallel nodes, conditions, flow nodes, and subgraphs.

```text
On Start
  -> Sequence
      -> Play Dialogue DSL (A, Interruptible=true, SaveState=true)
      -> Run In Parallel Until Any Completes
          -> Sequence                         // listener thread
              -> Has Dialogue Event
                  DslPath = A
                  EventName = asked_about_crew
                  Result = result
              -> Conditional Branch (result == true)
                  True  -> [arbitrary TRUE subtree]
                  False -> [arbitrary FALSE subtree]
          -> [other main-thread graph logic]
```

The listener Sequence is blocking only in BT terms. Sibling branches in the
Parallel node continue running. The Dialogue Engine and Unity main thread are
not blocked.

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
