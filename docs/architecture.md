# VentureOS architecture

> **Last reviewed:** 2026-11-11 · **Last updated:** 2026-11-11 · **Change:** minor (sidebars in windows) · **Target:** Cosmos gen3 3.0.94, x64 (ARM64 builds)

## Current state (2026-11-11)

What exists on `main` today. The numbered sections below describe the
design, and mark what's planned rather than built; where the code differs,
the code wins and this page is out of date.

- **Projects:** the kernel, `Venture.OS` (23 C# files, plus Lua system
  scripts in `system/`); a kernel-free library, `Venture.OS.Common` (39
  files: the shell framework, Lua support, bundles, UIScript, and the
  desktop's drawing); and an xUnit v3 test project
  for the library, `Venture.OS.Common.Tests` (705 tests, including runs of
  the shipped scripts, library, `Notes.app`, and the tutorial's example).
  Namespaces: `Venture.OS`, `Venture.OS.Kernel`, `Venture.OS.Startup`,
  `Venture.OS.Shell`, `Venture.OS.Shell.Commands`, and
  `Venture.OS.Scripting`.
- **Platform:** Cosmos gen3 3.0.94 on .NET 10, compiled ahead of time to an
  ELF kernel that Limine boots from an ISO. x64 is the primary target; ARM64
  builds but hasn't been booted.
- **Boot:** seven steps, three critical (RAM filesystem, timer, shell) and four
  optional (system files, user files, network, and `init.lua` after the banner). A critical failure panics with a report; an optional
  one is reported and boot continues. Both paths are proven in headless
  boots.
- **Shell:** a command registry with eleven built-in commands (`help`, `desktop`,
  `clear`, `cd`, `lua`, `open`, `close`, `apps`, `halt`, `reboot`,
  `shutdown`), a current folder the prompt shows, `.app` bundles that open
  and close as owners, plus Lua script commands
  from `/mnt/system/commands` (`echo`, `info`, `net`, and the file commands
  `ls`, `cat`, `write`, `mkdir`, `rm`, `df`, `pwd`) that reach the kernel
  through the `venture` table, and input split into a command and arguments
  with quoting. A failing command reports its error and
  returns to the prompt.
- **Storage:** a 32 MiB RAM disk, formatted FAT16 and mounted at `/mnt`,
  with the system files built into the kernel installed to `/mnt/system`,
  which nothing may change, and the user's to `/mnt/user`, which the user may.
  Nothing persists across boots.
- **Network:** DHCP on the first NIC when one is present. Nothing uses the
  network after boot.
- **Desktop:** `desktop` opens a graphical desktop with a menu bar, a dock
  that opens the system's apps, and movable windows that show `.uix`
  interfaces. Files is the desktop's own app, and Quick Look previews files.
  Apps open windows through `venture.ui`; `Notes.app` has one.
- **Not built yet:** keeping files across reboots
  (Blue Water); Terminal.app (Borealis); CI (removed for now: the kernel doesn't build like a typical .NET project), and published ISOs
  (every-stage work on the roadmap). See the
  [roadmap](./ROADMAP.md); verified status is in the
  [scorecard](./scorecard.md).

---

## 1. Goals and principles

**Goals**

- A hobby operating system in C#, the spiritual successor to
  [TOMAS](https://github.com/tonytins/tomas/), built to production
  standards because I use it myself.
- First a usable shell OS (the alpha gate): reliable boot, a shell with
  arguments, and commands to inspect and control the system.
- Then: a system that runs from RAM, extended in Lua: script commands, a
  system library, and apps that open and close cleanly as owners.
- Later: a Tiger-style desktop with `.uix` (UIScript) interfaces, files,
  networking, ARM64, and published ISOs. Keeping changes across reboots is
  optional.

**Principles**

1. **Cosmos owns the hardware; VentureOS owns the experience.** Drivers,
   memory, interrupts, the scheduler, the VFS, and the network stack come
   from Cosmos. VentureOS decides what runs at boot, what counts as fatal,
   what the user sees, and what the shell offers.
2. **Never crash uncontrolled.** During boot, critical steps panic
   deliberately and optional steps degrade. After boot, nothing may stop the
   system except the user's `halt` (§5).
3. **Observable without a screen.** Anything that tells you whether the
   system is healthy (boot step results, the version, failures, panics)
   reaches the serial log as well as the console, so a headless boot or CI
   can check it (§6).
4. **One version string.** The version and build the user sees on screen
   are the ones in the serial log and in a panic report (§10).
5. **Stay on Cosmos's supported API.** VentureOS programs against
   `Cosmos.Kernel.System` and the `IBlockDevice` contract. Cosmos's
   experimental APIs (`COSMOS0001`–`COSMOS0003`) and plugs are a decision I
   make deliberately, not a default.
6. **Describe only what works.** Docs, the scorecard, and the changelog
   record what a build or boot has shown, and say plainly what hasn't been
   verified.

### 1.1 Riding Cosmos's churn

Cosmos gen3 is young and moves fast. The 3.0.87 → 3.0.94 upgrade alone
renamed the filesystem, timer, network, input, and block-device namespaces
and two filesystem names. Its docs describe only the latest release. The
lesson is to keep Cosmos's surface small and in known places, so an upgrade
is a short list of mechanical fixes rather than a hunt.

**Rules I adopt now** (cheap today, expensive once the shell and storage
grow):

| Concern | Rule | Where |
|---|---|---|
| **Namespaces** | Cosmos namespaces are imported once, in `GlobalUsings.cs`. A rename is a one-file fix. | `GlobalUsings.cs` |
| **Cosmos calls** | Hardware and subsystem setup goes through `Startup/` steps, not scattered through commands. | `Startup/Bootstrap.cs` |
| **Version pins** | `Cosmos.Sdk` and the `Cosmos.*` packages move together, in a change of their own, with a build and a headless boot. | `Venture.OS.csproj` |
| **Docs drift** | Check an API exists in the pinned version before using it; the build is the quickest check. | [cosmos-gen3 skill](../.agents/skills/cosmos-gen3/SKILL.md) |
| **Experimental APIs** | Each one needs an explicit `<NoWarn>` and a deliberate decision from me, so a seam can't creep in unnoticed. | `Venture.OS.csproj` |
| **Missing runtime pieces** | Not all of .NET links under Cosmos. `System.Xml` doesn't in 3.0.94 (it needs `RhGetMaxGcGeneration`, which Cosmos's runtime lacks), so VentureOS reads XML with its own `FlatXml`. Prefer explicit code over reflection-backed helpers such as `Enum.GetValues`. The kernel build, not `Venture.OS.Common`'s analyzers, is what finds these. | `Venture.OS.Common/Xml/` |

The test: **a Cosmos upgrade should touch `GlobalUsings.cs`, `Startup/`,
and the project file, and nothing in the shell.**

---

## 2. Project layout

```text
VentureOS/
├─ src/
│  ├─ Venture.slnx              solution
│  ├─ Venture.OS.Common/        kernel-free library: no Cosmos, AOT-compatible, warnings as errors
│  │  └─ Shell/                 Venture.OS.Shell: the shell framework
│  │     ├─ CommandLine.cs      splits a line into words
│  │     ├─ CommandRegistry.cs  commands by name; source of `help`
│  │     ├─ CommandShell.cs     parse, look up, check arguments, run
│  │     ├─ IShellCommand.cs    what every command declares
│  │     ├─ ShellContext.cs     what a command can reach
│  │     ├─ IShellOutput.cs     where commands write
│  │     ├─ IShellHost.cs       halt, reboot, shutdown, implemented by the kernel
│  │     ├─ ICommandSource.cs   another place to find commands, after the registered ones
│  │     └─ ShellOutputWriter.cs  a TextWriter that hands complete lines to IShellOutput
│  │  └─ Scripting/             Venture.OS.Scripting: Lua system support
│  │     ├─ EmbeddedFile.cs     a file built into the kernel image
│  │     ├─ SystemDirectory.cs  writes those files to disk, after checking every path
│  │     ├─ LuaRunner.cs        runs Lua for a command: output to the shell, errors reported
│  │     ├─ LuaRepl.cs          the interactive Lua prompt behind plain `lua`
│  │     ├─ ScriptCommands.cs   <name>.lua files as commands
│  │     ├─ LuaOwner.cs         lasting Lua code and the undo list for what it set up
│  │     ├─ RuntimeCommands.cs  commands registered with venture.command, and who may take which name
│  │     ├─ ShellLibrary.cs     venture.command, for code with a lasting owner
│  │     ├─ FilesLibrary.cs     venture.files: list, read, write, and change files, never in /mnt/system
│  └─ Xml/FlatXml.cs         Venture.OS.Xml: the flat XML that Info.xml and .uix files use
│  └─ Desktop/               Venture.OS.Desktop: IPainter, the sky, the pointer, UIView, windows, the menu bar, the dock, DesktopSession, and UiLibrary (venture.ui)
│  └─ Interface/UIScript.cs  Venture.OS.Interface: reads .uix (UIScript) documents, with the menus feature
│  └─ Bundles/               Venture.OS.Bundles: Bundle (reads Contents/Info.xml), AppManager, AppLibrary (venture.app)
│  │     ├─ ISystemInfo.cs      what the kernel reports to scripts
│  │     └─ VentureLibrary.cs   the `venture` table
│  ├─ Venture.OS.Common.Tests/  xUnit v3 tests for Venture.OS.Common
│  └─ Venture.OS/
│     ├─ Venture.OS.csproj         Cosmos SDK project: package pins, CosmosKernelClass
│     ├─ GlobalUsings.cs           every Cosmos namespace, plus Venture.OS.Kernel
│     ├─ HostInfo.cs               name, version, and build number
│     ├─ Scripting/                Venture.OS.Scripting: KernelSystemInfo, ISystemInfo read from Cosmos
│     ├─ Kernel/                   Venture.OS.Kernel: the lifecycle and how the system reports and stops
│     │  ├─ VentureKernel.cs       the kernel class: BeforeRun (boot), Run (shell loop)
│     │  ├─ KernelLog.cs           system messages to serial and the console
│     │  └─ KernelPanic.cs         deliberate, reported halt
│     ├─ Startup/                  Venture.OS.Startup: boot steps
│     │  ├─ Bootstrap.cs           RAM disk, timer check, network and DHCP
│     │  └─ BootRamDisk.cs         in-memory IBlockDevice
│     ├─ Shell/                    Venture.OS.Shell: what needs Cosmos
│     │  ├─ ConsoleShellOutput.cs  IShellOutput on the console
│     │  └─ Commands/              one class per command
│     ├─ system/                   Lua system files, built into the kernel and installed to /mnt/system
│     │  ├─ init.lua               runs once after boot; prints the help hint
│     │  ├─ apps/Files.app/        the desktop's own app: browses folders, opens apps, Quick Look
│     │  ├─ apps/SystemMonitor.app/  version, uptime, memory, and network, kept current
│     │  ├─ apps/Terminal.app/     the shell in a window, in Fira Code
│     │  ├─ fonts/                 Open Sans and Fira Code, with their licenses
│     │  ├─ interfaces/about.uix   the About VentureOS window
│     │  ├─ commands/              Lua command scripts: echo, info, net, and ls, cat, write, mkdir, rm, df, pwd
│     │  └─ lib/venture/args.lua   the system library: the venture.args argument parser
│     ├─ user/                     the user's files, built into the kernel and installed to /mnt/user
│     │  ├─ apps/Calculator.app/   the first user app
│     │  ├─ apps/Notes.app/        the sample app: a window and a note command, notes kept in notes.txt
│     │  └─ apps/TextEdit.app/     opens, edits, and saves text files
│     ├─ Desktop/CanvasPainter.cs  draws the desktop on Cosmos's canvas with the system fonts
│     ├─ Bootloader/limine.conf    Limine boot entry; asks for a 1024x768 framebuffer
│     ├─ GitInfo.txt               base version
│     └─ src/C/                    reserved for native C sources (empty)
├─ global.json                  opts dotnet test into Microsoft.Testing.Platform
├─ docs/                        architecture, roadmap, scorecard
├─ .agents/skills/              C# and Cosmos guidance for agents
├─ changelog.md                 user-facing history
└─ run.sh                       check, build, and run x64 locally (--headless for serial only)
```

**Naming.** The kernel class is `VentureKernel` rather than `Kernel`,
because a class can't share its name with the `Venture.OS.Kernel`
namespace. Cosmos looks for `<RootNamespace>.Kernel` by default, so the
project file sets `CosmosKernelClass` to `Venture.OS.Kernel.VentureKernel`.

**Every .NET project lives under `src/`**, tests included, each in its own
folder and listed in `src/Venture.slnx`.

**Growth.** New subsystems (commands, drivers, filesystems) get their own
folder and namespace under `Venture.OS`, as `Kernel/`, `Startup/`, and
`Shell/` do. A new command is a class in `Shell/Commands/` plus one
`Register` call in `VentureKernel.CreateShell`.

---

## 3. Layering

```mermaid
flowchart TD
    subgraph VentureOS
        Kernel["Venture.OS.Kernel<br/>lifecycle, failure policy, system output"]
        Startup["Venture.OS.Startup<br/>boot steps"]
        Shell["Venture.OS.Shell<br/>commands"]
        Host["Venture.OS<br/>HostInfo"]
        SystemInfo["Venture.OS.Scripting<br/>KernelSystemInfo"]
        Kernel -- calls --> Startup
        Kernel -- runs --> Shell
        Shell -. "halt, reboot, shutdown<br/>through IShellHost" .-> Kernel
        Kernel -- uses --> Host
        SystemInfo -- uses --> Host
    end

    subgraph Common["Venture.OS.Common (no Cosmos kernel)"]
        Framework["Venture.OS.Shell<br/>parsing, registry, dispatch"]
        Scripting["Venture.OS.Scripting<br/>Lua runner, script commands, venture table"]
        Lua["Cosmos.Executable.Lua<br/>Lua 5.5"]
        Framework -- "unknown names" --> Scripting
        Scripting --> Lua
    end
    Kernel -- builds --> Framework
    Shell -- implements --> Framework
    SystemInfo -- implements --> Scripting

    Ring["Cosmos.Kernel.System<br/>the supported API"]
    Kernel --> Ring
    Startup --> Ring
    Shell --> Ring
    Host --> Ring
    SystemInfo --> Ring

    subgraph Cosmos
        Lower["Drivers · HAL · Core<br/>memory, GC, scheduler, serial · Native"]
        Boot["Limine · UEFI"]
        Lower --> Boot
    end

    Ring --> Lower
```

**Rules:**

1. **Only `Venture.OS.Kernel` stops the system.** Boot steps report success
   with `bool` (critical) or by returning (optional), and throw when
   something unexpected happens. Shell commands ask the kernel to halt,
   reboot, or shut down through `IShellHost`. Neither calls `KernelPanic`,
   `Power`, or `Stop` itself.
2. **Boot steps don't know whether they're critical.** `VentureKernel`
   decides that when it runs them (§4), so the policy lives in one place.
3. **System messages go through `KernelLog`** (§6). Command output goes
   through `IShellOutput`, which writes to the console.
4. **VentureOS code reaches Cosmos only through `Cosmos.Kernel.System`**
   and `IBlockDevice`. Cosmos's internal namespaces (`Cosmos.Kernel.Core`
   and the HAL beyond `IBlockDevice`) are off limits.
5. **No `unsafe` code.** Cosmos keeps pointers in its Core layer; VentureOS
   has no need for them.
6. **`Venture.OS.Common` never references the Cosmos kernel.** It holds
   logic that doesn't need hardware, so it can be tested on ordinary .NET.
   `Cosmos.Executable.Lua` is the one Cosmos-named package it uses, because
   it runs on ordinary .NET too. Scripts reach the kernel only through
   `ISystemInfo`, which the kernel implements. It builds
   with the AOT analyzers and warnings as errors, so code the kernel
   couldn't compile ahead of time fails there first. Its root namespace is
   `Venture.OS`, so a type keeps its namespace when it moves into it.

---

## 4. Boot

```mermaid
flowchart TD
    subgraph Cosmos
        Firmware["UEFI → Limine"]
        Kmain["kmain<br/>CPU, ACPI, heap, GC, HAL, scheduler, managers"]
        Start["Kernel.Start<br/>interrupts on, driver stage, OnBoot (console)"]
        Firmware --> Kmain --> Start
    end

    subgraph BeforeRun["VentureKernel.BeforeRun"]
        Version["Starting VentureOS &lt;version&gt;..."]
        Fs["filesystem<br/>Bootstrap.BootIntoRam"]
        SysFiles["system files<br/>Bootstrap.InstallSystemFiles"]
        UserFiles["user files<br/>Bootstrap.InstallUserFiles"]
        Timer["time manager<br/>Bootstrap.CheckTimeManager"]
        Net["network<br/>Bootstrap.ConnectToNetwork"]
        ShellStep["shell<br/>VentureKernel.CreateShell"]
        Banner["&lt;version&gt; booted successfully!"]
        Init["init script<br/>Bootstrap.RunInitScript"]
        Version --> Fs
        Fs -- ok --> SysFiles
        SysFiles -- "ok, or failure reported" --> UserFiles
        UserFiles -- "ok, or failure reported" --> Timer
        Timer -- ok --> Net
        Net -- "ok, or failure reported" --> ShellStep
        ShellStep -- ok --> Banner
        Banner --> Init
    end

    Panic["KernelPanic.Halt<br/>report, then halt forever"]
    Run["VentureKernel.Run<br/>called in a loop"]
    Halt["halt, reboot, or shutdown"]

    Start --> Version
    Fs -- "false or exception" --> Panic
    Timer -- "false or exception" --> Panic
    ShellStep -- exception --> Panic
    Init -- "ok, or failure reported" --> Run
    Run -- "power command" --> Halt
```

The filesystem, time manager, and shell steps are critical
(`RunCriticalStep`); the system files, user files, network, and init script steps are
optional (`RunOptionalStep`). `init.lua` prints through `KernelLogOutput`, so
its output reaches serial too, and it can use the `venture` table. Ctrl+C or
Esc stops it, and so does a 10-second limit, so a script that never finishes
can't keep the shell from starting, even in a headless boot.

| Step | Kind | Succeeds when | On failure |
|---|---|---|---|
| Filesystem | critical | The RAM disk registers, formats as FAT16, and mounts at `/mnt` | Reports which of the three failed, then panics |
| System files | optional | Every file under `src/Venture.OS/system/` is written to `/mnt/system` | An invalid path or I/O error is reported and boot continues; the built-in C# commands still work |
| Time manager | critical | Cosmos reports `TimerManager.IsInitialized` | Reports it, then panics |
| Shell | critical | Every command registers | Registration only fails on a programming error, such as two commands with one name; it panics |
| Network | optional | Always returns; records the MAC and DHCP address when it can | No NIC, a DHCP timeout, or no address is reported; an exception is reported and boot continues |
| Init script | optional | `/mnt/system/init.lua` runs to the end, after the banner so the screen clear doesn't hide its output | A missing script or a Lua error (with its traceback) is reported, and the shell starts anyway |

A step is **critical** when the system can't do its job without it, so
running on would only make later failures harder to understand. Adding a
boot step means choosing its kind and saying why in the change.

Each step pauses for 0.5 s (`BootStepDelay`). That's deliberate: without
it, boot is too fast to read on screen.

---

## 5. Failure model

| When | What happens | How |
|---|---|---|
| A critical boot step returns `false` or throws | Deliberate panic: report, then halt forever | `RunCriticalStep` → `KernelPanic.Halt` |
| An optional boot step throws | Reported; boot continues without it | `RunOptionalStep` |
| A command throws | Reported; back to the prompt | `VentureKernel.Run` catches everything |
| Invalid input reaches a device or API | A specific exception (`ArgumentException`, `ArgumentOutOfRangeException`), caught by the boundaries above | e.g. `BootRamDisk` range checks |
| The user mistypes a command | The shell explains: unknown command, the usage line, or an unclosed quote; nothing throws | `CommandShell.Execute` |
| The user types `halt`, `reboot`, or `shutdown` | `Stop()` ends the loop and Cosmos halts the CPU; `Power.Reboot()` or `Power.Shutdown()` | `VentureKernel` through `IShellHost` |

**The panic.** `KernelPanic.Halt` writes a report with the version, the
failed step, and the reason (for an exception, its type name and message),
then calls `Power.Halt()` in a loop, because Cosmos's halt returns on the
next interrupt. Cosmos has a panic of its own, but it's internal. A forced
panic has been verified in a headless boot: the report reached serial, no
later step ran, and the shell never started.

**What isn't caught.** Interrupt and timer callbacks run outside these
boundaries, and an exception there halts the kernel. VentureOS registers
none yet. When it does, they must not throw, allocate, or block
([cosmos-gen3 skill](../.agents/skills/cosmos-gen3/SKILL.md) §4).

---

## 6. Output

| Channel | What goes there | API |
|---|---|---|
| Console | Everything the user reads: boot messages, the banner, command output | `Console` (Cosmos `KernelConsole`, set up by `base.OnBoot()`) |
| Serial | System messages, each line prefixed `[Venture] `, beside Cosmos's own `[KMAIN]`, `[KERNEL]`, and `[Kernel]` lines | `KernelLog.WriteLine` → Cosmos `Log` |

`KernelLog.WriteLine` writes serial first, then the console, so a message
survives even when the console is what failed. Serial is COM1 on x64, and
`cosmos run` connects it to the terminal. Command output stays on the
console only, so the serial log stays a record of the system's health
rather than a transcript.

---

## 7. Storage

```text
BootRamDisk (IBlockDevice, 512-byte blocks × 65,536 = 32 MiB)
  → FatFileSystemType, registered as "ramfat"
  → formatted FAT16 → mounted at /mnt
  → reachable through System.IO (File, Directory, streams)
```

- **`BootRamDisk`** validates its size when constructed (no zero sizes, no
  disk larger than one .NET array) and range-checks every read and write,
  throwing as Cosmos's `IBlockDevice` contract requires instead of
  overflowing or copying the wrong range.
- `/` is Cosmos's read-only virtual root, so files live under `/mnt`.
- **System files.** Everything under `src/Venture.OS/system/` is turned
  into byte arrays in generated C# at build time (`GenerateSystemFiles` in
  `Venture.OS.csproj`), because Cosmos 3.0.94 doesn't expose Limine's
  modules, so the ISO can't hand the kernel loose files. At boot,
  `SystemDirectory.Install` checks every path, then writes the files to
  `/mnt/system`. The RAM disk starts empty, so the system files always match
  the kernel, and edits made on the running system are lost at reboot.
- **User files.** Everything under `src/Venture.OS/user/` is built in the same
  way (`SystemFiles.User`, tagged `Root="user"` in the project) and
  installed to `/mnt/user` by an optional step after the system files. The
  difference is protection: `venture.files` and the file commands refuse to
  change `/mnt/system` but may change `/mnt/user`, as on other systems.
  Cosmos's FAT16 keeps long file names (a 23-character name was read back in
  a boot test) and ignores case when looking names up.
