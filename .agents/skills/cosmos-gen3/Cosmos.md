# Cosmos gen3 notes

Notes taken from the Cosmos documentation at <https://cosmosos.github.io/> on
2026-10-15, plus checks against the Cosmos packages VentureOS uses. This is
reference material, not a contract: when it disagrees with the installed
package, the package wins.

## Version drift

The docs follow the latest Cosmos release. On 2026-10-15 that was **3.0.94**,
while VentureOS pins **3.0.87** (`cosmos update --check` reports
`3.0.87 -> 3.0.94 (3 pins)`). Some names differ between the two:

| Docs (3.0.94) | Installed (3.0.87) |
|---|---|
| `Cosmos.Kernel.System.FileSystem` (`VfsManager`, `MountFlags`) | `Cosmos.Kernel.System.Vfs` |
| `Cosmos.Kernel.System.FileSystem.Fat.FatFileSystemType` | `Cosmos.Kernel.System.Filesystems.Fat.FatFilesystemType` |
| `VfsManager.RegisterFileSystem` | `VfsManager.RegisterFilesystem` |
| `Cosmos.Kernel.System.Timers` | `Cosmos.Kernel.System.Timer` |
| `Cosmos.Kernel.HAL.Devices.Storage.IBlockDevice` | `Cosmos.Kernel.HAL.Interfaces.Devices.IBlockDevice` |
| `Kernel.Current`, `SessionManager` | Not present in 3.0.87 |

Before using an API from the docs, confirm it exists in the pinned version:
build against it, or list the package's types with `ilspycmd -l c` on
`~/.nuget/packages/cosmos.kernel.system/<version>/runtimes/linux-x64/lib/net10.0/Cosmos.Kernel.System.dll`.
`Cosmos.Kernel.HAL.Interfaces` ships an XML doc file; `Cosmos.Kernel.System`
does not.

`cosmos check` reports the toolchain and CLI status but not project pin
updates. `cosmos update --check` reports both without installing anything.

## What gen3 is

- C# compiled with .NET NativeAOT (ILC) plus the Cosmos IL patcher. There is
  no JIT and no IL2CPU.
- UEFI firmware, the Limine bootloader, and an ELF kernel on an ISO.
- x64 and ARM64. Some features are x64 only (x87 FPU math, PS/2).
- One CPU. SMP isn't started yet.
- Mark-and-sweep GC. A priority-based stride scheduler with a 10 ms quantum.
  `lock` works.
- BCL support is partial: core types, collections, `Math`, `Console`,
  `DateTime`, `Random`, `System.IO.File` and sockets are plugged.
- Debugging works through GDB, with known bugs in source linking and variable
  inspection in VS Code.

## Toolchain and CLI

- Install on Linux or macOS: `dotnet tool install -g Cosmos.Tools`, then
  `cosmos install`. This puts Clang, LLD, xorriso, QEMU and GDB bundles in
  `~/.cosmos/tools/`. QEMU is there even when it isn't on `PATH`.
- `cosmos check`: system and tool information, and whether the CLI itself
  has an update.
- `cosmos update`: updates the tools and, inside a kernel project, moves the
  `Cosmos.Sdk` and `Cosmos.*` package pins to the latest release.
  `--check` only reports; `--no-project` leaves project files alone;
  `--version <v>` picks a version.
- `cosmos build [-p project] [-a x64|arm64] [-c Debug|Release] [--all] [-v]`.
  Output goes to `output-<arch>/`.
- `cosmos run [-p project] [-a arch]`. Useful options:
  - `--headless`: no display window, serial only.
  - `--debug`: QEMU waits for GDB on port 1234 (`-s -S`).
  - `--disk path[,ahci|nvme|usb|virtio-blk|virtio-blk-mmio]`, repeatable.
  - `--nic none|e1000e|virtio-net-pci|virtio-net-device`. x64 defaults to an
    e1000e NIC; ARM64 needs one attached explicitly.
  - `--hostfwd tcp::4444-:4444`, repeatable.
  - `-m <MB>` (default 512) and `--cpu <model>`.
- `cosmos new <name>`, `cosmos info`, `cosmos uninstall`.

## Boot chain and lifecycle

```text
UEFI -> Limine -> kmain() (C)
  Phase 1  CPU: SIMD on, serial port up (logging starts here)
  Phase 2  Platform: ACPI RSDP, early ACPI parse
  Phase 3  Managed runtime: heap, GC, type system, HAL, scheduler,
           System managers (Timer, Keyboard, Mouse, Network, Display, Storage)
  Phase 4  Generated Main() -> Kernel.Start()
```

`Sys.Kernel.Start()` then:

1. Enables interrupts.
2. Runs the driver stage: drivers bind devices, and disks are registered
   and their partitions scanned.
