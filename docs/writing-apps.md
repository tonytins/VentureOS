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
- Apps add commands, open windows, or both. Anything an app writes to the
  RAM disk, such as `/mnt`, is gone after a reboot.
- Roundhouse, VentureOS's IDE, can start an app for you: File ▸ New App
  makes a window app with a button that counts its presses, opens it in its
  designer, and Run tries it in VentureOS without QEMU. This guide builds an
  app by hand, so you know what each part does.

## Make the bundle

An app's files live in the repository under `rootfs/system/apps/`
or `rootfs/user/apps/`. Everything there is built into VentureOS and
appears in `/mnt/system/apps` or `/mnt/user/apps` when it boots. System apps
can't be changed or deleted on the running system; user apps can. Put an app
the system relies on, such as Files, in `system/`, and anything else in
`user/`. Create this layout:

```text
rootfs/system/apps/Dice.app/
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
| `icon` | No | A PNG inside the bundle, such as `Contents/Resources/AppIcon.png`, shown in the dock and in About. Make it 64 pixels square; the dock shrinks it to 48. A full background is rounded to a tile; paint what should be clear in exactly `#00FF01` (or leave it transparent) for a shape of its own, smoothed edges and all, such as a folder, which gets a soft shadow. Without an icon, the dock shows a tile with the app's initial. |
| `description` | No | What the app is for, in a sentence, shown in its About window. |

The scopes for `needs`:

| Scope | Lets the app's code use |
|---|---|
| `shell` | `venture.command` to add commands, and `venture.shell` to run them |
| `files` | `venture.files` to list, read, and change files |
| `system` | `venture.uptime()` and `venture.memory()` |
| `network` | `venture.net()`, `venture.resolve(name)`, and `venture.ping(address)` |

