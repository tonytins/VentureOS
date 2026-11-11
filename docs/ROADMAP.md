# VentureOS roadmap

> **Last reviewed:** 2026-11-10 · **Last updated:** 2026-11-10 · **Change:** minor (Illini: the Lua prompt) · **Stage:** alpha

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
Hiawatha complete. Beta work starts with tests and CI (Wolverine).

Persistent storage is deferred until after alpha by my decision (Blue Water).

## Current focus

In order:

1. **CI checks** (Wolverine): a headless boot check in CI, and an ARM64
   build in CI.

## Milestones

"Done" is the share of the milestone's checklist below that is complete.
"Remaining" estimates the open items only.

| Route | Milestone | Done | Remaining (h, est.) | Notes |
|---|---|---:|---:|---|
| Cascades | Reliable boot | 100% | 0 | Boot steps, a proven panic, range-checked RAM disk, system messages on serial, and the boot delay kept |
| Hiawatha | Shell | 100% | 0 | Command registry, argument parsing, `info`, `net`, `reboot`, and `shutdown` |
| Wolverine | Tests and CI | 33% | 5–10 | Test project done (186 tests, run in CI). Missing: headless boot check and ARM64 build in CI |
| Blue Water | Persistent storage | 0% | 15–30 | Deferred past alpha. Real disks, file commands |
| Pere Marquette | Networking | 0% | 10–20 | DNS, network commands, maybe Telnet |
| Illini | Lua system scripts | 100% | 0 | System files install to `/mnt/system`, `<name>.lua` there runs as a command, the `venture` table lets `echo`, `info`, and `net` be scripts, and `init.lua` runs after boot |
| Saluki | ARM64 | 0% | 6–12 | Boot and test on ARM64 |
| Borealis | Releases | 0% | 3–6 | Publish ISOs |
| | **Total** | | **≈ 39–78** | |

## Upcoming work, ranked

| Rank | Work | Milestone | Estimate (h) |
|---|---|---|---|
| 1 | Headless boot check and ARM64 build in CI | Wolverine | 5–10 |
| 2 | File commands, then mounting a real disk | Blue Water | 15–30 |
| 3 | DNS and network commands | Pere Marquette | 10–20 |

Beta needs ranks 1–2 in substance, about 20–40 hours.

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

### Wolverine: Tests and CI

- [x] A test project for logic that doesn't need the kernel, starting with
  the argument parser: `Venture.OS.Common.Tests` covers the shell framework,
  which moved into the kernel-free `Venture.OS.Common` library.
- [ ] CI boots the kernel headless and fails when the serial log shows a
  panic or never reaches the shell.
- [ ] CI builds ARM64 as well as x64.

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

### Saluki: ARM64

- [ ] Boot on ARM64 under QEMU with a virtio NIC.
- [ ] Run the CI boot check on ARM64.

### Borealis: Releases

- [ ] Publish ISOs from tagged builds.

## Critical path

Cascades and Hiawatha, the alpha milestones, are done, and Wolverine's test
project is in. New kernel-free logic, such as Blue Water's path handling,
should go into `Venture.OS.Common` with tests. Blue Water's file commands
build on the shell's registry and arguments, and give Lua scripts somewhere
to live across boots, beside the read-only `/mnt/system`. Illini is done.
The long pole is Blue Water.

## Risks most likely to push estimates up

- Cosmos renames APIs between releases (3.0.87 to 3.0.94 moved the
  filesystem, timer, network, and storage namespaces), and its docs follow
  the latest release only.
- Only `Venture.OS.Common` is tested; kernel code is checked by booting by
  hand until CI boots the kernel (Wolverine).
- A Lua library may not work under the ahead-of-time compiler, or may need
  plugs.
- Nothing has run on real hardware, and the Cosmos docs warn against testing
  filesystem code there.
- The Cosmos TCP stack doesn't retransmit, which limits networking on real
  networks.

## Revision history

| Date | Change | Summary |
|---|---|---|
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