3. Calls `OnBoot()`. The default sets up `KernelConsole`, which is what makes
   `Console` work. Drop `base.OnBoot()` to boot headless.
4. Calls `BeforeRun()` once.
5. Calls `Run()` in a loop until `Stop()` is called.
6. Calls `AfterRun()` once.
7. Halts the CPU forever. A kernel never exits.

Notes:

- Nothing in `Start()` catches exceptions. An exception out of `BeforeRun()`
  or `Run()` takes the kernel down. The docs recommend wrapping `Run()` in
  `try`/`catch`.
- `Run()` is called again as soon as it returns, so it shouldn't loop itself.
- The kernel class defaults to `<RootNamespace>.Kernel`; override with
  `<CosmosKernelClass>`.
- The Limine `cmdline:` entry arrives through
  `Environment.GetCommandLineArgs()`.

## Stopping the machine

- `Power.Halt()` parks the CPU until the next interrupt and then **returns**.
  Use it in an idle or halt loop instead of `while (true) { }`.
- `Power.Reboot()` and `Power.Shutdown()` don't return.
- `Stop()` on the kernel ends the `Run()` loop, runs `AfterRun()`, then
  halts.
- `Cosmos.Kernel.Core.Panic.Halt(message)` is internal to Cosmos. A kernel
  writes its own panic on top of `Log` and `Power.Halt()`.

## Error handling guidance from the Cosmos coding guidelines

| Situation | Action |
|---|---|
| Hardware failure, corrupted state | Panic |
| Invalid API usage | Throw a specific exception |
| Feature compiled out, member acts on it | Throw `InvalidOperationException` naming the switch |
| Feature compiled out, member answers a question | Return `0`, `null`, `false` or empty |
| User-facing error in a kernel shell | `try`/`catch` and print the error |

- Exceptions work and are catchable, including the standard `System.IO`
  ones.
- Don't throw or allocate in interrupt handlers, timer callbacks, the GC or
  scheduler hot paths. Exception objects are heap allocated.
- Use the throw helpers (`ArgumentNullException.ThrowIfNull`,
  `ArgumentOutOfRangeException.ThrowIfGreaterThan`, ...) with `nameof`.
- How the Cosmos API reports failure: a read returns its empty value, an
  operation a caller can act on returns `bool` (`Try...`), and a state a
  correct kernel can't produce throws.

## Ahead-of-time compiler limits

Forbidden: `System.Reflection.Emit`, `dynamic`, `Assembly.Load`,
`Type.GetType("...")` by string, `MakeGenericType` and `MakeGenericMethod`.

Use with care: generic virtual methods and LINQ (it allocates iterators and
closures, so keep it off hot paths).

## Feature switches

Each subsystem is behind an MSBuild property, all `true` by default:
`CosmosEnableInterrupts`, `CosmosEnablePCI`, `CosmosEnableTimer`,
`CosmosEnableKeyboard`, `CosmosEnableMouse`, `CosmosEnableNetwork`,
`CosmosEnableStorage`, `CosmosEnableGraphics`, `CosmosEnableScheduler`,
`CosmosEnableUsb` (on when keyboard, mouse or storage is), `CosmosEnableAudio`
and `CosmosEnableUART` (the serial port). Setting one to `false` compiles the
subsystem out. `KernelFeatures` answers the question in code.

Drivers can be dropped or opted into in the `.csproj` with
`<CosmosDriverExclude Include="Full.Type.Name" />` and
`<CosmosDriverInclude ... />`.

## Logging

- `Log` (`Cosmos.Kernel.System.Diagnostics`) writes straight to the serial
  port: COM1 on x64, PL011 on ARM64. `cosmos run` shows it in the terminal.
- It works from the first line of `BeforeRun()` and after a crash has taken
  the framebuffer. `Console` needs graphics, repaints on every write and
  allocates.
- `WriteString`, `WriteNumber`, `WriteHex`, `WriteHexWithPrefix`,
  `WriteBytes` and `Write(params ...)`. There is no `WriteLine`; append
  `"\n"`. `Write` boxes numbers, so prefer the typed overloads on hot paths.
- With `CosmosEnableUART=false`, `Log` calls succeed but output goes nowhere.
- The serial log is the first thing to read when a kernel doesn't come up.

## Timers

| | `TimerManager` | `AlarmManager` |
|---|---|---|
| Callback runs in | Interrupt context | A kernel thread |
| Callback may block, allocate or lock | No | Yes |
| Schedule from | Anywhere | Thread context only |
| Needs the scheduler | No | Yes |
| Returns | `SoftwareTimer?` | `ulong` id, 0 on failure |

- `TimerManager.Wait(ms)` blocks. It throws `InvalidOperationException` when
  the timer is compiled out.
- `TimerManager.IsEnabled` is the compile-time question, `IsInitialized` the
  runtime one.
