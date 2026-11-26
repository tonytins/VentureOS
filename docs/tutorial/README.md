# Writing apps for VentureOS

An app is a folder ending in `.app` that VentureOS opens and closes as one
thing. While it's open, it can add commands to the shell, open windows on the
desktop, or both. When it closes, everything it added goes away. Apps are
written in Lua and run unchanged on x64 and ARM64.

These tutorials build **Dice**, one app, in two steps:

1. [A command-line app](./command-line.md): Dice adds a `roll` command to the
   shell. You write every file by hand, keep rolls in a file, and test the
   app with `airo test`. Start here; everything in it works from a text
   editor and a terminal.
2. [A desktop app](./desktop.md): Dice gets a window with a Roll button, then
   the rest of what a window can do: menus, sidebars, images, timers, and
   panels such as the dock. Roundhouse, VentureOS's IDE, helps most here.

[Troubleshooting](./troubleshooting.md) lists what VentureOS says when an app
won't open, and what to do when one crashes. Both tutorials link to it.

The app that ships with VentureOS, `Notes.app`, is a longer example in the
same shape: a command and a window that share their notes.

## Before you start

- Build and run VentureOS as the [README](../../README.md) describes. You'll
  use `./run.sh` to try your app.
- Anything an app writes to the RAM disk, such as `/mnt`, is gone after a
  reboot.
- A desktop app should look and speak like the rest of VentureOS. The
  [Venture Design Language](../design-language/apps.md) describes how.