- **Not built yet:** file commands (Blue Water). VentureOS runs from RAM, as
  many small systems do; keeping changes on a disk is later and optional,
  and whether VentureOS formats blank disks itself is an open decision
  (§13). `cosmos run --disk <image>` attaches a disk when that work starts.

---

## 8. Network

- At boot, if Cosmos found a NIC, `ConnectToNetwork` reports its name and
  MAC and runs DHCP. Nothing caches the result: `net` reads `NetworkManager`
  directly, so it shows the current state. Under QEMU the default x64 NIC is an
  e1000e, and DHCP leases 10.0.2.15.
- `NetworkManager.Primary` is a struct handle that may name no device, so
  it's only read after checking `NetworkManager.DeviceCount`.
- **Not built yet:** DNS (DHCP reports a server, but it isn't registered),
  and any use of the network after boot (Pere Marquette).
- Cosmos's limits apply: no TLS, and TCP doesn't retransmit.

---

## 9. Shell

`VentureKernel.Run` prints a prompt, reads a line with `Console.ReadLine()`
(Cosmos provides line editing), and passes it to `CommandShell.Execute`.
Cosmos calls `Run` again as soon as it returns, so it never loops itself.

**The current folder.** `ShellContext.CurrentDirectory` starts at `/mnt`, and
the prompt shows it, as `/mnt> `. `cd [folder]` changes it (with no folder it
goes back to `/mnt`), and is built in because it changes the shell itself.
`ShellPaths.Resolve` works out a full path from what was typed: `..` and `.`
work, `..` at `/` stays there, and empty parts are skipped. Script commands,
runtime commands, `lua`, and the prompt (before each line, so `!cd` takes
effect) all run Lua from the current folder.

```mermaid
flowchart LR
    Line["input line"] --> Parse["CommandLine.TryParse"]
    Parse -- "unclosed quote" --> Err["explain, back to prompt"]
    Parse -- words --> Find["CommandRegistry.TryGet"]
    Find -- "unknown" --> Err
    Find --> Count["argument count check"]
    Count -- "wrong" --> Usage["print usage"]
    Count -- ok --> Exec["IShellCommand.Execute"]
```

- **Parsing** (`CommandLine`): words are separated by spaces or tabs.
  `'single quotes'` are literal, `"double quotes"` allow `\"` and `\\`,
  and a backslash outside quotes makes the next character literal. An
  unclosed quote or a trailing backslash is reported, not thrown.
- **Commands** (`IShellCommand`) declare a name (lowercase letters, digits,
  and hyphens), a one-line summary, a usage line, and their minimum and
  maximum argument counts. The shell checks the count before running a
  command, so commands don't repeat that check.
- **The registry** (`CommandRegistry`) rejects invalid and duplicate names,
  looks names up ignoring case, and lists commands in name order. `help`
  is generated from it. Names it doesn't know go to its sources
  (`ICommandSource`) in order, so a source can never replace a built-in.
- **Script commands** (`ScriptCommands`, the one source) are
  `/mnt/system/commands/<name>.lua` files. The directory is read on every
  lookup, so a script saved on the running system works at once; if it's
  missing, there are just no script commands. Only names that pass the
  registry's rule are looked up, so a name can't climb out of the directory.
  A script's leading `--` comments (`ScriptHeader`) give its summary, a
  `-- Usage:` line, and a `-- Needs:` line; it gets its arguments as `...`,
  and it checks them itself.
- **Where it lives.** The framework (`CommandLine`, `CommandRegistry`,
  `CommandShell`, `ShellContext`, the interfaces, and script commands) is in
  `Venture.OS.Common` and covered by tests, including real Lua runs on
  ordinary .NET. The built-in commands and `ConsoleShellOutput` are in the
  kernel, because they use Cosmos.
- **Output and power.** Commands write through `IShellOutput` and ask for
  halt, reboot, or shutdown through `IShellHost`, which `VentureKernel`
  implements. That keeps power calls in `Venture.OS.Kernel` and lets tests
  capture output later.

| Command | Does |
|---|---|
| `help [command]` | Lists the commands, or shows one's summary and usage |
| `clear` | Clears the screen |
| `cd [folder]` | Changes the current folder, or with none, goes back to `/mnt` |
| `open <app>` | Opens an app from `/mnt/system/apps` by name, or any `.app` by path |
| `close <app>` | Closes an open app, undoing what it set up |
| `apps` | Lists the system's apps, and which are open |
| `desktop [interface.uix...]` | Opens the desktop, with any `.uix` files as windows; Ctrl+Shift+Q goes back to the shell |
| `echo [text...]` | Lua script: prints its arguments on one line, separated by single spaces |
| `ls [-l] [folder...]` | Lua script: lists a folder, folders marked with `/`; `-l` adds type and size |
| `cat <file...>` | Lua script: prints files |
| `write [-a] <file> [text...]` | Lua script: writes a line to a file, or with `-a` adds it to the end |
| `mkdir [-p] <folder...>` | Lua script: makes folders; `-p` makes missing ones above too |
| `rm [-r] <path...>` | Lua script: removes files and empty folders; `-r` removes a folder and everything in it |
| `df [folder]` | Lua script: free and total space where a folder is stored |
| `pwd` | Lua script: the current folder |
| `info` | Lua script: version and build, commit, Cosmos version, uptime, and free and total memory |
| `lua [code]` | Runs one chunk of Lua 5.5 in a fresh interpreter, or with no code, opens a Lua prompt |
| `net` | Lua script: device, MAC, link, readiness, and IPv4 address, subnet, and gateway |
| `halt` | Ends the shell loop; the CPU halts and the machine stays on |
| `reboot` | Restarts through ACPI |
| `shutdown` | Powers off through ACPI |

Uptime comes from `Stopwatch`, which Cosmos plugs to the CPU's timestamp
counter on x64, so it counts from when the CPU started. Under `cosmos run`,
QEMU runs with `-no-reboot -no-shutdown`, so `reboot` and `shutdown` pause
the virtual machine instead; without those flags both work.

**Lua.** `lua <code>` and script commands both run through `LuaRunner`,
on `Cosmos.Executable.Lua`, a Lua 5.5 interpreter from the CosmosOS
organization. Each run gets a fresh interpreter, disposed afterwards, so
nothing carries over. `print`, `io.write`, and `io.stderr` go through
`IShellOutput` (`ShellOutputWriter` joins pieces into lines), and relative
paths in `dofile` and `io.open` start at the shell's current folder. Lua errors are reported with
their traceback, and `os.exit` with a non-zero status as that status; neither
reaches the shell loop.

**The Lua prompt.** Plain `lua` opens `LuaRepl`, a read–eval–print loop in one
interpreter, so values carry over between lines until `exit`. It prompts with
`lua> `, so it can't be mistaken for the shell. A line is tried first as an
expression, whose value is printed (`1 + 1` prints `2`), then as statements;
one left unfinished, such as `for i = 1, 3 do`, continues at `...> `. Ctrl+C
or Esc stops only the running line: `LuaRunner.Create` hands the prompt a way
to clear the latched stop before each line. Errors show only their message,
and `os.exit` or the end of the input leaves the prompt. As in the reference
`lua`, an expression's value is printed even when it's incidental, so
`io.write('x')` also prints the file it returns. The prompt is written in C#
rather than with the package's `RunPrompt`, which uses the shell's own `> `
prompt and has no point between lines to clear the stop.

**The system library.** Lua modules that ship with the system live in
`/mnt/system/lib`, and `require` looks there only (`package.path` is set to
`/mnt/system/lib/?.lua` and `?/init.lua`; native modules are off). Modules
are namespaced under `venture.`, so plain names stay free for scripts and
apps. Tests load the shipped files with `require`, as scripts do.

`venture.args` is the first module: a command declares its arguments and
options, and gets parsing, clear errors, and a generated `--help`:

```lua
local options = require("venture.args").parse({...}, {
  name = "greet",
  summary = "Greet someone.",
  arguments = { "name" },
  options = { loud = { short = "l", summary = "Shout it." } },
})
if not options then return end
```

It reads `--name value`, `--name=value`, `-s value`, combined short flags
(`-lq`), and `--` before words that start with a dash; negative numbers count
as arguments. Option types are `flag`, `string`, and `number`. A user's
mistake prints the problem and the usage line and returns `nil`; a mistake in
the declaration is a Lua error, since it's the command's bug. It's opt-in, so
the shell's contract with commands doesn't change: they still get their words
as `...`. The shell can't see a script's options without running it, so
`help <command>` keeps showing the header, and `<command> --help` shows the
options.

**The `venture` table** is how scripts reach the kernel, in `lua` and in
script commands alike. Its functions return plain values or tables, and the
scripts do the formatting:

| Function | Scope | Returns |
|---|---|---|
| `venture.version()` | *(always)* | `{ name, version, build, commit, cosmos, display }` |
| `venture.uptime()` | `system` | Whole seconds since the CPU started counting, or `nil` |
| `venture.memory()` | `system` | `{ free, total }` in bytes, or `nil` |
| `venture.command{...}` | `shell` | Registers a command for as long as its owner lasts; see **Owners** below |
| `venture.shell(line)` | `shell` | Runs a line as if typed at the shell; in `init.lua` and at the `lua` prompt |
| `venture.files.*` | `files` | Lists, reads, writes, and changes files; see **Files** below |
| `venture.net()` | `network` | `nil` without an adapter, else `{ device, mac, link_up, ready, address, subnet, gateway }`; the last three are `nil` until IPv4 is configured |

**Scopes.** A script declares what it reaches with `-- Needs:` in its header,
like an app's permissions, such as `-- Needs: system, network`. Scopes are
broad, so new functions in an area don't need new names. Every function is in
the table, but one outside the declared scopes raises
`venture.net needs the network scope: add it to a script's -- Needs: line, or an app's needs in Info.xml`. A scope
VentureOS doesn't know keeps the script from running at all, with the reason, so a script
written for a newer VentureOS fails clearly rather than half working.
`help <command>` shows a script's needs. `init.lua` follows the same rule;
`lua` gets every scope, since it's the user typing. Scopes cover only the
`venture` table: Lua's own `io` still reaches files without declaring
anything, until persistence makes that matter (Blue Water).

**Files.** `venture.files` (`FilesLibrary`) lists, reads, and changes files,
with paths starting from the current folder. Failures a script should expect,
such as a missing file, return `nil` and a message, as Lua's `io` does, so a
command prints the reason and carries on; a wrong argument type is an error.

| Function | Does |
|---|---|
| `list([path])` | `{ {name, type, size}, ... }`, sorted by name; `type` is `"file"` or `"folder"` |
| `info(path)` | `{name, path, type, size}` |
| `read(path)`, `write(path, data, [append])` | Whole files, byte for byte |
| `mkdir(path, [parents])` | A folder; with `parents`, missing ones above too, and an existing one is fine |
| `remove(path, [recursive])` | A file or empty folder; with `recursive`, everything in it |
| `space([path])` | `{free, total}` in bytes, from the mount that holds the path |
| `resolve([path])` | The full path |

Nothing that changes files works inside `/mnt/system`
(`ISystemInfo.SystemDirectory`), or removes a folder holding it: it's
rebuilt at every boot, and a stray `rm -r` mustn't break the running system.
Reading it is fine.

**Owners and undo.** After the ideas in "A Programming Paradigm for
Spatiotemporal Composability", lasting Lua code runs as a `LuaOwner`: one
interpreter that outlives a single run, and a list of ways to undo what the
code set up. Ending the owner runs those undos newest first, then disposes
the interpreter, so nothing it set up outlives it. There are two owners:

| Owner | Lives | Its commands |
|---|---|---|
| `init.lua` | The whole boot; if it fails partway, its undos run at once, so no half-registered commands are left | Until shutdown |
| A `lua` prompt session | Until `exit` or the end of its input | Vanish on `exit`, restoring any script they replaced |

One-shot code (`lua "code"` and script commands) has no lasting owner, so
`venture.command` and `venture.shell` there explain that they only work in
`init.lua` and at the `lua` prompt.

At the prompt, a line starting with `!` runs the rest as a shell command, so
a command defined there can be tried, changed, and tried again before `exit`
clears it away. `venture.shell(line)` does the same from Lua. Each nested call
goes through C#, and a .NET stack overflow can't be caught, so
`venture.shell` refuses to nest more than 8 deep, which stops a command that
runs itself. A call back into an owner while other code in it is running
restores the caller's output and stop afterwards. The prompt prints the
handle a registration returns as `command "name"`.

`venture.command { name, summary, usage, run }` registers a command through
`RuntimeCommands`, the first command source, so it wins a lookup. Its undo
unregisters it and releases its `run` function; the handle it returns can run
that undo early with `:remove()`. A runtime command runs in its owner's
interpreter, so state carries over between calls, with its output going to
whoever ran it and its own Ctrl+C stop (`init.lua`'s 10-second limit covers
only its startup). `help <command>` shows `From:` its owner. Who may take a
name:

- No one can take a built-in command's name.
- A name another owner registered is taken until that owner ends or removes
  it; the same owner registering it again replaces its earlier command.
- Only code the user types (the `lua` prompt) may replace a system script
  command, and it says so: `echo now replaces the
  echo script until you exit.` `init.lua` and, later, apps can't, so nothing
  installed can quietly take over a system command.

`VentureLibrary` builds the table from an `ISystemInfo`, which
`KernelSystemInfo` implements by reading Cosmos on every call, so scripts see
current values. A value that can't be read reliably (an uptime counter with
no frequency, a page count that would overflow) is `nil`, and an exception
from a device becomes a Lua error that `pcall` can catch. Tests use a fake
`ISystemInfo` and run the shipped `info.lua` and `net.lua` against it.

