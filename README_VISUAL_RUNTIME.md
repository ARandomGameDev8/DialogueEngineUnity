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

## 8. Exact runtime reproduction (visual layout asset)

When the engine has **Engine Uses This Layout** enabled and a
`DialogueLayoutAsset` assigned, Play no longer approximates the layout through
the engine's inspector fields. Instead `DialogueVisualLayoutRuntimeUxml`
builds the play-mode UXML **directly from the resolved layout** — the same
geometry the editor canvas draws:

- the main panel at its resolved rect with its exact background, per-side
  borders, per-corner radii and opacity,
- every attached area, slot and component at its resolved rect with its exact
  styles,
- the first text panel becomes the live dialogue text, the first name panel
  the live speaker name, the first image panel the live portrait (icon or
  character figure) — the engine keeps writing all text, names and images,
- while this mode is active the engine suppresses its own restyling passes
  (panel resize, portrait frame, name re-flow, character-panel decoration) so
  nothing overwrites the edited layout.

The design canvas is the Panel Settings reference resolution, so keep the
panel's aspect ratio in mind when matching the editor preview 1:1. An
explicitly selected preset still wins over the asset.

## 9. Undo / redo

Every gesture in the visual editor (canvas drag, add/remove, inspector edit)
is recorded as ONE undo group covering both the layout asset and the bridged
engine fields — a single Ctrl+Z reverts the whole gesture everywhere, and
Ctrl+Z / Ctrl+Shift+Z repaint the canvas and re-sync the engine automatically.


If Play ever logs a UXML validation error, the raw generated XML is dumped to
`dialogue_runtime_copy.invalid.txt` and the console prints the offending
lines; the engine then falls back to a working layout instead of leaving the
scene without a UI.
