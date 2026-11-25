# Status items, startup items, and kept apps

**Status:** proposed for 0.8.5xx, Hokutosei. **Date:** 2026-11-25.

VentureOS's menu bar has a clock and, since 0.8.4xx, a bell, both drawn by the desktop. An app can't put anything
there, can't stay out of the dock, and can't start with the desktop; and the two apps the desktop keeps running,
Files and the Dock, are named in its C#. This design gives apps **status items** in the menu bar, lets them stay out
of the dock, lets the person choose apps to **open at startup**, which may run with no window at all, and moves the
desktop's **kept apps** out of its code and into the apps themselves.

It's 0.8.5xx, split from 0.8.4xx by the owner since it's bigger than toasts.

## Why

- Some apps are better as a small item in the menu bar than as a window: a weather reading, a timer, a volume.
- Some work belongs in the background, started with the desktop and out of sight, as macOS's login items and
  menu extras are, or as a daemon is.
- The kept apps are VentureOS's shell, as Explorer is Windows': the file manager and the desktop's own app (Files)
  and the taskbar (the Dock), restarted by the part that never goes away, the desktop session, while the wallpaper,
  windows, and toasts carry on without them. That shape is right; that the session knows them by name in C# isn't.

## Goals

- **Kept apps**, the system's: a system app's `Info.xml` says it's kept (`kept="true"`), and the session starts it
  with the desktop and restarts it whenever it ends, as Files and the Dock are now. Only apps in `/mnt/system/apps`
  are kept, so an app can't make itself unkillable, as the processes design asked.
- **Startup items**, the person's, after macOS's login items: apps the person chooses start with the desktop, after
  the kept apps, and are left ended if they quit, are closed, or crash. One may show a window, a status item, or
  nothing at all, as a background task.
- **Becoming one**, both ways, by the owner's decision: the person adds and removes them in a Settings pane, and an
  app may ask with an alert the person answers.
- **Out of the dock**: an app's `Info.xml` can say it isn't shown in the dock (`dock="hidden"`), and it can change
  that as it runs.
- **Status items**: a new UIScript root, `<StatusItem>`, after the sidebar's logic: one that holds nothing is a
  button; one that holds menus drops them down; one that holds controls and visuals opens a small panel of them. Its
  app's process owns it: it leaves the bar when the app ends, and comes back when it starts again.
- **The bell and the clock** are the bar's first status items: the same row, the same clicks, still the desktop's.
- **Background tasks are seen**: System Monitor lists the processes and can end one, since an app may now run out
  of sight. (The writer's suggestion; the owner didn't rule it out.)

## Non-goals

- **The desktop session as a process.** It's the kernel's frame loop; restarting it is its own project, and nothing
  here needs it.
- **Keeping startup items across a restart.** `/mnt/user` is installed again at every boot, so the person's choice
  lasts until restart, as the look does; a list in `rootfs/user/settings/` sets a build's defaults.
- **Apps running on threads of their own**, or isolation between them: still one thread, taking turns.
- **Status items in Hudini**: a game has no menu bar.

## Anti-goals

- **An app that can't be stopped.** Only system apps are kept; a startup item that crashes stays ended.
- **Something running the person never agreed to.** An app starts at startup only from the person's list; asking
  is an alert they answer, never a setting an app flips itself.
- **A bar an app can flood.** The bar has room for so many items; past it, the newest don't show, and the bell and
  the clock always do.
- **A crash in an item taking the bar down.** An item's events run in its app's process, which the supervisor
  catches, as every app's do.

## Approaches considered

For where the kept apps are named, the owner took the writer's pick:

| | Approach | Trade-off |
|---|---|---|
| **Chosen** | **`kept="true"` in a system app's `Info.xml`**, honored only in `/mnt/system/apps` | No new file; each app says what it is beside its name and icon; a user app that says it gets nothing. |
| | A system file listing them | One place to look, but a new format, and it drifts from the apps it names. |
| | `init.lua` declaring them | Flexible, but `init.lua` runs before the desktop and is stopped after ten seconds, so the session must remember what it was told, and the shell's most important list depends on a script finishing. |

The rest follows the toasts' approach: the desktop draws the bar and its items, so an item's app crashing never
takes the bar with it.

## How it works

### Kept apps

- `Info.xml`'s `<Info>` gains `kept`, `true` or `false`, `false` by default. A reader before it ignores and lists it,
  as it does any attribute it doesn't know.
- The session starts every app in `/mnt/system/apps` with `kept="true"` when it opens, under the `always` policy,
  and restarts it as Files and the Dock are restarted now. In `/mnt/user/apps`, `kept` is ignored without a word,
  since nothing goes wrong; `apps` and `ps` list such an app as not kept.
