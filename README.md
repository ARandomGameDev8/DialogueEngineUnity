# DialogueEngineUnity

A Unity dialogue system built around a small DSL that is compiled into a graph
of sections. The dialogue **text handles no logic — only narration**. All logic
lives in the already-present architecture around it: plain code, behaviour
trees, or anything else. The bridge between the two is the **@EMIT event
system** plus the **Service-Client interface** described below.

## The DSL

```
START

var my_var = "value"

SECTION meet

[Aria | aria_portrait.png]: "Hello there.";

SECTION shop

[Merchant]: "Buy something?";

CHOICE:
OPTION_0: "Buy"; goto buy; @EMIT lantern_bought;
OPTION_1: "Leave"; goto leave;
;

END_SECTION

END
```

- `SECTION id` … `END_SECTION` — a section (nesting is allowed).
- `[Speaker | image]: "text";` — a line of dialogue.
- `CHOICE:` … `;` — a choice block with `OPTION_n: "text"; goto id;` lines.
- `goto id` — the only way to jump between sections.
- `@ENTRY id` — optional explicit entry section.

### @EMIT — exporting logic as events

`@EMIT <event name>;` is now allowed as a standalone statement **anywhere in a
section** (not only after a goto). It is compiled into an `EventToken` — a
`SyntaxToken` leaf with no children whose only attribute is the **string of the
event that gets emitted**.

Three positions:

| Position | When it fires |
|---|---|
| standalone, inside a `SECTION` | when traversal reaches that point in the text |
| standalone, inside a `CHOICE:` block | when the choice is reached/shown |
| trailing segment of an `OPTION_` line | when that option is chosen (right before the GOTO) |

The event name is always a **string**; when it matches a declared `var`, the
var's value is used instead. See `DIALOGUE_SYSTEM_CORE/SampleScripts/emit_demo.txt`.

## The internal mini database

While playing, the engine writes into an in-memory database
(`Dialogue_Database.cs`) that records what every text does:

- **DSL table** — one row per unique text DSL (script) that was played
  (name, first-seen time, play count, last status).
  *1 text DSL → many event-table rows.*
- **Event table** — PK = `timestamp (minute:second of the play session) +
  text name`, mapping to
  - the **event emitted** (empty string when nothing was emitted), and
  - the **status code of the text**:
    `Idle` · `WaitingForInput` (waiting for IO: Enter / Space) ·
    `TakingChoice` (taking in a choice) · `EventEmitted`.

*1 text has many events; 1 event row always belongs to exactly one text.*

The database **dies with the play session**: it is wiped when play stops or
the play scene exits — but it is shared by every script played in that
session, which is how several scripts communicate with each other through the
engine.

## The Service-Client interface

The engine is a **server**; any other system (plain code or a behaviour-tree
action node) is a **client**. The client sends **HTML-like request messages**
and receives **HTML-like response messages** over one in-process message bus
(the "socket" that is not a socket). The server answers in `Update`.

```
client (code / BT action node)                Dialogue_Engine (server)
   │  <request type="snapshot" .../>              │
   └─────────────────────────────────────────────▶│  Update: drain inbox
   │  <response type="snapshot" status="200" …>  │  poll blocking waits
   ◀─────────────────────────────────────────────┘  deliver responses
```

Request types:

- `type="snapshot"` — live snapshot: where the user is inside the text, IO
  status, current dialogue/choice, last emitted event, recent DB rows.
  Non-blocking.
- `type="query"` — a simple query the server executes against its database:
  `command="events" | "status" | "dsl" | "texts" | "position" | "history"`.
- `type="wait"` — wait for an event to be emitted (`event="…"`) or for a
  message to be displayed (`text="…"`):
  - `blocking="false"` — **one check per request** (for loops): 200 when it
    got what it wanted, 204 when not (call it again next frame).
  - `blocking="true"` — the server keeps polling **conditionally** until the
    answer arrives, `timeout="…"` seconds pass, or the dialogue closes:
    200 / 408 / 503.

Status codes are HTTP-flavoured: `200` got it · `204` not yet · `400` bad
request · `404` unknown text · `408` timed out · `503` dialogue closed /
engine gone.

### Usage — plain code

```csharp
var client = new DialogueClient("my_script");

// live snapshot (non-blocking, read next frame)
client.RequestSnapshot();
if (client.TryGetResponse(out string snapshot))
    Debug.Log(snapshot);

// non-blocking event check — meaningful inside a loop:
void Update()
{
    if (client.CheckForEvent("shop_entered", out string r))
        Debug.Log("got it: " + r);
}

// blocking wait — the server polls until the event, the timeout, or the
// dialogue closes:
client.WaitForEvent("shop_entered", 10f, response => Debug.Log(response));

// query the internal database:
client.RequestQuery("events");        // every event row
client.RequestQuery("status");        // live status of the current text
client.RequestQuery("dsl");           // the DSL table (unique texts)
```

### Usage — behaviour tree

A BT action node holds one `DialogueClient` and either polls
`CheckForEvent` every tick (non-blocking) or drives
`WaitForEventCoroutine` until the node reports finished.

## Files

- `DIALOGUE_SYSTEM_CORE/Compiler_S.cs` — linearizer + compiler (AST with
  `EventToken`, graph building).
- `DIALOGUE_SYSTEM_CORE/Dialogue_Engine.cs` — the runtime engine (UI,
  traversal, database writes, service server).
- `DIALOGUE_SYSTEM_CORE/Dialogue_Database.cs` — the internal mini database.
- `DIALOGUE_SYSTEM_CORE/Dialogue_Service.cs` — the Service-Client message
  bus, the HTML-like protocol, and `DialogueClient`.
- `DIALOGUE_SYSTEM_CORE/File_S.cs` — file wrapper.
- `DIALOGUE_SYSTEM_CORE/SampleScripts/emit_demo.txt` — sample DSL script.
