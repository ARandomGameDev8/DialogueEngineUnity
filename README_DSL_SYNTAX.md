# Dialogue DSL Syntax Guide

This file explains the **text syntax** accepted by the dialogue compiler in
`Compiler_S.cs`.

If you want the runtime/C# API, read:
- `README.md`
- `CODE_API_DOCUMENTATION.md`

---

# 1. Full minimal example

```text
START
@ENTRY INTRO

var CAPTAIN_NAME = "Captain Mira";
var INTRO_EVENT = "intro_seen";

SECTION INTRO
[NARRATOR]: "Welcome aboard.";
[CAPTAIN_NAME]: "We leave at dawn.";
@EMIT INTRO_EVENT;

CHOICE:
OPTION_0: "Ask about the mission"; goto MISSION; @EMIT "asked_mission";
OPTION_1: "Leave"; goto EXIT;
;
END_SECTION

SECTION MISSION
[CAPTAIN_NAME | captain_portrait]: "The mission is dangerous.";
END_SECTION

SECTION EXIT
[NARRATOR]: "You step away.";
END_SECTION

END
```

---

# 2. Required top-level structure

Every DSL file must begin with:

```text
START
```

and end with:

```text
END
```

If either is missing, compilation fails.

---

# 3. Sections

## Explicit section syntax

```text
SECTION INTRO
[NARRATOR]: "Hello";
END_SECTION
```

Section IDs are plain identifiers such as:
- `INTRO`
- `MISSION_01`
- `CREW_TALK`

## Rules
- section IDs must be unique
- explicit sections must be closed with `END_SECTION`
- choices and character lines must live inside a section

---

# 4. Entry section

Use `@ENTRY` to select which section starts first:

```text
@ENTRY INTRO
```

Example:

```text
START
@ENTRY INTRO

SECTION INTRO
[NARRATOR]: "This runs first.";
END_SECTION

SECTION OTHER
[NARRATOR]: "This exists but does not start first.";
END_SECTION

END
```

## Rules
- `@ENTRY` is optional
- if present, it must point to an existing section
- only one `@ENTRY` is allowed

---

# 5. Variables

Syntax:

```text
var NAME = "value";
```

Example:

```text
var CAPTAIN = "Captain Mira";
var EVENT_NAME = "intro_seen";
```

Variables can be used in places where the compiler resolves text values:
- speaker names
- event names
- portrait keys
- dialogue text (when the whole text is a variable)

Example:

```text
var SPEAKER = "Captain Mira";
var EVENT_NAME = "intro_seen";

SECTION INTRO
[SPEAKER]: "Welcome aboard.";
@EMIT EVENT_NAME;
END_SECTION
```

## Rules
- variable names must be unique
- variable lines must end with `;`

---

# 6. Character dialogue lines

Basic syntax:

```text
[SPEAKER]: "Text here";
```

Example:

```text
[NARRATOR]: "Welcome to the station.";
[Captain]: "We need to move.";
```

## Rules
- line must start with `[` and contain `]`
- text must be inside double quotes
- the whole statement must end with `;`
- empty speaker names are invalid

---

# 7. Portrait syntax

Character lines may include a portrait source/key using `|`:

```text
[SPEAKER | portrait_key]: "Text here";
```

Example:

```text
[Captain | captain_portrait]: "We need to move.";
```

## How portrait keys resolve
The compiler tries this order:

1. If the right side matches a variable name, it resolves to that variable value.
2. Otherwise it is treated as an unresolved portrait key to be assigned in the Inspector.

Example with variable:

```text
var CAPTAIN_FACE = "Assets/Portraits/captain.png";

SECTION INTRO
[Captain | CAPTAIN_FACE]: "Ready?";
END_SECTION
```

Example with unresolved inspector key:

```text
SECTION INTRO
[Captain | captain_portrait]: "Ready?";
END_SECTION
```

That unresolved key is tracked so you can wire it later in Unity.

---

# 8. Event emission

## Standalone event statement

Syntax:

```text
@EMIT "event_name";
```

Example:

```text
@EMIT "door_opened";
```

You may also emit a variable-resolved event:

```text
var OPEN_EVENT = "door_opened";
@EMIT OPEN_EVENT;
```

## Rules
- `@EMIT` must end with `;`
- event string must not be empty
- standalone `@EMIT` may appear inside sections
- standalone `@EMIT` is not allowed inside an open `CHOICE` block

---

# 9. Choices

A choice begins with:

```text
CHOICE:
```

and ends with a line containing only:

```text
;
```

## Full choice example

```text
CHOICE:
OPTION_0: "Ask about the crew"; goto CREW; @EMIT "asked_crew";
OPTION_1: "Leave"; goto EXIT;
;
```

## Rules
- options must be inside a `CHOICE:` block
- the choice block must be closed by a standalone `;`
- nested choices are not allowed
- normal character lines are not allowed inside an open choice block
- standalone `@EMIT` lines are not allowed inside an open choice block

---

# 10. Options

Option syntax:

```text
OPTION_0: "Option text"; goto TARGET_SECTION;
```

Option with inline event:

```text
OPTION_0: "Ask about the crew"; goto CREW; @EMIT "asked_crew";
```

## Rules
- option text must be in double quotes
- each option must have a `goto TARGET`
- option indices must be sequential starting from `OPTION_0`
- inline `@EMIT` is optional

Example valid sequence:

```text
OPTION_0: "A"; goto A;
OPTION_1: "B"; goto B;
OPTION_2: "C"; goto C;
```

Example invalid sequence:

```text
OPTION_0: "A"; goto A;
OPTION_2: "B"; goto B;
```

