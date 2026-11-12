# VentureOS roadmap

> **Last reviewed:** 2026-11-11 · **Last updated:** 2026-11-11 · **Change:** minor (0.4.2xx, text selection) · **Stage:** alpha

Forward-looking: where VentureOS stands, what blocks the next stage, and what
comes next. Verified status and evidence are in the
[scorecard](./scorecard.md).

Keep it current: when work lands, update its milestone row and checklist,
re-estimate what remains, and add a line to the revision history.

Estimates are rough hours for one developer and are re-measured when a
milestone lands.

## How VentureOS releases

VentureOS is an operating system with its own apps, so it's planned in
releases, which are what people get, rather than milestones alone.

- **No schedule.** A release ships when its milestones land, not on a date.
- **Versions** follow the .NET SDK's feature bands: `0.4` is the release, each
  milestone in it takes a band (`0.4.1xx`, `0.4.2xx`, ...), and the last two
  digits count builds within the band. A build's version is the one the kernel
  reports, from `GitInfo.txt` and the commits since. `GitInfo.txt` moves to a
  milestone's band (`0.4.200`) when work on it begins; a new release
  (`0.5.100`) waits for my word.
- **Support.** Until 1.0, only the latest release is supported.
- **Stages.** A release is in development, then released: tagged
  (`v0.3.18`), with notes in [docs/releases](./releases/). Published ISOs come
  later.
- **Names.** Each release has a code name, and the milestones in it plain
  ones. 0.4, Pere Marquette, is the last named after an Amtrak route that runs
  Siemens Venture cars, the trains VentureOS is named after; earlier
  milestones carried those names too, and keep them below as history.
- **Apps** don't declare which VentureOS they need yet.

## Releases

| Release | Code name | Stage | Holds | Notes |
|---|---|---|---|---|
| 0.3 | (the desktop) | released 2026-11-11, `v0.3.18` | Borealis, Keystone, Downeaster, and the layout of the repository | [0.3](./releases/0.3.md) |
| 0.4 | Pere Marquette | in development | Standard menus, text selection, Edit, and networking | |

## Alpha gate

Alpha means VentureOS is a usable shell OS: each workflow below works end to
end on x64 under QEMU. A row blocks alpha only when a user can't complete
that workflow at all. Missing depth or a clumsy path is `partial, not
blocking`, which makes it a beta item. ARM64 only has to keep building.

| Core workflow | Works end to end? | Evidence | Hours to pass (est.) |
|---|---|---|---|
| Boot, and say clearly why when boot can't finish | yes | Boots to the shell; critical steps panic with a report and optional steps degrade (BOOT-1 to BOOT-5); a forced panic was proven in a real boot (BOOT-6); messages reach serial (BOOT-7) | 0 |
| Run commands that take arguments | yes | Commands come from a registry, and input is split into a command and arguments with quoting and clear errors (SHELL-3, SHELL-5) | 0 |
| Inspect and control the system: version, network, reboot, shutdown | yes | `info` and `net` show the system; `halt`, `reboot`, and `shutdown` control it, with reboot and shutdown verified in QEMU (SHELL-4, SHELL-6) | 0 |

**Result: alpha.** Every row passes as of 2026-10-15, with Cascades and
Hiawatha complete.

Persistent storage is deferred until after alpha by my decision (Blue Water).

## Current focus

In order:

1. **0.4, Pere Marquette**, a transition into releasing as an OS: standard
   menus first, then text selection, Edit, and networking.

VentureOS runs from RAM, as many small systems do; keeping changes across
reboots is later, optional work (Blue Water).

## Milestones

"Done" is the share of the milestone's checklist below that is complete.
"Remaining" estimates the open items only. The routes up to Downeaster were
planned before releases; 0.4's milestones are under its release below.