- Files and the Dock gain `kept="true"`, and `DesktopSession`'s list of the two goes. `FallbackApp`, `DockApp`, and
  `MenuBarApp` stay, since they name roles (whose menus show when nothing's in front, who draws the dock, who hosts
  the bar's panel), not who's kept.

### Startup items

- The person's list is `/mnt/user/settings/startup.xml`:

  ```xml
  <StartupItems version="1">
    <App identifier="dev.example.weather"/>
  </StartupItems>
  ```

  Unknown elements and attributes are skipped, as `appearance.xml`'s are. A missing file is an empty list.
- When the session opens, after the kept apps, it opens each listed app it finds, by identifier, under the `never`
  policy. One that isn't installed, or won't open, is told in a toast from VentureOS, with why, and the rest open.
- `venture.startupItems()` lists them, `{ identifier, name, running }` each, and `venture.setStartupItem(app, on)`
  adds or removes one, saving the file: both under the `system` scope, for Settings.
- **An app asks** with `venture.ui.askToOpenAtStartup(function(added) ... end)`: an alert, titled with the app's
  name, asks whether to open it at startup, with Open at Startup and Not Now; the answer is saved, and the function
  is told. An app already on the list isn't asked. It's an app's alone, since it calls the app back.
- **Settings** gains a Startup pane: the list, with each app's name and icon, a way to add any installed app, and to
  remove one. It's built in Settings' project; it ships when the owner builds it into `rootfs/`.

### Out of the dock

- `<Info>` gains `dock`, `shown` (the default) or `hidden`. A hidden app's icon doesn't show in the dock while it
  runs, and its windows, when it has any, still come to the front from the dock's minimized items and from the menu
  bar. A reader before it shows it in the dock: plainer, but nothing breaks.
- `venture.ui.setShownInDock(shown)` changes it while the app runs, as an app's own setting may; it's an app's
  alone.

### Status items

```xml
<UIScript version="1">
  <StatusItem name="weather" image="cloud" title="72°" width="240" height="140"/>
  <Image name="cloud" path="cloud.png"/>
  <Text name="summary" x="12" y="12" width="216" height="20" text="Cloudy"/>
  <Button name="refresh" x="12" y="100" width="80" height="24" text="Refresh"/>
</UIScript>
```

- `<StatusItem>` is a root, in place of `<Window>` or `<Panel>`, with `name`, a `title`, an `image` (a symbolic
  icon, drawn in the bar's text color, as toolbar items' are), or both, and, for a panel, `width` and `height`.
- **What it holds decides a click**, after the sidebar: nothing, a button reporting `pressed`; `<Menu>`s, which drop
  down under it as an app's menus do from the bar; controls and visuals, a panel of them under it, made of the
  shelf, as toasts are, placed from the panel's corner. Menus and controls together refuse the document.
- An app opens it with `venture.ui.open("status.uix")`, getting the handle a window gives: `on`, `setText`,
  `setValue`, and the rest, and `setTitle` for its text in the bar.
- The bar shows apps' items right to left from the bell, in the order they opened, then the bell, then the clock.
  Past the room between the menus and the bell, the newest don't show. A click opens its menu or panel, as the bell
  opens the history; Escape or a click outside closes it.
- Its app's process owns it: when the app ends, the item leaves the bar; when a kept app or a reopened app opens it
  again, it comes back. Its events run in the process, so a fault there counts as the app's.
- UIScript's spec gains a Status items section; Hudini doesn't read `<StatusItem>`, and refuses it as an unknown
  root. Roundhouse opens and draws one.

### The bell and the clock

They join the same row as the bar's first status items, drawn and handled by the desktop, so neither depends on an
app. Nothing about them changes for the person.

### System Monitor

It gains a Processes pane: every process, its name, how long it's run, and whether it's kept, from
`venture.processes()`, and a way to end one with `venture.kill`, which a kept app survives by restarting. Built in
its project; it ships when the owner builds it.

### vdb

A snapshot of an app's status item lists its controls, as a window's do, and `windows` lists it as the app's, with
`kind=statusItem`, so an agent can use one by name. The bar's own items, the bell and the clock, stay out of reach,
as the bar is.

## Where the code goes

- `src/Venture.OS.Common/Bundles/`: `kept` and `dock` on `Bundle`.
- `src/Venture.OS.Common/Desktop/`: the session's kept apps from bundles; startup items (`StartupItems`, reading and
  writing the file); the bar's row of items; status items' drawing, menus, and panels; the dock leaving hidden apps
  out; the alert for asking.
- `src/Venture.OS.Common/Scripting/`: `venture.startupItems`, `setStartupItem`; `venture.ui.askToOpenAtStartup`,
  `setShownInDock`.
- `src/Venture.OS.Interface/`: `<StatusItem>` in `UIScript`, the schema, and the spec.
- `src/Venture.OS.Common/Vdb/` and `tools/vdb/`: status items in `windows` and `snapshot`.
- `rootfs/system/apps/`: `kept="true"` on Files and the Dock; `rootfs/user/settings/startup.xml`, empty.
- `projects/Settings.rproj`, `projects/SystemMonitor.rproj`: the Startup and Processes panes, with their tests.
- `src/Venture.Roundhouse/`: opening and drawing a status item.
- Docs: the HIG (status items, startup items, the dock), the spec, the architecture page, the scorecard, the
  roadmap, the tutorials.

## Risks

- **A startup item that does nothing visible** is easy to forget; System Monitor's list and `ps` are the only way to
  see it, so the Processes pane matters.
- **Boot gets slower** with every startup item, since they open in turn on one thread.
- **Bar room**: a narrow screen, or many items, hides some; which show is the order they opened.
- **The shipped apps' panes** reach the system only when the owner builds them into `rootfs/`.

## Calls made in writing it

The owner decided: daemons as macOS's login items, chosen by the person and not restarted after a crash; either
form, window, status item, or none; both ways of adding one; kept apps in a system app's `Info.xml`; status items
after the sidebar's logic; out of the dock in `Info.xml` and the API. These were the writer's calls:

- The person-facing name is **Open at Startup**, and the code's `StartupItems`, since VentureOS has no login.
- `kept="true"` in a user app is ignored without a word.
- A startup item that can't open is told in a toast, and the rest open.
- The bar shows apps' items newest leftmost, and hides the newest past its room.
- A status item's panel is made of the shelf, as toasts are.
- System Monitor's Processes pane is in the band.
- vdb reaches apps' status items, not the bar's own.

## Left to whoever builds it

- How wide the bar's room is, and the gap between items.
- The Startup and Processes panes' layouts, in Settings' and System Monitor's projects.
- The alert's exact words, in the HIG's voice.
- The phases of the rollout, starting with `kept` and the session's kept apps, the smallest change with the most
  behind it.
