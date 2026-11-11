# Changelog

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