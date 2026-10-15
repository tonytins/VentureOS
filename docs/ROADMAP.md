# VentureOS roadmap

> **Last reviewed:** 2026-10-15 · **Last updated:** 2026-10-15 · **Change:** major (Hiawatha complete; the alpha gate passes) · **Stage:** alpha

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

1. **Tests and CI** (Wolverine): a test project for the argument parser, a
   headless boot check in CI, and an ARM64 build in CI.
2. **Choose the Lua library** (Illini): I'm tracking down a
   Cosmos-based Lua API to use in place of, or beside, `LuaCSharp`.

## Milestones

"Done" is the share of the milestone's checklist below that is complete.
"Remaining" estimates the open items only.

| Route | Milestone | Done | Remaining (h, est.) | Notes |
|---|---|---:|---:|---|
| Cascades | Reliable boot | 100% | 0 | Boot steps, a proven panic, range-checked RAM disk, system messages on serial, and the boot delay kept |
| Hiawatha | Shell | 100% | 0 | Command registry, argument parsing, `info`, `net`, `reboot`, and `shutdown` |
| Wolverine | Tests and CI | 0% | 8–16 | Test project, headless boot check in CI, ARM64 build in CI |
| Blue Water | Persistent storage | 0% | 15–30 | Deferred past alpha. Real disks, file commands |
| Pere Marquette | Networking | 0% | 10–20 | DNS, network commands, maybe Telnet |
| Illini | Lua scripting | 0% | 10–25 | Library not chosen yet |
| Saluki | ARM64 | 0% | 6–12 | Boot and test on ARM64 |
| Borealis | Releases | 0% | 3–6 | Publish ISOs |
| | **Total** | | **≈ 52–109** | |

## Upcoming work, ranked

| Rank | Work | Milestone | Estimate (h) |
|---|---|---|---|
| 1 | Test project for the argument parser and other kernel-free logic | Wolverine | 3–6 |
| 2 | Headless boot check and ARM64 build in CI | Wolverine | 5–10 |
| 3 | File commands, then mounting a real disk | Blue Water | 15–30 |
| 4 | Choose and wire up a Lua library | Illini | 10–25 |
| 5 | DNS and network commands | Pere Marquette | 10–20 |

Beta needs ranks 1–3 in substance, about 23–46 hours.

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

- [ ] A test project for logic that doesn't need the kernel, starting with
  the argument parser.
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

### Illini: Lua scripting

- [ ] Choose the library: the Cosmos-based Lua API I'm tracking
  down, or `LuaCSharp`.
- [ ] Confirm it works under the ahead-of-time compiler.
- [ ] Run a Lua script from the shell.
- [ ] Remove whichever Lua package goes unused.

### Saluki: ARM64

- [ ] Boot on ARM64 under QEMU with a virtio NIC.
- [ ] Run the CI boot check on ARM64.

### Borealis: Releases

- [ ] Publish ISOs from tagged builds.

## Critical path

Cascades and Hiawatha, the alpha milestones, are done. Wolverine's test
project should start with the shell's argument parser (`CommandLine`), the
first logic worth testing outside the kernel. Blue Water's file commands
build on the shell's registry and arguments. Illini waits on my library choice. The long
poles are Blue Water and Illini.

## Risks most likely to push estimates up

- Cosmos renames APIs between releases (3.0.87 to 3.0.94 moved the
  filesystem, timer, network, and storage namespaces), and its docs follow
  the latest release only.
- With no tests, regressions are found by booting by hand until Wolverine
  lands.
- A Lua library may not work under the ahead-of-time compiler, or may need
  plugs.
- Nothing has run on real hardware, and the Cosmos docs warn against testing
  filesystem code there.
- The Cosmos TCP stack doesn't retransmit, which limits networking on real
  networks.

## Revision history

| Date | Change | Summary |
|---|---|---|
| 2026-10-15 | major | Hiawatha complete: a command registry with generated `help`, argument parsing with quoting, and `info`, `net`, `reboot`, and `shutdown`. Both shell rows of the alpha gate pass, so the stage is alpha. |
| 2026-10-15 | minor | Cascades complete: a forced panic proven in a headless boot, and system messages copied to serial with a `[Venture]` prefix through `KernelLog`. Alpha now waits on Hiawatha alone, 10–20 h. |
| 2026-10-15 | minor | Boot delay kept by my decision (BUG-1 resolved): boot is otherwise too fast to read. Cascades at 75%, 2–5 h remaining. |
| 2026-10-15 | major | First roadmap: a usable-shell alpha gate (pre-alpha: the shell blocks), eight milestones named after Amtrak Venture routes, and estimates. Persistent storage deferred past alpha by my decision. |
