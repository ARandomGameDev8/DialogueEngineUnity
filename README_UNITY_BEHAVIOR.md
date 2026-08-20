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

Returns Running until the selected play completes, Success on completion, and
Failure if playback is rejected or the play is interrupted and discarded.

### Has Dialogue Event

Inputs: DSL Path, Event Name, Since Sequence.
Outputs: Result and Match Count.

Success means the specific DSL/event pair exists in the play-session database;
Failure means it does not.

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

## Example graph

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