| Route | Milestone | Done | Remaining (h, est.) | Notes |
|---|---|---:|---:|---|
| Cascades | Reliable boot | 100% | 0 | Boot steps, a proven panic, range-checked RAM disk, system messages on serial, and the boot delay kept |
| Hiawatha | Shell | 100% | 0 | Command registry, argument parsing, `info`, `net`, `reboot`, and `shutdown` |
| Wolverine | System library | 100% | 0 | Shared Lua modules in `/mnt/system/lib`, loaded with `require`; `venture.args` parses arguments and options with a generated `--help` |
| Blue Water | Files | 67% | 8–15 (persistence, later) | File commands on the RAM disk now; an optional save to disk later |
| Pere Marquette | Release 0.4 | 50% | 13–26 | Standard menus and text selection are done; Edit and networking are next |
| Illini | Lua system scripts | 100% | 0 | System files install to `/mnt/system`, `<name>.lua` there runs as a command, the `venture` table lets `echo`, `info`, and `net` be scripts, and `init.lua` runs after boot |
| Saluki | Apps | 90% | later | Runtime commands, bundles, and opening and closing apps are done, with `Notes.app` and a guide; installing apps from outside the system waits on persistence |
| Borealis | Desktop | 100% | 0 | A Tiger-style desktop: `.uix` (UIScript) interfaces, windows, a menu bar, and a dock; version 0.3 |
| Keystone | Desktop apps | 100% | 0 | Files, user apps, timers and System Monitor, a multi-line `<TextArea>` and TextEdit, and Terminal.app |
| Downeaster | Desktop refinement | 100% | 0 | Centred titles, an address that edits on click, sidebars, resizable windows, hover, dark mode and accents, style roles, Settings, and hover text |
| | **Total** | | **≈ 21–41** | |

## Upcoming work, ranked

| Rank | Work | Milestone | Estimate (h) |
|---|---|---|---|
| 1 | Edit: the clipboard (0.4.3xx) | Pere Marquette | 3–6 |
| 2 | Networking (0.4.4xx) | Pere Marquette | 10–20 |

Beta needs Blue Water in substance, about 8–15 hours.

## Milestone checklists

### Cascades: Reliable boot

- [x] Boot steps split into critical (RAM filesystem, timer) and optional
  (network).
- [x] Critical failures panic deliberately with a report on serial and the
  console.
- [x] Optional failures are reported and boot continues.
- [x] Each boot step says which part failed.
- [x] The RAM disk validates its size and range-checks every block access.
- [x] Trigger the panic in a real boot and confirm the report and the halt.
- [x] Copy key console lines (step results, the banner, panics) to serial.
- [x] Decide whether to keep the 0.5 s pauses around each boot step: kept, so
  boot messages stay readable (my decision).

### Hiawatha: Shell

- [x] A command registry: each command declares its name, help text, and
  handler, and `help` is generated from it.
- [x] Argument parsing, with quoting, and clear errors for bad arguments.
- [x] `info`: version, build, uptime, and memory.
- [x] `net`: adapter, MAC, link, and address.
- [x] `reboot` and `shutdown` beside `halt`.

### Wolverine: System library

Shared Lua modules that ship with the system, in `/mnt/system/lib`, loaded
with `require`. Namespaced under `venture.`, so plain names stay free for
scripts and apps.

- [x] `/mnt/system/lib` installed with the system files, and `require`
  pointed at it.
- [x] `venture.args`: declare a command's arguments and options, and get
  parsing, clear errors, and a generated `--help`.

The former Wolverine checklist held the test project, now done, and CI
checks, which were dropped by my decision on 2026-11-11.

### Blue Water: Files

VentureOS runs from RAM, as Tiny Core, Puppy Linux, and most live CDs do.
Files come first; keeping them across reboots is later and optional.

- [x] A current folder for the shell: `cd`, shown in the prompt, and Lua runs
  from it.
- [x] File commands on the RAM disk: `ls`, `cat`, `write`, `mkdir`, `rm`,
  `pwd`, and `df`, in Lua, with options through `venture.args`.
  `/mnt/system` is read-only to them.
- [x] The `files` scope, so scripts declare file access with `-- Needs:`.
- [ ] Later: keep changes on a disk when one is attached, as a save file
  beside the RAM system; `cosmos run --disk <image>` attaches one.
- [ ] Later: decide whether VentureOS formats blank disks itself.

### Pere Marquette: release 0.4

The bridge into releasing as an OS, and the last release named after an
Amtrak route. It gives every app the menus a global menu bar implies, and
brings networking. Whatever a standard menu offers, every app gets, with no
code of its own; an empty standard menu stays hidden.

**0.4.1xx, standard menus**

- [x] The desktop's File menu, with Close Window (Ctrl+W), and Help, with
  About *App* from the app's `Info.xml` in the About VentureOS window; Quit
  stays in the app menu.
