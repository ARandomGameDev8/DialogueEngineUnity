# Visual Layout Components at Runtime

How the visual editor's panel components are driven by `Dialogue_Engine` at
play time, and how play mode is isolated from the source UXML.

---

## 1. Component → runtime pipeline

```text
DialogueLayoutAsset (visual editor)
        |
        v
DialogueVisualLayoutBridge.ApplyToEngine   (edit time via OnValidate, and at play start)
        |
        v
Dialogue_Engine inspector fields
        |
        v
Dialogue_Engine.GenerateUxml  ->  disposable runtime UXML copy  ->  UIDocument
```

The engine owns all runtime content: it writes the dialogue text into the text
panel rendering, the speaker name into the name panel rendering, and the
speaker's image into the icon / character-figure slot (`ShowCharacter` →
`RenderDialogueText` / `RenderName` / `SetPortraitContent`).

Add components to slots in the visual editor (`Tools/Dialogue Editor`). The
first component of each kind drives the corresponding runtime renderer.

## 2. Text panel

Add → *Add Text Panel* in the palette or the slot inspector.

Editable properties: display name, alignment, offset, size, padding, z-layer,
background / border / shadow / opacity, plus:

- **Text style** — colour, font size, font weight, font (via a Resources font
  key), line height, letter spacing, horizontal/vertical alignment.
- **Typewriter** — enabled, characters per second, start delay, character
  audio key.
- **Letter behaviour** (`Letter Effect`) — how letters in words behave:
  - `Wave` — sine offset per letter (amplitude, frequency, phase)
  - `Zigzag` — alternating up/down
  - `Staircase` — incremental step per letter
  - `Shake` — per-letter noise jitter over time
  - `FadeIn` — per-letter alpha ramp
  - `Bounce` — per-letter hop over time
  - Parameters: amplitude, frequency, phase offset, animation speed, loop.
- **TextAnimationProfile** assets (`Base` / `Overlay`) override the inline
  letter-effect values when assigned.

## 3. Name panel

A text panel reserved for character names. Same styling and letter behaviour
as the text panel, plus **Uppercase**. The engine writes the current speaker's
name into it on every line.

## 4. Image panel — icon

`Mode = Icon`: a geometric shape (circle, rounded rectangle, square, diamond,
hexagon) with a fully customizable border (per-side thickness, colour, corner
radii) that fits the image assigned to the speaker inside the shape.
`Uniform Scale` sizes the icon; `Hide When Empty` controls whether the empty
frame stays visible without an image.

## 5. Image panel — character figure

`Mode = CharacterFigure`: a panel that shows the speaker's image and

- sizes itself to the image (`Fit To Image`, aspect-preserving),
- becomes invisible while no image is loaded (`Hide When Empty`),
- never grows beyond its parent container (`Max Size % Of Parent`, 100% = the
  whole parent),
- supports `Fill` (cover-crop) vs `Fit` (contain) and `Flip Horizontal`.

## 6. Speaker emphasis

`Speaker Emphasis` block on the layout asset root:

- `Grey Out Past Speakers` — when a new speaker interrupts, the most recent
  previous speaker stays on screen next to the current one.
- `Active Opacity` — the current speaker's name + image (default fully
  visible).
- `Greyed Opacity` + `Greyed Tint` — how interrupted speakers are greyed out.

## 7. Play-mode UXML isolation

The runtime never modifies the source UXML:

1. On play, `Dialogue_Engine` writes the current layout into
   `Assets/Scripts/Dialogue_Presets/dialogue_runtime_copy.uxml` and
   instantiates that copy (presets are copied the same way).
2. Anything the runtime changes during play lives only in that copy / the
   cloned visual tree.
3. On leaving play mode, `DialogueRuntimeUxmlIsolation` deletes the copy and
   clears all dialogue-UI-carried state (current speaker, section, traversal
   stack, typewriter, history, suspended dialogues) on every engine — also
   covering projects with domain reload disabled.

`Tools/Dialogue/Build Layout` (edit time) still writes the real
`dialogue_generated.uxml` source file.