**Stopping Lua.** Ctrl+C or Esc stops `lua` and script commands; `init.lua`
also stops after 10 seconds. `LuaRunner` takes a stop check (`ScriptStop`
builds time limits and combines checks; `KeyboardStop` reads the keyboard
without blocking) and installs it with the standard `debug.sethook` as a count
hook that runs every 1,000 instructions. A reason it returns becomes a Lua
error with that message, such as `Interrupted.`, reported like any other
error, with its traceback. Three things keep a script from escaping it:

- The stop latches. A key press is read only once, so once a check says stop,
  the runner keeps saying it, and the hook then runs on every instruction. The
  error escapes a `pcall` that catches it almost at once.
- `debug.sethook` keeps its hook per thread, and a new coroutine would inherit
  only the count, so `coroutine.create` and `coroutine.wrap` are replaced with
  versions that hook each new coroutine.
- `debug.sethook` is then replaced with a function that raises
  `debug.sethook is reserved by VentureOS`.

Keys typed while a script runs are read by the check and discarded.

Cosmos's limits apply: local time is UTC, `os.getenv` returns nil, and there
is no `io.popen` or `/tmp`.

**Bundles.** Like macOS, VentureOS treats some folders as one thing, with the
same shape for every kind:

```text
Notes.app/
└── Contents/
    ├── Info.xml      <App version="1"><Info identifier=... name=... /></App>
    ├── Lua/          code; main.lua runs when an app opens
    └── Resources/    images, .uix interfaces, and so on
```

