# Changelog

## 0.2.21

- The desktop has windows. It opens with About VentureOS; drag a window by its title bar, click one to bring it to the front, and click its red button to close it.
- `desktop <file.uix>` opens a `.uix` interface in a window, to preview it as you write it. A file that can't be read is reported in the shell, and the desktop doesn't open.

## 0.2.20

- New `desktop` command: a blue desktop with the pointer, drawn at 1024x768. Press Esc to go back to the shell. Windows, the menu bar, and the dock come next.
- The screen is now 1024x768 in the shell too.

## 0.2.19

- VentureOS ships its first app, Notes: `open notes`, then `note <text>` to add a note, `note` to list them, `note -d <number>` to delete one, and `note --clear` to start over. Notes are kept in `notes.txt` in the folder you opened it from.
- New guide: [Writing apps](docs/writing-apps.md).
- When a script or app uses something it hasn't declared, the message now says where to declare it: a script's `-- Needs:` line, or an app's `needs` in `Info.xml`.

## 0.2.18

- Apps: `open <app>` opens an `.app` bundle, `close <app>` closes it and removes everything it added, and `apps` lists the apps and which are open. An app opened from outside the system's apps says what it needs first.

## 0.2.17

- File commands: `ls` (with `-l`), `cat`, `write` (with `-a` to add to the end), `mkdir` (with `-p`), `rm` (with `-r`), `df`, and `pwd`. `--help` explains each one's options.
- `/mnt/system`, the system's own folder, can be read but not changed, so `rm -r` there is refused.
- Lua scripts can work with files through `venture.files`, after declaring `-- Needs: files`.

## 0.2.16

- The shell has a current folder, shown in the prompt (`/mnt> `). `cd <folder>` changes it, `cd ..` goes up, and `cd` on its own goes back to `/mnt`.
- Scripts and Lua code run from the current folder, so relative paths such as `dofile('notes.lua')` start there.

## 0.2.15

- Commands made at the `lua` prompt with `venture.command` can be tried right there: a line starting with `!` runs a shell command, such as `!greet Tony`. They vanish when you `exit`.
- At the prompt, a command can stand in for one of the system's script commands, such as `echo`, until you `exit`. VentureOS says so when it happens.
- `venture.shell("line")` runs a shell command from Lua, in `init.lua` and at the prompt.

## 0.2.14

- `init.lua` can add commands to the shell with `venture.command`, after declaring `-- Needs: shell`. They last until shutdown, and if `init.lua` fails, the commands it added are removed again.
- `help <command>` shows who added a command, such as `From: init.lua`.

## 0.2.13

- VentureOS ships a Lua library in `/mnt/system/lib` that scripts load with `require`. Its first module, `venture.args`, lets a command declare its arguments and options (`--loud`, `-l`, `--times 3`) and handles the rest: clear messages for mistakes, and a generated `--help`.

## 0.2.12

- Typing `lua` on its own opens a Lua prompt, `lua>`, for trying things out. Values carry over between lines, an expression prints its value (`1 + 1` prints `2`), and an unfinished statement continues on the next line. Ctrl+C or Esc stops the line that's running, and `exit` returns to the shell.

## 0.2.11

- Lua scripts declare what they use with a `-- Needs:` line in their header, like an app's permissions: `system` for `venture.uptime()` and `venture.memory()`, and `network` for `venture.net()`. Every script can call `venture.version()`.
- Calling an undeclared function names the line that's missing, and a script that needs a scope VentureOS doesn't offer won't run. `help <command>` shows what a script needs.
- `lua` can still use everything.

## 0.2.10

- Ctrl+C or Esc stops a Lua script that's running, from `lua` or a script command, with `Interrupted.`, and returns to the prompt. This works even if the script catches errors with `pcall` or runs in a coroutine.
- `init.lua` also stops on its own after 10 seconds, so a stuck script can't keep the shell from starting.
- Lua scripts can no longer replace the hook with `debug.sethook`; it reports that it's reserved by VentureOS.

## 0.2.9

- After boot, VentureOS runs `/mnt/system/init.lua`. If it fails, the error is shown and the shell starts anyway. For now it prints the "Type help" hint, which used to be built into the kernel.

## 0.2.8

- `info` and `net` are now Lua scripts in `/mnt/system/commands`, with the same output, except that `net` shows `yes` or `no` for the link and readiness instead of `True` or `False`.
- Lua code, in `lua` and in script commands, can read the system through a `venture` table: `venture.version()`, `venture.uptime()`, `venture.memory()`, and `venture.net()`.

## 0.2.7

- Commands can be written in Lua. A file at `/mnt/system/commands/<name>.lua` runs as the command `name`, with its arguments as `...`, and shows up in `help`. Its first comment line is its description, and a `-- Usage:` comment line its usage.
- `echo` is now one of these Lua scripts, and works as before.
- Lua output, from `lua` and from script commands, now goes through the shell like every other command.
- Boot installs VentureOS's own system files to `/mnt/system`, and says so in the boot messages. They come from the kernel, so changes made to them are lost at reboot.

## 0.2.5

- New `lua` command: runs a line of Lua 5.5, such as `lua "print(6 * 7)"`. Relative paths start at `/mnt`, so `lua "dofile('script.lua')"` runs a script from the RAM disk. Lua errors are shown with a traceback, and the shell carries on.

## 0.2.2

- New `echo` command: prints its arguments on one line, so `echo "a  b" c` shows `a  b c`.

## 0.2.0

- VentureOS reaches alpha: it boots reliably, explains failures, and has a shell with arguments and commands to inspect and control the system. No other changes since 0.1.117.

## 0.1.117

- Commands now take arguments. Quotes group words (`help "net"`), and a missing quote or the wrong number of arguments is explained instead of ignored.
- `help` lists every command with a short description, and `help <command>` shows how to use one.
- New `info` command: version, build, commit, Cosmos version, uptime, and memory.
- New `net` command: the network adapter, its MAC address and link, and its IP address, subnet, and gateway.
- New `reboot` and `shutdown` commands, beside `halt`.
- Command names ignore case, so `HELP` works like `help`.

## 0.1.116

- If a part of the system it can't run without fails at boot (filesystem or timer), VentureOS now stops with a panic screen that names the failed step and the reason, instead of crashing without explanation.
- If networking fails at boot, VentureOS reports it and carries on without the network.
- Boot messages say which step failed, such as the RAM disk's format or mount step, a DHCP timeout, or a missing network card.
- A failing command reports its error and returns to the prompt instead of taking the system down.
- Boot messages, the version, and any panic report are also written to the serial log, prefixed with `[Venture]`, so a headless boot can be followed.
- The version and build are shown at the start of boot as well as when boot finishes.
- Updated to Cosmos 3.0.94.

## 0.1.102

- Filesystem
- Networking

## 0.1.101

Start of the project!