---

# 11. `goto`

Used inside options:

```text
goto TARGET_SECTION
```

Example:

```text
OPTION_0: "Go to engineering"; goto ENGINEERING;
```

## Rules
- target must name an existing section
- undefined section targets fail validation

---

# 12. Comments

## Line comments

```text
// this is a line comment
```

## Block comments

```text
/*
this is a block comment
that can span multiple lines
*/
```

Comments are ignored by the compiler.

Unclosed block comments fail compilation.

---

# 13. Strings and line breaks

Dialogue text must be in double quotes:

```text
[NARRATOR]: "Hello";
```

Escaped newline text is supported as `\n` inside strings:

```text
[NARRATOR]: "First line\nSecond line";
```

The runtime text becomes:

```text
First line
Second line
```

---

# 14. Implicit section mode

If your file contains **no explicit `SECTION ... END_SECTION` blocks**, the
compiler creates an implicit section named:

```text
SECTION_0
```

Example:

```text
START
[NARRATOR]: "Hello";
[NARRATOR]: "Still valid";
END
```

This behaves as if those lines were inside one auto-generated section.

## Important
As soon as you use explicit `SECTION` blocks, content outside sections is invalid.

---

# 15. Nested sections

The compiler supports nested sections structurally.

Example:

```text
SECTION OUTER
[NARRATOR]: "Outer";

SECTION INNER
[NARRATOR]: "Inner";
END_SECTION

END_SECTION
```

However, for most projects, **flat sections are easier to reason about**.

Recommended style for ordinary use:
- keep sections flat
- use `goto` through choices for branching

---

# 16. Common valid patterns

## Simple linear dialogue

```text
START
SECTION INTRO
[NARRATOR]: "Hello";
[NARRATOR]: "Goodbye";
END_SECTION
END
```

## Dialogue with event emission

```text
START
SECTION INTRO
[NARRATOR]: "The door unlocks.";
@EMIT "door_unlocked";
END_SECTION
END
```

## Dialogue with choice branching

```text
START
@ENTRY INTRO

SECTION INTRO
[NARRATOR]: "Choose.";
CHOICE:
OPTION_0: "Go left"; goto LEFT;
OPTION_1: "Go right"; goto RIGHT;
;
END_SECTION

SECTION LEFT
[NARRATOR]: "You went left.";
END_SECTION

SECTION RIGHT
[NARRATOR]: "You went right.";
END_SECTION

END
```

## Choice with inline event

```text
START
@ENTRY INTRO

SECTION INTRO
CHOICE:
OPTION_0: "Accept"; goto ACCEPTED; @EMIT "quest_accepted";
OPTION_1: "Refuse"; goto REFUSED; @EMIT "quest_refused";
;
END_SECTION

SECTION ACCEPTED
[NARRATOR]: "Accepted.";
END_SECTION

SECTION REFUSED
[NARRATOR]: "Refused.";
END_SECTION

END
```

---

# 17. Common invalid patterns

## Missing `START`

```text
SECTION INTRO
[NARRATOR]: "Hello";
END_SECTION
END
```

Invalid because the file must begin with `START`.

## Missing `END`

```text
START
SECTION INTRO
[NARRATOR]: "Hello";
END_SECTION
```

Invalid because the file must end with `END`.

## Character line missing `;`

```text
[NARRATOR]: "Hello"
```

Invalid because character lines must end with `;`.

## Option outside choice

```text
OPTION_0: "Hello"; goto NEXT;
```

Invalid because options must be inside `CHOICE:`.

## Choice never closed

```text
CHOICE:
OPTION_0: "A"; goto A;
OPTION_1: "B"; goto B;
```

Invalid because the choice block must end with a standalone `;` line.

## Undefined goto target

```text
OPTION_0: "Go"; goto MISSING;
```

Invalid because `MISSING` is not a defined section.

## Multiple same section IDs

```text
SECTION INTRO
END_SECTION

SECTION INTRO
END_SECTION
```

Invalid because section IDs must be unique.

---

# 18. Practical authoring advice

## Keep the DSL narrative-first
Good use of the DSL:
- dialogue text
- branching choices
- event emission

Bad use of the DSL:
- trying to move all gameplay logic into dialogue text files

Use `@EMIT` to signal gameplay systems, then let C#/FSM/BT/quest code decide what happens.

## Good naming style
Recommended:
- section IDs like `INTRO`, `ASK_CREW`, `EXIT`
- event names like `quest_accepted`, `door_opened`, `asked_about_crew`
- variable names like `CAPTAIN_NAME`, `OPEN_EVENT`

---

# 19. Best workflow

1. Write the DSL text file.
2. Use `Dialogue_Engine.Play(path)` to test it.
3. Use `@EMIT` for meaningful gameplay signals.
4. React from C# with:
   - `OnEmit`
   - `Subscribe(...)`
   - `HasEvent`
   - `GetEvents`
   - live snapshot monitoring
5. Keep permanent state in your own save model, not the volatile runtime DB.

---

# 20. Related files

- `README.md` — overall package + API quick start
- `CODE_API_DOCUMENTATION.md` — full C# API reference
- `TEST_DSLS/README.md` — test dialogue pack notes

---

# 21. Syntax checklist

Before compiling, quickly verify:

- file starts with `START`
- file ends with `END`
- every explicit section closes with `END_SECTION`
- every character line ends with `;`
- every choice closes with standalone `;`
- options are sequential (`OPTION_0`, `OPTION_1`, ...)
- every option has a valid `goto`
- every event line ends with `;`
- strings use double quotes
- block comments are closed

If those are right, most compile errors disappear immediately.