The root element names the kind: `<App>` (`.app`), and later `<Theme>`
(`.theme`) and `<Library>` (`.library`). `Bundle.Read` checks the kind, so
handing a theme to something expecting an app says so. `<Info>` takes
`identifier` (required, reverse-DNS), `name` (required), `version`, `needs`
(scopes for all the bundle's code), `main` (an app's first script, by
default `Contents/Lua/main.lua`), and `icon`. Paths stay inside the bundle.
`Info.xml` follows UIScript's compatibility rules: a newer `version`, an
unknown `requires` feature, or an unknown element refuses the bundle with the
reason, and an unknown attribute is skipped and listed, to catch typos.

Both `Info.xml` and `.uix` files are read by `FlatXml`, because Cosmos can't
link `System.Xml` (§1.1). It reads exactly the flat subset they use: an
optional declaration, comments, one root whose elements hold attributes and
nothing else, the five standard entities, and numeric character references.
A `DOCTYPE`, text, deeper nesting, CDATA, or an unknown entity is refused
with its line.

**The desktop.** `desktop` switches the screen to graphics until VentureOS ▸
Return to Shell (Ctrl+Shift+Q). The
layout and drawing live in `Venture.OS.Desktop` and draw through `IPainter`,
so they're tested against a painter that records pixels; the kernel's
`CanvasPainter` draws on Cosmos's full-screen canvas, with Open Sans for the
interface and Fira Code for monospace, loaded from `/mnt/system/fonts`.