Dice adds a command, so it needs `shell`. Ask for only what the app uses.
Windows need no scope: any app can open them, and any script, app or not, can
show a message with `venture.ui.alert(message, title)`, Quick Look a file with
`venture.ui.preview(path)`, or open an app with `venture.ui.launch(app, file)`.

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
file, in the [UIScript format](https://github.com/tonytins/Hudini/blob/main/docs/UISCRIPT.md),
in `Contents/Resources/`. Opening windows needs no scope, so `needs` stays
as it was. (Before 0.6, windows needed `ui`; it's still accepted, and means
nothing.)

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
  `setChecked`, `focus`, `setTitle`, `setSubtitle` (a line under the title,
  or `nil` for none), and `close`, with readers `text`,
  `value`, `items`, and `selected`. Use a colon: `window:setText(...)`.
- **The title bar:** `<ToolbarItem name="add" title="Add"/>` puts a button
  in the window's title bar, which reports `pressed` like any button, and
  `<TitleField name="address"/>` makes the title editable, as Files'
  address bar is: it reads as the title until it's clicked or given focus,
  then becomes a field that reports `submitted` on Enter. Notes uses
  toolbar items for Add and Remove. Items sit at the right end; give one
  `placement="start"` to put it after the window's buttons instead. Give
  one `image="addIcon"`, naming an `<Image>`, and it shows that image as a
  symbol in the text's color, so it follows light and dark, with its title
  as hover text and a taller bar. Draw symbols as dark shapes, with
  `#00FF01` or transparency where they're clear. `subtitle="3 notes"` on
  `<Window>` puts a smaller line under the title, as `window:setSubtitle`
  does.
- **Images:** declare one with `<Image name="logo" path="images/logo.png"/>`,
  its path beside the `.uix` file, and show it with
  `<Visual name="banner" image="logo" x="10" y="10" width="64" height="64"/>`,
  scaled to the visual's size. A `<Button>` with `image="logo"` shows the
  picture in place of a label, ringed in the accent while it's checked
  (`setChecked`), as a picker marks its choice. Images are PNG. One that
  can't be read stays a red outline, and the reason shows in a message
  window. `window:setImage("logo", path)` changes a picture while the app
  runs, from beside the document, the app's bundle, or the system's
  folder; anywhere else needs the `files` scope. It returns `true`, or
  `nil` and why.
- **Styles:** say what a color or size is for, and your app follows the
  desktop's light or dark look and accent: `textColor="secondaryText"`,
  `backgroundColor="inputBackground"`, `fontSize="title"`. A hex color such
  as `#1E5AA0` stays exactly that in every look unless you give it a dark
  twin, such as `darkTextColor="#8CB8F0"`. See
  [UIScript styles](./uiscript/styles.md) for every role.
- **Hover text:** give a control `tooltip="saveTip"`, naming a
  `<String name="saveTip" table="1" index="3"/>`, and put its text in a
  table beside your `.uix`, named `_1_<anything>.cst`, as `3 ^Save the file^`.
  It shows after the pointer rests on the control for half a second.
  Settings' color swatches do this. VentureOS reads keys on the same line
  as their text for now, and tells you about anything else in the table.
- **Menus:** add `requires="menus"` and `<Menu>` elements to the `.uix` file,
  and they appear in the menu bar while the window is in front. A chosen
  item reports `pressed` with its name, like a button. Every app already
  has File, with Close Window (Ctrl+W); Edit, with Cut, Copy, Paste, and
  Select All; and Help, with About your app, which shows its `name`,
  `version`, and `description`. A menu with `role="file"`
  (or the title `File`) adds its items to the top of File, and the same goes
  for `edit`, `view`, and `help`. Edit works on every text field and area
  with no code of yours, Undo and Redo included; setting a field's text
  yourself starts its undo history over. Don't add your own Close Window, or
  your own copy and paste. See
  [UIScript menus](./uiscript/menus.md).
- **A sidebar:** add `sidebar` to `requires` and
  `<Sidebar name="places" width="120"/>`, and the window gets a list of
  places down its left, its full height, as Files has. Fill and read it like
  a selectable list: `setItems`, `select`, `selected`, and a `selected`
  event. A row can be a table with an icon, naming an `<Image>`, and
  `{ separator = true }` puts a line between groups, as Files sets System
  apart:

  ```lua
  window:setItems("places", {
    { text = "Home", icon = "homeIcon" },
    { separator = true },
    { text = "System", icon = "driveIcon" },
  })
  ```

  Separators aren't rows, so System is row 2. Icons show in the text's
  color, as toolbar icons do; draw them as dark shapes. Only a sidebar
  takes tables; a plain list takes strings. The sidebar sits beside the
  content, so the window's size and your controls' positions don't change.
  See [UIScript sidebar](./uiscript/sidebar.md).
- **Resizing:** `resizable="true"` on `<Window>` puts a grip in the window's
  bottom-right corner. The window starts at its `width` and `height` and
  shrinks no further than `minWidth` and `minHeight` (or its starting size,
  without them). Give a control edge offsets to keep it to the window's
  edges: `edgeOffsetRight` alone pins it to the right, and `edgeOffsetLeft`
  with `edgeOffsetRight` stretches it, as Files' list does. Leave the bottom
  right 16 pixels clear for the grip.
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

## Panels and shelves

An app's interface can be a **panel**, pinned to an edge of the screen with
no title bar, as the dock is. Write `<Panel>`, with an `edge`, in place of
`<Window>`:

```xml
<UIScript version="1" requires="shelf">
  <Panel width="400" height="72" edge="bottom"/>
  <Shelf name="apps" x="0" y="0" width="400" height="72"/>
</UIScript>
```

- A panel sits above every other window, never takes the keyboard, and
  can't be moved, closed by a button, minimized, or zoomed. A bottom panel
  is as wide as its shelf and centred, and follows the dock's setting in
  Settings; a top panel spans the screen.
- The top edge holds the menu bar, Files' top panel, and there's one top
  panel at a time, so opening another is an error.
- A **shelf** (`<Shelf>`, with `requires="shelf"`) is a row of icons. Fill
  it with `window:setItems`, each item a table: `title`, and if you like an
  `icon` (a PNG, in your app or any app's bundle), `open = true` for a dot
  under it, a `group` number, with a divider between groups, and
  `window = true` for a window's pale tile. A click is a `pressed` event with
  the item's `index`.
- See [panels](uiscript/panel.md) and [the shelf](uiscript/shelf.md) for the
  rules.

An app with the `system` scope can do what the dock does through
`venture.desktop`:

| Function | Does |
|---|---|
| `venture.desktop.apps()` | Every app, Files first, then the system's and the user's: `{ name, path, icon, identifier, user, open }` each |
| `venture.desktop.open(path)` | Opens an app as the dock does: brings it forward if it's open, with a new window if it has none |
| `venture.desktop.minimized()` | Minimized windows, `{ id, title, app }` each |
| `venture.desktop.restore(id)` | Brings a minimized window back |
| `venture.desktop.onChange(fn)` | Calls `fn` when apps open, close, start, or end, or windows are minimized or restored, at most once a frame |

`Dock.app` in `rootfs/system/apps` is all of this in about fifty lines.

## Test your app

The tests in `tests/Venture.OS.Common.Tests` run on ordinary .NET, without
booting. `NotesAppTests.cs` opens the shipped `Notes.app`, runs its command,
and checks what it printed. Copy its setup for your app, then run:

```sh
dotnet test --project tests/Venture.OS.Common.Tests
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
| `there's no app called dice` | Check the folder is `Dice.app` in `rootfs/system/apps/`, and that you rebuilt. `apps` lists what VentureOS found. |
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
| `Dice crashed: its code failed 3 times within a minute, so VentureOS closed it.` | See [When an app crashes](#when-an-app-crashes). |

Ctrl+C or Esc stops a command or a `main.lua` that's running too long.

## When an app crashes

Every open app runs as a process, with an ID. `ps` lists them, and `kill`
ends one, by its ID or name, such as `kill dice`:

```
/> ps
  ID  NAME   STATE           UP  KEPT
   1  Files  running     2m 05s  yes
   2  Dice   running        12s
```

An error in one of your app's handlers, timers, or commands is shown, and
the app carries on. But when its code fails three times within a minute,
whether by an error or by running past its time, VentureOS treats the app as
crashed: it closes it, with its windows and commands, and says so. Fix the
error the messages before it point to, and open it again.

A failing `main.lua` isn't a crash: the app just doesn't open, as above.

Files is kept running: if it's closed, killed, or crashes, VentureOS starts
it again, at once, and waits longer between tries if it keeps failing. Your
apps aren't kept; once they end, they stay ended until they're opened again.
