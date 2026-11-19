# Roundhouse: design

> **Status:** approved 2026-11-18, built in 0.6, Izumo (released as `v0.6.505`) · **Date:** 2026-11-18 · **Release:** VentureOS 0.6, Izumo

Roundhouse is VentureOS's IDE, named after where locomotives are built and serviced. This document says what it is,
why, and how it gets built, so the plan for 0.6 can be made from it.

## Why

- **A designer where there isn't one.** Outside Windows and macOS there's no proper interface designer of the
  WinForms or Interface Builder kind. Roundhouse is one, for VentureOS's apps, and it runs on Linux too.
- **For everyone who makes VentureOS apps:** VentureOS's own apps get built faster, outside authors get a real way
  in, and anyone on Linux who wants a designer has one.
- **Faithful by construction.** The canvas is drawn by VentureOS's own code, so what you design is what the app
  shows.

Prior art: Xcode's Interface Builder and the WinForms designer for the experience; [Passenger](https://github.com/Zaneris/passenger),
an Avalonia IDE, for how to build one in ordinary .NET (its live XAML preview, and headless rendering for tests).

## Goals

1. Open a `.uix` file and see it drawn exactly as VentureOS draws it, beside its text, redrawing as you type.
2. Design an interface visually: select, move, and resize controls; an outline of the document; an inspector for a
   control's attributes; a library of controls to add; window settings; the title bar; menus; and the sidebar. Undo
   and Redo every change.
3. Save without loss: comments, attributes Roundhouse doesn't know, their order, and the layout of the file all stay
   as they were, apart from what was changed.
4. Run an app in a simulated VentureOS desktop inside Roundhouse, built from the same code, with no QEMU.
5. Edit a whole app bundle: its files, its Lua with highlighting, and its `Info.xml` through a form.
6. Run on Linux, macOS, and Windows.

## Non-goals

- The native Roundhouse, running on VentureOS itself. It comes after the desktop version, in a later release, and
  needs platform work first (see the [roadmap](../ROADMAP.md)).
- Debugging, language servers, or completion for Lua. Highlighting is enough for 0.6.
- Designing anything but VentureOS's UIScript: not Hudini's `.uis`, not XAML.
- Packaging or publishing apps; there's no way to install them from outside yet.

## Anti-goals

Roundhouse fails, even if it works, if:

- **The canvas drifts from VentureOS.** An interface that looks right in Roundhouse and different on VentureOS
  makes the designer untrustworthy.
- **Saving damages files.** Rewriting, reordering, or dropping what it doesn't understand, even once.
- **It lets you make documents VentureOS refuses.** Every save is a document VentureOS's reader accepts.
- **It becomes the only way to make apps.** `.uix` and Lua stay files anyone can write by hand, and Roundhouse
  shows the text beside the canvas at all times.

## Constraints

- **The VentureOS projects stay Cosmos-safe.** `Venture.OS.Common` and `Venture.OS.Interface` never reference
  Avalonia or Skia, keep building with the AOT analyzers and warnings as errors, and keep running in the kernel. What
  Roundhouse needs from them that's useful on VentureOS too (the round-trip document model, for the native version
  later) goes in them; anything about Avalonia stays in Roundhouse.
- **Dependencies:** Avalonia, AvaloniaEdit, and AvaloniaEdit's TextMate add-on (`AvaloniaEdit.TextMate`, with
  `TextMateSharp` and its grammars, for Lua highlighting) are approved, for Roundhouse only. SkiaSharp comes with
  Avalonia, which draws with it. Anything else needs approval.
- **The kernel build doesn't change.** `cosmos build` and the ISO are untouched by Roundhouse.
- **Verification:** headless tests render windows off-screen; screenshots of the real app come from the Ubuntu
  distrobox's Xvfb display, as for any graphical app on this machine.
- **License:** the repository's Unlicense. Passenger is MIT: learn from its patterns, don't copy its code.

## Approaches

Every approach starts with the same piece: a painter that draws VentureOS's desktop code into an Avalonia window.
After that, the order of the big parts was the choice.

| Approach | What it trades | |
|---|---|---|
| **Designer first:** live preview, then the round-trip writer, the designer, the simulator, and the editor and form | Goes straight at the gap that motivates Roundhouse; you can't Run until later | **Chosen** |
| Simulator first: VentureOS's desktop in a window, then the designer, then the editor | Useful soonest, but the designer, the heart of it, comes second | |
| Editor first: navigator, Lua editor, and form, then the rest | Easiest start, but a plain editor for a long time, and editors aren't what's missing | |

Designer first was chosen because the missing designer is the reason for Roundhouse. Its first milestone, the
read-only live preview, is useful on its own and proves the painter the canvas is built on.

## Decisions made

