# Writing apps for VentureOS

An app is a folder ending in `.app` that VentureOS opens and closes as one
thing. While it's open, it can add commands to the shell. When it closes,
everything it added goes away. Apps are written in Lua and run unchanged on
x64 and ARM64.

This guide builds **Dice**, an app that adds a `roll` command, then shows how
to give an app access to files, how to test it, and what to do when it won't
open. The app that ships with VentureOS, `Notes.app`, is a longer example in
the same shape.

**Before you start**

- Build and run VentureOS as the [README](../README.md) describes. You'll
  use `./run.sh` to try your app.
- Apps are text-only: they add commands. Anything an app writes to the RAM
  disk, such as `/mnt`, is gone after a reboot.

## Make the bundle

An app's files live in the repository under `src/Venture.OS/system/apps/`
or `src/Venture.OS/user/apps/`. Everything there is built into VentureOS and
appears in `/mnt/system/apps` or `/mnt/user/apps` when it boots. System apps
can't be changed or deleted on the running system; user apps can. Put an app
the system relies on, such as Files, in `system/`, and anything else in
`user/`. Create this layout:

```text
src/Venture.OS/system/apps/Dice.app/
└── Contents/
    ├── Info.xml
    └── Lua/
        └── main.lua
```

The folder name, `Dice.app`, is how `open dice` finds it.

## Describe the app in Info.xml

`Contents/Info.xml` says who the app is and what it's allowed to reach:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<App version="1">
  <Info identifier="dev.example.dice" name="Dice" version="1.0" needs="shell"/>
</App>
```

| Attribute | Required | What it's for |
|---|---|---|
| `identifier` | Yes | A reverse-DNS name no other app uses, such as `dev.example.dice`. VentureOS uses it to tell apps apart. |
| `name` | Yes | The name people see, in `apps` and in `help`. `open` also finds the app by it. |
| `version` | No | Your app's own version, shown by `apps`. |
| `needs` | No | The scopes the app's code may use, separated by spaces. Without one, the code can only call `venture.version()`. |
| `main` | No | The script to run when the app opens. Defaults to `Contents/Lua/main.lua`. |
| `icon` | No | An image inside the bundle, such as `Contents/Resources/AppIcon.png`. |

The scopes for `needs`:

| Scope | Lets the app's code use |
|---|---|
| `shell` | `venture.command` to add commands, and `venture.shell` to run them |
| `files` | `venture.files` to list, read, and change files |
| `system` | `venture.uptime()` and `venture.memory()` |
| `network` | `venture.net()` |
| `ui` | `venture.ui` to open windows on the desktop |

Dice adds a command, so it needs `shell`. Ask for only what the app uses.

## Add a command in main.lua

`main.lua` runs once, when the app opens. Its job is to set the app up, which
for most apps means registering commands with `venture.command`. Put this in
`Contents/Lua/main.lua`:

```lua
local args = require("venture.args")

local spec = {
  name = "roll",
  summary = "Roll dice and add them up.",
  arguments = { { name = "count", optional = true } },
  options = {
    sides = { short = "s", type = "number", default = 6, summary = "How many sides each die has." },
  },
}

local function roll(...)
  local options = args.parse({...}, spec)
  if not options then return end

  local count = tonumber(options.count or "1")
  if not count or count < 1 or count > 20 or count % 1 ~= 0 then
    print("roll: the count must be a whole number from 1 to 20.")
    return
  end
  if options.sides < 2 or options.sides % 1 ~= 0 then
    print("roll: a die needs a whole number of sides, 2 or more.")
    return
  end

  local rolls, total = {}, 0
  for index = 1, count do
    rolls[index] = math.random(options.sides)
    total = total + rolls[index]
  end
  print(table.concat(rolls, " + ") .. " = " .. total)
end