- [x] An optional `description` on `<Info>`, which About shows.
- [x] `role` on `<Menu>` (`file`, `edit`, `view`, `help`), or a menu's exact
  title without one, merges an app's menu into the standard one, its items
  first.
- [x] An app's item with a standard item's shortcut replaces the standard
  item, so apps written before keep working as they did.
- [x] The shipped apps drop their own Close Window, and describe themselves.

**0.4.2xx, text selection**

- [x] Select text in fields and areas by dragging, with Shift and the arrows,
  and all at once (Ctrl+A); the title field too, which selects all of itself
  when editing starts.

**0.4.3xx, Edit**

- [ ] A clipboard, and Edit's Cut, Copy, Paste, and Select All for whatever
  text has focus, in every app. Undo and Redo come later.

**0.4.4xx, networking**

- [ ] Check what Cosmos 3.0.94 offers for DNS and ICMP.
- [ ] Register the DNS server DHCP reports.
- [ ] `resolve` and `ping` commands, on `venture.net`, with every wait bounded.

A Telnet shell was dropped by my decision on 2026-11-11.

### Illini: Lua system scripts

Move what doesn't need the kernel into Lua scripts that live in their own
system directory, `/mnt/system`. Power, `help`, `clear`, and `lua` stay in
C#, so the shell still works if the scripts don't.

- [x] Lua 5.5 through `Cosmos.Executable.Lua` 4.0.1, in place of
  `LuaCSharp`, and a one-shot `lua <code>` command.
- [x] Phase 1, scaffolding: files under `rootfs/system/` are built
  into the kernel and installed to `/mnt/system` by an optional boot step.
- [x] Phase 2, script commands: the shell runs
  `/mnt/system/commands/<name>.lua` for names it doesn't know, `help` lists
  them, and `echo` moves to Lua.
- [x] Phase 3, the `venture` table: kernel functions for scripts, tested in
  `Venture.OS.Common`; `info` and `net` move to Lua.
- [x] Phase 4, `init.lua`: runs after boot as an optional step.
- [x] Ctrl+C or Esc stops running Lua, and `init.lua` stops after 10 seconds.
- [x] Scripts declare what they reach with `-- Needs:` (`system`, `network`;
  `files` reserved).
- [x] Plain `lua` opens an interactive Lua prompt.

### Saluki: Apps

After the ideas in "A Programming Paradigm for Spatiotemporal
Composability": whatever a component sets up carries its own undo, and
removing the component runs them, newest first.

- [x] Owners: something that holds an interpreter and the undo list for what
  it registered, starting with `init.lua` (the whole boot).
- [x] Register a command at runtime, removed again when its owner goes:
  `venture.command`, under the `shell` scope.
- [x] Decide whether commands made at the `lua` prompt vanish on `exit`: they
  do. Only code the user types may replace a system script command.
- [x] The `lua` prompt as an owner, and running shell commands from it
  (`venture.shell` and `!`).
- [x] Bundles: macOS-style `Name.<kind>/Contents/` with `Info.xml`
  (`<App>`, later `<Theme>` and `<Library>`), read by a flat XML reader,
  since `System.Xml` doesn't link under Cosmos.
- [x] Opening and closing apps: `open`, `close`, and `apps`; each app is an
  owner with one `needs` for the whole app, `venture.app`, and its own
  modules for `require`. One instance per app; any `.app` opens by path.
- [x] Apps shipped with the system, in `/mnt/system/apps/`, starting with
  `Notes.app`, and a tested guide to writing apps (`docs/writing-apps.md`).
- [ ] Later, with persistence: apps installed from outside the system image.

### Borealis: Desktop

An experimental desktop after the classic Mac OS X Tiger look: a glossy menu
bar, a blue desktop, pale gradient title bars with a red close button, and a
dock with a dot under each running app. Distro Tycoon 2's interface is the
reference. Interfaces are written in UIScript, the format from Hudini, in
`.uix` files; its spec is a Draft and lives in Hudini, and both readers test
against the same examples. In UIScript the host owns data, actions, and text,
which here is an app's Lua code.

Version 0.3 is a complete desktop that can launch an `.app`, reached from
the shell with a `desktop` command, at 1024×768. Its text is set in Open Sans,
and Terminal.app's in Fira Code, both shipped with their own licenses. In
phases:

