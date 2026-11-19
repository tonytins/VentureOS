# VentureOS roadmap

> **Last updated:** 2026-11-19 · **Latest release:** 0.6, Izumo · **Next:** 0.7, Seto, in development

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
| 0.5 | Cassiopeia | released 2026-11-18, `v0.5.506`; [notes](./releases/0.5.md) | The desktop's look: images and icons, a glossy menu bar with a clock, header bars, sidebars, and look options |
| 0.6 | Izumo | released 2026-11-18, `v0.6.505`; [notes](./releases/0.6.md) | Roundhouse, VentureOS's IDE: a live preview, a round-trip writer, an interface designer, Run in a simulated desktop, and bundle editing |
| 0.7 | Seto | in development | vdb, for sending apps to a running VentureOS; apps as supervised processes; the dock and the menu bar as apps; refinements; then Roundhouse on VentureOS itself |

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

## 0.6, Izumo

Named after the Sunrise Izumo sleeper. 0.6 is Roundhouse, VentureOS's IDE, named
after where locomotives are built and serviced: a real interface designer,
which outside Windows and macOS doesn't exist, for VentureOS's own apps and
anyone else's. Roundhouse shares VentureOS's version, as Xcode ships with the
SDK it builds for. Its [design](./design/2026-11-18-roundhouse-design.md) has
the why, the decisions, and how the parts fit.

**The desktop version** is a C# app on Avalonia, for macOS, Linux, and
Windows, in this repository. Its canvas is drawn by VentureOS's own code, so
what you design is what the app shows, close but not pixel-perfect in text.
Avalonia, AvaloniaEdit, and AvaloniaEdit's TextMate add-on are approved, for
Roundhouse only.

**0.6.1xx, preview, and no `ui` scope**

- [x] Apps don't ask for `ui`; commands, `init.lua`, and the `lua` prompt can
  show an alert, Quick Look, and launch an app.
- [x] Roundhouse opens a `.uix` and draws it beside its text, as VentureOS
  does, redrawing as you type, in light and dark.

**0.6.2xx, round trip**

- [x] A document model that writes a `.uix` back with only what was edited
  changed, keeping comments, unknown attributes, and order. A document with
  an element it refuses opens read-only, saying why.

**0.6.3xx, designer**

- [x] Select, move, and resize; an outline, an inspector, and a library of
  controls; window settings, the title bar, menus, and the sidebar; Undo and
  Redo.

**0.6.4xx, Run**

- [x] A simulated VentureOS desktop in Roundhouse, built from the same code,
  running the app being edited, with no QEMU.

**0.6.5xx, bundles**

- [x] A navigator for a bundle's files, a Lua editor with highlighting, a form
  for `Info.xml`, and a new app from a template.

## 0.7, Seto

Named after the Sunrise Seto, which runs coupled to the Izumo. 0.7 takes
Roundhouse onto VentureOS itself, starting with a way to get apps there.

0.7 is a Snow Leopard: it started small, and with vdb making it quick to try
changes on the real system, it's as much about refining what's there, such as
the dock, as about new features. It may run longer than earlier releases.

**0.7.1xx, vdb**

vdb sends an app to a running VentureOS and opens it, from a command-line tool
on another computer, once developer mode is on and the computer is trusted.
It stands in until VentureOS can keep files and install from a disk. Its
[design](./design/2026-11-18-vdb-design.md) has the protocol and the rules.
The tool is written in Swift, in `tools/vdb/` for now; Swift Argument Parser
and swift-log are approved for it alone.

- [x] Developer mode, in Settings and from scripts, and trusting a computer,
  by an alert or ahead of time on the owner's own builds.
- [x] The vdb/1 protocol: installing an app, checked as boot checks apps, and
  opening it, with what it prints sent back.
- [x] The `vdb` tool, for Linux, macOS, and Windows (built and tested on Linux).

**0.7.2xx, supervised processes**

Every app, the system's included, becomes a process with an ID and a
lifecycle, which a supervisor starts, watches, ends, and, for the apps the
desktop keeps running, restarts at once. It's what the next band stands on,
and leaves room for apps to run independently later. Its
[design](./design/2026-11-18-processes-design.md) has the rules.

- [x] Processes and a supervisor: crashes noticed, with the reason, and
  restart policies, `never` and `always`.
- [x] `ps` and `kill`, and `venture.processes()` and `venture.kill(id)`.

**0.7.3xx, the dock and the menu bar as apps**

The dock becomes `Dock.app`, and the menu bar a panel Files hosts, rather than
parts of the desktop's own code: one less thing that can take the desktop
down, easier to change later, and windows that stop covering or being covered.
The dock gains a choice of always visible, hiding on its own, or hiding when
a window would cover it. Its [design](./design/2026-11-18-dock-app-design.md)
adds panel windows and a shelf to UIScript.

- [x] Panel windows, top and bottom, and the work area between them.
- [x] The shelf, `venture.desktop`, and `Dock.app`.
- [x] The menu bar as Files' top panel.
- [x] The dock's three modes, in Settings.

**0.7.4xx, refinements**

No theme of its own: what using 0.7 so far has shown needs fixing or finishing.
Panels and sidebar panes become elements, so what an interface holds is in its
`.uix` rather than its Lua, and Roundhouse can show it. vdb's conversation is
encrypted, which [its design](./design/2026-11-19-vdb-encryption-design.md)
lays out; it stays vdb/1, and older tools stop working. swift-crypto is
approved for the `vdb` tool alone.