venture.command {
  name = "roll",
  summary = "Roll dice and add them up.",
  usage = "roll [count] [-s <sides>]",
  run = roll,
}
```

- `venture.command` adds `roll` to the shell. `run` gets the words typed after
  the command's name, as text.
- `summary` and `usage` are what `help roll` shows.
- `venture.args` reads those words: it handles `-s 20`, `--sides 20`, and
  `--help`, and explains mistakes, such as `--sides` without a number. When
  it returns `nil`, it has already told the user what went wrong.

Command names use lowercase letters, digits, and hyphens.

## Open it and try it

Build and boot with `./run.sh`, then:

```text
/mnt> open dice
/mnt> roll 3
4 + 1 + 6 = 11
/mnt> roll 2 -s 20
17 + 3 = 20
/mnt> help roll
Roll dice and add them up.
Usage: roll [count] [-s <sides>]
From: Dice
/mnt> close dice
```

Your numbers will differ. After `close dice`, `roll` is no longer a command.
`apps` lists every app and marks the ones that are open.

Dice also appears in the desktop's dock. Type `desktop` and click its icon to
open it; anything it prints when it opens shows in a window, and Dice ▸ Quit
Dice (Ctrl+Q) closes it. Ctrl+Shift+Q goes back to the shell, where `roll`
still works until you close Dice.

To change the app, edit its files, then build and boot again.

## Keep things in files

To keep each roll in a file, add `files` to `needs` in `Info.xml`:

```xml
  <Info identifier="dev.example.dice" name="Dice" version="1.0" needs="shell files"/>
```

Then write through `venture.files` at the end of `roll`:

```lua
  local ok, problem = venture.files.write("rolls.txt", total .. "\n", true)
  if not ok then print("roll: " .. problem) end
```

- Paths start from the shell's current folder, as they do for commands typed
  at the prompt. To keep using one file after a `cd`, work out the full path
  when the app opens: `local path = venture.files.resolve("rolls.txt")` at the
  top of `main.lua`, as `Notes.app` does.
- `venture.files` returns `nil` and a message when something fails, such as a
  missing folder, so print the message and carry on.
- Apps can read `/mnt/system`, but can't change anything in it.

The full list of functions is under **Files** in the
[architecture overview](./architecture.md).

## Use files from the bundle

Put images, interfaces, and other files in `Contents/Resources/`, and get the
full path to one with `venture.app.resource`:

```lua
local text = venture.files.read(venture.app.resource("help.txt"))
```

Reading it still needs the `files` scope. `venture.app` also gives `name`,
`identifier`, `version`, and `path` (the bundle folder).

To split the app's code into several files, put modules in `Contents/Lua/`.
`require("dice-rules")` loads `Contents/Lua/dice-rules.lua`, and
`require("venture.args")` still finds the system library.

## Give the app a window

An app can open windows on the desktop. Each window's layout is a `.uix`
file, in the [UIScript format](https://github.com/tonytins/Hudini/blob/main/doc/UISCRIPT.md),
in `Contents/Resources/`. Add `ui` to `needs` in `Info.xml`:

```xml
  <Info identifier="dev.example.dice" name="Dice" version="1.0" needs="shell ui"/>
```

Save this as `Contents/Resources/main.uix`:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<UIScript version="1">
  <Window width="240" height="100"/>
  <Text name="result" x="12" y="12" width="216" height="40" alignment="center" text="Press Roll."/>
  <Button name="roll" x="84" y="62" width="72" height="26" text="Roll"/>
</UIScript>
```

Then open it at the end of `main.lua`, and handle the button:

```lua
local window = venture.ui.open("main.uix")

window:on("roll", function()
  window:setText("result", "You rolled " .. math.random(1, 6) .. ".")
end)
```

Open Dice, type `desktop`, and its window is there; clicking **Roll** shows a
new number. `roll` still works in the shell, since both live in the same app.

- **Closing the window quits Dice.** An app quits when the last window it
  opened closes, and quitting closes its windows and removes its commands.
- **Events** call the function you gave `window:on` for that control, with a
  table: `kind` (`pressed`, `submitted`, `changed`, or `selected`),
  `control`, and, where they apply, `text`, `value`, and `index` (from 1). A
  text field reports `submitted` when you press Enter, and a selectable list
  when an item is double-clicked, after `selected` for the first click.
