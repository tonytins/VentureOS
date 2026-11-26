# A command-line app

This tutorial builds **Dice**, an app that adds a `roll` command to the
shell, by hand: you write each file, open the app on VentureOS, keep its
rolls in a file, and test it with `airo test`. [A desktop app](./desktop.md)
then gives the same Dice a window.

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
| `dock` | No | `shown`, the default, or `hidden` for an app that stays out of the dock, such as one that lives in the menu bar or runs in the background. `venture.ui.setShownInDock(shown)` changes it while the app runs. |

An app that should open with the desktop can ask the person, with
`venture.ui.askToOpenAtStartup(function(added) ... end)`: VentureOS asks in an alert only the person can answer, and
the function hears whether they said yes. It returns `true` at once, asking nothing, when the app is already on the
list. The person can also add or remove apps in Settings. An app that opens at startup and quits, is closed, or
crashes stays ended until it's opened again.

The scopes for `needs`:

| Scope | Lets the app's code use |
|---|---|
| `shell` | `venture.command` to add commands, and `venture.shell` to run them |
| `files` | `venture.files` to list, read, and change files |
| `system` | `venture.uptime()` and `venture.memory()` |
| `network` | `venture.net()`, `venture.resolve(name)`, and `venture.ping(address)` |

Dice adds a command, so it needs `shell`. Ask for only what the app uses.
Windows need no scope: any app can open them, and any script, app or not, can
show a message with `venture.ui.alert(message, title)`, post a toast with
`venture.ui.toast(text)`, Quick Look a file with `venture.ui.preview(path)`, or
open an app with `venture.ui.launch(app, file)`. An alert waits for OK; a toast
is a heads-up that slides away on its own and stays in the history.

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
- Its messages follow [the design language's voice](../design-language/foundations.md#voice):
  they say what's wrong, and what's allowed, in a few words.

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

To change the app, edit its files, then build and boot again. If it won't
open, [Troubleshooting](./troubleshooting.md) lists what VentureOS says and
what to do.

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
[architecture overview](../architecture.md).

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

## Test your app

An app's tests are Lua, kept in a Roundhouse Project beside the app, and use
it the way a person would. A project holds the app's `Contents/` as `App/`,
and its tests in `Tests/`. Make one by hand, with the app's files copied
into `App/`:

```text
Dice.rproj/
├── Project.xml     <Project version="1"><Info name="Dice"/></Project>
├── App/            what was Dice.app/Contents
└── Tests/
    └── roll.lua
```

Save this as `Tests/roll.lua`:

```lua
test.case("The roll command adds up its dice", function(app)
  local a, b, c, total = app:command("roll 3")[1]:match("^(%d) %+ (%d) %+ (%d) = (%d+)$")
  test.expect(a, "roll 3 shows three dice and their total")
  test.equal(tonumber(total), a + b + c)
end)

test.case("A count out of range says why", function(app)
  test.equal(app:command("roll 0")[1], "roll: the count must be a whole number from 1 to 20.")
end)
```

Then run them:

```sh
dotnet run --project src/Venture.Airo -- test Dice.rproj
```

airo prints a line for each case and the counts, and exits with 1 when any
failed. Each case starts on a fresh VentureOS with the app open, so one
case's commands and files don't reach the next. Test the cases a user can
get wrong, such as `roll 0` above. [Testing an app](../airo/testing.md) has
every call a test can make. When the app's ready, `airo build Dice.rproj
--output rootfs/user/apps` puts it where VentureOS is built from.

## Open an app from somewhere else

`open` also takes the path to an `.app` anywhere, such as `open ./Dice.app`.
An app opened this way from the shell says what it needs before it starts,
such as `Dice needs shell.`, because it isn't in either apps folder. The
desktop doesn't show this; the scopes are enforced either way.

Next, [give Dice a window](./desktop.md).