- [x] Phase 1, the UIScript reader: `.uix` files read with Hudini's rules,
  tested against Hudini's examples, plus a `menus` feature
  ([draft](./uiscript-menus.md)) for the global menu bar.
- [x] Phase 2, drawing and `desktop`: graphics mode, a frame loop, the blue
  desktop and pointer, and a way back to the shell.
- [x] Phase 3, windows: Tiger-style frames (title bar, close dot, dragging,
  focus, stacking) and a UIScript view that lays out, draws, and handles
  input.
- [x] Phase 4, the menu bar and dock: the VentureOS menu, the app's menu,
  then the focused window's menus, with Alt and Ctrl shortcuts; a dock that
  opens apps and marks the open ones.
- [x] Phase 5, apps with windows: `venture.ui` and the `ui` scope; an app
  quits when its last window closes; Notes gets a window.

With the desktop up and running, VentureOS moved to version 0.3 (a new base
in `GitInfo.txt`) on 2026-11-11, by my decision. Terminal.app, planned to
follow, moved to Keystone.

### Keystone: Desktop apps

Borealis built the desktop; Keystone fills it with apps, starting at 0.3.
The route is moving to the Siemens Airo set.

Apps ship in two folders, protected differently, as on other systems:
`/mnt/system/apps` can't be changed, so the system can't lose an app it
depends on, and `/mnt/user/apps` can be, by the user and the file commands.
User apps say what they need when they open, since they may have been
changed. Lua drawing in regions and images wait for a later route.

- [x] Phase 1, Files (system): the desktop's fallback app, like Finder. It
  runs whenever the desktop does and can't be quit; its menu shows when no
  other app is in front; the desktop opens with a Files window on `/mnt`,
  and About stays in the VentureOS menu. It browses folders, opens apps,
  previews `.uix` files, and opens text files with an app. Quick Look is a
  desktop feature any app can use (`venture.ui.preview`). A double-click
  opens an item, as Open and Ctrl+O do.
- [x] Phase 2, user apps: `/mnt/user/apps`, shipped with VentureOS from
  `rootfs/user/apps/`; the dock shows system apps, then user apps.
  Calculator (user) is the first.
- [x] Phase 3, the unified title bar (an intermediate phase, after the
  design in `docs/design/interface.figcraft`): close, minimize, the title,
  and toolbar items in one bar, with `<ToolbarItem>` and `<TitleField>` in
  UIScript; minimized windows in the dock; Files with an address field, Go,
  and bookmarks for Home, User, Apps, and System; Notes, moved to the
  user's apps, with Add and Remove in its toolbar. The desktop stops
  showing what apps need.
- [x] Phase 4, timers: `window:every(seconds, fn)`, and System Monitor
  (system): version, uptime, memory, and network, kept current.
- [x] Phase 5, multi-line text: a `<TextArea>` element in UIScript's core,
  written up for Hudini's rolling spec; and TextEdit (user), which opens,
  edits, and saves text files.
- [x] Phase 6, Terminal.app (system): the shell in a read-only
  `<TextArea>` that follows its newest text, in Fira Code.

### Downeaster: Desktop refinement

A follow-up to Keystone in the spirit of Snow Leopard: refining what's
there rather than adding apps. The title bar follows CityLimits', and
sidebars follow Nautilus in GNOME 45, running the window's full height
with the close and minimize buttons on top, as Big Sur's Finder does.

- [x] Phase 1, the title: titles are centred in the bar, and a
  `<TitleField>` reads as a plain title until it's clicked or given focus,
  then becomes a field across the room. Files drops Go for Enter, and its
  address shows the full path, keeping its end when cut.
- [x] Phase 2, `<Sidebar>` in UIScript: a flat list down the window's left,
  with a name and a width and no position, behind `requires="sidebar"`, and
  drafted for Hudini in [uiscript-sidebar.md](./uiscript-sidebar.md).
- [x] Phase 3, the sidebar in the frame: drawn the window's full height in
  its own shade, with the buttons on top and the title bar over the
  content only. Files' bookmarks move into one.
- [x] Phase 4, resizable windows: `resizable` on `<Window>`, ignorable by
  older readers, starting at its size and no smaller than its `minWidth`
  and `minHeight` (which default to it); controls follow Hudini's edge
  offsets, rather than a new `anchor` attribute, by Tony Bark's choice; a
  grip of dots in the corner. Files and TextEdit resize; Terminal waits,
  since it lays out by character cells.
