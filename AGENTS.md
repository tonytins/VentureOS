# AGENTS.md: guide for AI agents and contributors

VentureOS is a hobby operating system written in **C#** on the
[Cosmos](https://github.com/CosmosOS/Cosmos) gen3 framework
(`Cosmos.Sdk` 3.0.94, .NET 10). Cosmos compiles the kernel ahead of time to
a bootable ELF, which Limine loads from an ISO. It targets **x64** and
**ARM64** and runs in QEMU. It is the spiritual successor to
[TOMAS](https://github.com/tonytins/tomas/). Write the name as **VentureOS**
in prose; the project is `Venture.OS`, the assembly is `Venture`, and the
namespaces start with `Venture.OS`.

This is early-stage, solo work held to production standards. Read this file
first, then [README.md](./README.md) and [changelog.md](./changelog.md).

## 1. Workspace map

```text
Venture.slnx                 solution (the VS Code default solution)
Venture.OS/
  Venture.OS.csproj          Cosmos SDK project; package versions live here
  Kernel.cs                  entry point: OnBoot, BeforeRun (boot sequence), Run (shell loop)
  HostInfo.cs                OS name, version, build number, network identity
  KernelPanic.cs             deliberate halt with a report to serial and the console
  GlobalUsings.cs            global usings for System and Cosmos.Kernel.* namespaces
  Startup/
    Bootstrap.cs             boot steps: RAM disk, timer check, network and DHCP
    BootRamDisk.cs           in-memory IBlockDevice backing the FAT16 disk at /mnt
  Bootloader/limine.conf     Limine boot entry (boots /boot/Venture.elf)
  GitInfo.txt                base version for GitInfo (major.minor.patch)
  src/C/                     reserved for native C sources (empty)
  .vscode/tasks.json         build, run, and debug tasks for QEMU on x64 and ARM64
run.sh                       check, build, and run x64 with the Cosmos CLI
.github/workflows/cosmos.yml CI: installs the Cosmos toolchain and builds
changelog.md                 user-facing history by version
```

Build output (`bin/`, `obj/`, `output-*/`, `*.iso`, `*.img`) is ignored;
never commit it.

`Stash/` is also ignored. It's the owner's personal space for brainstormed
material and notes. Read it for context when it helps, but treat it as
drafts rather than decisions, and don't depend on it from code, docs, or
skills.

## 2. How it works today

- **Boot.** `Kernel.BeforeRun` runs the `Bootstrap` steps in order through
  `RunCriticalStep` or `RunOptionalStep`, then clears the screen and shows
  the version banner. The RAM filesystem and the timer are critical: they
  return `false` on failure, and a failure or exception panics the kernel.
  Networking is optional: an exception is reported and boot continues.
- **Panic.** `KernelPanic.Halt` writes the version, the failed step, and the
  reason to serial (via `Log`) and the console, then halts the CPU in a
  loop. It never returns.
- **Storage.** A 32 MiB RAM disk is formatted as FAT16 and mounted at `/mnt`
  through `VfsManager`. Nothing persists across boots.
- **Network.** If a NIC is present, `Bootstrap.ConnectToNetwork` records its
  MAC and name in `HostInfo` and asks DHCP for an address.
- **Shell.** `Kernel.Run` is called in a loop. It reads one line and
  `ExecuteCommand` dispatches `help`, `clear`, and `halt` in a `switch`.
  `Run` catches every exception, reports it, and returns to the prompt. Keep the `help` text
  in step with the commands.
- **Version.** `HostInfo.Version` comes from the GitInfo package:
  `GitInfo.txt` holds the base `major.minor.patch`, and the patch number
  goes up by one with each commit after it. `BuildNumber` is derived from
  the commit hash. The changelog only records versions with user-facing
  changes, so its latest entry is expected to lag behind the version the
  kernel reports.
- **Lua.** `LuaCSharp` is referenced in the project but not used in code
  yet.

## 3. Golden rules

### Never crash uncontrolled

There is no host process underneath. An unhandled exception in the kernel
stops the machine with no explanation, so treat stability as the top
priority. The rule depends on the phase:

- **During boot (`BeforeRun`):** optional steps degrade, and critical ones
  panic deliberately. A step is critical if the system can't do its job
  without it; running on with it broken only makes later failures harder
  to understand. Critical steps return `bool` and go through
  `RunCriticalStep`; optional steps go through `RunOptionalStep`. When you
  add a boot step, decide which it is and say so in the change. Never let
  an exception escape `BeforeRun` on its own.
- **After boot (the `Run` loop):** never crash. Nothing that happens once
  the shell is running may halt the system, except the user's `halt`
  command. Commands and the subsystems they call report errors and return
  to the prompt. `Run` catches everything as a last resort; don't rely on
  it in place of handling errors where they happen.
- **Panic only through `KernelPanic.Halt`**, and only during boot. Don't
  call `Power.Halt`, `Stop`, or an endless loop to stop the system
  anywhere else.
- **Validate everything that crosses a boundary:** shell input, file and
  disk contents, network packets, device-reported values, and sizes passed
  to constructors. Reject bad input with a clear console message.
- **Check sizes and offsets before casting or slicing.** Block devices and
  buffers use `ulong` counts; check range and overflow before narrowing to
  `int` or calling `AsSpan`.
- **Report failures explicitly.** Boot steps and commands print what failed.
  Don't swallow a failure or substitute a silent default, and don't print
  success when a step was skipped.
- **Bound loops and waits** that depend on hardware or the network, such as
  DHCP. A missing device must not hang boot.
- **Don't add `Thread.Sleep` or blocking waits** to code paths that don't
  already need them.

### Cosmos constraints

- Use the [Cosmos gen3 skill](./.agents/skills/cosmos-gen3/SKILL.md) for
  the CLI, the kernel lifecycle, execution contexts, logging, and subsystem
  APIs. The Cosmos docs track the latest release, so check that an API
  exists in the pinned version before using it.
- The kernel is compiled ahead of time with no JIT. Avoid reflection,
  `dynamic`, runtime code generation, and APIs that Cosmos doesn't
  implement. Prefer simple types, `Span<T>`, and explicit control flow.
- Use Cosmos APIs (`Cosmos.Kernel.*`, aliased as `Sys`) for hardware,
  storage, timers, and networking. Check the Cosmos gen3 docs or the
  package's public API before assuming a .NET BCL feature works in the
  kernel. If you can't confirm something works, say so rather than guess.
- Keep both architectures building. Don't add x64-only code without a guard
  and a note.
- Changing the `Cosmos.*` package versions changes the kernel runtime.
  Bump them deliberately, in a change of their own, and call it out.
- New NuGet dependencies need the owner's approval, and they must work
  under Cosmos's ahead-of-time compiler.

### Code style

- Follow the [.NET guidelines skill](./.agents/skills/dotnet-guidelines/SKILL.md)
  for naming, comments, control flow, interfaces, API shape, error handling,
  and tests. Where it and this file disagree, this file wins for VentureOS.
- Follow [.editorconfig](./.editorconfig): file-scoped namespaces, `var`,
  pattern matching and switch expressions, expression-bodied members on one
  line, PascalCase types, members, and constants, camelCase locals and
  private fields, and `I`/`T` prefixes for interfaces and type parameters.
- Put a namespace's shared usings in `GlobalUsings.cs` rather than at the
  top of each file, and don't add duplicates there.
- Give public and internal APIs `///` doc comments. Comments explain why,
  not what.
- Keep `Kernel.cs` thin. Put boot steps in `Startup/`, and put new
  subsystems (commands, drivers, filesystems) in their own folder and
  namespace under `Venture.OS`.
- Match the surrounding code when it disagrees with this file, and mention
  the disagreement.

## 4. Before you finish a task

There is no test project yet. The build plus a boot in QEMU is the gate.
Always run `cosmos check` before building. It reports system and tool
information and checks for updates to the Cosmos CLI itself. If it reports
an update, mention it to the owner after the build succeeds.

```sh
cosmos check                                   # always first: system and tool info, CLI updates
cosmos build -p Venture.OS                     # must build with no new warnings
./run.sh                                       # boots x64 in QEMU; confirm the banner and your change
```

- If you touched boot, storage, or networking, boot it and read the
  console. Without QEMU, say that you couldn't boot it.
- If you touched architecture-sensitive code, also build ARM64
  (`cosmos build -a arm64 -p Venture.OS`, or the "Build ARM64" VS Code
  task).
- For logic that doesn't depend on the kernel (parsing, formatting,
  arithmetic), prefer adding a small test project over relying only on a
  boot. Propose it first if none exists yet.
- Update [changelog.md](./changelog.md) only for changes a user would
  notice, under the version the kernel reports for that commit. Skip
  internal changes such as refactors, docs, and tooling. Also update
  [README.md](./README.md) if setup or build steps change, and this file if
  the layout or rules change.
- Don't edit `GitInfo.txt` unless you're asked to cut a version.

Describe only what works today, and keep the tree building at every step.

## 5. Calling things out

The owner reviews changes alone, so make review easy:

- State breaking changes, package version bumps, new dependencies, and
  boot-order changes clearly in the summary or commit message.
- List judgment calls and anything you couldn't verify, such as "builds,
  but not booted on ARM64".
- If you notice a risk outside your task, report it. Don't fix it quietly
  in an unrelated change.
