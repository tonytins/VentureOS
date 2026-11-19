# The dock and the menu bar as apps

**Status:** built in 0.7.3xx, Seto; the [architecture](../architecture.md) and [writing apps](../writing-apps.md) say how it works now. The bar's panel is its own `menubar.uix` in Files, beside `main.uix`, and a desktop without a bar app, as in tests, still draws the bar itself.
**Date:** 2026-11-18.

The dock becomes `Dock.app`, a system app written in Lua like Files, instead of code inside the desktop, and the
global menu bar, which Files stays running for, becomes a panel Files hosts. To make that possible, VentureOS gains
two things any app can use: **panel windows**, which the window manager pins to an edge of the screen and keeps
other windows clear of, and a **shelf**, a row of icons. The menu bar needs nothing new in the format: it's a top
panel, and its menus are the `menus` module's, as now.

## Why

- **The dock gets in the way.** It's drawn by `DesktopSession` after everything else, outside the window manager, so
  nothing knows it's there: a zoomed window runs underneath it, losing its bottom rows and resize corner, and a
  window near the bottom has its contents covered with no way out.
- **One less way for the desktop to fail.** A fault in the dock is today a fault in the desktop. As an app, it's one
  app's, and the desktop restarts it as it restarts Files.
- **Easier to change.** What the dock shows and does becomes a Lua app's business, which a later release, or someone
  else, can change without touching the desktop.
- **A bigger toolkit.** Panels and shelves are widgets every app gets, as sidebars were.
- **The menu bar is the same problem, at the top.** It's drawn by `DesktopSession` too, and the space under it is a
  number (`WindowManager.TopInset`) rather than something the window manager knows. As a panel, both edges work the
  same way.

## Goals

- `Dock.app`, in `rootfs/system/apps`, does what today's dock does: the system's apps, then the user's, then
  minimized windows, in groups with a divider between; each app's icon, or a colored tile with its initial; a dot
  under open apps; the name of the icon under the pointer; a click opens an app or restores a window.
- Three ways for the dock to behave, chosen in Settings:
  - **Always visible** (the default): zoom and newly opened windows stop above it. A window can still be dragged
    under it on purpose, and the dock stays on top, as on macOS.
  - **Hide automatically:** it slides away, and comes back when the pointer reaches the bottom edge.
  - **Hide when covered** ("dodge"): it's shown unless a window overlaps it, and comes back at the bottom edge too.
- The desktop keeps the dock running, restarting it if it stops, and never quits it.
- The menu bar is a top panel in Files' `.uix`, which looks and works as the bar does today: the VentureOS menu, the
  front app's name and menus, the gloss, and the clock.
- `DesktopSession` no longer draws or holds a dock or a menu bar.

## Non-goals, for now

- Keeping apps in the dock, reordering them, menus on icons, a Trash, or dragging files onto icons.
- Docks on the left or right edge, or more than one panel at once. The panel role names an edge so they can come.
- Magnifying icons, or bouncing while an app opens.

## Anti-goals

- **A desktop that can't be got past.** If Dock.app can't run, Files' menus are in the bar, and File ▸ New Window
  reaches every app. If Files can't run, the dock still opens apps. Both are kept processes, which the supervisor
  restarts at once when they end (see [processes](./2026-11-18-processes-design.md)); nothing in the desktop may
  wait on either.
- **Dock-only machinery.** Whatever the dock needs from the platform is a general widget or call, documented for any
  app, not a back door for one app.
- **Per-frame Lua.** The shelf, hover, hiding, and sliding are drawn and timed in C#; Lua runs only when something
  changes, such as an app opening, or a click.

## Approaches considered

1. **A panel window and a shelf widget, in UIScript** (chosen). Dock.app's `.uix` gives its window a panel role and
   holds a `<Shelf>`; C# draws the shelf and the window manager applies the panel's rules; Lua fills it and handles
   clicks.
2. **A general drawing surface for Lua** (`<Canvas>`): the most flexible, but Lua would draw and track hover every
   frame, which is slower, more to get right, and gives other apps no shelf.