- [x] Phase 5, hover: whatever the pointer rests on shows it, in any window
  and with no change to apps. The close and minimize buttons show their
  marks, the title field's text takes the accent, buttons and toolbar
  items lighten, and sidebar rows are shaded.
- [x] Phase 6, a switchable theme: the desktop's colours come from a
  current palette, with a dark palette beside today's light one, both
  built from an accent colour after Distro Tycoon 2, with its swatches.
  Everything without a style follows. `venture.setAppearance` and the
  `appearance` command switch it until reboot, ahead of Settings.
- [x] Phase 7, style roles: colour roles (`textColor="secondary"`) and
  font-size roles (`fontSize="title"`) on `<Style>`, written as if in
  Hudini's spec and drafted in `docs/uiscript-styles.md`, with layered
  `dark*` colours for apps that need literal ones. The shipped apps move to
  roles, and a test checks every shipped interface stays readable in both
  palettes.
- [x] Phase 8, Settings (system): light or dark and the accent colour,
  saved in `/mnt/user`, so the choice lasts until reboot now and across
  reboots once Blue Water persists files.
- [x] Phase 9 (optional), hover text: a transitional reader for a strict
  subset of the CST spec, resolving `<String>` resources from tables in an
  app's bundle, so Hudini's `tooltip` works, until CST.NET is reworked.

Appearance changes reach apps without changing them; only changes to
layout, such as sidebars and resizing, are opt-in. Collapsing a sidebar in
a narrow window is left for later.

## Critical path

In 0.4, the standard menus come first, since Edit merges into the menu bar
they build. Text selection comes before Edit, which acts on what's selected.
Networking stands apart and could move earlier without blocking the rest.
Kernel-free logic still goes into `Venture.OS.Common` with tests, and the
interface formats into `Venture.OS.Interface`.

## Risks most likely to push estimates up

- Cosmos renames APIs between releases (3.0.87 to 3.0.94 moved the
  filesystem, timer, network, and storage namespaces), and its docs follow
  the latest release only.
- Only `Venture.OS.Common` and the Lua system files are tested; kernel code
  is checked by booting by hand.
- `Cosmos.Executable.Lua` is young (4.0.1, from 2026-10); a bug there lands
  in every script.
- Nothing has run on real hardware, and the Cosmos docs warn against testing
  filesystem code there.
- The Cosmos TCP stack doesn't retransmit, which limits networking on real
  networks.

## Revision history

