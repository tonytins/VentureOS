# Troubleshooting apps

What VentureOS says when an app won't open or misbehaves, and what to do
about it. The examples use Dice, from [the tutorials](./README.md).

Ctrl+C or Esc stops a command or a `main.lua` that's running too long.

## When an app won't open

`open` says what's wrong, and the app doesn't open:

| Message | What to do |
|---|---|
| `there's no app called dice` | Check the folder is `Dice.app` in `rootfs/system/apps/`, and that you rebuilt. `apps` lists what VentureOS found. |
| `Dice.app has no Contents/Info.xml.` | Put `Info.xml` inside `Contents/`, not next to it. |
| `Dice.app's Contents/Info.xml, line 3: ...` | The file isn't valid. Text between elements, a missing quote, or a `<!DOCTYPE>` all stop it. Each element holds attributes only. |
| `"dice" isn't a reverse-DNS identifier` | Use at least two parts separated by dots, such as `dev.example.dice`. |
| `Dice.app has no Contents/Lua/main.lua to run` | Add `main.lua`, or point `main` at your script. |
| `Lua error: ...`, then `Dice didn't open` | `main.lua` stopped with an error. The message gives the line. Anything it had set up is removed. |
| `Lua error: Stopped after 10 seconds.` | `main.lua` took too long. Set the app up in `main.lua` and do the work in commands. |
| `Dice is already open.` | An app opens once. `close dice` first to open it again. |

## When it opens but something's off

| Message | What to do |
|---|---|
| `venture.command needs the shell scope: ...` | Add the scope to `needs` in `Info.xml`. Each `venture` function names the scope it needs. |
| `main.uix has no control or menu item called "reslut"` | The name in `window:setText` or `window:on` doesn't match the `.uix` file. Names are case-sensitive. |
| `venture.ui.open: main.uix: Line 4: ...` | The `.uix` file isn't valid; the message says where. |
| `"echo" is a system command, and Dice can't replace it.` | Pick another name. Apps can't take over the system's commands. |
| `"roll" is already registered by Games.` | Another open app uses the name. Close it, or pick another name. |
| `close: Dice can't close while its own code is running.` | An app's command tried to close the app. Close it from the prompt. |
| `Dice crashed: its code failed 3 times within a minute.` | See [When an app crashes](#when-an-app-crashes). |

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
