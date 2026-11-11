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
| [Boot](#boot) | 8 | 0 | 0 | 7 boot steps: 3 critical, 4 optional |
| [Storage](#storage) | 4 | 0 | 1 | 32 MiB RAM disk, FAT16 at `/mnt`; system files at `/mnt/system`, read-only to file commands |
| [Network](#network) | 2 | 0 | 2 | DHCP lease 10.0.2.15 under QEMU |
| [Shell](#shell) | 18 | 0 | 0 | 11 built-in commands, plus 10 Lua script commands; `Notes.app` ships |
| [Platform](#platform) | 4 | 0 | 1 | Cosmos 3.0.94, .NET 10; x64 ISO 10.6 MB |
| [Reliability](#reliability) | 3 | 1 | 1 | 613 tests, all passing |
| [Documentation](#documentation) | 4 | 1 | 0 | Includes a tested guide to writing apps |
| [Desktop](#desktop) | 5 | 0 | 0 | Version 0.3; opens in 48 ms at 1024x768 |
| [Desktop apps](#desktop-apps) | 2 | 0 | 3 | Keystone phase 2 of 5; Files, Notes, and Calculator have windows |
| [Distribution](#distribution) | 0 | 0 | 5 | No CI for now; tests and builds run locally |
| [Known issues](#known-issues) | | | | 2 open, 3 resolved |

Kernel-free code in `Venture.OS.Common` is covered by tests
(`dotnet test --project src/Venture.OS.Common.Tests`). Everything else is
proven by a build or a boot. Boots were run headless under the QEMU that
Cosmos bundles:

```sh
cosmos run -p src/Venture.OS --headless            # default e1000e NIC
cosmos run -p src/Venture.OS --headless --nic none # no network card
```

VentureOS's system messages reach the serial log with a `[Venture] ` prefix
(BOOT-7), so a headless boot shows each step's result and the banner. Command
output stays on the console.

## Boot

| Id | Target | Status | Evidence |
|---|---|---|---|
| BOOT-1 | The kernel boots to the shell loop on x64 | done | Headless boot on 3.0.94: `[Kernel] Entering main loop...`, `[Kernel] Calling Run()...` |
| BOOT-2 | Critical steps (RAM filesystem, timer) panic deliberately on failure or exception | done | `VentureKernel.RunCriticalStep`; `Bootstrap.BootIntoRam` and `CheckTimeManager` return `bool` |
| BOOT-3 | Optional steps (system files, network, init script) report a failure and boot continues | done | `VentureKernel.RunOptionalStep`; boot with `--nic none` reaches `Calling Run()` |
| BOOT-4 | A panic reports the version, failed step, and reason to serial and the console, then stays halted | done | `KernelPanic.Halt`: `Log` first, then `Console`, then `Power.Halt()` in a loop |
| BOOT-5 | Each boot step says which part failed | done | `BootIntoRam` reports register, format, or mount; `ConnectToNetwork` reports no device, DHCP timeout, or no address |
| BOOT-6 | The panic path is proven by a boot that triggers it | done | 2026-10-15, a temporary critical step that threw `InvalidOperationException`: the full report reached serial with the exception type name, no later step ran, `Run()` was never called, and the machine stayed halted until QEMU was stopped |
| BOOT-7 | Boot step results, the banner, failures, and panics reach the serial log | done | `KernelLog.WriteLine`; headless boot shows `[Venture]` lines for the version at start, every step, the network details, and the banner, all using `HostInfo.DisplayVersion` |
| BOOT-8 | `/mnt/system/init.lua` runs after boot, and a failure in it doesn't stop the shell | done | `Bootstrap.RunInitScript`; a test runs the shipped `init.lua`. Headless boots 2026-11-10: serial shows `[Venture] Type "help" for a list of commands.` after the banner; with a temporary `error("probe failure")` added, serial showed the Lua error, its traceback, and `The shell still works.`, and `Calling Run()` followed |

## Storage

| Id | Target | Status | Evidence |
|---|---|---|---|
| STOR-1 | A RAM disk is formatted FAT16 and mounted at `/mnt` | done | `Bootstrap.BootIntoRam`; headless boot passes the critical filesystem step |
| STOR-2 | The RAM disk validates its size and range-checks every read and write, throwing as `IBlockDevice` requires | done | `BootRamDisk` constructor and `GetByteRange` |
| STOR-3 | Files persist across boots on a real disk | missing | Only the RAM disk is mounted; nothing reads `StorageManager.Partitions` |
| STOR-4 | The shell can list, read, and write files, and show free space | done | SHELL-16 |
| SHELL-17 | `.app` bundles open and close as owners, with `open`, `close`, and `apps` | done | `AppManager`, `AppLibrary`, `OpenCommand`, `CloseCommand`, `AppsCommand`; 20 tests, and opening that fails without undoing fails one. Boot test 2026-11-11: a `Hello.app` made with `mkdir` and `write` opened by path (`Hello needs shell.`), its command ran, a second `open` was refused, `help hello` showed `From: Hello`, `apps` listed it as open, and after `close` its command was gone |
| SHELL-18 | An app ships with the system, and a tested guide shows how to write one | done | `system/apps/Notes.app`, `docs/writing-apps.md`; 16 tests open the shipped Notes and build the guide's Dice example from its code blocks. Boot test 2026-11-11: `apps` listed Notes 1.0; `open notes`, adding, listing, and deleting notes, `cat notes.txt`, and `note --help` worked; after `close notes`, `note` was gone |
| STOR-5 | The system files built into the kernel are installed to `/mnt/system` at boot | done | `Bootstrap.InstallSystemFiles`, `SystemDirectory.Install` (15 tests). Boot test 2026-11-06: serial shows `Installed 1 system file to /mnt/system.` (2 with the probe), and `lua` read `system/init.lua` back, as well as a temporary `system/commands/long-name-probe.lua` |

## Network

| Id | Target | Status | Evidence |
|---|---|---|---|
| NET-1 | DHCP configures the primary NIC and reports the address | done | Headless boot: `[DHCP CONFIG] IP configuration _applied.`, address 10.0.2.15 on `e1000e` |
| NET-2 | No NIC means network setup is skipped without touching the adapter | done | `NetworkManager.DeviceCount` guard; boot with `--nic none` reaches `Calling Run()` |
| NET-3 | DNS resolution | missing | No `DnsConfig` setup, although DHCP reports a DNS server (10.0.2.3) |
| NET-4 | Anything uses the network after boot | missing | No shell command or service touches the network |

## Shell

| Id | Target | Status | Evidence |
|---|---|---|---|
| SHELL-1 | `help`, `clear`, and `halt` work, names ignore case, and unknown commands are reported | done | `Shell/Commands/`, `CommandShell.Execute`; boot test 2026-10-15: `HELP info` worked, `nope` was reported as not a command |
| SHELL-2 | A failing command reports its error and returns to the prompt | done | `VentureKernel.Run` catches every exception and logs it to serial |
| SHELL-3 | Commands take arguments, with quoting and clear errors | done | `CommandLine.TryParse`; boot test: `help "net"` passed the quoted name, `help "open` reported the missing quote, `help a b` printed the usage |
| SHELL-4 | Commands show the version, uptime, memory, and network | done | `info` (version, commit, Cosmos version, uptime, memory) and `net` (device, MAC, link, address); boot test showed real values, such as 10.0.2.15 and 462 of 500 MiB free |
| SHELL-5 | Commands are added by registering them, and `help` is generated from the registry | done | `CommandRegistry` rejects invalid and duplicate names; `help` lists 9 commands from it; boot tests 2026-11-05: `echo` and `lua`, each added by one `Register` call, appeared in `help`, and `echo "a  b" c` printed `a  b c` |
| SHELL-6 | `reboot` and `shutdown` work | done | Boot tests in QEMU without `-no-reboot`/`-no-shutdown`: a build that ran `reboot` at boot rebooted 13 times in 60 s; one that ran `shutdown` powered QEMU off in 4 s. `cosmos run` passes both flags, so there the VM pauses instead |
| SHELL-7 | Commands can be written in Lua, as `/mnt/system/commands/<name>.lua` | done | `ScriptCommands`, `LuaRunner`, `ShellOutputWriter` (37 tests, running real Lua). Boot test 2026-11-06: `help` listed `echo` from its script, `help echo` showed the script's summary and usage, `ECHO "a  b" c` printed `a  b c`, and a `greet.lua` written through `lua` ran at once as `greet Tony` |
| SHELL-8 | Lua scripts can read the system through a `venture` table, and `info` and `net` are scripts | done | `VentureLibrary`, `KernelSystemInfo`; tests run the shipped `info.lua` and `net.lua` against a fake `ISystemInfo`. Boot tests 2026-11-06: `info` showed the version, commit, uptime, and 458 of 497 MiB free; `net` showed the e1000e at 10.0.2.15, and `No network device.` with `--nic none` |
| SHELL-9 | Ctrl+C or Esc stops running Lua, and `init.lua` stops after 10 seconds | done | `LuaRunner`, `ScriptStop`, `KeyboardStop`; 17 tests, including loops inside `pcall` and coroutines with a check that says stop only once. Boot tests 2026-11-10: Ctrl+C and Esc stopped `while true do end`, a `pcall` loop, and a loop in `coroutine.wrap`, `debug.sethook()` was refused, and the shell carried on; a headless boot with an endless `init.lua` logged `Stopped after 10 seconds.` and reached `Calling Run()` |
| SHELL-10 | Scripts reach the `venture` table only through scopes they declare with `-- Needs:` | done | `ScriptHeader`, `ScriptScopes`, `VentureLibrary`; 22 tests, and the shipped-script tests fail if `info.lua` drops its `-- Needs: system`. Boot test 2026-11-10: `help net` showed `Needs: network`; a script calling `venture.net()` without it got the scope error; one needing `files` refused to run |
| SHELL-11 | Plain `lua` opens an interactive Lua prompt | done | `LuaRepl`; 12 tests with scripted input, and removing the per-line stop reset fails one. Boot test 2026-11-10: values carried over between lines, `m.free // 1048576` printed `457`, a `for` loop continued at `...> `, Ctrl+C stopped `while true do end` and the prompt came back, and `exit` returned to the shell |
| SHELL-12 | Scripts can `require` the system library, and `venture.args` parses arguments and options | done | `system/lib/venture/args.lua`, `LuaRunner` sets `package.path`; 36 tests run the shipped module through `require`. Boot test 2026-11-10: a `greet.lua` script command using it printed `TONY` for `greet Tony -l`, explained `greet` (missing `<name>`) and `--shout` (unknown option) with the usage, and showed generated `--help`; a headless boot installed 5 system files |
| SHELL-13 | `init.lua` can register commands with `venture.command`, undone if it fails | done | `LuaOwner`, `RuntimeCommands`, `ShellLibrary`; 20 tests, including undo order and the name rules, and running undos oldest first or letting `init.lua` replace a script fails them. Boot tests 2026-11-10 with a temporary `init.lua`: `hello Tony` ran its command, `help hello` showed `From: init.lua`, replacing `echo` was refused; when it failed after registering `hello`, `hello` was gone |
| SHELL-14 | Commands made at the `lua` prompt work through `!` and `venture.shell`, and vanish on `exit` | done | `LuaRepl` as a `LuaOwner`, `ShellLibrary.Shell`; 10 tests, including a command that runs itself (stopped at 8 levels) and a nested call keeping the line's stop, which fails if the stop isn't restored. Boot test 2026-11-10: `!hi Tony` ran a prompt-made command, replacing `echo` was announced and worked, `!help hi` showed `From: the lua prompt`, and after `exit`, `hi` was gone and `echo` was the script again |
| SHELL-15 | The shell has a current folder: the prompt shows it, `cd` changes it, and Lua runs from it | done | `ShellContext.CurrentDirectory`, `ShellPaths`, `CdCommand`; 23 tests. Boot test 2026-11-10: `cd system` changed the prompt to `/mnt/system> ` and Lua opened `init.lua` from there; `cd commands/../..`, `cd /`, and `cd` worked; a missing folder and a file were refused with the reason |
| SHELL-16 | File commands on the RAM disk, and `/mnt/system` read-only to them | done | `FilesLibrary`, `system/commands/{ls,cat,write,mkdir,rm,df,pwd}.lua`; 34 tests, and dropping the system-folder check fails 5. Boot tests 2026-11-11: `mkdir -p`, `write`, `write -a`, `cat`, `ls -l`, `pwd`, and `df` worked in `/mnt/notes`; `rm -r /mnt/system` and writing into it were refused; `rm` on a full folder asked for `-r`; `df` dropped by 2 MiB for a 2 MiB file and recovered after `rm` |

## Platform

| Id | Target | Status | Evidence |
|---|---|---|---|
| PLAT-1 | Builds for x64 with no warnings | done | `cosmos build -v -p src/Venture.OS`: 0 warnings, 0 errors |
| PLAT-2 | Builds for ARM64 | done | `cosmos build -a arm64 -p src/Venture.OS`: build completed |
| PLAT-3 | Pinned to the latest Cosmos release | done | `Venture.OS.csproj`: `Cosmos.Sdk` 3.0.94; `cosmos update --check` reported nothing newer on 2026-10-15 |
| PLAT-4 | Boots on ARM64 | missing | Not booted; ARM64 needs `-a arm64` and a virtio NIC to test networking |
| PLAT-5 | Lua scripting | done | `Cosmos.Executable.Lua` 4.0.1 builds for x64 and ARM64 with no warnings. x64 boot test 2026-11-05: `lua` printed `Hello from Lua 5.5`, reported a runtime error with its traceback and `os.exit(3)` as a status, ran a script written to `/mnt` through `dofile`, and returned to the prompt each time |

## Reliability

| Id | Target | Status | Evidence |
|---|---|---|---|
| REL-1 | Nothing crashes uncontrolled: boot failures panic deliberately or degrade, and nothing crashes after boot | done | BOOT-2, BOOT-3, SHELL-2 |
| REL-2 | Input from devices and callers is validated where it enters | partial | `BootRamDisk` validates sizes and ranges; shell input is only matched against known commands, which is safe today but untested |
| REL-3 | `BuildNumber` can't throw | done | `HostInfo.BuildNumber` returns 0 when the commit hash is shorter than four bytes |
| REL-4 | A test project covers logic that doesn't need the kernel | done | `Venture.OS.Common.Tests`: 59 xUnit v3 tests for `CommandLine`, `CommandRegistry`, and `CommandShell`, all passing on 2026-10-15. Breaking single-quote parsing on purpose failed 7 of them |
| REL-5 | Boot tests run automatically and check the serial log | missing | Boots are run by hand |

## Documentation

| Id | Target | Status | Evidence |
|---|---|---|---|
| DOC-1 | An agent and contributor guide matches the code | done | `AGENTS.md`, reviewed 2026-10-15 |
| DOC-2 | Coding guidance for C# and for Cosmos | done | `.agents/skills/dotnet-guidelines`, `.agents/skills/cosmos-gen3` |
| DOC-3 | Every declaration has a documentation comment where its name isn't enough | partial | `KernelPanic`, `BootRamDisk`, the boot steps, and `BuildNumber` have them; `VentureKernel.OnBoot` doesn't |
| DOC-4 | An architecture overview in `docs/` | done | `docs/architecture.md`, written 2026-10-15 |
| DOC-5 | A guide to writing apps, whose examples are tested | done | `docs/writing-apps.md`; `WritingAppsTutorialTests` builds and runs its Dice app from the guide's code blocks (2026-11-11) |

## Desktop

| Id | Target | Status | Evidence |
|---|---|---|---|
| DESK-1 | `.uix` (UIScript) documents are read with Hudini's rules, plus the `menus` feature | done | `Interface/UIScript.cs`; 79 tests, including Hudini's five examples; removing the button-only image rule fails one. A boot probe 2026-11-11 read a document with menus, a fractional window size, a style, a slider binding, and an unknown attribute, and refused a control without a size |
| DESK-2 | `desktop` switches to graphics and back | done | `DesktopCommand`, `CanvasPainter`, `DesktopSession`, `DesktopBackground`, `Pointer`; 9 tests against a recording painter. Boot test 2026-11-11: the sky, glow, and an Open Sans hint at 1024x768, opened in 85 ms; the PS/2 pointer moved, ten moves gave ten redraws and none while still; Esc returned to a working shell |
| DESK-3 | Tiger-style windows show `.uix` interfaces and handle input | done | `UIView`, `DesktopWindow`, `WindowManager`, `about.uix`; tests for placement, drawing, events, focus, dragging, closing, and the shipped interfaces. Boot test 2026-11-11: `desktop t.uix` opened About VentureOS and the preview in 72 ms with shadows and the inactive fade; a click focused the text field and typing showed; the title bar dragged; a click raised About; OK and the close button closed their windows; Esc returned to the shell; a missing file was reported in the shell |
| DESK-4 | A global menu bar and a dock | done | `MenuBar`, `Dock`, `DesktopSession`, `HeldOutput`, `DesktopAlert`; tests for clicking, dragging, the keyboard, shortcuts, disabled and checked items, the dock, opening and quitting an app, and held output. Boot test 2026-11-11: the VentureOS menu opened by click, the arrows moved its highlight, and Esc closed it; a preview's File and Edit menus opened with Alt+F and Right; clicking Notes in the dock opened it with its message in a window, a dot, and a bold Notes menu; Ctrl+Q quit it; Ctrl+Shift+Q returned to the shell, where `note` worked for an app opened from the dock; a document with a bad shortcut was reported in the shell |
| DESK-5 | Apps open windows through `venture.ui`, and quit when their last one closes | done | `UiLibrary`, `DesktopSession`, `Notes.app`; tests for every method, events, mistakes, the scope, quitting, the last-window rule, windows outlasting the desktop, Notes' window, and the guide's window step. Boot test 2026-11-11: `open notes` and `note buy milk` in the shell, then `desktop` showed Notes' window with the note; typing and Enter added and selected a second note; Alt+N opened the Note menu; Ctrl+D deleted the selection; the close button quit Notes, removing its dot and `note`, and `notes.txt` kept the rest |

## Desktop apps

| Id | Target | Status | Evidence |
|---|---|---|---|
| APP-1 | Files is the desktop's fallback app: always running, it browses folders, opens apps, previews `.uix` files, and opens text files | done | `Files.app`, `QuickLook`, `DesktopSession.FallbackApp`, `AppWindows`; tests for Quick Look's kinds, Files' browsing, Quick Look, launching, and new windows, and the fallback's dock place, menus, last-window exemption, and restarts with growing waits. Boot test 2026-11-11: `desktop` opened Files on `/mnt`; Quick Look showed a text file; Ctrl+O opened `system` and `apps`, and launched Notes; closing Files' window left it running, and its dock icon opened a new one; after `close files`, the next `desktop` started it again |
| APP-2 | User apps ship in `/mnt/user/apps`, which the user can change, and say what they need when they open; Calculator is one | done | `SystemFiles.User`, `Bootstrap.InstallUserFiles`, `AppManager` (two folders), `Dock` (divider), `Calculator.app`; tests for the listing order, name lookup, the needs line, the dock, and 19 calculations. Boot test 2026-11-11: 3 user files installed to `/mnt/user`; `apps` marked Calculator `user`; the dock showed it past a divider; clicking it opened it with `Calculator needs ui.`; 2 + 3 × 4 = showed 20; `rm -r` removed it from `/mnt/user/apps` but was refused for `/mnt/system/apps/Files.app` |
| APP-3 | Windows can run code on a timer, and System Monitor shows the system as it changes | missing | Keystone phase 3 |
| APP-4 | `.uix` interfaces have a multi-line `<TextArea>`, and TextEdit edits and saves text files | missing | Keystone phase 4 |
| APP-5 | Terminal.app runs the shell in a window | missing | Keystone phase 5 |

## Distribution

| Id | Target | Status | Evidence |
|---|---|---|---|
| DIST-1 | CI builds x64 on every push and pull request | missing | The workflow was removed on 2026-11-11 (owner decision): the kernel doesn't build like a typical .NET project |
| DIST-5 | CI runs the tests on every push and pull request | missing | Removed with the workflow; the tests run locally |
| DIST-2 | CI also builds ARM64 | missing | No CI |
| DIST-3 | CI boots the kernel headless and checks it reaches the shell | missing | |
| DIST-4 | Release ISOs are published | missing | No release workflow |

## Known issues

| Id | Issue | Status | Note |
|---|---|---|---|
| BUG-1 | Boot waits 0.5 s before and after each step, about 2 s in all | resolved | Kept deliberately (owner decision, 2026-10-15): boot is otherwise too fast to read. Documented at `VentureKernel.BootStepDelay` |
| BUG-2 | VentureOS's console output doesn't reach the serial log | resolved | System messages go through `KernelLog` to serial and the console (BOOT-7); command output stays console-only by design |
| BUG-3 | `DhcpClient.SendDiscoverPacket()` returning `-1` for a timeout was an assumption | resolved | Confirmed by the Cosmos network docs |
| BUG-4 | A boot occasionally faults inside Cosmos's APIC setup, before any VentureOS code runs | open | Seen twice in about 40 x64 boots under QEMU on 2026-11-06 and 2026-11-10 (Cosmos 3.0.94): `[INT] FATAL: Exception 13` right after `[LegacyPIC] Remapping and disabling 8259 PIC...`. Not reproduced on demand: 8 of 8 boots of the same build succeeded straight after. Outside VentureOS's control; worth reporting to Cosmos if it recurs |
| BUG-5 | Non-ASCII text is garbled in the serial log | open | Seen 2026-11-11: a boot probe logged `café` as `caf�` through `KernelLog`, though the text itself was right. Likely how characters reach the serial port; console output is unaffected. Only matters for reading headless logs |