- **Screen.** Limine is asked for 1024x768 in `limine.conf`; without it,
  QEMU's framebuffer is 1280x800. The text console uses the same screen.
- **Drawing.** The canvas doesn't blend, and the sky's glow is
  translucent, so `DesktopBackground` works the whole sky out once, as
  pixels (85 ms at 1024x768 under QEMU), and each frame copies it, then draws
  on top. `Canvas.Display` shows the finished frame at once.
- **Translucency.** The canvas's own drawing doesn't blend, so
  `CanvasPainter` blends a translucent fill (window shadows, the fade over
  inactive windows) into the canvas's buffer itself.
- **Windows.** `WindowManager` keeps the windows back to front. Pressing a
  window brings it to the front and focuses it; its title bar drags it,
  kept on screen far enough to grab again; its close button closes it on
  release.
- **The unified title bar** (`DesktopWindow`), after CityLimits' window
  frame: one 28 px band with a red close button and an orange minimize
  button on the left, then the title, then the document's toolbar items
  (`<ToolbarItem>`) along the right end, which report `pressed` on release.
  The title is centred in the window when it fits clear of both ends of
  the bar, and otherwise in the room between them, cut with an ellipsis.
  A `<TitleField>` makes the title editable, as Files' address bar is,
  after CityLimits' city name: it reads as a plain centred title, keeping
  its end when cut, and only that text takes clicks, so the bar around it
  still drags. A click starts an edit, which turns it into a field across
  all the room; Enter submits it under the field's name, and Esc or a
  click elsewhere puts the old text back; a click on the same window's
  toolbar keeps the edit, so an item can act on what was typed. Item and
  title widths come from the font, so they're laid out when drawn.
