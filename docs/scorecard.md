# VentureOS scorecard

Maintained by hand. Every status below was checked against the code, a build,
or a boot on 2026-10-15, and each row names the file, command, or boot log
that proves it. Numbers here supersede estimates elsewhere. What comes next
is in the [roadmap](./ROADMAP.md).

When a change affects a row, update its status and evidence in the same
change. Statuses are `done`, `partial`, and `missing`.

## Summary

| Area | Done | Partial | Missing | Key numbers |
|---|---:|---:|---:|---|
| [Boot](#boot) | 5 | 1 | 0 | 3 boot steps: 2 critical, 1 optional |
| [Storage](#storage) | 2 | 0 | 2 | 32 MiB RAM disk, FAT16 at `/mnt` |
| [Network](#network) | 2 | 0 | 2 | DHCP lease 10.0.2.15 under QEMU |
| [Shell](#shell) | 2 | 1 | 2 | 3 commands |
| [Platform](#platform) | 3 | 0 | 2 | Cosmos 3.0.94, .NET 10; x64 ISO 10.6 MB |
| [Reliability](#reliability) | 2 | 1 | 2 | 0 tests |
| [Documentation](#documentation) | 2 | 1 | 1 | |
| [Distribution](#distribution) | 1 | 0 | 3 | CI builds x64 only |
| [Known issues](#known-issues) | | | | 2 open, 1 resolved |

There is no test project yet, so most evidence is a build or a boot. Boots
were run headless under the QEMU that Cosmos bundles:

```sh
cosmos run -p Venture.OS --headless            # default e1000e NIC
cosmos run -p Venture.OS --headless --nic none # no network card
```

The serial log shows Cosmos's own boot lines but not VentureOS's console
output, so a boot proves that boot steps passed (no panic) and that the shell
loop started, not what was printed.

## Boot

| Id | Target | Status | Evidence |
|---|---|---|---|
| BOOT-1 | The kernel boots to the shell loop on x64 | done | Headless boot on 3.0.94: `[Kernel] Entering main loop...`, `[Kernel] Calling Run()...` |
| BOOT-2 | Critical steps (RAM filesystem, timer) panic deliberately on failure or exception | done | `Kernel.RunCriticalStep`; `Bootstrap.BootIntoRam` and `CheckTimeManager` return `bool` |
| BOOT-3 | Optional steps (network) report a failure and boot continues | done | `Kernel.RunOptionalStep`; boot with `--nic none` reaches `Calling Run()` |
| BOOT-4 | A panic reports the version, failed step, and reason to serial and the console, then stays halted | done | `KernelPanic.Halt`: `Log` first, then `Console`, then `Power.Halt()` in a loop |
| BOOT-5 | Each boot step says which part failed | done | `BootIntoRam` reports register, format, or mount; `ConnectToNetwork` reports no device, DHCP timeout, or no address |
| BOOT-6 | The panic path is proven by a boot that triggers it | partial | Code reviewed only; no boot has forced a critical step to fail yet |

## Storage

| Id | Target | Status | Evidence |
|---|---|---|---|
| STOR-1 | A RAM disk is formatted FAT16 and mounted at `/mnt` | done | `Bootstrap.BootIntoRam`; headless boot passes the critical filesystem step |
| STOR-2 | The RAM disk validates its size and range-checks every read and write, throwing as `IBlockDevice` requires | done | `BootRamDisk` constructor and `GetByteRange` |
| STOR-3 | Files persist across boots on a real disk | missing | Only the RAM disk is mounted; nothing reads `StorageManager.Partitions` |
| STOR-4 | The shell can list, read, and write files | missing | No file commands in `Kernel.ExecuteCommand` |

## Network

| Id | Target | Status | Evidence |
|---|---|---|---|
| NET-1 | DHCP configures the primary NIC and records the address in `HostInfo` | done | Headless boot: `[DHCP CONFIG] IP configuration _applied.`, address 10.0.2.15 on `e1000e` |
| NET-2 | No NIC means network setup is skipped without touching the adapter | done | `HostInfo.IsLinkedToNetwork` guard; boot with `--nic none` reaches `Calling Run()` |
| NET-3 | DNS resolution | missing | No `DnsConfig` setup, although DHCP reports a DNS server (10.0.2.3) |
| NET-4 | Anything uses the network after boot | missing | No shell command or service touches the network |

## Shell

| Id | Target | Status | Evidence |
|---|---|---|---|
| SHELL-1 | `help`, `clear`, and `halt` work, and unknown input is reported | done | `Kernel.ExecuteCommand` |
| SHELL-2 | A failing command reports its error and returns to the prompt | done | `Kernel.Run` catches every exception and logs it to serial |
| SHELL-3 | Commands take arguments | missing | Input is matched whole after `ToLower()`; nothing splits arguments |
| SHELL-4 | Commands show system information (version, network, mounts) | partial | The boot banner shows the version and build; no command shows it, the network, or mounts afterwards |
| SHELL-5 | Commands can be added without growing one `switch` | missing | Every command is a `case` in `ExecuteCommand` |

## Platform

| Id | Target | Status | Evidence |
|---|---|---|---|
| PLAT-1 | Builds for x64 with no warnings | done | `cosmos build -v -p Venture.OS`: 0 warnings, 0 errors |
| PLAT-2 | Builds for ARM64 | done | `cosmos build -a arm64 -p Venture.OS`: build completed |
| PLAT-3 | Pinned to the latest Cosmos release | done | `Venture.OS.csproj`: `Cosmos.Sdk` 3.0.94; `cosmos update --check` reported nothing newer on 2026-10-15 |
| PLAT-4 | Boots on ARM64 | missing | Not booted; ARM64 needs `-a arm64` and a virtio NIC to test networking |
| PLAT-5 | Lua scripting | missing | `LuaCSharp` 0.5.7 is referenced but unused, and untested under the ahead-of-time compiler |

## Reliability

| Id | Target | Status | Evidence |
|---|---|---|---|
| REL-1 | Nothing crashes uncontrolled: boot failures panic deliberately or degrade, and nothing crashes after boot | done | BOOT-2, BOOT-3, SHELL-2 |
| REL-2 | Input from devices and callers is validated where it enters | partial | `BootRamDisk` validates sizes and ranges; shell input is only matched against known commands, which is safe today but untested |
| REL-3 | `BuildNumber` can't throw | done | `HostInfo.BuildNumber` returns 0 when the commit hash is shorter than four bytes |
| REL-4 | A test project covers logic that doesn't need the kernel | missing | No test project |
| REL-5 | Boot tests run automatically and check the serial log | missing | Boots are run by hand |

## Documentation

| Id | Target | Status | Evidence |
|---|---|---|---|
| DOC-1 | An agent and contributor guide matches the code | done | `AGENTS.md`, reviewed 2026-10-15 |
| DOC-2 | Coding guidance for C# and for Cosmos | done | `.agents/skills/dotnet-guidelines`, `.agents/skills/cosmos-gen3` |
| DOC-3 | Every declaration has a documentation comment where its name isn't enough | partial | `KernelPanic`, `BootRamDisk`, the boot steps, and `BuildNumber` have them; `Kernel.OnBoot`, `HostInfo.IpAddress`, and `MacAddress` don't |
| DOC-4 | An architecture overview in `docs/` | missing | Only `docs/README.md` and this scorecard |

## Distribution

| Id | Target | Status | Evidence |
|---|---|---|---|
| DIST-1 | CI builds x64 on every push and pull request, with cached tools | done | `.github/workflows/cosmos.yml`; cache hit confirmed by the owner on 2026-10-15 |
| DIST-2 | CI also builds ARM64 | missing | The workflow builds the default architecture only |
| DIST-3 | CI boots the kernel headless and checks it reaches the shell | missing | |
| DIST-4 | Release ISOs are published | missing | No release workflow |

## Known issues

| Id | Issue | Status | Note |
|---|---|---|---|
| BUG-1 | Boot waits 0.5 s before and after each step, about 2 s in all | open | `Kernel.BootStepDelay`; deliberate pacing or leftover, owner to decide |
| BUG-2 | VentureOS's console output doesn't reach the serial log | open | Limits what a headless boot can verify; mirroring key lines through `Log` would fix it |
| BUG-3 | `DhcpClient.SendDiscoverPacket()` returning `-1` for a timeout was an assumption | resolved | Confirmed by the Cosmos network docs |