3. **A thin app around today's C# dock:** quick, but the dock's hard-coded place in the desktop, which is the
   problem, stays.

The owner chose 1. The owner also chose macOS's way for "always visible": windows stop above the dock by default,
not by force.

## Panel windows

A window becomes a panel through two attributes on `<Window>`:

```xml
<UIScript version="1" requires="shelf">
  <Window width="400" height="72" role="panel" edge="bottom"/>
  <Shelf name="apps" x="0" y="0" width="400" height="72"/>
</UIScript>
```

| Attribute | Value |
|---|---|
| `role` | `panel` for a panel; anything else, or none, is an ordinary window. |
| `edge` | The edge it's pinned to: `top` or `bottom`. Any other value is refused, so a later edge can't be misread. |

A panel:

- has no title bar, buttons, or frame, can't be moved, resized, minimized, or zoomed, and is centred along its edge,
  as wide as its content (see [the shelf](#the-shelf)), so clicks beside it reach the windows behind;
- is drawn above ordinary windows and below the menu bar's open menus;
- never takes keyboard focus, and clicking it doesn't change the app in front, so the menu bar stays as it is;
- isn't listed among windows anywhere, such as for cycling;
- on the bottom edge, follows the dock's behaviour: always visible, hiding automatically, or hiding when covered. A
  top panel is always visible.

The window manager's work area runs from the top panel's bottom to the bottom panel's top, while that one is
visible, replacing today's fixed `TopInset`: zoom fills the work area, and new windows open inside it. Dragging
doesn't stop at the bottom panel, as on macOS; a window can't go above the top one, as now. When the bottom panel
hides, the work area runs to the bottom edge. Top panels span the screen; bottom ones are as wide as their content.

**Older readers.** Both attributes are skippable: a reader without panels shows an ordinary window holding the
shelf. That's plainer but works, so neither needs a `requires` name, as with `subtitle`. They're drafted for Hudini
in `docs/uiscript/panel.md`.

## The shelf

`<Shelf>` is a control: a row of icons with dividers between groups, on the dock's glass, as today's dock draws it.
It's a new element, and an unknown element refuses a document, so it's a module: a document that uses it names
`shelf` in `requires`, and a reader without shelves refuses it with a clear reason. It's drafted for Hudini in
`docs/uiscript/shelf.md`.

| Attribute | Value |
|---|---|
| `name`, `x`, `y`, `width`, `height` | As any control. In a panel, the width is where it starts; the panel grows and shrinks with the items. |

Lua fills it with `window:setItems(name, items)`, as a list or sidebar is filled, each item a table:

| Field | Meaning |
|---|---|
| `title` | The name shown above it under the pointer. |
| `icon` | A PNG, drawn as the dock draws icons today (a tile or a shape, with the key colour cleared); without one, a tile with the title's initial in a colour from the title. |
| `open` | Whether to put a dot under it. |
| `group` | A number; a divider goes between items whose groups differ. |

A click gives the app a `pressed` event with the item's `index`, as lists do. The shelf draws hover and the name
itself, with no Lua.

## The menu bar

The menu bar is a top panel in a document that uses the `menus` module, which Files' `main.uix` gains:

```xml
<UIScript version="1" requires="menus">
  <Window width="1024" height="24" role="panel" edge="top"/>
  <!-- Files' own menus, as now. -->
  <Menu name="file" title="File"/>
  ...
</UIScript>
```

Nothing in it says what the bar shows. A top panel is the menu bar, and the host draws into it what the bar shows
today: the VentureOS menu as its icon, the front app's name in bold and the menus from its own document, the gloss,
and the clock, opened and chosen as now, with the bar's keys (Alt and its letters, shortcuts, Escape). Which app is
in front, and its menus, are the desktop's knowledge; Files only says the bar is there. Choices go to the app they
belong to, as now.

- **One top panel at a time,** and it's the menu bar. An app asking for a top panel while one is open is refused,
  with the reason that the top edge holds the menu bar, rather than given a second bar or an ordinary window.
- **Older readers,** and Hudini, see a window with menus: plainer, and still correct. The format gains only the
  panel attributes; there's no new element, and no new module.
- Files opens the panel when it starts, with no other window, as 0.7.1xx made it.

## What Lua gains: `venture.desktop`

A new table for apps that act for the desktop, under the `system` scope, since it reaches other apps' state:

| Function | Gives |
|---|---|
| `venture.desktop.apps()` | Every app the dock shows: `{ name, path, icon, identifier, user, open }`, the system's then the user's, each sorted by name, as the dock orders them today. |
| `venture.desktop.minimized()` | Minimized windows: `{ id, title, app }`, oldest first. |
| `venture.desktop.restore(id)` | Restores a minimized window; `true`, or `nil` and why. |
| `venture.desktop.onChange(callback)` | Calls back, at most once a frame, when apps open or close, are installed, or a window is minimized or restored. Undone when the app quits, through its `LuaOwner`. |

Opening an app is `venture.ui.launch`, which exists.

## Dock.app

- `rootfs/system/apps/Dock.app`: `Info.xml` with `needs="system"`, `main.uix` as above, and `main.lua`, which fills
  the shelf from `apps()` and `minimized()`, refills it on `onChange`, and launches or restores on `pressed`. It
  leaves itself out.
- The desktop keeps it running as it keeps Files: both are kept processes, with the `always` policy, started at the
  desktop's start, restarted at once when they end, without Quit, and never quit for having no windows.
- Dock.app's window is a panel, so it's never in front, and the menu bar never shows Dock's menus.

## The dock's behaviour

| Mode | Shown | Work area |
|---|---|---|
| Always visible | Always | Ends at the dock's top |
| Hide automatically | When the pointer touches the bottom edge, until it leaves the dock, then a second more | Runs to the bottom |
| Hide when covered | Unless an unminimized window overlaps where it sits; also when the pointer touches the bottom edge | Runs to the bottom |

It slides in and out over about 150 ms, drawn by the window manager. The choice is `dock` in `appearance.xml`
(`always`, `autohide`, or `dodge`), read and written through `venture.appearance()` and `venture.setAppearance{
dock = ... }`, and shown in a **Dock** pane in Settings. An older VentureOS skips the attribute, as `appearance.xml`
allows, and keeps the dock always visible; a value this one doesn't know reads as `always`.

## Where the code goes

- **`Venture.OS.Interface`:** `role` and `edge` on `UIWindow`, the `shelf` module and `UIShelf`, and
  `UIScriptSchema` for them, with tests against the reader.
- **`Venture.OS.Common/Desktop`:** drawing the shelf in `UIView` (moving `Dock.cs`'s drawing there), and the
  menu bar into a top panel (moving `MenuBar.cs` from `DesktopSession` to the window manager's panels); panels and the work area in `WindowManager`; the dock's modes; `Dock` and the menu
  bar removed from `DesktopSession`; `venture.desktop`.
- **`rootfs/system/apps/Dock.app`**, Files' top panel, and Settings' Dock pane.
- **Roundhouse:** its designer reads and draws both, since it draws with `UIView`, showing a panel as its content
  with no title bar to select or drag; its library gains a shelf. Its Run gets Dock.app from `rootfs/` like the
  kernel.
- **Docs:** the UIScript drafts, `docs/writing-apps.md` for panels, shelves, and `venture.desktop`, and the
  architecture.

## Risks and what to check

- **The idle frame.** Sliding and hover must still allocate nothing when idle (`IdleFrameTests`).
- **The desktop without a dock or a bar.** A boot where Dock.app or Files fails, or is killed, must leave a usable
  desktop, and the other must stay up while it's restarted.
- **Menus moving into a widget.** The bar's keyboard handling (Alt and its letters, shortcuts, Escape) must work as
  before, wherever the panel is.
- **Lua speed.** Refilling the shelf runs on changes, not frames; a burst of changes is coalesced to one call a frame.

## Left to the builder

- Exact names of types and fields, and how the panel's width follows the shelf.
- How sliding is timed against the frame clock, and the second of grace in auto-hide.
- The Settings pane's layout.
