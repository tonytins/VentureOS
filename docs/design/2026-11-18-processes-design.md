# Apps as supervised processes

**Status:** built in 0.7.2xx, Seto; the [architecture](../architecture.md) and [writing apps](../writing-apps.md) say how it works now. **Date:** 2026-11-18.

Every app, the system's included, becomes a **process**: something with an ID and a lifecycle, which a
**supervisor** starts, watches, ends, and, for the apps the desktop can't do without, restarts. It's the ground the
next band stands on, where the dock and the menu bar become apps the desktop keeps alive, and it leaves room for
apps to run independently of each other later.

## Why

- **Nothing knows when an app goes down.** An app is a `LuaOwner` in `AppManager`'s list. An error in one of its
  event handlers is reported and it carries on; a handler that runs too long is cut off at 5 seconds. The desktop
  notices Files has gone only by checking, every frame, whether it's in the list, and restarts it on a timer of its
  own (`DesktopSession.KeepFallbackRunning`). Nothing can say why an app ended, or tell a crash from a quit.
- **The next band depends on it.** With the dock and the menu bar as apps, the desktop must know at once when one
  fails, and bring it back at once, without a gap in which there's no way to reach the VentureOS menu.
- **VentureOS is becoming a desktop with background processes**, and a process table is where that starts: something
  to list, to end, and later to schedule.

## Goals

- Every app that opens is a process, with an ID, a name, its bundle, a state (starting, running, exited, or
  crashed), when it started, and, once it's over, why.
- One supervisor owns the processes. Opening, closing, quitting, and ending an app all go through it, and it's the
  one place that says an app ended, and why.
- **Crashes are noticed.** An app crashes when its startup fails, or when its code keeps failing: errors escaping
  its handlers, or handlers cut off by the time limit, three times within a minute. A crashed app is ended as if it
  quit, its windows closed, and the reason reported once.
- **Restart policies.** Each process has one: `never`, for ordinary apps, or `always`, for the apps the desktop keeps
  running. An `always` process is restarted at once when it ends, for any reason; if it keeps crashing, the waits
  grow (at once, then 2, 5, 10, and every 30 seconds), and settle back to at once after it has run for a minute.
- The kept apps are the desktop's choice, not the apps': the desktop names them (Files now; Dock and the menu bar's
  host next band). An app can't make itself unkillable.
- Anything can ask to hear about processes starting and ending, such as the dock, and later System Monitor.
- `ps` lists processes, and `kill` ends one, by ID or name; Lua reaches them through `venture.processes()` and
  `venture.kill(id)`, under the `system` scope.

## Non-goals, for now

- **Apps running independently**, each on its own thread. Everything stays on the kernel's one thread, taking
  turns, as now. (See [Room for later](#room-for-later).)
- **Isolation**: each process with memory of its own. Cosmos runs everything in one managed heap, in kernel mode.
- **Limits per process** on memory or time beyond the handler limits there are now; the shared heap can't be
  measured per app.
- **Processes for scripts**: script commands, `init.lua`, and the `lua` prompt stay owners as they are.
- System Monitor's process list, which can follow in a later band.

## Anti-goals

- **A supervisor that can take the desktop down.** Whatever an app does, the supervisor catches it, ends the app,
  and goes on.
- **Restart storms.** A kept app that can't start is retried on the growing waits, with one message per new reason,
  as Files is now.
- **A second way to open apps.** `AppManager` keeps reading bundles and running their code; the supervisor decides
  when, and keeps the record.

## Approaches considered

1. **Supervised processes on one thread** (chosen): a process table and supervisor in `Venture.OS.Common`, around
   the `LuaOwner`s apps already have.
2. **Each app on a Cosmos thread:** one busy app wouldn't stall the rest, but Cosmos's scheduler has one CPU, its
   sockets are safe on one thread only, and the desktop's windows, the Lua library, and the held output would all
   need locking. Large and risky, for a benefit that waits on more of the platform.
3. **Isolated processes:** not possible on Cosmos.

The owner chose 1, with room left for 2.

## The process

| Field | Meaning |
|---|---|
| `Id` | A whole number from 1, counting up, never reused while VentureOS runs. |
| `Name` | The app's name, from its `Info.xml`. |
| `Bundle` | The app it runs. |
| `State` | `Starting`, `Running`, `Exited`, or `Crashed`. |
| `Started` | When, on the desktop's clock. |
| `Policy` | `Never` or `Always`. |
| `Ending` | Once it's over: how (quit, closed, killed, failed to start, crashed) and the reason in words. |

A process that's over leaves the table, but the supervisor raises its ending first, with the process, so whoever
listens can say why.

## The supervisor

- `Start(app, policy)` reads the bundle through `AppManager`, makes the process, runs its startup, and gives the
  process, or why it couldn't start.
- `End(process, how)` disposes its owner, which closes its windows and undoes what it set up, and records how.
- Each frame, `Tick(now)` restarts `Always` processes whose wait is over.
- Faults are reported to it by whatever ran the app's code: `AppWindows` for handlers and timers, `AppManager` for
  startup. It counts them per process and ends one as crashed at the third in a minute.
- `Started` and `Ended` events, raised after the table changes.

`apps` and `close` keep working, through the supervisor; `close` on a kept app ends it, and it comes back, as Files
does today.

## Room for later

Every call into an app's code, its startup, an event, a timer, or a callback, goes through its process
(`process.Run(work)`), never straight to its interpreter. Today that runs the work at once. Later, a process can
keep a queue and drain it on a thread of its own, and nothing that calls into apps has to change. That's the one
rule this band holds to for the future.

## Where the code goes

- **`Venture.OS.Common/Processes`** (`Venture.OS.Processes`): the process, the supervisor, policies, and faults,
  tested with xUnit like the rest of Common.
- **`AppManager`** starts and ends through the supervisor; **`AppWindows`** reports faults; **`DesktopSession`**
  hands the supervisor its kept apps, and its own restart timer goes.
- **`venture.processes()`** and **`venture.kill(id)`** in `VentureLibrary`; `ps` and `kill` as Lua commands in
  `rootfs/system/commands`.
- **The kernel** ticks the supervisor from the desktop's frame loop, and vdb's host closes apps through it.
- **Roundhouse's Run** gets it with `DesktopSession`.

## Risks and what to check

- **Behaviour change:** an app whose handlers keep failing is now ended at the third failure in a minute, where it
  used to carry on. Apps that fail once in a while are unaffected.
- **The idle frame** must still allocate nothing (`IdleFrameTests`).
- **Booting with Files broken,** to see the waits and the single message.

## Left to the builder

- Exact names, and how events reach listeners.
- How `ps` lays out its columns.