- **Sidebars.** A `<Sidebar>` runs down the window's left, its full height,
  in its own shade, as Nautilus has it in GNOME 45: the buttons sit on its
  top, and the title bar spans only the content, whose corner moves right
  by the sidebar's width. `UIView` keeps it as a selectable list placed left
  of that corner, so selection, scrolling, and Lua's list calls serve it
  unchanged; only its drawing differs, with taller rows and no box.
- **Minimize.** The orange button sends a window to the dock: it stays in
  the window list but isn't drawn, takes no input, and can't be in front.
  The dock shows each minimized window as a pale tile after the apps, past
  a divider; clicking the tile, or the app's icon when the app has no
  window showing, restores it. Keys go to the focused window, the wheel to the window under the
  pointer. Each window shows a `UIView`, which places a `.uix` document's
  controls as Hudini does, draws them, and turns input into Hudini's events
  (pressed, submitted, changed, selected), which the window's owner handles.
  A double-click (two presses on one item within 500 ms, timed by
  `DesktopInput.Time`) in a selectable list reports `submitted` with the
  item, a VentureOS addition proposed for Hudini's spec.
- **Menu bar.** `MenuBar` shows the VentureOS menu (About VentureOS, Return
  to Shell), then the active app's menu (Quit, Ctrl+Q), then the focused
  window's own `.uix` menus. The active app is the app of the window last
  brought forward, or the app last opened from the dock. Clicking or
  press-drag-release chooses; Alt and a menu's hotkey opens it; the arrows,
  Enter, item hotkeys, and Esc work while a menu is open, and an open menu
  takes every key and the click that closes it. Shortcuts match from
  anywhere, the desktop's own first. A chosen window item reaches the window
  as a pressed event with the item's name, and items are disabled and
  checked by name through the window's `UIView`. Titles and items are laid
  out when drawn, so the pointer hits what was last on screen.
- **Dock.** `Dock` shows the system's apps, then the user's after a divider
  (`AppManager.Available`), as
  colored tiles with their initial, since images aren't drawn yet, with a
  dot under open ones and the name over the one under the pointer. A click
  opens the app from the current folder, or brings an open one forward. A
  user app opens by its path, so it can't be mistaken for a system app with
  the same name.
- **App output.** An app keeps the output it opened with, and the console
  would draw over the desktop, so apps opened from the dock, and every
  window event handler, write to one `HeldOutput` the kernel keeps. What
  they say while the desktop shows appears in a `DesktopAlert` window; when
  the desktop closes, anything left is printed and later lines go straight
  to the shell.
- **Windows that last.** The kernel keeps one `WindowManager` for the whole
  boot, so an app opened from the shell has its window waiting in the
  desktop. Leaving the desktop closes its own windows (About, messages,
  previews) and keeps apps' (`DesktopWindow.OpenedByApp`).
- **Apps quit with their last window.** When a window an app opened closes
  and the app has no other, the app quits, which closes its messages and
  removes its commands. Messages don't count, so closing one never quits
  its app.
- **The fallback app.** Files is to the desktop what Finder is to a Mac
  (`DesktopSession.FallbackApp`). The desktop starts it before anything
  else, so it opens a window on the folder `desktop` was typed in; it's
  first in the dock and always marked open; its menu shows whenever no
  other app is in front; it has New Window and no Quit; and closing its
  last window leaves it running. If it isn't running, such as after
  `close files`, or fails to start, the desktop keeps going and starts it
  again, waiting 2, 5, 10, then every 30 seconds between failures, with one
  message per new reason.
- **Quick Look** (`QuickLook`). A preview window for a path: a `.uix` file
  as its live interface, or why it can't show; text as its lines from the
  top, up to 64 KB; an app's name, identifier, version, and needs; a
  folder's item count; anything else, its size. A file is text unless its
  first kilobyte holds a zero byte. Previews belong to the desktop and close
  with it.
- **Input order.** The open menu, then shortcuts and menu hotkeys, then the
  bar, the dock, and the windows. A press belongs to what it landed on until
  release. Windows ask to close with `RequestClose`, and the window manager
  closes them at the end of the frame.
- **Opening.** `desktop` starts Files, then opens each `.uix` named on the
  command line in front of it, which previews an interface while it's
  written. About VentureOS, from `/mnt/system/interfaces/about.uix` with the
  version filled in, is in the VentureOS menu. Those are read before the screen switches, so a broken document
  is reported in the shell.
- **Frame loop.** `DesktopCommand` reads the PS/2 mouse, wheel, and keyboard,
  and `DesktopSession` redraws only when they changed. The loop keeps the
  CPU busy while the desktop is open, so a frame where nothing changes
  allocates nothing (`IdleFrameTests`): per-frame lists are reused and
  copied out only when they hold something, loops over interfaces use
  indexes rather than `foreach`, which would allocate an enumerator, and
  the menu bar is rebuilt only when the active app or front window
  changes.
- **Leaving.** The console draws on the same canvas and can't be hidden, so
  leaving clears it, which redraws the shell; earlier shell output isn't
  kept. Any exception in the desktop clears back to the console and is
  reported, so the screen is never left in graphics. While the desktop is
  open, system messages go to serial only (`KernelLog.WriteSerialLine`).

**Interfaces.** `UIScript` reads `.uix` documents in the format Hudini
defines (a Draft, version 1), with the same rules as Hudini's reader:
version and `requires` are checked first; unknown elements refuse the
document and unknown attributes are listed; references, sizes, and
per-control attributes are validated, so a document reads completely or not
at all. It's tested against Hudini's five example documents, copied into the
tests with Hudini's license. VentureOS adds two features, drafted for
Hudini to take up: `menus` (`<Menu>`, `<MenuItem>`, `<MenuSeparator>`, with
hotkeys and Ctrl or Alt shortcuts), in [uiscript-menus.md](./uiscript-menus.md),
and `sidebar` (`<Sidebar>`, a list of places beside the content), in
[uiscript-sidebar.md](./uiscript-sidebar.md). `UIView` draws documents in
desktop windows.