- [x] Cosmos 3.0.99.
- [x] `<Panel>` in place of `<Window role="panel">`, which goes.
- [x] `<Pane>` in a `<Sidebar>`: its rows, and one pane shown at a time, with
  no Lua; Settings moves to it.
- [x] Roundhouse's designer draws a sidebar's rows and its selected pane, and
  switches panes from the canvas.
- [x] SHA-256, HMAC, HKDF, and ChaCha20-Poly1305 for VentureOS, and vdb/1
  encrypted with them, the identity no longer sent.
- [x] Roundhouse sends the app it has open to a running VentureOS over vdb,
  and shows what it prints.

**Roundhouse on VentureOS,** after vdb, and not yet planned: a text editor
first, as capable as a desktop code editor, and in the long run a designer
like the desktop version's, gaining widgets as it needs them. A designer needs
from the platform: a `<Region>` that hosts and draws another `.uix`, pointer
input in regions, UIScript to and from Lua, text with more than one style, and
persistence.

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
| 2026-11-19 | minor | 0.7.4xx is complete: Roundhouse runs the app it has open on VentureOS over vdb, with Common's client. The next band is the owner's to plan. |
| 2026-11-19 | patch | 0.7.4xx: vdb/1 is sealed, with VentureOS's own SHA-256, HKDF, and ChaCha20-Poly1305, and the identity sent only at pairing. |
| 2026-11-19 | patch | 0.7.4xx: panes are elements, and Roundhouse's designer shows a sidebar's rows and one pane at a time. |
| 2026-11-19 | patch | 0.7.4xx: Cosmos 3.0.99, and `<Panel>` in place of `<Window role="panel">`, are done. |
| 2026-11-19 | minor | 0.7.4xx, refinements, begins: Cosmos 3.0.99, `<Panel>` and `<Pane>` as elements, Roundhouse's sidebar fix and vdb, and vdb/1 encrypted, by my decision, from its design. swift-crypto is approved for the `vdb` tool. |
| 2026-11-19 | minor | 0.7.3xx is complete: panels and shelves, Dock.app, the menu bar as Files' top panel, and the dock's three modes. The next band is the owner's to plan. |
| 2026-11-19 | minor | 0.7.2xx is complete: apps run as supervised processes, with crashes, kept apps, `ps`, and `kill`. 0.7.3xx, the dock and the menu bar as apps, is next, being planned. |
| 2026-11-18 | minor | 0.7.2xx begins: apps as supervised processes, from its approved design. The dock moves to 0.7.3xx, with the menu bar as a panel Files hosts, built on processes. Designs get an index, `docs/design/README.md`. |
| 2026-11-18 | minor | 0.7.1xx is complete, closed out with a boot that follows the README's vdb steps. 0.7 is a Snow Leopard, refining as much as adding, and may run longer than earlier releases. 0.7.2xx, the dock as an app, is next, being planned. |
| 2026-11-18 | minor | 0.7.1xx's vdb is done, with the `vdb` tool in Swift. The desktop no longer opens a Files window. The dock becomes an app in a band of its own, 0.7.2xx, by my decision. |
| 2026-11-18 | patch | VentureOS's side of vdb is done: developer mode, the Trust alert, the protocol, and listening from the desktop, checked by sending an app into QEMU. The `vdb` tool is next. GitVersion gains a tag switch, so vdb reports VentureOS's version. |
| 2026-11-18 | minor | 0.7, Seto, begins with 0.7.1xx, vdb, from its approved design: developer mode, trusting a computer, a plain-text protocol with files in base64, and a Swift tool. Roundhouse on VentureOS follows, a text editor first. |
| 2026-11-18 | minor | 0.6, Izumo, is released as `v0.6.505`. 0.7, Seto, is being planned. |
| 2026-11-18 | patch | 0.6.5xx's bundles are done: a navigator, editors for Lua and text, the `Info.xml` form, New App, a Log tab, and a prompt before leaving unsaved edits. Every band of 0.6 is complete; shipping it is the owner's call. |
| 2026-11-18 | patch | 0.6.4xx's Run is done: VentureOS's desktop runs in Roundhouse over a fresh copy of its files, with scripts' paths kept within it, and the designer shows a document's menus above its window, as Xcode does. The band is complete. |
| 2026-11-18 | patch | 0.6.3xx's designer is done, with Roundhouse's own menus for the global menu bar, a title bar after VentureOS's, and packaging for Linux, Windows, and macOS, after CityLimits'. The band is complete. |
| 2026-11-18 | patch | 0.6.2xx's round trip is done: `UIDocument` edits attributes and elements in place, refusing what VentureOS refuses, and Roundhouse saves through it. The band is complete. |
| 2026-11-18 | patch | 0.6.1xx's preview is done: Roundhouse opens a `.uix` and redraws it beside its text as you type, in light and dark. The band is complete. |
| 2026-11-18 | minor | 0.6 is Izumo, Roundhouse, from its approved design, in five bands, designer first; apps no longer ask for `ui`. 0.7 will be Seto. |
| 2026-11-18 | minor | 0.5, Cassiopeia, is released as `v0.5.506`. Roundhouse is next, being planned. |
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
