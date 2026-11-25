# A value slider

**Status:** proposed for 0.8.4xx, Hokutosei. **Date:** 2026-11-25.

UIScript has no control for choosing a value. `<Slider>` came from Hudini, which had it from CityLimits' older C#
code, which followed TSO's UIScript, where a list's scroll bar was a slider bound to it: one control doing two jobs.
0.8.3xx gave lists and text areas scroll bars of their own and took `<Slider>` out, leaving a `<ProgressBar>` between
− and + buttons as a stopgap. This design brings `<Slider>` back with one job: choosing a number between a minimum
and a maximum, for VentureOS's apps and Hudini's games equally, by the owner's decisions.

It's the last part of 0.8.4xx, after `vdb mcp` and toasts.

## Why

- Settings, apps, and games need to choose a value: a volume, a size, a tax rate. − and + buttons beside a progress
  bar take many presses and show no handle.
- The old slider's second job, scrolling, is done by scroll bars now, so a slider can be designed for its first.
- Hudini still reads `<Slider>`. A slider both read the same way keeps VentureOS's interfaces and Hudini's games on
  one vocabulary.

## Goals

- **A value slider**: a track and a thumb, choosing a number from `minValue` to `maxValue`, as `<ProgressBar>` names
  them.
- **Both readers alike**: a slider that uses nothing new reads the same in VentureOS and in Hudini; anything new is
  safe for a reader that doesn't know it to ignore.
- **Optional `step`**: the thumb snaps to whole steps, so an app only ever sees those values, such as whole percents.
- **Two events**: `changed` as it moves, and `committed` once when it's let go, so a volume can be heard while
  dragged and a game can recompute only at the end.
- **Direction from shape**: wider than tall runs left to right; taller than wide, bottom to top.
- **The keyboard**: once it has focus, the arrow keys move it by a step, or a tenth of the range without one; Home
  and End go to the ends.
- **The look**, by the owner's choice from the mockups: libadwaita's thin rail and round white knob, with the rail's
  filled part given Tiger's gloss, a bright band over its top half and a sheen along its top edge, over the accent,
  and the empty part sunken. A progress bar takes the same rail.
- App tests and agents can move one by name.

## Non-goals

- **Scrolling.** `scrolls` and `direction` stay retired, and a document with them stays refused.
- **Tick marks**, labels along the track, and a range with two thumbs.
- **`orientation`.** Shape decides, as the old slider did without it; `orientation` stays ignored and listed, as now.
- **Hudini's side.** Hudini draws its own sliders and adopts `step` and `committed` when it follows; nothing here
  changes Hudini.

## Anti-goals

- **A new meaning for an old name.** `minValue`, `maxValue`, and `changed` mean what they meant; a slider Hudini
  reads today must behave the same here.
- **A slider that can show what it can't choose.** The value is always within the range and on a step.
- **Two controls in one again.** A progress bar shows a level and a slider chooses one; they share drawing, not a
  type.

## Approaches considered

| | Approach | Trade-off |
|---|---|---|
| **Chosen** | **`<Slider>` again**, a value only, with an optional `step` | Hudini reads it already, so games and VentureOS share it; a reader before `step` still gets a working, unsnapped slider. |
| | A new name, such as `<ValueSlider>` | No history to step around, but Hudini wouldn't read it, so the two would part on the most basic value control. |
| | `<ProgressBar>` made draggable | One fewer element, but two jobs in one control again, the lesson the scroll bar split taught. |

The owner chose the first.

## How it works

### UIScript

```xml
<Slider name="volume" x="16" y="40" width="200" height="20" minValue="0" maxValue="100"/>
<Slider name="taxes" x="16" y="70" width="200" height="20" minValue="0" maxValue="20" step="1"/>
```

| Attribute | Required | Value |
| --- | --- | --- |
| `minValue`, `maxValue` | No | The range, `0` to `1` by default, as for `<ProgressBar>`. `minValue` below `maxValue`, or the document is refused. It starts at `minValue`. |
| `step` | No | A positive number: the thumb snaps to `minValue` plus whole steps. One that's not positive, or larger than the range, refuses the document. Without it, any value in the range. |

- It runs along its longer side; a square one runs left to right.
- `scrolls`, or `direction`, still refuse the document, with the reason the reader gives today, reworded for a
  slider that's back: say it scrolls nothing now, and that lists and text areas scroll themselves.
- **Compatibility:** a reader from before `step` ignores it and lists it, giving a slider that doesn't snap: plainer,
  but still correct, since the app gets values in the same range. A reader from before `committed` never sends it; an
  app that waits only for it then hears nothing, so the spec says to listen for `changed` when a reader may not know
  `committed`. The spec stays version 1, a draft, as with the slider's removal.

### Events

