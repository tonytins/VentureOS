# Project Guidelines

## Project Structure

VentureOS is a hobby operating system written in C# on the  
[Cosmos](https://github.com/CosmosOS/Cosmos) gen3 framework (`Cosmos.Sdk` 3.0.99, .NET 10). Cosmos compiles the kernel ahead of time to an ELF, which Limine boots from an ISO. It targets x64 and ARM64 and runs in QEMU. Write the name as **VentureOS** in prose; the project is `Venture.OS`, the assembly is  
`Venture`, and namespaces start with `Venture.OS`.

Every .NET project lives in its own folder, under `src/`, or `tests/` for tests, and is listed in `src/Venture.slnx`:

- `src/Venture.OS.Kernel` is the kernel. `Kernel/` holds the entry point  
  (`VentureKernel`), `KernelPanic`, and `KernelLog`; `Startup/` holds the boot  
  steps and the RAM disk; `Shell/Commands/` holds one class per shell command.  
  `CosmosKernelClass` and the package versions are in `Venture.OS.Kernel.csproj`.
- `rootfs/` holds the files the kernel is built with and installs at boot.  
  `rootfs/system/` holds the system files (Lua scripts, apps, fonts, icons,  
  wallpapers, the shared string tables in `strings/`, and the desktop's  
  `.uix` interfaces), which the build turns into C# and boot  
  installs to `/mnt/system`, where nothing may change them; `rootfs/user/`  
  holds the user's files, such as `apps/Calculator.app` and  
  `settings/appearance.xml`, installed to `/mnt/user`, where the user may;  
  `rootfs/system/init.lua` runs once after boot.
- `src/Venture.OS.Common` is kernel-free code: the shell framework  
  (`CommandLine`, `CommandRegistry`, `CommandShell`, and its interfaces) and  
  Lua system support (`Scripting/`), bundles (`Bundles/`), and the desktop's  
  drawing logic (`Desktop/`, drawn through `IPainter`). It must never  
  reference the Cosmos kernel; `Cosmos.Executable.Lua` is allowed, since it  
  runs on ordinary .NET.
- `src/Venture.OS.Interface` holds the formats interfaces are written in.  
  VentureOS keeps the UIScript spec, in  
  [docs/uiscript](docs/uiscript/README.md), and its reader (`UIScript.cs`)  
  reads all of it. Hudini reads a subset, the parts a game needs; its  
  examples are copied into the tests, so every document Hudini reads, VentureOS  
  reads the same way. Change the spec and the reader together, and keep  
  additions safe for a subset reader by the spec's compatibility rules.  
  The project also holds what editors  
  build on (`UIDocument`, which edits a `.uix` in place, and  
  `UIScriptSchema`, each element's attributes, which must match the reader);  
  the flat XML reader (`Xml/`); and CST string tables (`Strings/`), read by  
  [CST.NET](https://github.com/tonytins/cstdotnet) (the `CSTNet` package).  
  It references nothing but the BCL and CSTNet.
- `src/Venture.Roundhouse` is Roundhouse, VentureOS's IDE: an Avalonia app  
  for Linux, macOS, and Windows that draws with `Venture.OS.Common`. Avalonia,  
  AvaloniaEdit, its TextMate add-on, and NLog are approved for it alone; the  
  VentureOS projects never reference them. Its Run runs VentureOS's own  
  desktop code over a copy of `rootfs/` (`Simulator/`). See  
  [its design](docs/design/2026-11-18-roundhouse-design.md).
- `src/Venture.Airo` is airo, VentureOS's build tool: a command-line app  
  (`airo build`, `airo test`) that builds a Roundhouse Project (`.rproj`)  
  into the `.app` VentureOS runs, writing it where it's told, and runs the  
  app's tests, Lua files in the project's `Tests/`  
  ([their format](docs/airo/testing.md)). `src/Venture.Airo.Kit` holds  
  what airo and Roundhouse share: projects, the simulated disk, and the  
  test runner; it runs  
  on the computer making apps, never on VentureOS, so the kernel doesn't  
  reference it. System.CommandLine is approved for airo alone. See  
  [its README](docs/airo/README.md).
- `projects/` holds the built-in apps' Roundhouse Projects, where they're  
  designed and tested (`airo test projects/Settings.rproj`), and the  
  owner's other work in progress, such as `about.rproj`. A project and the  
  shipped `.app` in `rootfs/` may differ: an app ships when its project is  
  built into `rootfs/` with `airo build --replace`. See  
  [its README](projects/README.md).
- `tools/vdb/` is vdb, a Swift package (`swift build`, `swift test`,  
  `./install.sh [--rebuild]`) that sends apps to a running VentureOS over  
  [vdb/1](docs/vdb/README.md), whose recorded conversations both its tests  
  and Common's replay, and whose `vdb mcp` gives an agent VentureOS's app  
  windows over MCP. Swift Argument  
  Parser, swift-log, swift-crypto, and GitVersion are approved for it alone.  
  It moves to a repository of its own later.
- `packaging/` holds the scripts that build Roundhouse into packages for  
  Linux, Windows, and macOS, after CityLimits', writing to `dist/`; see  
  [its README](packaging/README.md). Its icon is a placeholder, made from  
  `src/Venture.Roundhouse/Assets/Roundhouse.png` by `packaging/icons.sh`.
- `tests/Venture.OS.Common.Tests` covers `Venture.OS.Common` and  
  `Venture.OS.Interface` with xUnit v3, `tests/Venture.Roundhouse.Tests`  
  drives Roundhouse off-screen with `Avalonia.Headless`, and  
  `tests/Venture.Airo.Tests` covers airo and its kit.

[docs/architecture.md](docs/architecture.md) explains how the system works,  
[docs/ROADMAP.md](docs/ROADMAP.md) tracks releases and ideas, and  
[docs/scorecard.md](docs/scorecard.md) records verified status, and  
[docs/design-language](docs/design-language/README.md), the Venture Design  
Language, records the design the desktop draws and apps follow; change it  
with the look. Read  
`docs/architecture.md` before changing boot, storage, networking, or the shell.

Never commit build output (`bin/`, `obj/`, `output-*/`, `dist/`, `*.iso`, `*.img`).

## Build and Test

```sh
dotnet test --project tests/Venture.OS.Common.Tests   # all tests must pass
dotnet test --project tests/Venture.Roundhouse.Tests  # Roundhouse's, off-screen
dotnet test --project tests/Venture.Airo.Tests        # airo's and its kit's
cosmos check                                        # always before building
cosmos build -p src/Venture.OS.Kernel               # no new warnings
./run.sh                                            # boots x64 in QEMU; --headless for serial only, --vdb for vdb's port
(cd tools/vdb && swift test)                        # vdb's, when it or the protocol changes
dotnet run --project src/Venture.Airo -- test projects/<App>.rproj  # a built-in app's own tests
```

- To try a change on the running system, `./run.sh --vdb` and send it with  
  `vdb install <App>.app --launch --for 5`. A git-ignored  
  `rootfs/system/developer.local.lua` (from `vdb identity --lua`) turns on  
  developer mode, trusts the computer, and boots to the desktop, so no one  
  types into QEMU or presses Trust.
- To use an app on the running system, use `vdb mcp`'s tools when you have  
  them, or the `ui` commands they're built on: `snapshot` to read a window's  
  controls, `act` to press, type, submit, and choose by name, `key` for  
  shortcuts, and `screenshot` to look. QEMU ignores xdotool's pointer, so  
  don't click.

- `cosmos check` also reports updates to the Cosmos CLI. Mention one to the  
  owner after the build succeeds.
- Kernel code has no unit tests; it is checked by booting. If you touch boot,  
  storage, or networking, boot it and read the console or serial log. If you  
  can't boot, say so.
- If you touch architecture-sensitive code, also build ARM64 with  
  `cosmos build -a arm64 -p src/Venture.OS.Kernel`.
- If you change `Venture.OS.Common`, add or update its tests in the same change  
  and check that a new test fails without the fix.
- VentureOS's tests, in `tests/`, cover the system, not the built-in apps.  
  They must not open a shipped app or pin what ships: an app's layout,  
  labels, or text, or the standard look (compare with `DesktopLook.Standard`,  
  or set a look of the test's own). When a system test needs an app or an  
  interface, it uses a sample of its own. Rules over everything that ships,  
  such as every interface reading and keeping its contrast, are fine.  
  `DockAppTests` is the one exception, until `airo test` has a desktop.
- A built-in app is tested on its own terms, in its project's `Tests/`  
  (`projects/<App>.rproj`), with `airo test` or Roundhouse's Run ▸ Test. If  
  you change an app's behavior, change its tests in the same change and run  
  them. Work on an app in its project; it reaches `rootfs/` only when built  
  there with `airo build --replace`, which is the owner's call, so a  
  project and its shipped `.app` may differ.

## C# and Cosmos

Follow the [.NET guidelines skill](.agents/skills/dotnet-guidelines/SKILL.md) and [.editorconfig](.editorconfig) for style, and the [Cosmos gen3 skill](.agents/skills/cosmos-gen3/SKILL.md) for the CLI and kernel APIs. Where they disagree with this file, this file wins.

## Golden Rules

### Design Process

- **Iterate**. A first design or name is a draft. Rebuild clumsy parts as understanding improves.
- **Ask when a decision is open**, and explain the reasoning behind what you suggest.
- **Keep it simple**. The simplest approach that matches what the operating system does wins.
- **Say what you're unsure of**: assumptions, unverified behaviour, and what needs testing.

### Never Crash (Outranks Feature Work)

There is no host process underneath, because VentureOS is the host: an unhandled exception stops the machine with no explanation. Stability outranks features.

- **During boot**, `VentureKernel.BeforeRun` runs each step through  
  `RunCriticalStep` or `RunOptionalStep`. A step is critical if the system  
  can't do its job without it; it returns `bool`, and a failure panics. An  
  optional step reports its failure and boot continues. When you add a step,  
  decide which it is and say so in the change.
- **After boot**, nothing may halt the system except the user's `halt`,  
  `reboot`, or `shutdown`. Commands report errors and return to the prompt.  
  `Run` catches everything as a last resort; don't rely on it in place of  
  handling errors where they happen.
- Panic only through `KernelPanic.Halt`, and only during boot.
- Validate everything that crosses a boundary: shell input, disk contents,  
  network packets, device-reported values, and sizes passed to constructors.  
  Check `ulong` sizes and offsets before narrowing to `int` or slicing.
- Report failures explicitly. Don't swallow one, substitute a silent default,  
  or print success for a skipped step.
- Bound every loop and wait that depends on hardware or the network. Don't add  
  `Thread.Sleep` to paths that don't already need it.

### Cosmos Constraints

- The kernel is compiled ahead of time with no JIT. Avoid reflection, `dynamic`,  
  runtime code generation, and BCL APIs Cosmos doesn't implement. Prefer simple  
  types, `Span<T>`, and explicit control flow.
- The Cosmos docs track the latest release. Check that an API exists in the  
  pinned version before using it, and say so when you can't confirm it.
- Put anything that doesn't need hardware (parsing, formatting, validation) in  
  `Venture.OS.Common`, or `Venture.OS.Interface` for the interface formats,  
  which build with the AOT analyzers and warnings as errors, so problems the  
  kernel couldn't compile fail there first.
- Keep both architectures building. Guard and note any x64-only code.
- Bump the `Cosmos.*` package versions only in a change of their own. New NuGet  
  dependencies need the owner's approval, except the owner's own packages  
  (published by `tonytins`, such as CSTNet), which are approved. Every  
  package must still work under Cosmos's compiler, and the kernel must boot  
  with it.

### Code

- Keep `VentureKernel.cs` thin. Put boot steps in `Startup/` and new subsystems  
  in their own folder and namespace under `Venture.OS`.
- Prefer writing a new command in Lua, as `rootfs/system/commands/<name>.lua`:  
  its first `--` comment is its summary, a `-- Usage:` comment its usage, a  
  `-- Needs:` comment the scopes it uses (`system`, `network`, `shell`, `files`; windows need none), and it gets its  
  arguments as `...`. It reaches the kernel through the `venture` table  
  (`VentureLibrary`, backed by `ISystemInfo`); extend that table when a script  
  needs more, and put each new function under a scope (`ScriptScopes`). A  
  command that takes options should use `require("venture.args")` rather than  
  reading `...` by hand. Shared Lua modules go in `rootfs/system/lib/`,  
  namespaced under `venture.`.
- Write a command in C# only when it must control the kernel, as a class in  
  `src/Venture.OS.Kernel/Shell/Commands/` registered in `VentureKernel.CreateShell`;  
  built-in commands win any name clash. `help` is generated from both. C#  
  commands write through `IShellOutput` and ask for power changes through  
  `IShellHost`.
- Apps are `.app` bundles in `rootfs/system/apps/` (apps the system  
  relies on, such as Files) or `rootfs/user/apps/` (the rest). Follow  
  [the tutorials](docs/tutorial/README.md) to write one: an agent works  
  from [the command-line tutorial](docs/tutorial/command-line.md), whose  
  steps need only files and `airo test`, and from what  
  [the desktop one](docs/tutorial/desktop.md) does by hand; its Roundhouse  
  steps are for people until Roundhouse can be driven over MCP. Keep them  
  accurate: `WritingAppsTutorialTests` builds and runs their Dice, and  
  `WritingAppsTestingTests` runs their tests on it, so change the  
  tutorials and the code together.
- Paths that cross between scripts and the system go through `FileRoot`  
  (`ISystemInfo.Root`): `FileRoot.Host` for a path a script names before it's  
  opened, and `FileRoot.Venture` for a host path a script is shown. The  
  kernel has no root, so both pass paths through; Roundhouse's Run keeps  
  scripts inside its copy with them. A new way for scripts to reach files  
  needs both, and a rooted test, as `RootedScriptTests` has.
- Anything Lua code sets up that should outlive a single run goes through a  
  `LuaOwner` and registers it's undo with `AddUndo`, so ending the owner  
  removes it.
- Words the desktop shows a person, such as menu items, window names, and  
  button labels, go in `rootfs/system/strings/_101_desktop.cst`, read  
  through `DesktopText` by key, not in C#. Messages, developers' errors,  
  and the kernel's log stay in code. Write them all in  
  [the design language's voice](docs/design-language/foundations.md#voice).
- Use `KernelLog.WriteLine` for messages a headless boot should be able to  
  check (boot results, the banner, failures). Ordinary command output goes to  
  the console only.
- Give public and internal APIs `///` doc comments that explain why, not what.  
  Put shared usings in `GlobalUsings.cs`.

## Keeping Docs Current

Describe only what works today, and keep the tree building at every step.

- [changelog.md](changelog.md): only changes a user would notice, under the  
  version the kernel reports for that commit. `GitInfo.txt`, at the  
  repository root, holds the base version, and the patch number rises with  
  every commit, docs included. When work on the  
  next milestone in a release begins, set `GitInfo.txt` to its feature band  
  (`0.4.200` for the second), in the commit that starts it, as  
  [the roadmap](docs/ROADMAP.md) lays out. Starting a new release, such as  
  `0.5.100`, needs the owner's word.
- `docs/architecture.md`: update the section, the current-state summary, and  
  the revision history when behavior, layout, or a principle changes.
- `docs/scorecard.md`: flip the rows you moved, with evidence, and update the  
  summary counts.
- `docs/ROADMAP.md`: tick a band's checklist items, move a release to its next  
  stage, and add a revision-history row. Leave scope decisions, such as what a  
  release holds or when it ships, to the owner. When a release ships, write its  
  notes in `docs/releases/`.
- [README.md](README.md) when setup or build steps change, and this file when  
  the layout or rules change.

VentureOS is a personal operating system, not a platform with outside users,  
so breaking changes are allowed: update every shipped app and document in the  
same change. Hudini's users are another matter; a change to the formats it  
shares (UIScript) crosses a version boundary rather than breaking its documents.

State breaking changes, package bumps, new dependencies, and boot-order changes  
in the summary or commit message, along with anything you couldn't verify. If  
you notice a risk outside your task, report it rather than fixing it quietly.

See [REVIEW.md](REVIEW.md) for the project's review checklist.

## Personal Configuration

Keep personal preferences and machine-specific guidance in git-ignored  
`*.local` files or directories (such as `AGENTS.local.md`), rather than in  
shared project instructions. Check for relevant local guidance before  
environment-specific work. You can create or update these files for details  
such as:

- Coding preferences
- Local build and development environment
- Personal shortcuts or workflows