| Date | Change | Summary |
|---|---|---|
| 2026-11-11 | minor | 0.4.2xx done: text selection in fields, areas, and the title field, by Shift and the arrows, Ctrl+A, and dragging. About 21–41 h of milestones remain. |
| 2026-11-11 | minor | 0.4.1xx done: standard File and Help menus for every app, with About from `Info.xml`'s `description`, menus merged by `role` or title, and an app's own shortcut winning. About 25–49 h of milestones remain. |
| 2026-11-11 | major | Tony Bark moved VentureOS to releases: no schedule, .NET-style feature bands, only the latest release supported until 1.0, tags and notes now and ISOs later, and code names for releases. 0.3 is released as `v0.3.18`, holding Borealis, Keystone, Downeaster, and the repository's new layout. 0.4, Pere Marquette, the last Amtrak name, holds standard menus, text selection, Edit, and networking; Telnet is dropped. About 29–57 h of milestones remain. |
| 2026-11-11 | minor | Downeaster phase 9, the optional one: hover text, through a transitional reader for the settled part of the CST specification; Settings' swatches name themselves. About 18–35 h of milestones remain. |
| 2026-11-11 | minor | Downeaster complete: phase 8, Settings, with every change saved to `/mnt/user/settings/appearance.xml` and restored by a new optional boot step. Hover text stays optional. About 18–35 h of milestones remain. |
| 2026-11-11 | minor | Downeaster 88%: phase 7, colour and size roles and `dark` twins on `<Style>`, drafted in `docs/uiscript-styles.md`; the shipped apps use roles, and a contrast test covers every shipped interface in every palette. About 21–41 h of milestones remain. |
| 2026-11-11 | minor | Downeaster 75%: phase 6, light and dark palettes from an accent after Distro Tycoon 2, switched by `venture.setAppearance` and the `appearance` command. About 25–49 h of milestones remain. |
| 2026-11-11 | major | Tony Bark extended Downeaster with hover, a dark palette and accent colours after Distro Tycoon 2, style roles drafted for Hudini, Settings, and optional hover text through a transitional CST reader; the docs-and-boot phase and the every-stage list (CI, ARM64 boots, ISO releases) are dropped, and beta needs only Blue Water. Phase 5, hover, is done (63% of the required phases). About 29–57 h of milestones remain. |
| 2026-11-11 | minor | Downeaster 80%: phase 4, resizable windows with a grip, following Hudini's edge offsets instead of a new `anchor` attribute; Files and TextEdit resize. About 19–38 h of milestones remain. |
| 2026-11-11 | minor | Downeaster 60%: phase 3, sidebars drawn the window's full height with the buttons on top, and Files' bookmarks in one. Phase 4 uses Hudini's `minWidth` and `minHeight` for the smallest size. About 24–47 h of milestones remain. |
| 2026-11-11 | minor | Downeaster 40%: phase 2, `<Sidebar>` in UIScript behind the `sidebar` feature, drafted for Hudini. About 26–51 h of milestones remain. |
| 2026-11-11 | minor | Downeaster planned in five phases, after Tony Bark's idea: centred titles and an address that edits on click, sidebars like Nautilus in GNOME 45, and resizable windows with anchors. Phase 1, the title, is done (20%). About 28–55 h of milestones remain. |
| 2026-11-11 | minor | Keystone complete: phase 6, Terminal.app, with shells of their own for apps, a monospace font in styles, and text areas that follow their newest text. About 18–35 h of milestones remain. |
| 2026-11-11 | minor | Keystone 83%: phase 5, `<TextArea>` and TextEdit, which Files opens text files in. Terminal.app is the last phase. About 24–47 h of milestones remain. |
| 2026-11-11 | minor | Keystone 67%: phase 4, `window:every` timers and System Monitor. About 30–59 h of milestones remain. |
| 2026-11-11 | minor | Keystone 50%: an intermediate phase 3, the unified title bar and minimize from the new design, with Files' address field and bookmarks and Notes in the user's apps; timers, `<TextArea>`, and Terminal.app become phases 4 to 6. |
| 2026-11-11 | minor | Keystone 40%: phase 2, user apps in `/mnt/user/apps`, divided from the system's in the dock, and Calculator; list items open with a double-click. About 33–65 h of milestones remain. |
| 2026-11-11 | minor | Keystone 20%: phase 1, Files, the desktop's fallback app, with Quick Look; the desktop restarts it if it stops. About 36–71 h of milestones remain. |
| 2026-11-11 | major | Keystone planned in five phases: Files as the desktop's fallback, user apps in `/mnt/user/apps`, timers and System Monitor, `<TextArea>` and TextEdit, and Terminal.app, moved from Borealis. Borealis is complete. About 44–86 h of milestones remain. |
| 2026-11-11 | minor | Version 0.3.0: the desktop, with all five Borealis phases done. |
| 2026-11-11 | minor | Borealis 95%: phase 5, apps open windows through `venture.ui` and quit with their last window, and Notes has one. The desktop is ready for 0.3; Terminal.app is next. |
| 2026-11-11 | minor | Borealis 80%: phase 4, the menu bar (VentureOS, the app's, and the window's menus, with Alt hotkeys and Ctrl shortcuts) and a dock that opens the system's apps. |
| 2026-11-11 | minor | Borealis 60%: phase 3, windows show `.uix` interfaces, drag, focus, and close; About VentureOS opens with the desktop, and `desktop` previews `.uix` files. |
| 2026-11-11 | minor | Borealis 40%: phase 2, `desktop` draws the sky and pointer at 1024x768 and returns to the shell on Esc. |
| 2026-11-11 | major | Borealis planned in five phases to 0.3 (a `desktop` command, 1024×768, Open Sans and Fira Code), with Terminal.app right after; menus join UIScript as a `menus` feature. Phase 1, the UIScript reader, is done. |
| 2026-11-11 | minor | The desktop (Borealis) will mark version 0.3. |
| 2026-11-11 | minor | Saluki 90%: `Notes.app` ships, with a tested guide to writing apps. Only installing apps from outside the system remains, after persistence; Borealis is the current focus. |
| 2026-11-11 | minor | Saluki 80%: `open`, `close`, and `apps`; apps run as owners with their declared needs. A shipped `Notes.app` is next. |
| 2026-11-11 | minor | Saluki 67%: the bundle format and its reader. `System.Xml` doesn't link under Cosmos 3.0.94, so XML is read by VentureOS's own flat reader, which Borealis's `.uix` files will share. |
| 2026-11-11 | minor | Blue Water 67%: `venture.files` and the file commands on the RAM disk. Persistence stays for later; Saluki's `.app` bundles are the current focus. |
| 2026-11-10 | minor | Blue Water 25%: the shell's current folder and `cd`. |
| 2026-11-10 | major | RAM-first: Blue Water is now files, with persistence later and optional; `.app` bundles ship with the system, so they no longer wait on it; Borealis is scoped as a Tiger-style desktop with `.uix` (UIScript) interfaces, after Distro Tycoon 2. About 66–131 h of milestones remain, plus 14–28 h every-stage. |
| 2026-11-10 | minor | Saluki 60%: the `lua` prompt is an owner, with `!` and `venture.shell`. Runtime commands are done; `.app` bundles wait on Blue Water, which is now the current focus. |
| 2026-11-10 | minor | Saluki 50%: owners with undo, and `venture.command` for `init.lua`. The `lua` prompt as an owner is next. |
| 2026-11-10 | minor | Wolverine complete: the system library in `/mnt/system/lib` and `venture.args`. Saluki's runtime commands are the current focus; about 41–82 h of milestones remain. |
| 2026-11-10 | major | Routes repurposed: Wolverine is now the system library (`venture.args`), Saluki apps (runtime commands with undo, then `.app` bundles), and Borealis a possible GUI. CI checks, ARM64, and releases became every-stage work. About 46–92 h of milestones remain, plus 14–28 h every-stage. |
| 2026-11-10 | minor | Illini: plain `lua` opens an interactive Lua prompt for debugging. Undoable changes are next. |
| 2026-11-10 | minor | Illini: scripts declare `-- Needs:` scopes, after the capability ideas in "A Programming Paradigm for Spatiotemporal Composability". Undoable changes are the planned next step. |
| 2026-11-10 | minor | Illini: Ctrl+C or Esc now stops running Lua, and `init.lua` has a 10-second limit, closing the endless-loop gap. |
| 2026-11-10 | minor | Illini complete: phase 4, `init.lua` runs after boot. CI checks (Wolverine) are the current focus again; about 39–78 h remain overall. |
| 2026-11-06 | minor | Illini 80%: phase 3, the `venture` table, with `info` and `net` moved to Lua. 2–4 h left; about 41–82 h overall. |
| 2026-11-06 | minor | Illini 60%: phase 2, script commands from `/mnt/system/commands`, with `echo` moved to Lua. 4–8 h left; about 43–86 h overall. |
| 2026-11-06 | minor | Illini repurposed: Lua system scripts installed to `/mnt/system`, in four phases, and phase 1 is done. It's now the current focus, with 6–12 h left; about 45–90 h remain overall. |
| 2026-11-05 | minor | Illini complete: `Cosmos.Executable.Lua` replaces `LuaCSharp`, and `lua <code>` runs a chunk from the shell, verified in an x64 boot. About 39–78 h remain overall. |
| 2026-10-15 | minor | Wolverine 33%: the shell framework moved into the kernel-free `Venture.OS.Common` library, with 59 xUnit v3 tests that run locally and in CI. Wolverine has 5–10 h left; beta needs about 20–40 h. |
| 2026-10-15 | major | Hiawatha complete: a command registry with generated `help`, argument parsing with quoting, and `info`, `net`, `reboot`, and `shutdown`. Both shell rows of the alpha gate pass, so the stage is alpha. |
| 2026-10-15 | minor | Cascades complete: a forced panic proven in a headless boot, and system messages copied to serial with a `[Venture]` prefix through `KernelLog`. Alpha now waits on Hiawatha alone, 10–20 h. |
| 2026-10-15 | minor | Boot delay kept by my decision (BUG-1 resolved): boot is otherwise too fast to read. Cascades at 75%, 2–5 h remaining. |
| 2026-10-15 | major | First roadmap: a usable-shell alpha gate (pre-alpha: the shell blocks), eight milestones named after Amtrak Venture routes, and estimates. Persistent storage deferred past alpha by my decision. |
