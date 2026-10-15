---
name: cosmos-gen3
description: How to write, build, run, and debug a Cosmos gen3 kernel in C# (NativeAOT, Limine, x64 and ARM64). Covers the cosmos CLI, the kernel lifecycle, stopping and panicking, serial logging, timers, the VFS and FAT, networking, feature switches, ahead-of-time compiler limits, and the version drift between the docs and a pinned package. Apply when working on kernel code that uses Cosmos.Kernel.* APIs, when running or debugging a kernel in QEMU, or when answering questions about what Cosmos supports.
---

# Cosmos gen3

Cosmos gen3 compiles a C# kernel with .NET NativeAOT and the Cosmos IL
patcher, boots it with Limine on UEFI, and targets x64 and ARM64. The
documentation lives at <https://cosmosos.github.io/>. Gen3 is new, so the docs
are still rough in places. Fuller notes, with the details behind this skill,
are in [Cosmos.md](./Cosmos.md): the CLI options, the boot phases, and the
subsystem APIs and limitations. Read the relevant section when a task goes
beyond what this file covers.

## 1. Check the API exists in the pinned version

The docs follow the latest Cosmos release, and a project pins an older one in
its `.csproj` (`Sdk="Cosmos.Sdk/<version>"` and the `Cosmos.*` package
references). Names move between releases: for example, 3.0.87 had
`Cosmos.Kernel.System.Vfs.VfsManager.RegisterFilesystem` and
`Filesystems.Fat.FatFilesystemType`, which became
`FileSystem.VfsManager.RegisterFileSystem` and `FileSystem.Fat.FatFileSystemType`
in 3.0.94. [Cosmos.md](./Cosmos.md) lists the renames from that upgrade.

- Before using an API from the docs, confirm it exists in the pinned version.
  The build is the quickest check. To browse, list the package's types with
  `ilspycmd -l c` on the DLL under
  `~/.nuget/packages/cosmos.kernel.system/<version>/runtimes/linux-x64/lib/net10.0/`.
- When the docs and the package disagree, the package wins. Say so when you
  report back.
- Don't bump the Cosmos version as a side effect. `cosmos update` inside a
  project rewrites its pins; that's a change of its own, and the owner
  decides.

## 2. The CLI

- `cosmos check` before building. It reports system and tool information and
  whether the CLI has an update. If it reports one, mention it after the
  build succeeds.
- `cosmos update --check` reports CLI, tool, and project pin updates without
  installing anything. `cosmos check` doesn't report pin updates. When an
  upgrade has been agreed, `cosmos update -y` applies it; without `-y` it
  prompts, which fails outside an interactive terminal.
- `cosmos build -p <project> [-a x64|arm64] [-c Debug|Release] [-v]` writes
  `output-<arch>/`.
- `cosmos run -p <project> [-a arch]` boots in QEMU. Useful options:
  - `--headless` runs serial only, with no display window.
  - `--debug` waits for GDB on port 1234.
  - `--nic none` boots with no network card, which is how to test the
    no-network path.
  - `--disk image[,ahci|nvme|usb|virtio-blk]` attaches a disk.
  - `--hostfwd tcp::H-:G` forwards a host port to the guest.
- QEMU, Clang, LLD, xorriso, and GDB come bundled in `~/.cosmos/tools/`. Look
  there before concluding a tool is missing.

## 3. Lifecycle and failure

`Sys.Kernel.Start()` enables interrupts, runs the driver stage, then calls
`OnBoot()`, `BeforeRun()` once, `Run()` in a loop until `Stop()`, `AfterRun()`
once, and halts the CPU forever.

- `Start()` catches nothing. An exception that leaves `BeforeRun()` or `Run()`
  takes the kernel down with no report on screen. Catch at those boundaries.
- `Run()` is called again as soon as it returns, so don't loop inside it.
- `base.OnBoot()` sets up `KernelConsole`, which `Console` needs. Without it
  the kernel is headless.
- `Power.Halt()` parks the CPU and **returns** on the next interrupt, so
  halting for good means calling it in a loop. `Power.Reboot()` and
  `Power.Shutdown()` don't return.
- Cosmos's own `Panic.Halt` is internal. A kernel writes its own panic: log to
  serial, print to the console, then halt in a loop.

Cosmos's guidance on choosing a failure path:

| Situation | Do |
|---|---|
| Hardware failure or corrupted state | Panic |
| Invalid API usage | Throw a specific exception, using the throw helpers with `nameof` |
| A feature compiled out, in a member that acts | Throw `InvalidOperationException` naming the switch |
| A feature compiled out, in a member that answers | Return `0`, `null`, `false`, or empty |
| A user-facing error in a shell | Catch, and print the error |

Check the project's own rules (AGENTS.md) too; they decide when a failure is
fatal for that kernel.

## 4. Execution contexts

- Interrupt context includes `TimerManager` callbacks and interrupt handlers.
  Code there must not block, allocate, take a lock, or let an exception
  escape. An escaped exception halts the kernel.
- Work that needs any of that goes on a thread: `AlarmManager` for delayed or
  recurring work, which needs the scheduler. Never call `AlarmManager` from a
  timer callback; it hangs.
- Exceptions are heap allocated, so don't throw on interrupt, GC, or scheduler
  paths.
- The scheduler runs on one CPU. Sockets aren't safe to use from two threads.

## 5. Ahead-of-time compiler limits

- Forbidden: `System.Reflection.Emit`, `dynamic`, `Assembly.Load`,
  `Type.GetType` with a string, `MakeGenericType`, and `MakeGenericMethod`.
- Use with care: generic virtual methods, and LINQ on hot paths (it allocates
  iterators and closures).
- Only part of the BCL works. Core types, collections, `Math`, `Console`,
  `DateTime`, `Random`, `System.IO`, and sockets are plugged. Check the
  docs or try a build before relying on anything else, and say when you
  couldn't confirm something works.

## 6. Logging and diagnosis

- `Log` in `Cosmos.Kernel.System.Diagnostics` writes straight to the serial
  port. It works from the first line of `BeforeRun()` and after the screen is
  gone. `Console` needs graphics and allocates.
- Use `Log.WriteString(text + "\n")` and the typed `WriteNumber` and
  `WriteHex` overloads. There is no `WriteLine`. `Log.Write(params ...)` boxes
  numbers.
- When a kernel doesn't come up, read the serial log first. Each boot phase
  logs a `[KMAIN]`, `[KERNEL]`, or `[Kernel]` line, so the last line shows how
  far it got.
- `CosmosEnableUART=false` silences `Log` without errors.

## 7. Subsystem notes

- **Timers:** `TimerManager.Wait(ms)` blocks. `TimerManager.IsInitialized`
  says whether the tick source came up.
- **Filesystem:** standard `System.IO` over a Unix-style VFS (`/mnt/...`).
  Register a filesystem type, then format and mount. Every setup call
  returns `bool`, so check each one and report which failed. `/` is a
  read-only virtual root. A RAM disk is an `IBlockDevice`, which must throw
  on out-of-range requests. Check `StorageManager.Partitions.Count` before
  indexing. Don't test filesystem code on real hardware.
- **Network:** check `NetworkManager.DeviceCount > 0` before touching
  `NetworkManager.Primary`, a struct handle that may name nothing.
  `DhcpClient.SendDiscoverPacket()` returns `-1` on timeout. There's no TLS,
  and TCP doesn't retransmit. Under QEMU user networking, the host is
  `10.0.2.2`.
- **Input and display:** `Console.ReadLine()` has a line editor. Graphics are
  32-bit colour only and drawn by the CPU.

## 8. Feature switches and drivers

- Subsystems are switched in the `.csproj` (`CosmosEnableTimer`,
  `CosmosEnableNetwork`, `CosmosEnableStorage`, `CosmosEnableGraphics`,
  `CosmosEnableScheduler`, and others, all `true` by default). A switched-off
  subsystem is compiled out, and its API returns empty values or throws
  `InvalidOperationException`.
- `<CosmosDriverExclude Include="Full.Type.Name" />` drops a shipped driver.
- Experimental APIs need an explicit `<NoWarn>`: `COSMOS0001` (scheduler
  policy), `COSMOS0002` (packet crafting), and `COSMOS0003` (driver kit).
  They carry no compatibility promise, so treat them as a judgment call to
  raise with the owner, not a default.

## 9. Plugs

Plugs replace BCL or runtime members at IL level: `[Plug(typeof(T))]` on a
static class, and `[PlugMember]` on every replacing member. Instance members
take the instance as the first parameter (`aThis`). A member missing
`[PlugMember]` silently patches nothing. Reach for a plug only when no Cosmos
API covers the need, and flag it, since plugs are tied closely to the Cosmos
version.