- A timer callback must not let an exception escape: nothing catches it, and
  the kernel halts. Calling `AlarmManager` from a timer callback hangs.
- `TimerManager.Frequency` sets the tick rate. Out-of-range values throw.

## Filesystem

- Standard `System.IO` (`File`, `Directory`, `FileStream`, readers and
  writers) on a Unix-style VFS. Paths look like `/mnt/file.txt`.
- FAT12/16/32 with formatting; MBR, GPT and EBR partitioning. An ext2 driver
  exists but is only tested on the host.
- Set up: register a filesystem type under a name, then mount a partition
  (or an injected `IBlockDevice` with an empty source) at a mount point.
  Every call returns `bool`.
- `/` is a virtual, read-only root that lists the mount points.
- `StorageManager.Partitions` is empty when no disk is attached; check
  `Count` first. Prefer the `Partition` overload of `TryMount` to index
  strings.
- Formatting is refused while mounted.
- Errors are the standard `System.IO` exceptions and are catchable. With
  nothing mounted, access fails with exceptions, never a kernel fault.
- Limits: no symlinks or hard links, timestamps not persisted, `DriveInfo`
  not wired up (use `VfsManager.TryStatFs`).
- The docs warn against testing filesystem code on real hardware.
- `IBlockDevice` contract (from the HAL XML docs): `ReadBlock`, `WriteBlock`
  and `Flush` throw on failure, including out-of-range requests. A normal
  return means the operation completed.

## Network

- Protocols: ARP, IPv4, link-local IPv6, UDP, TCP, DHCP and DNS. No TLS, so
  no HTTPS.
- NICs: Intel E1000E (also binds e1000) and virtio-net.
- `NetworkManager.DeviceCount`, `Name`, `MacAddress`, `LinkUp` and `Ready`
  describe the primary adapter. `NetworkManager.Primary` is a
  `NetworkAdapter` struct, a handle that may name nothing (`IsValid`).
- `DhcpClient.SendDiscoverPacket()` returns the elapsed milliseconds, or
  `-1` on timeout, and applies the configuration.
- Manual setup: `IPConfig.Enable(address, mask, gateway)`.
- Standard `TcpClient`, `TcpListener`, `NetworkStream` and
  `System.Net.Sockets.UdpClient` work. Cosmos has its own `UdpClient`, so
  name the .NET one in full when both namespaces are imported.
- DNS: register a server with `DnsConfig.Add`, then use `Dns` (A records
  only) or `DnsClient`.
- QEMU user networking: guest on `10.0.2.0/24`, host at `10.0.2.2`,
  built-in DHCP.
- Limits: the TCP stack doesn't retransmit, there's no half-close, and
  sockets aren't safe across threads.

## Other subsystems

- **Keyboard:** `Console.ReadLine()` includes a line editor (arrows,
  Home/End, Backspace/Delete). PS/2, USB and virtio keyboards. Layouts
  include German, Spanish, Turkish, French and British.
- **Graphics:** `Canvas` on the primary display, 32-bit colour only, BMP and
  PNG images, TrueType fonts, CPU-drawn 2D.
- **Sessions (newer than 3.0.87):** virtual consoles (Alt+F1 to F12) and a
  Telnet server package.
- **Audio:** Intel HD Audio playback.
- **Drivers:** a driver kit on an experimental API (`COSMOS0003`).

## Plugs

- Plugs replace members of existing types at IL level before ILC compiles
  them: `[Plug(typeof(T))]` or `[Plug("Full.Name")]` on a static class, and
  `[PlugMember]` on each replacing member.
- An instance member is plugged by a static method whose first parameter is
  the instance (`aThis`).
- Pitfall: a `[Plug]` class member missing `[PlugMember]` silently patches
  nothing.

## Experimental APIs

These are public but have no compatibility promise. Using one is a build
error until the project adds the ID to `<NoWarn>`:

- `COSMOS0001`: the scheduler policy API.
- `COSMOS0002`: the network packet API.
- `COSMOS0003`: the driver kit.

`Cosmos.Kernel.System` is the supported, stable API. Other assemblies are
internal apart from `IBlockDevice` and `MacAddress`.

## Testing

Cosmos's own test kernels use `Cosmos.TestRunner.Framework`: tests run in
`BeforeRun()` through `TestRunner.Run(name, action)` with an `Assert` class
that records failures without exceptions, then `Stop()`. Results go out over
serial. It isn't confirmed whether that framework ships as a package usable
outside the Cosmos repository.

## Doc pages

- User: install, startup, debugging, filesystem, network, timers, keyboard,
  mouse, graphics, audio, sessions, drivers, scheduler plugging.
- Developer: build pipeline, coding guidelines, kernel project layout, plugs,
  public API, testing, GC and scheduler internals.
- Roadmap: <https://cosmosos.github.io/roadmap.html>.