- **Release:** Roundhouse is VentureOS 0.6, Izumo, after the Sunrise Izumo sleeper, sharing VentureOS's version,
  as Xcode ships with the SDK it builds for. Its milestones are 0.6's bands, below. 0.7, whatever it holds, is Seto,
  after Izumo's sister train.
- **No `ui` scope.** Opening windows is harmless, so apps no longer list `ui` in `needs`; a bundle or script that
  still says `ui` reads as before, and the word is ignored. Code that isn't an app (shell commands, `init.lua`, the
  `lua` prompt) gets the part of `venture.ui` that needs nothing to stay alive: `alert`, to show a message,
  `preview`, for Quick Look, and `launch`, to open an app. Interactive windows stay with apps, since a window's
  handlers need the code that opened it to keep running. Without the desktop, `alert` prints its message instead.
- **Avalonia 12 and AvaloniaEdit 12,** the current versions; AvaloniaEdit 12.0.0 targets Avalonia 12. Lua is
  highlighted with AvaloniaEdit's TextMate add-on.
- **Close, not pixel-perfect.** The canvas is drawn by VentureOS's code, but text through Skia (see
  [Known limits](#known-limits)); a pixel's difference in text width is accepted.
- **Drawing through Skia,** which Avalonia already uses: a `SkiaPainter` implements `IPainter` over a Skia bitmap
  whose pixels are VentureOS's own `0xAARRGGBB` layout, so `DrawPixels`, `DrawImage`, and `ReadPixels` (for rounded
  corners) are plain memory, and text is drawn with the same Open Sans and Fira Code files VentureOS ships.

## Bands

Each band follows the project's rules: `GitInfo.txt` moves to it when work on it begins, it's planned in phases, and
each phase is committed working.

| Band | Milestone | Done when |
|---|---|---|
| 0.6.1xx | **Preview,** and no `ui` scope | Roundhouse opens a `.uix` and shows it beside its text, redrawing as you type, in light and dark; apps and scripts follow the `ui` decision above |
| 0.6.2xx | **Round trip** | A document model in `Venture.OS.Interface` reads a `.uix`, takes edits, and writes it back with only those edits changed; Hudini's examples and every shipped `.uix` come back byte for byte when nothing changed |
| 0.6.3xx | **Designer** | Select, move, resize, outline, inspector, library, window settings, title bar, menus, sidebar, and Undo and Redo, all saving through the round trip |
| 0.6.4xx | **Run** | A simulated VentureOS desktop in a Roundhouse window runs the bundle being edited, with the system's apps |
| 0.6.5xx | **Bundles** | A navigator for a bundle's files, a Lua editor with highlighting, an `Info.xml` form, and a new app from a template |

## How it fits together

- **`src/Venture.Roundhouse`**, an Avalonia app, in `src/Venture.slnx`, with **`tests/Venture.Roundhouse.Tests`**,
  which drive it headlessly. Namespaces start with `Venture.Roundhouse`.
- **The preview** hosts a `DesktopWindow` around a `UIView` of the document, drawn by `SkiaPainter` into a bitmap
  Avalonia shows, at VentureOS's size, one pixel to one. It loads the document's images and strings the way
  `venture.ui.open` does, and redraws when the text parses; while it doesn't, the last good drawing stays, with the
  reader's message and line.
- **The round trip** is a document model that keeps every token of the file, comments, whitespace, attribute order,
  and quoting, and edits it in place: set or remove an attribute, add, remove, or move an element. After every edit,
  the text is read by `UIScript` again, so the designer never holds a document VentureOS would refuse. A document
  with an element Roundhouse doesn't know opens read-only, saying why, as the roadmap says.
- **The designer** works on the round-trip model, not on `UIScript`'s, so every edit is an edit to the text. Undo and
  Redo are a history of those edits.
- **The simulator** runs `DesktopSession` in a Roundhouse window: Avalonia's pointer and keys become
  `DesktopInput`, a timer runs the frame loop, `AppManager` opens bundles from a temporary copy of `rootfs/` with the
  edited bundle among the user's apps, and the clock is the computer's. Lua runs on ordinary .NET already.

## Left to the builder

- Each band's phases, and what each phase's tests cover.
- The window's layout: where the canvas, text, outline, inspector, and library sit, and whether panels dock.
- Grid and snapping on the canvas, zoom, and multiple selection.
- How the inspector groups a control's attributes.
- The template a new app starts from.
- Whether headless tests use `Avalonia.Headless.XUnit`, if it supports xUnit v3, or `Avalonia.Headless` directly.

## Known limits

- Skia draws and measures text, where VentureOS uses Cosmos's TrueType renderer, from the same font files. A text's
  width can differ by a pixel, and with it anything laid out by width, such as a centred title or where a long label
  is cut; a glyph's edges may differ by a shade. Everything else (positions, sizes, colors, shapes, and images) is
  drawn by the same code and matches exactly.

## Questions

None open.
