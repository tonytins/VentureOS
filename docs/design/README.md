# Designs

Each document here is the design for one piece of VentureOS, written and agreed before it was built: why it's
wanted, what it will and won't do, the ways it could be built and which was chosen, and where the code goes. They're
for anyone who wants to know why something works the way it does, or who's about to change it.

A design records a decision at a point in time. Once something is built, the code, [the architecture](../architecture.md),
and the reference docs it links to describe how it works today; where they differ from a design, they win, and the
design's status says so. What's planned and when is in [the roadmap](../ROADMAP.md).

## The designs

| Design | For | Status |
|---|---|---|
| [Roundhouse](./2026-11-18-roundhouse-design.md) | VentureOS's IDE: the desktop version on Avalonia, and the native one later | Built in 0.6, Izumo |
| [vdb](./2026-11-18-vdb-design.md) | Sending apps to a running VentureOS from another computer | Built in 0.7.1xx, Seto |
| [Supervised processes](./2026-11-18-processes-design.md) | Every app as a process the desktop watches, ends, and restarts | Built in 0.7.2xx, Seto |
| [The dock and the menu bar as apps](./2026-11-18-dock-app-design.md) | Panel windows and shelves, with the dock and the menu bar as apps | Built in 0.7.3xx, Seto |
| [vdb: encrypting the conversation](./2026-11-19-vdb-encryption-design.md) | Sealing vdb/1 with keys from the computer's identity, which stops being sent | Proposed for 0.7.4xx, Seto |

[`interface.figcraft`](./interface.figcraft) is a mock-up of the desktop's windows, made in
[Figcraft](https://github.com/storytold/figcraft), from when the title bar was designed.

## Writing one

- Name it `YYYY-MM-DD-<topic>-design.md`, by the day it was written, and add it to the table above.
- Start with a status line: proposed, approved, or built, and for which release.
- Cover why, goals, non-goals, and anti-goals (what would make it a failure even if it worked); the approaches
  considered and which was chosen, and by whom; how it works; where the code goes; risks; and what's left to whoever
  builds it.
- Keep it to decisions. How the code works belongs in the code and the architecture.
- When it's built, update its status, and point to where it's documented now.
