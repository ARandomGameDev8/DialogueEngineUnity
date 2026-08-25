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

Warning: this remains a query-style action. Do not build high-frequency generic
monitoring loops around it when dedicated live event registration exists for
continuous monitoring.

### Listen For Dialogue Events

Inputs: DSL Path and `TargetEventEnum`.
Outputs: `MatchedEvent`, `MatchedEventName`, and `MatchedSequence`.

`TargetEventEnum` must be a Blackboard enum variable of your own enum type. The
wrapper reads that enum type's member names as the target event set and writes
back to `MatchedEvent` using the SAME enum type, so a Switch node can branch on
your actual enum members.

Recommended pattern for your enum:

- `None = 0` or `NoMatch = 0`
- one member per target event, for example `AskedAboutCrew`
- names that normalize to the event text, for example:
  - DSL event `asked_about_crew`
  - enum member `AskedAboutCrew`

The action keeps listening to the latest play of that DSL:

- Running while the DSL is still alive and none of the enum-defined targets were emitted.
- Success with `MatchedEvent` set to the matched enum member and `MatchedEventName` set to the emitted event string.
- Success with `MatchedEvent` left at `None`/`NoMatch` if the DSL ends without any target match.
- Failure on actual input/service/runtime errors, or if the input/output enum variable types do not match.

### Get Dialogue Live Snapshot

Outputs the current engine snapshot immediately: current DSL path, section, text
name, text, IO status, last event, playing flag, latest sequence, and XML-like
service message.

Warning: this is still a one-shot snapshot action. Use dedicated live snapshot
registration for continuous monitoring instead of generic loop polling.

### Get Dialogue Live Snapshot Blocking

Input: DSL Path.
Outputs: Dialogue path, section, text name, text, IO status, last event,
playing flag, latest sequence, and message.

This action behaves like a blocking watcher in BT terms:

- Running while the requested DSL is still alive.
- Success when that DSL reaches end-of-life without actual errors.
- Failure on actual input/service/runtime errors.

While Running, it keeps updating the Blackboard snapshot fields.

### Get Dialogue Events

Queries status/event history by DSL, optional event name, and sequence checkpoint.
Outputs emitted event count, underlying history row count, and the service
response message.

### Get Dialogue DSL Record

Queries the unique DSL table. Outputs Found, Dialogue ID, and Play Count.

### Dialogue Service Query

Generic visual wrapper around every `DialogueRequestType`. It exposes request
fields and writes Matched, Response Code, and Response Message.

## Example graphs

### Branch off one specific event

```text
On Start
  -> Sequence
      -> Play Dialogue DSL (intro.txt, Interruptible=true, SaveState=true)
      -> Has Dialogue Event (intro.txt, asked_about_crew -> Result)
      -> Branch On Result
          False -> Play fallback.txt
          True  -> Play crew_response.txt
```

### Branch off several possible targeted events

```text
On Start
  -> Sequence
      -> Play Dialogue DSL (intro.txt, Interruptible=true, SaveState=true)
      -> Listen For Dialogue Events
           DslPath = intro.txt
           TargetEventEnum = DialogueOutcome enum variable
           -> MatchedEvent
      -> Switch / compare MatchedEvent
           AskedAboutCrew -> Play crew_response.txt
           SkippedTopic   -> Play alternate.txt
           None           -> Play timeout_or_default.txt
```

All native actions delegate to the same framework-neutral nodes and internal
service used by C# code. Code-driven and visually-authored dialogue therefore
share the same database, interruption stack, event history, and snapshots.
