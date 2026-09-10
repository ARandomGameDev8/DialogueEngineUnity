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

There is **one single builder** and the **visual editor owns it**. The engine
never re-derives or approximates your layout at play time:

1. `DialogueVisualEditorUxml` (owned by the visual editor) builds THE
   canonical UXML from the asset's resolved layout — the same rects, borders,
   radii, opacity and text styles the editor canvas draws. The canonical file
   lives next to the asset as `<AssetName>_dialogue_ui.uxml`.
2. The visual editor rewrites that file automatically after every edit
   gesture (drag, inspector change, add/remove, undo/redo).
3. At Play, `Dialogue_Engine` takes a **byte-for-byte copy** of that exact
   file (refreshed by the same single builder only if the editor window
   wasn't open) and instantiates it through the disposable runtime copy.
4. While this mode is active the engine suppresses its own restyling passes
   (panel resize, portrait frame, name re-flow, character-panel decoration)
   so nothing overwrites the edited layout.
5. **Cast slots are indexed.** EVERY image panel and EVERY name panel in the
   layout is live, ordered by layout position. The k-th speaker (order of
   first appearance) owns the k-th name panel + the k-th image panel — 3
   characters, 3 panels: character 2's name/figure never lands on panel 1.
   The speaking character renders at full opacity; interrupted speakers stay
   on their own panels greyed out (`Inactive Portrait Opacity` + tint on the
   engine, both adjustable). Panels with no assigned speaker stay hidden, and
   a figure panel marked *hide when empty* only appears once its image loads.
6. **Tools/Dialogue/Open Visual Layout Preview** is a TRUE preview: it clones
   the exact same built UXML file with UI Toolkit at the Panel Settings
   reference resolution — what you see in that window is literally the tree
   Play instantiates.

Requires **Engine Uses This Layout** on the engine (the editor warns when it
isn't). An explicitly selected preset still wins over the asset. For player
builds, put the canonical UXML into a Resources folder as
`Dialogue_Presets/dialogue_generated`.

## 9. Undo / redo

Every gesture in the visual editor (canvas drag, add/remove, inspector edit)
is recorded as ONE undo group covering both the layout asset and the bridged
engine fields — a single Ctrl+Z reverts the whole gesture everywhere, and
Ctrl+Z / Ctrl+Shift+Z repaint the canvas and re-sync the engine automatically.


If Play ever logs a UXML validation error, the raw generated XML is dumped to
`dialogue_runtime_copy.invalid.txt` and the console prints the offending
lines; the engine then falls back to a working layout instead of leaving the
scene without a UI.

## 10. Choice event UI (visual editor)

Plain bland choice buttons are gone — the choice event gets the same design
treatment as everything else:

- Enable **Choice Panel Enabled** in the layout root inspector. A purple
  panel appears on the canvas: that is the choice event panel, at the exact
  rect and with the exact styles Play will use. It has the FULL main-panel
  customization surface — anchor, fill mode, size, min/max, padding,
  background, border, shadow, opacity, z-layer. In the Choice Panel
  inspector the panel's own surface is the **Choice Panel Surface (this
  panel)** section at the TOP, right where "Main Panel Style" sits for the
  main box; the **Choice Button Preset** further down styles the buttons
  only, never the panel.
- A panel's **border stays visible above its children** (canvas and Play):
  the border is drawn/emitted on its own overlay layer, so a region or slot
  that covers the panel cannot hide it. Padding decides how much of the
  panel's background shows around the region — set Padding 0 and the region
  fills the panel completely, hiding the background (the border still
  shows). While the Choice Panel / Main Panel is selected the region stops
  tinting it, so colour and border edits are plainly visible.
- Background **Mode = None** means the element paints NOTHING (the canvas
  only shows a placeholder tint so you can see it while editing). Picking a
  colour switches the mode to Solid Colour for you, and the same goes for a
  border: editing a colour/thickness on an `Enabled = false` or fully
  transparent border switches it on.
- Its **Choice Region** IS an inner region — exactly the same object, the
  same resolver math and the same inspector as the **Inner Region** of the
  main panel: Display Name, Enabled, Width/Height (percent of the panel!),
  Offset, Partition Level 0-2, inter-slot spacing and Z Layer. Slots cannot
  be partitioned further. A disabled region resolves to nothing.
- **Region styling and slot styling are fully independent.** A slot keeps
  its own background, border, shadow and opacity; the region keeps its own,
  and changing one never changes the other. Adding partition pieces only
  creates the slot definitions. Two buttons in the region inspector are the
  only ways to touch slot data: **Copy Region Style To Slots** (writes the
  region's surface onto the visible slots — opt-in, never automatic) and
  **Reset Slot Sizes/Offsets To Auto** (geometry only, colours untouched).
  Note that a slot with Background = None is *transparent*, so the region's
  colour is visible through it — give the slot its own background to make it
  opaque. The same applies to attached areas.
- **Partition Level 1+ turns every NON-holder slot into one choice option**
  ("one slot = one option", counted in order). Those slots behave exactly
  like any other slot: add components, style them, move/resize them, select
  them from the hierarchy or canvas. The **first Text Panel** in the slot is
  that option's live label — at Play the option text is written into it with
  its complete text style (colour, font size, spacing, weight, alignment) —
  and clicking the slot picks that option. Padding and offset per slot work
  as everywhere else, and slots past the option count hide. The Choice Panel
  inspector lists the option slots and warns when one has no Text Panel.
- The region slots: **choose which one holds the buttons** ("Holds The Choice
  Buttons" on the slot, or the Button Holder popup on the panel). Default is
  the bottom-most slot (or the slot itself at partition level 0); the other
  slots hold whatever components you like. Region orientation Vertical
  (stacked rows, default) or Horizontal (columns). The holder slot cannot
  take components (it renders buttons only) — the visual editor says so
  instead of silently adding something that would never show.
- The holder slot IS the choice area — ONE slot inside the choice region,
  in its rightful place (no ghost panels, no group boxes). Its content
  partitions **AUTOMATICALLY**: at Play the ACTUAL option count decides the
  arrangement (up to 6: rows of 1, then rows of 2 — 3 choices stack
  vertically, 6 fill a 3x2 grid). The engine computes it with the exact
  same resolver math as the editor, so Play is the layout you designed for
  that count. While designing, **Preview Choice Count (0-6)** shows any
  hypothetical count INSIDE the holder slot. The choice subtree draws ABOVE
  the main panel and portraits while editing — its exact runtime z-order.
- The **Choice Button Preset** (on the Choice Panel inspector) styles every
  button: background, border, shadow, opacity, text padding, full text style,
  hover colour. Every instance shares it EXACTLY. Sizing: **Fixed** = one
  width/height relative to the **button's cell** (the holder split into the
  rows/columns the arrangement needs), identical for all buttons;
  **Variable** = each button may set its own width/height (select it on the
  canvas) — the ONLY per-instance difference. Either way 100% fills the cell
  and the resolved size is clamped to the cell, then to the holder's content
  rect: a button can never be wider or taller than the container it lives in,
  so 4+ options (rows of 2) stay inside the holder instead of overflowing it.
- At Play the panel is hidden until a choice fires; each option's text lands
  on its button AND on its option slot's label, unused buttons and unused
  option slots hide, and clicking either a button or an option slot picks
  that option. More options than buttons logs a warning and shows the first N
  (the DSL itself is not limited).
- Shadows are an editor-side preview only (UI Toolkit has no box-shadow); a
  panel/region/slot shadow shows on the canvas but is not written to the UXML,
  for the main panel and the choice panel alike.
- The **True Preview** window has a **Peek Choice Panel** toggle so you can
  design the panel without running a dialogue.

Everything follows the one-builder rule: the visual editor writes the
canonical UXML (including the hidden choice panel), and Play instantiates
the exact same file.
