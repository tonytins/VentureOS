# Project Guidelines

## Project structure

VentureOS is a hobby operating system written in C# on the
[Cosmos](https://github.com/CosmosOS/Cosmos) gen3 framework (`Cosmos.Sdk`
3.0.94, .NET 10). Cosmos compiles the kernel ahead of time to an ELF, which
Limine boots from an ISO. It targets x64 and ARM64 and runs in QEMU. Write the
name as **VentureOS** in prose; the project is `Venture.OS`, the assembly is
`Venture`, and namespaces start with `Venture.OS`.

Every .NET project lives in its own folder under `src/` and is listed in
`src/Venture.slnx`:

- `src/Venture.OS` is the kernel. `Kernel/` holds the entry point
  (`VentureKernel`), `KernelPanic`, and `KernelLog`; `Startup/` holds the boot
  steps and the RAM disk; `Shell/Commands/` holds one class per shell command.
  `system/` holds Lua system files, which the build turns into C# and boot
  installs to `/mnt/system`. `CosmosKernelClass` and the package versions are
  in `Venture.OS.csproj`.
- `src/Venture.OS.Common` is kernel-free code: the shell framework
  (`CommandLine`, `CommandRegistry`, `CommandShell`, and its interfaces) and
  Lua system support (`Scripting/`). It must never reference the Cosmos
  kernel; `Cosmos.Executable.Lua` is allowed, since it runs on ordinary .NET.
- `src/Venture.OS.Common.Tests` covers `Venture.OS.Common` with xUnit v3.

[docs/architecture.md](docs/architecture.md) explains how the system works,
[docs/ROADMAP.md](docs/ROADMAP.md) tracks milestones, and
[docs/scorecard.md](docs/scorecard.md) records verified status. Read
`docs/architecture.md` before changing boot, storage, networking, or the shell.

Never commit build output (`bin/`, `obj/`, `output-*/`, `*.iso`, `*.img`).

## Build and test

```sh
dotnet test --project src/Venture.OS.Common.Tests   # all tests must pass
cosmos check                                        # always before building
cosmos build -p src/Venture.OS                      # no new warnings
./run.sh                                            # boots x64 in QEMU; --headless for serial only
```

- `cosmos check` also reports updates to the Cosmos CLI. Mention one to the
  owner after the build succeeds.
- Kernel code has no unit tests; it is checked by booting. If you touch boot,
  storage, or networking, boot it and read the console or serial log. If you
  can't boot, say so.
- If you touch architecture-sensitive code, also build ARM64 with
  `cosmos build -a arm64 -p src/Venture.OS`.
- If you change `Venture.OS.Common`, add or update its tests in the same change
  and check that a new test fails without the fix.

## C# and Cosmos

Follow the [.NET guidelines skill](.agents/skills/dotnet-guidelines/SKILL.md)
and [.editorconfig](.editorconfig) for style, and the
[Cosmos gen3 skill](.agents/skills/cosmos-gen3/SKILL.md) for the CLI and kernel
APIs. Where they disagree with this file, this file wins.

### Never crash

There is no host process underneath: an unhandled exception stops the machine
with no explanation. Stability outranks features.

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

### Cosmos constraints

- The kernel is compiled ahead of time with no JIT. Avoid reflection, `dynamic`,
  runtime code generation, and BCL APIs Cosmos doesn't implement. Prefer simple
  types, `Span<T>`, and explicit control flow.
- The Cosmos docs track the latest release. Check that an API exists in the
  pinned version before using it, and say so when you can't confirm it.
- Put anything that doesn't need hardware (parsing, formatting, validation) in
  `Venture.OS.Common`, which builds with the AOT analyzers and warnings as
  errors, so problems the kernel couldn't compile fail there first.
- Keep both architectures building. Guard and note any x64-only code.
- Bump the `Cosmos.*` package versions only in a change of their own. New NuGet
  dependencies need the owner's approval and must work under Cosmos's compiler.

### Code

- Keep `VentureKernel.cs` thin. Put boot steps in `Startup/` and new subsystems
  in their own folder and namespace under `Venture.OS`.
- To add a command, write a class in `src/Venture.OS/Shell/Commands/` and
  register it in `VentureKernel.CreateShell`. `help` is generated from the
  registry. Commands write through `IShellOutput` and ask for power changes
  through `IShellHost`.
- Use `KernelLog.WriteLine` for messages a headless boot should be able to
  check (boot results, the banner, failures). Ordinary command output goes to
  the console only.
- Give public and internal APIs `///` doc comments that explain why, not what.
  Put shared usings in `GlobalUsings.cs`.

## Keeping docs current

Describe only what works today, and keep the tree building at every step.

- [changelog.md](changelog.md): only changes a user would notice, under the
  version the kernel reports for that commit. `GitInfo.txt` holds the base
  version, and the patch number rises with each commit that touches
  `src/Venture.OS/`, so the changelog is expected to lag. Don't edit
  `GitInfo.txt` unless asked to cut a version.
- `docs/architecture.md`: update the section, the current-state summary, and
  the revision history when behavior, layout, or a principle changes.
- `docs/scorecard.md`: flip the rows you moved, with evidence, and update the
  summary counts.
- `docs/ROADMAP.md`: tick checklist items, update the milestone's progress and
  estimate, and add a revision-history row. Leave scope decisions to the owner.
- [README.md](README.md) when setup or build steps change, and this file when
  the layout or rules change.

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