**Apps.** `AppManager` opens, closes, and lists `.app` bundles. Opening one
reads its bundle, makes an owner named after the app with the app's `needs`,
and runs its `main` script from the current folder, stopped by Ctrl+C, Esc,
or a 10-second limit. If that fails, its undos run at once. Closing ends the
owner, so its commands vanish; an app can't be closed while its own code is
running, since its interpreter would vanish under it (`LuaOwner.IsRunning`).

- **Finding an app:** by folder or `Info.xml` name in `/mnt/system/apps`,
  then `/mnt/user/apps`, ignoring case, or by path. A bundle opened by path
  from outside both folders says what it needs first, such as
  `Hello needs shell.`, as a note for developers; only `open` in the shell
  shows it, since people using the desktop needn't read about scopes, which
  are enforced either way. `apps` marks each app `system` or `user`.
- **One instance:** an app already open, by identifier, isn't opened again.
- **What its code gets:** the `venture` table through its `needs`, as any
  script; `venture.app` (`name`, `identifier`, `version`, `path`, and
  `resource(path)` for `Contents/Resources/`); and `require` searching its
  own `Contents/Lua/`, then the system library. Apps can't replace system
  script commands, and `help` shows their commands as `From:` the app.
- **Windows** (`venture.ui`, `UiLibrary`, the `ui` scope):
  `venture.ui.open("main.uix", { title = ... })` opens a window from the
  app's `Contents/Resources/` and registers its undo, so quitting closes it.
  `window:on(name, fn)` keeps `fn` in the Lua registry and calls it through
  the owner for that control's or menu item's events, with an event table,
  writing to the held output and stopped after 5 seconds, since the desktop
  holds the keyboard. Methods (`setText`, `setItems`, `select`,
  `setDisabled`, and the rest) refuse names the document doesn't have.
  Code that isn't an app's gets an error saying only apps have windows.
  `venture.ui.preview(path)` opens Quick Look; `venture.ui.launch(path)`
  opens an `.app`, or brings an open one forward, with what it says going
  to the desktop's messages; `venture.ui.onNewWindow(fn)` gives the app
  New Window (Ctrl+N), which the dock also uses when the app has no window.
  The app menu shows Quit for every app but the fallback.
