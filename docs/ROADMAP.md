# VentureOS roadmap

> **Last updated:** 2026-11-17 · **Latest release:** 0.4, Pere Marquette · **Next:** 0.5, Cassiopeia, in development

Where VentureOS is going: the release in development, the ones after it, and
ideas not yet given a release. What has shipped is in
[docs/releases](./releases/), and verified status is in the
[scorecard](./scorecard.md). The roadmap VentureOS was planned with until 0.4
is archived in [docs/history](./history/roadmap-2026.md).

## How VentureOS releases

VentureOS is a hobby operating system with its own apps, so it's planned in
releases, which are what people get.

- **No schedule.** A release ships when its milestones land, not on a date.
- **Versions** follow the .NET SDK's feature bands: `0.4` is the release, each
  milestone in it takes a band (`0.4.1xx`, `0.4.2xx`, ...), and the last two
  digits count builds within the band. A build's version is the one the kernel
  reports, from `GitInfo.txt` at the repository root and every commit since.
  `GitInfo.txt` moves to a milestone's band (`0.4.500`) when work on it
  begins; a new release (`0.5.100`) waits for my word.
- **Stages.** A release is *considered* (a theme and candidate features),
  then *in development* (bands with checklists), then *released*: tagged
  (`v0.3.18`), with notes in [docs/releases](./releases/). Published ISOs come
  later.
- **Support.** Until 1.0, only the latest release is supported.
- **1.0** is whenever I feel VentureOS is ready. There's no gate for it.
- **Names.** Each release has a code name, and the milestones in it plain
  ones. From 0.5, code names are Japanese sleeper trains; when those run out,
  Japanese limited expresses. 0.4, Pere Marquette, is the last named after an
  Amtrak route that runs Siemens Venture cars, the trains VentureOS is named
  after.
- **Apps** don't declare which VentureOS they need yet.

Keep it current: tick a band's items as they land, move a release from one
stage to the next, and add a line to the revision history.

## Releases

| Release | Code name | Stage | Holds |
|---|---|---|---|
| 0.3 | (the desktop) | released 2026-11-11, `v0.3.18`; [notes](./releases/0.3.md) | The desktop, its apps, Settings, and the look |
| 0.4 | Pere Marquette | released 2026-11-17, `v0.4.500`; [notes](./releases/0.4.md) | Standard menus, text selection, Edit, networking, and Undo and Redo |
| 0.5 | Cassiopeia | in development | The desktop's look: images and icons, a glossy menu bar with a clock, header bars, sidebars, and look options |

## 0.4, Pere Marquette

The bridge into releasing as an OS. It gives every app the menus a global
menu bar implies, edits text the same way everywhere, and brings
networking. Whatever a standard menu offers, every app gets, with no code of
its own.

**0.4.1xx, standard menus**

- [x] File, with Close Window, and Help, with About the app from its
  `Info.xml`, including an optional `description`.
- [x] `role` on `<Menu>`, or a menu's exact title, merges an app's menu into
  the standard one; an app's own shortcut wins over a standard item's.
- [x] The shipped apps drop their own Close Window, and describe themselves.

**0.4.2xx, text selection**

- [x] Select text in fields, areas, and the title field by dragging, with
  Shift and the arrows, and all at once.

**0.4.3xx, Edit**

- [x] A clipboard, and Cut, Copy, Paste, and Select All for whatever text
  has focus, in every app.

**0.4.4xx, menus always there, and networking**

- [x] File, Edit, and Help always on the bar, their items dimmed when they
  can't act.
- [x] `net` shows the DNS server DHCP registered, and `resolve` and `ping`
  use Cosmos's DNS and ICMP clients.

**0.4.5xx, Undo and Redo**

- [x] Edits recorded as values (where, what was there, what replaced it) at
  the one place every change to text passes through, so Undo applies the
  inverse and Redo the edit again.
- [x] Typing grouped into steps, broken by a space, a caret jump, or a paste.
- [x] A history for each text field, area, and title field, capped at 100
  steps, and cleared when the app sets the text itself.
- [x] Edit's Undo (Ctrl+Z) and Redo (Ctrl+Shift+Z), dimmed when there's
  nothing to undo or redo.