- **`changed`**, with the value, as the thumb moves: a drag reports each new value (a step's, with `step`), a click
  on the track jumps the thumb there and reports it, and a key reports the new value.
- **`committed`**, with the value, once when a drag or a track click ends, and after each key, so an app that only
  wants the final value has one place to listen.
- A value that doesn't change, such as a drag that stays on one step, reports nothing.
- `UIEvent` gets its `Value` back, and the Lua event table its `value`. `window:setValue(name, number)` and
  `window:value(name)` work on sliders as on progress bars; setting snaps to a step, keeps within the range, and
  reports nothing, as setting a field's text doesn't.

### Drawing

- The rail is 6 pixels tall (or wide, upright), centered on the slider's frame, with round ends. Its empty part is
  sunken, a gradient darker at the top with an inner shadow line; its filled part, from the minimum to the thumb, is
  the accent with Tiger's gloss: the bright band over its top half and the sheen along its top edge, from the
  palette's `MenuBarGloss` and `MenuBarSheen`.
- The knob is 18 pixels, round, white, with a soft shadow; focused, it gets the focus ring other controls get. Dimmed,
  the slider is drawn at the disabled opacity and takes no input.
- `<ProgressBar>` draws the same rail and fill, without a knob.
- New color roles, if the existing ones don't serve, for the sunken rail in light and dark; they join the contrast
  checks for parts (3 to 1 for the knob against the rail).

### Tests, agents, and Roundhouse

- airo gets `app:slide(name, value)` back: it sets the value as a drag would, snapped and kept in the range, and
  sends `changed` then `committed`. `app:value(name)` reads a slider's value too.
- `WindowControls.TrySlide` holds that, shared with vdb, whose `act` gains `slide <name> <value>`, and `vdb mcp`'s
  `act` tool a `slide` action with `value`. A snapshot shows a slider's `value`.
- Roundhouse's library offers Slider again, its inspector shows `minValue`, `maxValue`, and `step`, and its designer
  draws it.
- VentureOS's copies of Hudini's examples get their sliders back, in place of the progress bar between − and +
  buttons, as Hudini has them, so the copies come closer to Hudini's originals; their README says so.

### The HIG

The Depth section's gloss rule changes, by the owner's choice: the menu bar, the window buttons, and the filled part
of sliders and progress bars. Structure gains a Slider entry: what it's for, `step`, the two events, and when to use
a slider rather than − and + buttons.

## Where the code goes

- `src/Venture.OS.Interface/`: `UIControlType.Slider` and `step` in `UIScript`, the reader's checks, and
  `UIScriptSchema`; the spec in `docs/uiscript/`, with its change log and Readers section.
- `src/Venture.OS.Common/Desktop/`: `UIView` (state, input, keys, drawing, the shared rail), `UIEvent.Value` and
  `Committed`, `WindowControls.TrySlide` and the snapshot's value, the palette roles if needed.
- `src/Venture.OS.Common/Desktop/UiLibrary.cs`: the Lua event's `value`, `setValue` and `value` on sliders.
- `src/Venture.Airo.Kit/Testing/`: `app:slide`.
- `src/Venture.OS.Common/Vdb/` and `tools/vdb/`: `act … slide`, the tool's `slide` action, conversations.
- `src/Venture.Roundhouse/`: the library, inspector, and designer.
- `tests/`: the reader, events, keys, snapping, drawing, contrast, `app:slide`, vdb, and the copies of Hudini's
  examples.
- Docs: the HIG, the spec, the tutorials if they mention values, the architecture page, the scorecard, the roadmap.

## Risks

- **The progress bar's new look** changes every app that shows one, such as System Monitor; it's the owner's choice,
  and contrast tests check it.
- **Hudini and VentureOS diverge on `step` and `committed`** until Hudini follows; a game relying on either behaves
  differently there.
- **Software drawing** of a gradient rail is more per frame than a flat bar; small, but measured on a boot.

## Calls made in writing it

The owner decided: a value slider, for apps and games equally; `<Slider>` again; optional `step`, no ticks;
`changed` and `committed`; direction from shape; the keyboard; libadwaita's look with Tiger's gloss on the rail's
fill, shared by the progress bar, and the HIG's gloss rule changing with it. These were the writer's calls:

- A click on the track jumps the thumb there; `committed` follows each key press.
- `setValue` snaps and clamps, and reports nothing.
- `step` larger than the range, or not positive, refuses the document.
- airo's verb is `app:slide` again, and vdb's action `slide`.
- No shipped app changes to use a slider in this band; the copies of Hudini's examples get theirs back.

## Left to whoever builds it

- Exact gradient stops for the sunken rail, within the mockup's look.
- Whether a click on the track jumps or pages toward the pointer; jumping is proposed above, as libadwaita does.
- The phases of the rollout.
