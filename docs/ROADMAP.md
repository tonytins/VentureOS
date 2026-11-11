# VentureOS roadmap

> **Last reviewed:** 2026-11-11 · **Last updated:** 2026-11-11 · **Change:** minor (Borealis phase 4: the menu bar and dock) · **Stage:** alpha

Forward-looking: where VentureOS stands, what blocks the next stage, and what
comes next. Verified status and evidence are in the
[scorecard](./scorecard.md).

Keep it current: when work lands, update its milestone row and checklist,
re-estimate what remains, and add a line to the revision history.

Milestones are named after Amtrak routes that run, or are moving to, Siemens
Venture cars, the trains VentureOS is named after. The names are labels, not
a release order. Estimates are rough hours for one developer and are
re-measured when a milestone lands.

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

1. **Desktop** (Borealis): a UIScript reader, drawing and a frame loop, then
   windows from `.uix` files and a window manager.

VentureOS runs from RAM, as many small systems do; keeping changes across
reboots is later, optional work (Blue Water).

## Every stage

Not a milestone: each stage carries its own tests, and these land when they
fit, rather than waiting on a route.

- [ ] CI boots the kernel headless and fails when the serial log shows a
  panic or never reaches the shell.
- [ ] CI builds ARM64 as well as x64.
- [ ] Boot on ARM64 under QEMU with a virtio NIC, and run the CI boot check
  there.
- [ ] Publish ISOs from tagged builds.

## Milestones

"Done" is the share of the milestone's checklist below that is complete.
"Remaining" estimates the open items only.

| Route | Milestone | Done | Remaining (h, est.) | Notes |
|---|---|---:|---:|---|
| Cascades | Reliable boot | 100% | 0 | Boot steps, a proven panic, range-checked RAM disk, system messages on serial, and the boot delay kept |
| Hiawatha | Shell | 100% | 0 | Command registry, argument parsing, `info`, `net`, `reboot`, and `shutdown` |
| Wolverine | System library | 100% | 0 | Shared Lua modules in `/mnt/system/lib`, loaded with `require`; `venture.args` parses arguments and options with a generated `--help` |
| Blue Water | Files | 67% | 8–15 (persistence, later) | File commands on the RAM disk now; an optional save to disk later |
| Pere Marquette | Networking | 0% | 10–20 | DNS, network commands, maybe Telnet |
| Illini | Lua system scripts | 100% | 0 | System files install to `/mnt/system`, `<name>.lua` there runs as a command, the `venture` table lets `echo`, `info`, and `net` be scripts, and `init.lua` runs after boot |
| Saluki | Apps | 90% | later | Runtime commands, bundles, and opening and closing apps are done, with `Notes.app` and a guide; installing apps from outside the system waits on persistence |
| Borealis | Desktop | 80% | 8–20 | A Tiger-style desktop: `.uix` (UIScript) interfaces, windows, a menu bar, and a dock |
| | **Total** | | **≈ 66–131**, plus every-stage work (≈ 14–28) | |

## Upcoming work, ranked

| Rank | Work | Milestone | Estimate (h) |
|---|---|---|---|
| 1 | A UIScript reader, then drawing and a frame loop | Borealis | 15–30 |
| 2 | DNS and network commands | Pere Marquette | 10–20 |

Beta needs the CI boot check (every stage) and Blue Water in substance,
about 20–40 hours.

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

The former Wolverine checklist (the test project, now done, and CI checks)
became the every-stage list above.

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

### Pere Marquette: Networking

- [ ] Register the DNS server DHCP reports.
- [ ] `resolve` and `ping` commands.
- [ ] Decide whether to serve a Telnet shell.

### Illini: Lua system scripts

Move what doesn't need the kernel into Lua scripts that live in their own
system directory, `/mnt/system`. Power, `help`, `clear`, and `lua` stay in
C#, so the shell still works if the scripts don't.

- [x] Lua 5.5 through `Cosmos.Executable.Lua` 4.0.1, in place of
  `LuaCSharp`, and a one-shot `lua <code>` command.
- [x] Phase 1, scaffolding: files under `src/Venture.OS/system/` are built
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
- [ ] Phase 5, apps with windows: `venture.ui` and the `ui` scope; an app
  quits when its last window closes; Notes gets a window.

When the desktop is up and running, VentureOS moves to version 0.3 (a new
base in `GitInfo.txt`), by my decision. Right after it:

- [ ] Terminal.app: the shell in a window.

## Critical path

Cascades, Hiawatha, Illini, and Wolverine are done, and so are Saluki's
runtime commands. Blue Water's file commands come next, as the first real
users of `venture.args`. Because VentureOS runs from RAM, `.app` bundles ship
with the system and don't wait on persistence. Borealis builds on Saluki's
apps, which become its windows. New kernel-free logic, such as the UIScript
reader, still goes into `Venture.OS.Common` with tests. The long pole is
Borealis.

## Risks most likely to push estimates up

- Cosmos renames APIs between releases (3.0.87 to 3.0.94 moved the
  filesystem, timer, network, and storage namespaces), and its docs follow
  the latest release only.
- Only `Venture.OS.Common` and the Lua system files are tested; kernel code
  is checked by booting by hand until CI boots the kernel (every stage).
- `Cosmos.Executable.Lua` is young (4.0.1, from 2026-10); a bug there lands
  in every script.
- Nothing has run on real hardware, and the Cosmos docs warn against testing
  filesystem code there.
- The Cosmos TCP stack doesn't retransmit, which limits networking on real
  networks.

## Revision history

| Date | Change | Summary |
|---|---|---|
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