## 0.5, Cassiopeia

Named after the luxury sleeper, for the release that makes the desktop look
its best, after the goals in my notes: unified GNOME-style header bars and
sidebars, a glossy Tiger-style menu bar, rounded corners, wallpapers, and
automatic dark mode. Window buttons stay on the left, macOS-style.

**0.5.1xx, images**

- [x] Draw PNG images: app icons in the dock and About, and wallpapers.
- [x] Icons with full backgrounds rounded to a tile, as on modern macOS,
  and a key color (`#00FF01`) for what's clear in a shaped icon, which
  gets a shadow, so AniCraft's PNGs work as they are.
- [x] The Earthset wallpaper, as a PNG, pre-scaled to the screen.

**0.5.2xx, the menu bar**

- [x] VentureOS's icon in place of its name, as Tiger's Apple, white while
  its menu is open.
- [x] Tiger's gloss: a bright band and a sheen line.
- [x] Status items on the right, starting with a clock: the hardware
  clock with an offset from UTC set in Settings or with `clock`, and no
  daylight saving.

**0.5.3xx, header bars**

- [x] Toolbar items with icons, flat until the pointer's over them, at the
  start or the end of the bar.
- [x] Symbolic icons recolored to the text color, so they follow light and
  dark, starting with the toolbar's; the sidebar's use them in 0.5.4xx.
- [x] A subtitle under the title; Files shows its item count there.
- [x] A taller bar when it holds a subtitle or toolbar icons, and a
  slightly taller plain one, with more room to drag.
- [x] The green zoom button for resizable windows, filling the screen
  below the menu bar, as macOS does.
- [x] A flat header bar that blends with the content, with faint lines
  under it and beside the sidebar, after GNOME's.
- [x] GNOME's colors in place of Distro Tycoon's: neutral surfaces, a
  neutral menu bar, and GNOME's nine accents in Settings.

**0.5.4xx, sidebars**

- [x] An icon on each row, and a rounded selection; icons are symbols in
  the text color, as the toolbar's are.
- [x] Separators between groups of rows; Files sets System apart.
- [x] Settings in panes, Appearance and Clock, in a sidebar, as GNOME
  Settings has them.

**0.5.5xx, look options**

- [x] Rounded corners on all four corners of a window, on or off in
  Settings; on by default, at 10 pixels.
- [x] Wallpapers to choose from in Settings, with light and dark variants,
  and the sky kept as one. Wallpapers are bundles; the variants are read,
  but none ships yet.
- [x] Automatic dark mode, on a schedule set in Settings or with
  `appearance auto --from 19:00 --until 07:00`. Real sunset and sunrise,
  from a location, could follow.

## After 0.5: Roundhouse

Roundhouse is VentureOS's IDE, named after where locomotives are built and
serviced. It comes after 0.5, so its canvas shows the finished look.

**The desktop version** is a C# app on Avalonia, for macOS, Linux, and
Windows, in this repository. It edits an app's bundle: a navigator for its
files, a Lua editor with highlighting (AvaloniaEdit), a form for `Info.xml`,
and an Xcode-style interface builder for `.uix` files (a canvas, selection
handles, an outline, an inspector, a library of controls, window settings,
and menus). The canvas is drawn by VentureOS's own `UIView` through an
Avalonia painter, so it looks exactly as VentureOS draws it, and Run starts
the app in a simulated VentureOS desktop built from the same code, with no
QEMU. Avalonia and AvaloniaEdit are approved, for Roundhouse only.

It needs a UIScript writer that keeps what it doesn't understand: comments,
unknown attributes, and element order. A document with an element it
refuses opens read-only, saying why.

**The native version,** an app on VentureOS itself, comes after the desktop
one. It needs from the platform: a `<Region>` that hosts and draws another
`.uix`, pointer input in regions, UIScript to and from Lua, text with more
than one style, and persistence.

## Ideas

Worth doing someday, in no order, and not yet given a release.

- **Persistence:** keeping files, apps, and settings across reboots on a
  real disk, which the RAM disk can't. Installing apps from outside the
  system waits on it.
- **Undo for apps:** apps registering their own steps, such as deleting a
  note, beside text's.