- **Text areas** (`<TextArea>`, a VentureOS addition to UIScript's core). A
  multi-line entry: Enter adds a line, and typing reports `changed` with the
  text. Its text wraps at spaces, cutting a word too long for a line, and
  the wrapped lines are kept until the text or width changes; words are
  measured one at a time, so wrapping costs the same for any length up to
  its 65,536-character default limit. The arrows, Home, and End move the
  caret by those lines, a click places it, and the area scrolls by the wheel
  or a bound slider (`scrolls`) and to keep the caret in view. Clicks are
  measured with the painter last drawn with. No selection or clipboard yet.
- **Shells for apps** (`venture.openShell()`, under `shell`). An app gets a
  shell of its own, with its own current folder, that writes to a caught
  output rather than the console: `session:run(line)` returns what the
  command printed and whether it cleared the screen, and a command's
  exception is reported there, since no shell loop sits under it. Its
  `ShellContext.HasConsole` is false, so `desktop` and the `lua` prompt,
  which use the console directly, refuse with a reason. A command's time
  doesn't count against the app's event limit (`LuaOwner.WithoutStop`):
  Ctrl+C or Esc stops scripts as anywhere, and the app gets 5 more seconds
  once it returns. Terminal.app is the shell in a window this way, with up
  to 500 lines kept, `exit` to close, and Shell ▸ Clear.
- **Fonts and following.** A `<Style>` may ask for `font="monospace"`, which
  the view draws through a painter that uses Fira Code. A `<TextArea>` with
  `follows="true"` jumps to its newest line when its text changes while it
  was scrolled to the end, as a log list does.
- **Focus comes back.** A window that leaves the front remembers its focused
  field, and gets it back when it comes forward again (`UIView.RestoreFocus`),
  unless the click that brought it forward landed elsewhere in it.
- **Opening files with apps.** `venture.ui.onOpenFile(fn)` says how an app
  opens a file, and `venture.ui.launch(app, file)` (by path or by name,
  such as `"TextEdit"`) starts the app if it isn't open, then hands it the
  file. An app started with no window of its own, from the dock or by a
  launch with no file, gets one from its New Window. `AppManager` refuses to
  open an app whose main script is still running, so an app that launches
  itself while it starts can't recurse.
- **Timers** (`window:every(seconds, fn)`, `AppWindows.Every` and `Tick`).
  The session ticks them once a frame, from its clock, and redraws when one
  ran. A timer runs through the app's owner like an event handler, with the
  same output and 5-second limit, at least 0.1 seconds apart. It stops with
  its `stop()`, when its window closes, or when the app quits. Timers only
  run while the desktop shows, and lost time isn't made up: a late timer
  runs once and waits a whole interval again.

`Notes.app` ships as the example: a window with a text field, the list of
notes, Delete, and a Note menu, plus `note <text>`, `note -l`, `note -d <n>`,
and `note --clear`, both working on `notes.txt` in the folder it opened
from. [Writing apps](./writing-apps.md) is the step-by-step guide; a test
builds and runs its example, so it can't drift from what works.

---

## 10. Version and build identity

- **Version:** [GitInfo](https://github.com/devlooped/GitInfo) reads
  `GitInfo.txt` (base `major.minor.patch`) and adds the number of commits
  that changed the `src/Venture.OS/` folder since that file changed. Commits
  that only touch docs don't change the version, and a build needs the full
  history.
- **Moving `GitInfo.txt` resets the count.** GitInfo finds the file's last
  change with `git log` without following renames, so a move looks like a
  new base. Set a new base version in the same commit as any move: moving
  the project into `src/` started 0.2.0. The desktop started 0.3.0.
- **Build number:** derived from the commit hash: its first four characters
  read as a number and reduced to at most six digits. It identifies a build
  but doesn't increase over time.
- **`HostInfo.DisplayVersion`** (`VentureOS 0.3.0 (Build 123456)`) is the
  one string used at the start of boot, in the banner, and in the panic
  report.
- **The changelog** records only versions with changes a user would notice,
  so its latest entry trails the version the kernel reports.

---

## 11. Build and run

- **Locally:** `dotnet test --project src/Venture.OS.Common.Tests`, then
  `cosmos check` (always first before building: tool status and CLI updates),
  `cosmos build -p src/Venture.OS`, then `./run.sh` (add `--headless`
  for serial only) or `cosmos run -p src/Venture.OS --headless`. `--nic none`
  boots without a network card.
- **CI:** none for now. The kernel needs Cosmos's toolchain and doesn't build
  like a typical .NET project, so the workflow was removed (2026-11-11).
  Tests and builds run locally.
- **Not built yet:** CI, a headless boot check, and published ISOs
  (every-stage work on the roadmap).

---

## 12. Testing and verification

| Kind | Today | Planned |
|---|---|---|
| Build | x64 and ARM64 with no warnings, locally | CI, when it returns |
| Boot | Headless boots by hand, read through `[Venture]` serial lines | CI boots headless and fails on a panic or a missing banner |
| Failure paths | A forced panic and a no-NIC boot, verified by hand | Kept as repeatable checks |
| Logic | xUnit v3 tests for `Venture.OS.Common`, run locally | Each new kernel-free type comes with tests |

xUnit v3 runs on Microsoft.Testing.Platform, which the .NET 10 SDK's
`dotnet test` only uses when `global.json` opts in; without it, `dotnet
test` stops with an error.

Cosmos's own test kernels use `Cosmos.TestRunner.Framework`. Whether it's
published for use outside the Cosmos repository hasn't been confirmed.

---

## 13. Open decisions

1. **Formatting disks.** Whether VentureOS formats a blank disk itself, or
   only mounts existing FAT partitions (Blue Water). The Cosmos docs warn
   against testing filesystem code on real hardware.
2. **Telnet.** Whether to serve a shell over the network, given that it has
   no login and sends everything in the clear (Pere Marquette).
3. **Experimental Cosmos APIs.** None are used. Any future need, such as
   custom drivers through the driver kit, is a decision I'll make when it
   comes up.

## Revision history

| Date | Change | Summary |
|---|---|---|
| 2026-11-11 | minor | Sidebars (§9): windows draw a `<Sidebar>` down their left, full height, with the buttons on top; the view keeps it as a list; Files' bookmarks move into one. |
| 2026-11-11 | minor | Interfaces (§9): the `sidebar` feature and `<Sidebar>` in UIScript, drafted for Hudini; the desktop doesn't draw it yet. |
| 2026-11-11 | minor | Title bar (§9): titles are centred; a `<TitleField>` reads as a plain title until it's clicked, and only its text takes clicks; Files drops Go for Enter. |
| 2026-11-11 | minor | Terminal (§2, §9): `venture.openShell` gives apps shells of their own without the console; Terminal.app ships; styles take `font`, text areas `follows`; windows get their focus back when they come forward. |
| 2026-11-11 | minor | Text (§2, §9): `<TextArea>`; TextEdit ships with the user's apps; Files opens text files in it through `venture.ui.launch(app, file)` and `onOpenFile`. |
| 2026-11-11 | minor | Frame loop (§9): idle frames allocate nothing, which stopped memory churning between about 50 and 430 MiB (BUG-6). |
| 2026-11-11 | minor | Timers (§2, §9): `window:every` runs code on a schedule while the desktop shows; System Monitor ships. |
| 2026-11-11 | minor | The unified title bar (§2, §9): close, minimize, toolbar items, and an editable title field in one bar; minimized windows go to the dock; Files gets an address field and bookmarks; Notes moves to the user's apps; the desktop no longer shows what apps need. |
| 2026-11-11 | minor | User apps (§2, §4, §7, §9): `user/` installs to `/mnt/user`, which the user may change, in a new optional boot step; apps open from either folder, the system's first; the dock divides them; Calculator ships. |
| 2026-11-11 | minor | Double-click (§9): a selectable list reports `submitted` for a double-clicked item, and Files opens with it. |
| 2026-11-11 | minor | Files (§2, §9): the desktop's fallback app, started first and restarted with growing waits; Quick Look; `venture.ui.preview`, `launch`, and `onNewWindow`; About moves to the VentureOS menu only. |
| 2026-11-11 | minor | Version 0.3.0 (§10): a new base in `GitInfo.txt` for the desktop. |
| 2026-11-11 | minor | Apps with windows (§2, §9): `venture.ui` under the `ui` scope; windows last for the whole boot; an app quits when its last own window closes; event handlers write to the held output with a 5-second limit; Notes has a window. |
| 2026-11-11 | minor | The menu bar and dock (§2, §9): the VentureOS menu, the active app's menu, and the focused window's `.uix` menus with hotkeys and shortcuts; a dock that opens the system's apps; app output held while the desktop shows. Ctrl+Shift+Q, not Esc, returns to the shell. |
| 2026-11-11 | minor | CI removed for now (§2, §11, §12): the kernel doesn't build like a typical .NET project. The desktop's sky loses its bubbles. |
| 2026-11-11 | minor | Desktop windows (§2, §9, §11): windows show `.uix` interfaces through `UIView`, About VentureOS opens with the desktop, `desktop` previews `.uix` files, and translucent fills blend. CI moved to `dotnet.yml`, which builds the solution and runs the tests. |
| 2026-11-11 | minor | The desktop (§2, §9): `desktop` draws the sky and pointer through `IPainter` with the system fonts, at 1024x768, and Esc returns to the shell. |
| 2026-11-11 | minor | Interfaces (§2, §9): a UIScript reader matching Hudini's, tested on its examples, with a `menus` feature drafted for Hudini. |
| 2026-11-11 | minor | `Notes.app` ships in `/mnt/system/apps` (§2, §9), with a tested tutorial in `docs/writing-apps.md`. The missing-scope error now names both places a scope is declared. |
| 2026-11-11 | minor | Apps (§9): `open`, `close`, and `apps`; each app is an owner with its declared `needs`, `venture.app`, and its own modules first for `require`. |
| 2026-11-11 | minor | Bundles (§1.1, §2, §9): macOS-style `Contents/Info.xml`, read by `Bundle.Read` through `FlatXml`, since `System.Xml` doesn't link under Cosmos 3.0.94. |
| 2026-11-11 | minor | Files (§2, §9): `venture.files` under the new `files` scope, and `ls`, `cat`, `write`, `mkdir`, `rm`, `df`, and `pwd` as Lua commands; `/mnt/system` is read-only to them. `ui` is now the reserved scope. |
| 2026-11-10 | minor | The current folder (§9): the shell keeps one, the prompt shows it, `cd` changes it, and Lua runs from it. |
| 2026-11-10 | minor | Direction (§1, §7): VentureOS runs from RAM, with persistence later and optional; the desktop will use `.uix` (UIScript) interfaces. |
| 2026-11-10 | minor | The `lua` prompt as an owner (§9): its commands vanish on `exit`; `!` and `venture.shell` run shell commands, nested at most 8 deep. |
| 2026-11-10 | minor | Owners and `venture.command` (§9): `init.lua` runs as a `LuaOwner` and can register commands under the new `shell` scope; they're undone if it fails, and only user-typed code may replace system scripts. |
| 2026-11-10 | minor | The system library (§2, §9): `require` loads from `/mnt/system/lib`, and `venture.args` parses a command's arguments and options with a generated `--help`. |
| 2026-11-10 | minor | The Lua prompt (§2, §9): plain `lua` opens `LuaRepl`, where values carry over between lines and Ctrl+C stops only the running line. |
| 2026-11-10 | minor | Scopes (§9): scripts declare `-- Needs: system` or `network`; the `venture` table refuses undeclared functions, and unknown or reserved scopes keep a script from running. |
| 2026-11-10 | minor | Stopping Lua (§4, §9): Ctrl+C or Esc stops `lua` and script commands through a count hook, and `init.lua` also stops after 10 seconds. |
| 2026-11-10 | minor | `init.lua` (§2, §4): runs once after the banner as an optional step, with its output on serial through `KernelLogOutput`; it now prints the help hint. |
| 2026-11-06 | minor | The `venture` table (§2, §3, §9): scripts reach the kernel through `ISystemInfo`, and `info` and `net` are now Lua scripts. |
| 2026-11-06 | minor | Script commands (§2, §9): `/mnt/system/commands/<name>.lua` runs as `name` through a new command source, Lua output goes through `IShellOutput`, and `echo` is now a script. |
| 2026-11-06 | minor | System files (§2, §4, §7): Lua scripts under `src/Venture.OS/system/` are built into the kernel and installed to `/mnt/system` by a new optional boot step. |
| 2026-11-05 | minor | Lua (§9): `Cosmos.Executable.Lua` replaces `LuaCSharp`, and the `lua` command runs one chunk; the Lua library is no longer an open decision (§13). |
| 2026-11-05 | minor | `HostInfo` holds only the build's identity; the network fields it cached at boot were never read and are gone (§2, §3, §8). |
| 2026-11-05 | minor | The `echo` command, bringing the shell to eight commands (§9). |
| 2026-10-15 | minor | The shell framework moved into `Venture.OS.Common`, a library with no Cosmos reference, tested by `Venture.OS.Common.Tests` (xUnit v3, 59 tests) locally and in CI (§2, §3 rule 6, §9, §11, §12). |
| 2026-10-15 | minor | The project moved into `src/` (§2, §11), and the version base became 0.2.0 because moving `GitInfo.txt` resets GitInfo's count (§10). |
| 2026-10-15 | minor | The shell (§9): parsing, the registry, seven commands, and the shell boot step; layering and boot diagrams updated. Versioning corrected: only commits that change `Venture.OS/` raise the version. |
| 2026-10-15 | major | First architecture overview: principles, the rules for absorbing Cosmos changes, layout, layering, boot, failure model, output, storage, network, shell, versioning, build and CI, testing, and open decisions. |