- **Changing the window:** `setText`, `setValue`, `setItems` (a list's
  items), `select` (a list's selection), `setDisabled`, `setHidden`,
  `setChecked`, `focus`, `setTitle`, and `close`, with readers `text`,
  `value`, `items`, and `selected`. Use a colon: `window:setText(...)`.
- **The title bar:** `<ToolbarItem name="add" title="Add"/>` puts a button
  in the window's title bar, which reports `pressed` like any button, and
  `<TitleField name="address"/>` makes the title editable, as Files'
  address bar is: it reads as the title until it's clicked or given focus,
  then becomes a field that reports `submitted` on Enter. Notes uses
  toolbar items for Add and Remove.
- **Menus:** add `requires="menus"` and `<Menu>` elements to the `.uix` file,
  and they appear in the menu bar while the window is in front. A chosen
  item reports `pressed` with its name, like a button. See
  [UIScript menus](./uiscript-menus.md).
- Anything a handler prints shows in a message window on the desktop.
- **A shell of its own:** `local shell = venture.openShell()` (with `shell` in
  `needs`), then `shell:run("ls")` returns what the command printed, and
  `shell:directory()` its current folder. Terminal is built on it.
- **Longer text:** `<TextArea>` is a text field with several lines: Enter
  adds a line, and it reports `changed` as you type. TextEdit uses one.
- **Opening files:** `venture.ui.onOpenFile(function(path) ... end)` lets
  Files hand your app a file, through `venture.ui.launch("YourApp", path)`.
  If your app opens no window when it starts, also give it
  `venture.ui.onNewWindow`, which the dock uses to open one.
- **Timers:** `window:every(1, refresh)` runs `refresh` every second while
  the desktop shows, until the window closes or the app quits; keep the
  timer it returns and call `timer:stop()` to stop sooner. Intervals start
  at 0.1 seconds. System Monitor uses one.
- **More:** `venture.ui.preview(path)` opens a file in Quick Look,
  `venture.ui.launch(path)` opens another app, and
  `venture.ui.onNewWindow(fn)` adds New Window (Ctrl+N) to your app's menu;
  the dock calls it too when your app has no window open. `Files.app` uses
  all three.

`Notes.app` has a fuller window: a text field, a list, and a menu.

## Test your app

The tests in `src/Venture.OS.Common.Tests` run on ordinary .NET, without
booting. `NotesAppTests.cs` opens the shipped `Notes.app`, runs its command,
and checks what it printed. Copy its setup for your app, then run:

```sh
dotnet test --project src/Venture.OS.Common.Tests
```

Test the cases a user can get wrong too, such as `roll 0` or `roll -s 1`.

## Open an app from somewhere else

`open` also takes the path to an `.app` anywhere, such as `open ./Dice.app`.
An app opened this way from the shell says what it needs before it starts,
such as `Dice needs shell.`, because it isn't in either apps folder. The
desktop doesn't show this; the scopes are enforced either way.

## When an app won't open

`open` explains what's wrong, and the app doesn't open:

| Message | What to do |
|---|---|
| `there's no app called dice` | Check the folder is `Dice.app` in `src/Venture.OS/system/apps/`, and that you rebuilt. `apps` lists what VentureOS found. |
| `Dice.app has no Contents/Info.xml, so it isn't a bundle` | Put `Info.xml` inside `Contents/`, not next to it. |
| `Dice.app's Contents/Info.xml, line 3: ...` | The file isn't valid. Text between elements, a missing quote, or a `<!DOCTYPE>` all stop it. Each element holds attributes only. |
| `"dice" isn't a reverse-DNS identifier` | Use at least two parts separated by dots, such as `dev.example.dice`. |
| `Dice.app has no Contents/Lua/main.lua to run` | Add `main.lua`, or point `main` at your script. |
| `Lua error: ...`, then `Dice didn't open` | `main.lua` stopped with an error. The message gives the line. Anything it had set up is removed. |
| `Lua error: Stopped after 10 seconds.` | `main.lua` took too long. Set the app up in `main.lua` and do the work in commands. |
| `Dice is already open.` | An app opens once. `close dice` first to open it again. |

And when it opens but something's off:

| Message | What to do |
|---|---|
| `venture.command needs the shell scope: ...` | Add the scope to `needs` in `Info.xml`. Each `venture` function names the scope it needs. |
| `main.uix has no control or menu item called "reslut"` | The name in `window:setText` or `window:on` doesn't match the `.uix` file. Names are case-sensitive. |
| `venture.ui.open: main.uix: Line 4: ...` | The `.uix` file isn't valid; the message says where. |
| `"echo" is a system command, and Dice can't replace it.` | Pick another name. Apps can't take over the system's commands. |
| `"roll" is already registered by Games.` | Another open app uses the name. Close it, or pick another name. |
| `close: Dice can't close while its own code is running.` | An app's command tried to close the app. Close it from the prompt. |

Ctrl+C or Esc stops a command or a `main.lua` that's running too long.