- **Published ISOs** from tagged releases.
- **Apps declaring the VentureOS they need,** with a rule for deprecating
  what they use.
- **The full CST specification,** through CST.NET once it follows it, in
  place of the transitional reader.
- **Text:** selecting from read-only text, such as Terminal's output;
  dragging to select in the title field; double-clicking a word.
- **Windows:** resizing Terminal; collapsing a sidebar in a narrow window.
- **Lua:** the clipboard for apps.
- **ARM64:** booting it under QEMU, not only building it.
- **Hudini:** adopting VentureOS's UIScript drafts (menus, sidebars, and
  styles) in its own spec.

## Risks

- Cosmos renames APIs between releases (3.0.87 to 3.0.94 moved the
  filesystem, timer, network, and storage namespaces), and its docs follow
  the latest release only.
- Only `Venture.OS.Common`, `Venture.OS.Interface`, and the Lua system files
  are tested; kernel code is checked by booting by hand.
- `Cosmos.Executable.Lua` is young (4.0.1, from 2026-10); a bug there lands
  in every script.
- Nothing has run on real hardware, and the Cosmos docs warn against testing
  filesystem code there.
- The Cosmos network stack doesn't retransmit TCP, and its ARP wait is
  bounded by a count of tries rather than a time.

## Revision history

| Date | Change | Summary |
|---|---|---|
| 2026-11-17 | minor | 0.5.5xx done, in four phases: the look grows, rounded corners, wallpapers with a picker, and automatic dark mode on a schedule, which I chose over sunset times for now. Every band of 0.5 is done. |
| 2026-11-17 | minor | 0.5.4xx gains a fourth phase at my suggestion: Settings' panes in a sidebar. |
| 2026-11-17 | minor | 0.5.4xx done, in three phases: sidebar rows with icons, a rounded selection, and separators between groups. Shipped sidebars keep text until their icons are drawn. |
| 2026-11-17 | minor | 0.5.3xx done again: VentureOS takes GNOME's colors and nine accents. |
| 2026-11-17 | minor | 0.5.3xx reopens, by my decision, for two more phases: a flat header bar, then GNOME's colors. |
| 2026-11-17 | minor | 0.5.3xx done, in four phases: flat toolbar items with symbols at either end, the subtitle, taller bars, and zoom. The bar grows only for a subtitle or icons, by my decision. Shipped apps keep text items until their icons are drawn. |
| 2026-11-17 | minor | 0.5.2xx done: Tiger's gloss, and the time on the right of the menu bar, with an offset from UTC I chose over a time zone database. |
| 2026-11-17 | minor | 0.5.2xx begins with VentureOS's icon in the menu bar, an item added at my request. Smoothed icon edges are unmixed from the key. |
| 2026-11-17 | minor | 0.5.1xx done: icons with full backgrounds are rounded to a tile, and `#00FF01` is clear in a shaped one. Symbolic icons move to 0.5.3xx. |
| 2026-11-17 | minor | 0.5, Cassiopeia, is in development. 0.5.1xx draws PNG pictures: apps' icons in the dock and About, and the Earthset wallpaper. Symbolic icons wait for toolbar and sidebar icons. |
| 2026-11-17 | minor | Roundhouse, VentureOS's IDE, is planned after 0.5: a desktop version on Avalonia first, in this repository, with VentureOS's own drawing and a built-in simulator; the native version after it. |
| 2026-11-17 | minor | 0.5 is Cassiopeia, the first Japanese sleeper train: the desktop's look, in five bands, from my notes' desktop goals. |
| 2026-11-17 | minor | 0.4, Pere Marquette, is released as `v0.4.500`. 0.5 is next, being planned. |
| 2026-11-12 | minor | 0.4.5xx done: Undo and Redo for every text field, area, and title field. Every band of 0.4 is done. |
| 2026-11-12 | major | The roadmap starts over for an evolving OS, by my decision: releases and their stages, bands without hour estimates, an Ideas list, and 1.0 whenever I'm ready. The route milestones and their history moved to [docs/history](./history/roadmap-2026.md). 0.4 gains a fifth band, Undo and Redo. Code names from 0.5 are Japanese sleeper trains, then Japanese limited expresses. |
