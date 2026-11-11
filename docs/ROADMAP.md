# VentureOS roadmap

> **Last reviewed:** 2026-11-10 · **Last updated:** 2026-11-10 · **Change:** minor (Wolverine complete: the system library and `venture.args`) · **Stage:** alpha

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

1. **Apps, part 1** (Saluki): commands registered at runtime, each with its
   own undo, owned by whatever registered them.
2. **Persistent storage** (Blue Water), then **apps, part 2** (Saluki):
   `.app` bundles that install and uninstall without a reboot.

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
| Blue Water | Persistent storage | 0% | 15–30 | Deferred past alpha. Real disks, file commands |
| Pere Marquette | Networking | 0% | 10–20 | DNS, network commands, maybe Telnet |
| Illini | Lua system scripts | 100% | 0 | System files install to `/mnt/system`, `<name>.lua` there runs as a command, the `venture` table lets `echo`, `info`, and `net` be scripts, and `init.lua` runs after boot |
| Saluki | Apps | 0% | 16–32 | Runtime commands with undo, then `.app` bundles (installs wait on Blue Water) |
| Borealis | GUI (possible) | 0% | — | Unscoped: a desktop after the classic Mac OS X look |
| | **Total** | | **≈ 41–82**, plus every-stage work (≈ 14–28) | |

## Upcoming work, ranked

| Rank | Work | Milestone | Estimate (h) |
|---|---|---|---|
| 1 | Commands registered at runtime, with undo | Saluki | 6–12 |
| 2 | File commands, then mounting a real disk | Blue Water | 15–30 |
| 3 | `.app` bundles | Saluki | 10–20 |
| 4 | DNS and network commands | Pere Marquette | 10–20 |

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

### Blue Water: Persistent storage

- [ ] File commands on the RAM disk: `ls`, `cat`, `write`, `mkdir`, `rm`,
  `cd`, `pwd`, and `df`.
- [ ] Mount a FAT partition from a real disk at boot, as an optional step.
- [ ] Decide whether VentureOS formats blank disks itself.

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

- [ ] Owners: something that holds an interpreter and the undo list for what
  it registered, such as `init.lua` (the whole boot) or a `lua` prompt
  session.
- [ ] Register a command at runtime, removed again when its owner goes.
- [ ] Decide whether commands made at the `lua` prompt vanish on `exit`.
- [ ] `.app` bundles: install and uninstall without a reboot, declaring
  `-- Needs:` for the whole app. Waits on Blue Water, since an install has
  to outlive a reboot.

### Borealis: GUI (possible)

Not scoped yet. The idea, from my notes: an experimental desktop after the
classic Mac OS X look, with `.app` bundles (Saluki) as its applications.

- [ ] Decide whether, and what the first version shows.

## Critical path

Cascades, Hiawatha, and Illini are done. Wolverine's `venture.args` comes
first because Blue Water's file commands (`ls -l`, `rm -r`) are its first
real users. Saluki's runtime commands need only Illini, but `.app` installs
need Blue Water, since an install has to outlive a reboot. Borealis would
build on Saluki's apps. New kernel-free logic still goes into
`Venture.OS.Common` with tests. The long poles are Blue Water and Saluki.

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
