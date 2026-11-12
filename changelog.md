# Changelog

## 0.3.12

- The desktop has a dark appearance, and any accent color: `appearance dark`, `appearance light`, or `appearance --accent "#E8922A"` in the shell or Terminal. Windows, the sky, the menu bar, and the dock change at once. The choice lasts until reboot.
- Scripts: `venture.appearance()` and `venture.setAppearance(mode, accent)`, with `system` in `-- Needs:`.

## 0.3.11

- The desktop shows what the pointer rests on: the close and minimize buttons show their × and −, a window's address turns blue, buttons lighten, and sidebar places are shaded.
- Files and TextEdit windows can be resized: drag the dots in the bottom-right corner.
- App authors: `resizable="true"` on `<Window>` makes a window resizable, no smaller than its `minWidth` and `minHeight`; controls follow the edges their edge offsets name.

## 0.3.10

- Files has a sidebar: Home, User, Apps, and System run down the window's left, its full height, with the close and minimize buttons on top.
- App authors: `<Sidebar>`, with `requires="sidebar"`, gives a window a sidebar of its own.

## 0.3.9

- Window titles sit in the middle of the title bar.
- Files' address reads as the window's title until you click it or press Ctrl+L, then becomes a field. Press Enter to go there; the Go button is gone. A long path keeps its end in view.

## 0.3.8

- New app: Terminal, the shell in a window, in Fira Code. Each window has its own current folder; `exit` closes it, and Shell ▸ Clear (Ctrl+K) empties it. `desktop` and the interactive `lua` prompt only work on the text console, and say so.
- A window gets its typing back where it was when you bring it forward again.

## 0.3.7

- New app: TextEdit, in your apps. Type a path in its title bar and press Enter to open a file, or Save (Ctrl+S) to create one.
- Double-clicking a text file in Files opens it in TextEdit.
- App authors: `<TextArea>` is a multi-line text field, and `venture.ui.launch(app, file)` with `venture.ui.onOpenFile` hands a file to an app.

## 0.3.5

- New app: System Monitor shows the version, how long the system has been up, memory in use, and the network, updated every second.
- App authors: `window:every(seconds, fn)` runs code on a schedule while the desktop shows.

## 0.3.4

- Windows have a new title bar: close and minimize on the left, the title, and the window's own buttons on the right. Minimize sends a window to the dock; click its tile, or its app's icon, to bring it back.
- Files has an address bar: type a folder and press Enter or Go. Bookmarks down the left go to Home, User, Apps, and System.
- Notes moved to your apps, in `/mnt/user/apps`. Add and Remove are in its title bar.
- Apps no longer say what they need when they open from the desktop or from your apps.

## 0.3.3

- New app: Calculator, for adding, subtracting, multiplying, and dividing.
- Your own apps live in `/mnt/user/apps`, next to the system's in `/mnt/system/apps`. Unlike system apps, you can change or delete them, and they say what they need each time they open. `apps` shows which is which, and the dock puts yours after a divider.

## 0.3.2

- Double-click a folder, app, or file in Files to open it.
- App authors: double-clicking an item in a selectable list reports `submitted`.

## 0.3.1

- New app: Files. The desktop opens with a Files window. Select something and press Open (Ctrl+O): a folder opens in the window, an app starts, and anything else shows in Quick Look (Ctrl+Y). Go ▸ Enclosing Folder (Ctrl+U) goes back up.
- Quick Look shows a file without opening it: text, an interface (`.uix`), an app's details, or a file's size.
- Files is always there. Closing its last window leaves it running; click it in the dock, or choose Files ▸ New Window (Ctrl+N), for another. If it stops, the desktop starts it again.
- About VentureOS is now in the VentureOS menu, rather than opening with the desktop.

## 0.3.0

VentureOS has a desktop. Type `desktop` at the prompt to open it, and choose VentureOS ▸ Return to Shell (Ctrl+Shift+Q) to come back.

- A Tiger-style desktop at 1024x768: a menu bar, a dock with the system's apps, and windows you can drag, bring to the front, and close.
- Apps open from the dock and can have windows. Notes has one, with a Note menu; closing an app's last window quits it.
- Interfaces are `.uix` files in Hudini's UIScript format, with menus. `desktop <file.uix>` previews one.
- App authors: `venture.ui` and the `ui` scope; see [Writing apps](docs/writing-apps.md).

The 0.2.20 to 0.2.23 entries below describe each step.

## 0.2.23

- Apps can have windows. Notes opens one: type a note and press Enter to add it, select one and press Delete (or Ctrl+D) to remove it, and use the Note menu. The `note` command still works, on the same notes.
- Closing an app's last window quits the app.
- An app's windows stay open when you leave the desktop and come back, and an app opened with `open` shows its window the next time you type `desktop`.
- App authors: `venture.ui` opens windows from `.uix` files, under the new `ui` scope. See [Writing apps](docs/writing-apps.md).

## 0.2.22

- The desktop has a menu bar and a dock. The dock shows the system's apps: click one to open it, and a dot marks the open ones. The app's menu has Quit (Ctrl+Q).
- An interface's own menus appear in the menu bar while its window is in front. Alt and the underlined letter open a menu; shortcuts such as Ctrl+N work from anywhere.
- To leave the desktop, choose VentureOS ▸ Return to Shell, or press Ctrl+Shift+Q. Esc no longer leaves; it closes an open menu.
- Messages from an app you open in the desktop show in a window.

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