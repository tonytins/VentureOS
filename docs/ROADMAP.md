# VentureOS roadmap

> **Last reviewed:** 2026-10-15 · **Last updated:** 2026-10-15 · **Change:** major (first roadmap) · **Stage:** pre-alpha

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
| Boot, and say clearly why when boot can't finish | partial, not blocking | Boots to the shell; critical steps panic with a report and optional steps degrade (BOOT-1 to BOOT-5). The panic has never been triggered in a real boot (BOOT-6) | 3–6 |
| Run commands that take arguments | **no** | Input is matched as a whole line, and every command is a `case` in one `switch` (SHELL-3, SHELL-5) | 6–12 |
| Inspect and control the system: version, network, reboot, shutdown | **no** | The boot banner shows the version, and `halt` stops the machine. No command shows the network afterwards, and there's no reboot or shutdown (SHELL-4) | 4–8 |

**Result: pre-alpha.** The shell rows block, about 10–20 hours of work
(Hiawatha). With Cascades finished as well, alpha is about 13–26 hours away.

Persistent storage is deferred until after alpha by the owner (Blue Water).

## Current focus

In order:

1. **Finish Cascades:** trigger the panic in a real boot, copy key console
   lines to serial so headless boots can check them, and decide whether to
   keep the boot delay (BUG-1).
2. **Build the shell** (Hiawatha): a command registry, argument parsing, and
   system commands.
3. **Choose the Lua library** (Illini): the owner is tracking down a
   Cosmos-based Lua API to use in place of, or beside, `LuaCSharp`.

## Milestones

"Done" is the share of the milestone's checklist below that is complete.
"Remaining" estimates the open items only.

| Route | Milestone | Done | Remaining (h, est.) | Notes |
|---|---|---:|---:|---|
| Cascades | Reliable boot | 63% | 3–6 | Boot steps, panic, and range-checked RAM disk done. Missing: a proven panic, serial output, the boot delay decision |
| Hiawatha | Shell | 0% | 10–20 | Command registry, arguments, system commands |
| Wolverine | Tests and CI | 0% | 8–16 | Test project, headless boot check in CI, ARM64 build in CI |
| Blue Water | Persistent storage | 0% | 15–30 | Deferred past alpha. Real disks, file commands |
| Pere Marquette | Networking | 0% | 10–20 | DNS, network commands, maybe Telnet |
| Illini | Lua scripting | 0% | 10–25 | Library not chosen yet |
| Saluki | ARM64 | 0% | 6–12 | Boot and test on ARM64 |
| Borealis | Releases | 0% | 3–6 | Publish ISOs |
| | **Total** | | **≈ 65–135** | |

## Upcoming work, ranked

| Rank | Work | Milestone | Estimate (h) |
|---|---|---|---|
| 1 | Prove the panic, serial output, the boot delay decision | Cascades | 3–6 |
| 2 | Command registry and argument parsing | Hiawatha | 6–12 |
| 3 | `info`, `net`, `reboot`, and `shutdown` commands | Hiawatha | 4–8 |
| 4 | Test project for the argument parser and other kernel-free logic | Wolverine | 3–6 |
| 5 | Headless boot check and ARM64 build in CI | Wolverine | 5–10 |
| 6 | File commands, then mounting a real disk | Blue Water | 15–30 |
| 7 | Choose and wire up a Lua library | Illini | 10–25 |
| 8 | DNS and network commands | Pere Marquette | 10–20 |

Beta needs ranks 1–6 in substance, about 36–72 hours.

## Milestone checklists

### Cascades: Reliable boot

- [x] Boot steps split into critical (RAM filesystem, timer) and optional
  (network).
- [x] Critical failures panic deliberately with a report on serial and the
  console.
- [x] Optional failures are reported and boot continues.
- [x] Each boot step says which part failed.
- [x] The RAM disk validates its size and range-checks every block access.
- [ ] Trigger the panic in a real boot and confirm the report and the halt.
- [ ] Copy key console lines (step results, the banner, panics) to serial.
- [ ] Decide whether to keep the 0.5 s pauses around each boot step.

### Hiawatha: Shell

- [ ] A command registry: each command declares its name, help text, and
  handler, and `help` is generated from it.
- [ ] Argument parsing, with quoting, and clear errors for bad arguments.
- [ ] `info`: version, build, uptime, and memory.
- [ ] `net`: adapter, MAC, link, and address.
- [ ] `reboot` and `shutdown` beside `halt`.

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

- [ ] Choose the library: the Cosmos-based Lua API the owner is tracking
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

Cascades and Hiawatha are alpha and can run in parallel. Wolverine's test
project should start with Hiawatha's argument parser, since that's the first
logic worth testing outside the kernel. Blue Water's file commands need
Hiawatha's arguments. Illini waits on the owner's library choice. The long
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
| 2026-10-15 | major | First roadmap: a usable-shell alpha gate (pre-alpha: the shell blocks), eight milestones named after Amtrak Venture routes, and estimates. Persistent storage deferred past alpha by the owner. |
