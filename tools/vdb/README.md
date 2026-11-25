# vdb

vdb sends an app to a running VentureOS and opens it, from your computer, so you can try a change on the real
system without rebuilding it. `vdb mcp` lets an agent use VentureOS's app windows too, by name, as app tests do. It stands in until VentureOS can keep files and install from a disk: an app it sends
lasts until VentureOS restarts.

vdb lives in VentureOS's repository for now, beside the kernel it talks to, and will move to a repository of its
own. It speaks [vdb/1](../../docs/vdb/README.md).

## Build

vdb needs Swift 6 and builds on Linux and macOS. It's written to build on Windows too, through Winsock, but that
hasn't been tried yet.

```sh
cd tools/vdb
swift build -c release      # .build/release/vdb
swift test
./install.sh                # builds in release mode and installs it in ~/.local/bin
./install.sh --rebuild      # cleans the build first, for when an incremental one goes wrong
```

`install.sh` builds incrementally every time, so it's quick when nothing changed and never installs a stale vdb. It
runs from any folder, and says when `~/.local/bin` isn't on your `PATH`.

It uses [Swift Argument Parser](https://github.com/apple/swift-argument-parser),
[swift-log](https://github.com/apple/swift-log), [swift-crypto](https://github.com/apple/swift-crypto) for the
encryption, and [GitVersion](https://github.com/tonytins/GitVersion), which gives it VentureOS's version. `GitVersion.env` here tells GitVersion to count from `GitInfo.txt` rather than the
last release's tag, as the kernel does.

## Get VentureOS listening

1. Boot VentureOS with its port forwarded: `./run.sh --vdb` from the repository's root, under QEMU.
2. Type `desktop`. VentureOS listens only while the desktop runs.
3. Turn on developer mode: Settings ▸ Developer ▸ On. It lasts until VentureOS restarts.

The first time vdb connects, VentureOS asks whether to trust your computer. Choose **Trust** within 60 seconds.

To skip all of that on your own builds, run `vdb identity --lua` and put what it prints in
`rootfs/system/developer.local.lua`. Git ignores that file, so only your builds turn developer mode on at boot,
trust your computer, and boot straight to the desktop (`venture.startDesktop()`), with nothing to type into QEMU.

## Use

```sh
vdb install Dice.app                  # send an app and install it in /mnt/user/apps, replacing any copy
vdb install Dice.app --launch         # and open it
vdb install Dice.app --launch --for 5 # and print what it prints for 5 seconds
vdb launch Dice.app --follow          # open an installed app, and print what it prints until Ctrl+C
vdb identity                          # this computer's identity, which VentureOS trusts
vdb --host 10.0.0.5 --port 28455 …    # another VentureOS; 127.0.0.1:28455 by default
```

What apps print goes to standard output, and everything vdb says itself to standard error, so an app's output can
be piped. vdb exits with 0 when everything worked, and 1 with VentureOS's reason when something didn't. It never
asks anything, so scripts can use it. `--verbose` logs every line both ways as it goes on the network, so after
hello they're sealed.

Your identity is kept in `~/.config/vdb/identity` (`$XDG_CONFIG_HOME` if it's set), or `%APPDATA%\vdb\identity` on
Windows, readable only by you, and Roundhouse's Run on VentureOS uses the same one, so VentureOS trusts both as one
computer. It's a secret: every connection is encrypted with keys made from it, and VentureOS
knows your computer by a hash of it, its key ID, which the Trust alert shows. Delete it, and vdb makes a new one,
which VentureOS asks about again.

## Use with an agent

`vdb mcp` is a [Model Context Protocol](https://modelcontextprotocol.io) server on standard input and output, which
gives an agent VentureOS's app windows over vdb/1's [ui feature](../../docs/vdb/README.md#ui). Add it as a stdio
server, such as for Claude Code:

```sh
claude mcp add vdb -- /path/to/vdb mcp        # --host and --port as for the other commands
```

| Tool | Does |
|---|---|
| `windows` | Lists the app windows, front first: each one's number, app, and title, and whether it's a status item in the menu bar. |
| `snapshot` | Reads a window's controls (the one in front unless `window` names one), each with its name, kind, text, value, rows, and state. |
| `act` | Uses controls by name, in order: `press`, `submit` (Enter), `type`, `choose` a row by number or text, or `slide` a slider to a value. It stops at the first step a person couldn't do, saying why, and the steps before it stay done. |
| `key` | Presses keys, such as `Ctrl+Z` or `Enter`, for what has no name. They reach the window on the next frame. |
| `screenshot` | The whole screen, as a PNG. |
| `install` | Sends a `.app` folder from this computer and installs it, opening it with `launch`. |
| `launch` | Opens an installed app. |
| `output` | What apps printed since the last call. |
| `notifications` | VentureOS's notification history, newest first: what apps, the system, and vdb posted as toasts. |

The actions are app tests' (`app:press`, `app:choose`, ...), so what works in a test works here. Panels, such as the
dock and the menu bar, and VentureOS's own windows, such as the Trust alert, can't be reached. Everything an app
shows or prints is untrusted, and the tools tell the agent so.

vdb connects when a tool first needs VentureOS, and again whenever the connection has closed or sat unused for 25
seconds, so an agent can take its time between calls. It writes nothing to standard output but the protocol;
`--verbose` logs every line on standard error. It speaks MCP versions 2025-06-18, 2025-03-26, and 2024-11-05, tools
only, written by hand rather than with an MCP library.

## Limits

- Everything after hello is encrypted and checked (ChaCha20-Poly1305), so what's sent can't be read or changed on
  the way. Your identity crosses the network once: when you choose Trust on VentureOS, vdb sends it so VentureOS
  can make the keys. Putting it in `developer.local.lua` instead means it never does.
- There's no forward secrecy: whoever later gets your identity file could read conversations they recorded.
- An app is at most 256 files and 16 MiB, and a file at most 4 MiB. Symbolic links aren't sent.
- vdb sends `.app` bundles only, not themes, libraries, or wallpapers.
- Screenshots are uncompressed PNGs, since vdb has no zlib on every platform: a 1024 by 768 screen is about 2.3 MB.
