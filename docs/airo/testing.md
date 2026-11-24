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
| `app:press(name)` | Presses a button. |
| `app:submit(name, text)` | Types `text` into a field and presses Enter. |
| `app:choose(name, row)` | Clicks a list's or sidebar's row, counting from 1. |
| `app:slide(name, value)` | Drags a slider to `value`. |

**Reading it.** Each fails when the control isn't there.

| Call | Gives |
|---|---|
| `app:text(name)` | A label's, button's, or field's text, or `nil`. |
| `app:checked(name)` | Whether a checkbox, radio button, or toggle is on. |
| `app:dimmed(name)` | Whether it's dimmed. |
| `app:showing(name)` | Whether it's showing: not hidden, and in the pane shown. |
| `app:value(name)` | A slider's value, or `nil`. |
| `app:rows(name)` | A list's or sidebar's rows, as a table of text. |
| `app:chosen(name)` | The chosen row, counting from 1, or `nil`. |

**The rest.**

| Call | Gives |
|---|---|
| `app:title()` | The front window's title. |
| `app:pane()` | The name of the sidebar pane shown, or `nil`. |
| `app:windows()` | How many windows are open. |
| `app:printed()` | Everything the app and VentureOS printed so far, as a table of lines. |
| `app:read(path)` | A file's text on the test's VentureOS, such as `"/mnt/user/settings/appearance.xml"`, or `nil` and why. |

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
- There's no desktop around the app: no dock, menu bar, or Files. Timers don't fire, since there are no frames.
- The app runs with the system files airo was built with, from the `rootfs/` beside it or in the repository above it.
