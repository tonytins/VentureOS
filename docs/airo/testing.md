# Testing an app

> **Status:** draft, 0.8.1xx. The calls below work today; the format may still change before 0.8 ships.

An app's tests live in its Roundhouse Project, in `Tests/`, and are written in Lua. They use the app as a person
would: press its buttons, type in its fields, choose rows, and read what its windows say. `airo test` runs them,
each on a fresh VentureOS with the project's app built into it and opened.

```
Settings.rproj/
  Project.xml
  App/
  Tests/
    appearance.lua
    panes.lua
```

```
airo test Settings.rproj
```

In Roundhouse, with the project open, Run ▸ Test (Ctrl+U) runs the same tests against the app as it's edited,
saved or not, and puts each result in the Log tab.

```
pass Tests/appearance.lua: Dark switches the look and saves it
fail Tests/appearance.lua: A custom accent says its colour: Tests/appearance.lua:12: expected "Custom, #12ABEF", got "Blue"
1 passed, 1 failed
```

VentureOS's own tests, in `tests/`, cover the system: what a sidebar of panes or a menu does in any app. An app's
tests cover what that app does, and live with it, so changing its design changes only its own tests.

## A test file

Every `.lua` file at the top of `Tests/` is a test file, run in name order. A file declares its cases with
`test.case`, and each case is handed the app:

```lua
test.case("Dark switches the look and saves it", function(app)
  app:press("dark")
  test.expect(app:checked("dark"))
  local saved = app:read("/mnt/user/settings/appearance.xml")
  test.expect(saved:find('mode="dark"', 1, true), "appearance.xml should say dark")
end)

test.case("A custom accent says its colour", function(app)
  app:submit("custom", "#12abef")
  test.equal(app:text("accentName"), "Custom, #12ABEF")
end)
```

- **Each case starts fresh.** Its VentureOS is a new copy of the system's files, so what one case saves, another
  doesn't see. The look starts as the system's `appearance.xml` sets it.
- **A file's own code runs once,** before its cases, so a local declared at the top is shared by every case in it.
- **A test file runs on your computer,** as a build script would, with Lua's standard libraries. Only the app runs
  inside VentureOS's rules.
- `print` in a test goes to airo's error output, so it doesn't mix with the results.

## Checks

| Call | Fails when |
|---|---|
| `test.expect(value, message)` | `value` is `false` or `nil`. The message is optional. |
| `test.equal(actual, expected, message)` | `actual ~= expected`, saying both. The message is optional and goes first. |

A failure names the test file's line. Any other Lua error in a case fails it the same way.

## The app

Every call acts on the window in front, the one a person would be using, so a window the app opens is the one the
next call uses. Use a colon: `app:press("ok")`. A control is named as in the app's `.uix`.

**Using it.** Each fails when the control isn't there, isn't showing (hidden, or in another pane), or is dimmed,
since a person couldn't use it either.

| Call | Does |
|---|---|
| `app:press(name)` | Presses a button, toolbar item, or menu item. |
| `app:submit(name, text)` | Types `text` into a field or the title bar's field and presses Enter. |
| `app:type(name, text)` | Types `text` into a field or text area, without Enter. |
| `app:slide(name, value)` | Moves a slider to `value`, as a drag that ends there: snapped to its step and kept in its range, then `changed` and `committed`. |
| `app:choose(name, row)` | Clicks a list's or sidebar's row, or a grid's item: its number, counting from 1, its text, such as `"Clock"`, or a grid item's name. |

A menu item or toolbar item is always there while its window is, so only its dimming counts.

**Reading it.** Each fails when the control isn't there.

| Call | Gives |
|---|---|
| `app:has(name)` | Whether the front window has a control, menu item, toolbar item, or title field of that name. It doesn't fail. |
| `app:text(name)` | A label's, button's, or field's text, or `nil`. |
| `app:checked(name)` | Whether a checkbox, radio button, or toggle is on. |
| `app:dimmed(name)` | Whether it's dimmed. |
| `app:showing(name)` | Whether it's showing: not hidden, and in the pane shown. |
| `app:value(name)` | A progress bar's or slider's value, or `nil`. |
| `app:rows(name)` | A list's or sidebar's rows, or a grid's titles, as a table of text. |
| `app:chosen(name)` | The chosen row or grid item, counting from 1, or `nil`. |

**The rest.**

| Call | Gives |
|---|---|
| `app:title()` | The front window's title, or `nil` when its title bar is blank. |
| `app:subtitle()` | The front window's subtitle, such as Files' `"3 items"`, or `nil`. |
| `app:pane()` | The name of the sidebar pane shown, or `nil`. |
| `app:focused()` | The name of the control with the keyboard, or `nil`. |
| `app:windows()` | How many windows are open, panels included. |
| `app:printed()` | Everything the app and VentureOS printed so far, as a table of lines. |

**The rest of VentureOS.** Paths are VentureOS's, such as `/mnt/user/notes.txt`; a test can't reach files outside
its copy.

| Call | Does |
|---|---|
| `app:newWindow()` | Asks the app for a new window, as File ▸ New Window does. Gives whether it opens one. |
| `app:open(path)` | Hands the app a file, as Files does. Gives whether the app opens files. |
| `app:command(line)` | Runs `line` in the shell, in `/mnt/user`, such as `"note buy milk"`. Gives what it printed, as a table of lines. |
| `app:wait(seconds)` | Lets time pass for the app's timers, which run as they would have. Up to an hour. |
| `app:write(path, text)` | Writes a file, and any folders it needs. |
| `app:read(path)` | A file's text, or `nil` and why. |

The app opens in `/mnt/user`, so that's where it starts, and where a command it adds keeps its files.

## When a case fails

- A check fails, or the case raises an error.
- The app didn't open. The failure says what VentureOS printed.
- The app hits a Lua error while the case runs, even one the case didn't check for, since a person would have seen it.
- It runs for more than 30 seconds.

A file that can't be loaded, such as one with a syntax error, or that declares no cases, is a failure of its own,
and the other files still run.

## Limits

- Nothing is drawn, so a test reads what a window says, not what it looks like. Pictures are checked for but not
  loaded.
- There's no desktop around the app: no dock, menu bar, or Files. The app's own windows and panels open, and
  another app it hands a file to opens too, but a person's clicks on the dock, minimizing, and zooming aren't there.
- Timers run only when a test waits.
- A test's VentureOS has developer mode, off, as one that just started; it has no network, measured memory, or
  Cosmos, so the app sees them as missing.
- The app runs with the system files airo was built with, from the `rootfs/` beside it or in the repository above it.
