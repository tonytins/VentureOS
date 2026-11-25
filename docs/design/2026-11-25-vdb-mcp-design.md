# vdb: driving apps over MCP

**Status:** proposed for 0.8.4xx, Hokutosei. **Date:** 2026-11-25.

An agent working on VentureOS can build an app, send it with `vdb install`, and read what it prints, but it can't
use the app. QEMU's window takes keys from xdotool and ignores its pointer, so nothing an agent does reaches a
button, a list, or a field by pointing. This design gives vdb a `vdb mcp` command: a Model Context Protocol server,
on stdio, through which an agent sees an app's windows and uses their controls by name, as app tests already do.

It's the first part of the 0.8.4xx band, which ties up loose threads and adds notification toasts; toasts and a
simpler `<Slider>` follow it and have designs of their own.

## Why

- Pointing never reaches QEMU from xdotool, so checking an app on the real system means a person at the window.
- Pixels are the wrong level anyway. VentureOS knows every control's name, kind, text, and state; an agent asking
  for them gets a smaller, exact answer than a screenshot read by eye, in one round trip.
- App tests already have a vocabulary for this ([`app:press`, `app:choose`, ...](../airo/testing.md#the-app)).
  Reusing it means an agent learns one set of words for tests and for the running system.

The approach borrows from [UI SDK](https://github.com/vercel-labs/ui) (Apache-2.0; only ideas are borrowed, no
code): a snapshot that lists each control with its state, actions called by name rather than synthetic clicks, a
batch of steps that reports which took effect, and anything an app shows treated as untrusted content.

## Goals

- A developer's build boots to the desktop when `developer.local.lua` asks, so no one types `desktop` into QEMU.
- `vdb mcp` speaks MCP over stdio and lets an agent: list the app windows, read a window's controls, act on them by
  name, press keys, take a screenshot, read what apps printed, and install and launch apps.
- The actions and reads are airo's: the same names, the same meanings, the same way of naming a control and a row.
  airo's tests and vdb call one implementation in `Venture.OS.Common`.
- **App windows only.** Panels (the dock, the menu bar) and the system's own alerts, Trust among them, can't be
  read or acted on. An alert an app shows belongs to its window and can.
- An older vdb, or an older VentureOS, goes on working, and one that lacks the new commands says so plainly.
- Nothing an agent sends can stop VentureOS: every command is validated, bounded, and answered.

## Non-goals

- **Driving the whole desktop**: the dock, the menu bar, Files' desktop, or system alerts. By the owner's decision.
- **Roundhouse over MCP.** Driving the IDE is a different set of tools, held off by the owner's decision.
- **Pointer input.** Named actions replace it; an action airo doesn't have isn't faked with coordinates.
- **Streaming changes** (UI SDK's `observe`). An agent reads a snapshot after acting; MCP notifications can come
  later if snapshots prove too coarse.
- **MCP resources, prompts, sampling, or the HTTP transport.** Tools over stdio only.

## Anti-goals

- **A second vocabulary.** If an agent's word for an action differs from a test's, one of them is wrong.
- **A way around trust.** Nothing new is reachable before a computer is trusted, or without developer mode.
- **The frame loop waiting on the network.** A slow or silent computer must never freeze the desktop.

## Approaches considered

| | Approach | Trade-off |
|---|---|---|
| **Chosen** | MCP written by hand in vdb: newline-delimited JSON-RPC on stdio, answering `initialize`, `tools/list`, and `tools/call`, at one pinned protocol version, on Foundation's `Codable` | No new dependency, and vdb stays small for its move to its own repository. We follow spec changes ourselves, which with three methods and one version is little. |
| | The official Swift MCP SDK | Tracks the spec, but a new dependency needing approval, with its own dependencies and unchecked Windows support. |
| | MCP in C#, from airo or Roundhouse over Common's `VdbClient` | Mixes this with driving Roundhouse, a separate idea, and the owner asked for vdb. |

## How it works

### The verbs move to Common

The actions and reads in `src/Venture.Airo.Kit/Testing/TestLibrary.cs` already work on Common's `UIView`,
`AppWindows`, and `DesktopSession`. They move into `Venture.OS.Common` as plain C# methods over a window, apart
from Lua: `Press(name)`, `Submit(name, text)`, `Type(name, text)`, `Choose(name, row)`, and the reads (`Text`,
`Checked`, `Dimmed`, `Showing`, `Value`, `Rows`, `Chosen`, `Pane`, `Focused`, `Title`, `Subtitle`). airo's Lua
library becomes a thin layer over them, so its tests keep passing unchanged and pin the behaviour vdb gets. The
actions that are about the test's world rather than the app's (`app:wait`, `app:write`, `app:command`, ...) stay
in airo.

A snapshot is new: one call that walks a window and gives every control, menu item, toolbar item, and the title
field, each with its name, kind, text, value, and state (dimmed, showing, checked, focused), and a list's rows and
chosen row. It's built from the same reads, so it can't disagree with them.

### vdb/1 gains a `ui` feature

VentureOS offers `ui` in its `hello` (`ok vdb/1 install launch ui`). A computer that doesn't know it never sends
its commands; `vdb mcp` refuses to start against a VentureOS that doesn't offer it, saying which feature is missing.
The new commands, sealed like every other, after trust:

| Command | Answer |
|---|---|
| `windows` | `window <id> <app> <title>` lines for each app window, front first, then `ok` |
| `snapshot <window>` | one `control ...` line per control, then `ok` |
| `act <window> <action> <name> [text]` | `ok`, or `error <reason>`, after the app has handled it |
| `key <window> <keys>` | `ok` |
| `screenshot` | `pixels <width> <height>`, `data <base64>` lines of raw RGB, then `ok` |

The line formats, escaping of names and text with spaces, and the exact lines are settled when the commands are
built, recorded as [conversations](../vdb/README.md#conversations) that both sides' tests replay, as the existing
commands are. A window that isn't an app's, or has closed, is `error`. `act` brings its window to the front first,
as a click would.

One `act` per step keeps each line bounded and each answer about one thing; `vdb mcp` sends a batch's steps in
turn and stops at the first error, leaving earlier steps applied and saying which ran (UI SDK's rule).

### Answering without holding the loop

`VdbSession` is driven from the desktop's frame loop, and handing an app an event runs its Lua handler at once,
under the same limit a click's handler runs under, as airo's tests rely on. So an `act` is answered in the frame it
arrives in, once the handler has returned: nothing waits on the app or the network. (Corrected while building
phase 1; the first draft had the answer come on a later frame.) A screenshot is read from the framebuffer in one frame and sent over the following ones, so the
line limits and the loop's pace both hold.

### `vdb mcp`

A new subcommand, with vdb's usual connection options. It reads JSON-RPC requests from stdin, one per line, and
writes answers to stdout; its log goes to stderr, so it never mixes with the protocol. It connects to VentureOS
when the first tool needs it and connects again if the connection has closed, such as after vdb/1's 30-second
silence limit, so an agent pausing to think isn't an error.

| Tool | Arguments | Gives |
|---|---|---|
| `windows` | | The app windows, front first |
| `snapshot` | `window` (default: front) | The window's controls and their state |
| `act` | `window`, `steps`: `[{action, name, text?, row?}]` | Which steps ran, and the first error |
| `key` | `window`, `keys` | |
| `screenshot` | | The screen, as a PNG image |
| `install` | `path`, `launch?` | As `vdb install` |
| `launch` | `app` | As `vdb launch` |
| `output` | | What apps printed since the last call |

`windows`, `snapshot`, `screenshot`, and `output` are marked read-only. Every result that carries an app's text
says it's untrusted content, since an app can print anything, including words that look like instructions.

The screenshot shows the whole screen, panels and all: the boundary is on acting, not seeing. vdb encodes the PNG
itself, with uncompressed deflate blocks, CRC-32, and Adler-32, so it needs no image library on any platform.

### Booting to the desktop

An agent shouldn't have to type `desktop` into QEMU before it can use any of this. `init.lua` already runs a
developer's git-ignored `developer.local.lua`, so that file is where it goes, by the owner's decision:

```lua
venture.startDesktop()
```

`init.lua` can't run `desktop` itself: the desktop runs until it's left, and `init.lua` is stopped after 10
seconds. So `venture.startDesktop()`, under the `system` scope, only asks: once boot has finished and `init.lua`
has returned, the kernel opens the desktop in place of the first prompt, as if `desktop` had been typed. Leaving the
desktop lands at the prompt as today. If the desktop can't open, such as on a headless boot, that's reported as
`desktop` reports it and the prompt starts. Asking twice is the same as asking once.

`vdb identity --lua` prints the line along with the trust lines, so a new developer's file boots to the desktop
too. This is also the scaffolding for booting straight to the desktop in later versions, when `init.lua` itself,
or a setting, makes the same request for everyone; nothing here decides that.

## Where the code goes

- `src/Venture.OS.Common/Desktop/`: the verbs and the snapshot, with tests.
- `src/Venture.OS.Common/Vdb/`: the `ui` commands in `VdbSession`, through new `IVdbHost` members, with tests and
  recorded conversations.
- `src/Venture.Airo.Kit/Testing/`: the Lua library over Common's verbs.
- `src/Venture.OS.Kernel/Vdb/VdbListener.cs`: the host's new members, over `AppWindows` and the framebuffer.
- `src/Venture.OS.Common/Scripting/` and the kernel's boot: `venture.startDesktop()`, and opening the desktop
  after `init.lua` when it was asked for.
- `tools/vdb/`: `VdbKit` learns the commands; `vdb mcp`, its JSON-RPC, and the PNG writer, with tests, including
  replaying the conversations.
- Docs: [docs/vdb](../vdb/README.md), its own section for `vdb mcp`, `docs/architecture.md`, the scorecard, and
  the roadmap's 0.8.4xx band.

## Risks

- **Kernel behaviour is checked by booting.** The verbs and session are tested in Common; the frame-loop wiring and
  the framebuffer read are only checked on a running VentureOS.
- **The screen size and pixel format** the kernel's framebuffer reports may not be RGB in that order, or may carry
  padding per row; the host must convert, and check the sizes before allocating.
- **MCP's spec changes.** The pinned version may age; an agent's client that insists on a newer one won't connect
  until vdb follows.
- **A large screenshot** is a few megabytes of base64 over QEMU's user network, which may take a few seconds.

## Left to whoever builds it

- The exact vdb/1 line formats, and how names and text are escaped.
- Whether `key` accepts names such as `Enter` and `Ctrl+Z`, and which.
- The MCP protocol version to pin, the tools' JSON schemas, and their descriptions for agents.
- How the snapshot lays out lists with many rows (a cap, with the count of what was left out).
- Whether boot opens the desktop through the shell's `desktop` command or the code behind it, as long as the two
  behave alike.
- The phases of the rollout, starting with moving the verbs into Common.
