# Roundhouse projects: design

> **Status:** approved 2026-11-20 · **Date:** 2026-11-20 · **Release:** VentureOS 0.7, Seto, band 0.7.6xx

Roundhouse gets a place for work in progress, a Roundhouse Project (`.rproj`), and a way in that starts with
drawing: a welcome window, and a New Interface that opens a blank canvas at once. The Run button becomes a drop-down
with both places an app can run. This is its own band, 0.7.6xx, before UIScript nesting, which moves to 0.7.7xx.

## Why

- **Design first.** The owner works best by showing rather than telling, so Roundhouse should let an interface be
  drawn before anything else is decided: what app it belongs to, where it's saved, or what code it needs. VentureOS
  still lacks pieces such as Open and Save As dialogs; drawing them is the first step, and wiring them into the
  system, by the owner or an agent, comes after.
- **A proper home for work in progress.** A `.app` means "this runs", on macOS and in VentureOS alike, so one sitting
  in Documents, such as the tutorial's Dice, is out of place, and VentureOS takes it for the real thing. A project
  is where an app and its interfaces live while they're being made; an `.app` only appears where something runs it.
- **One way in.** Today Roundhouse starts on an empty window that says to open a `.uix`, remembers nothing between
  launches, and can only make something new by making a whole `.app`.

Prior art: JetBrains Rider's and Xcode's welcome windows (recent items beside the ways to start); Xcode projects,
which hold the sources an app is built from rather than the app.

## Goals

1. A welcome window at startup: Roundhouse's logo, name, and version with New Interface…, New App…, New Project…,
   and Open… under them, centred, each with an icon; recent items beside them; a status bar along the bottom that
   explains what's under the pointer and holds "Show at startup".
2. New Interface… opens an untitled canvas straight away. Saving asks where: into a project, as a loose `.uix`
   anywhere, or into a new project.
3. Roundhouse Projects: a `.rproj` folder holding at most one app and any number of interfaces, opened, edited,
   run, and exported by Roundhouse.
4. File ▸ New… offers New Project, New App, and New Interface, as the welcome window does. New App and New Interface
   add to the open project; with none open, New App asks to create one, and New Interface opens an untitled canvas.
5. Run is a plain drop-down, "▶ Run ▾", holding Run in Simulation (Ctrl+R) and Run on VentureOS (Ctrl+Alt+R).

## Non-goals

- **Wiring an interface to code.** Roundhouse saves a clean `.uix`; connecting it to Lua or the system is done by
  hand afterwards. No outlets, actions, or generated code.
- **VentureOS reading projects.** VentureOS never sees a `.rproj`; it gets the `.app` Roundhouse makes from one.
- **More than one app per project,** workspaces, or project-wide settings beyond a name.
- **Nesting** (0.7.7xx), and any change to the UIScript format.
- **Replacing `.app` editing.** The shipped apps in `rootfs/` stay `.app`s, edited in place, since that's where
  VentureOS installs them from.

## Anti-goals

- A dialog between choosing New Interface and the canvas. Drawing comes first.
- An `.app` anywhere but where something runs or installs it: a project must not contain one.
- A project format that changes how an app is laid out, so a project and an `.app` need converting rather than
  copying.
- Roundhouse silently losing an untitled interface: leaving or quitting with one unsaved asks, as unsaved files do now.

## Constraints

- Roundhouse is Avalonia, on Linux, macOS, and Windows; its dependencies stay as approved (Avalonia, AvaloniaEdit,
  its TextMate add-on, and NLog).
- The simulator (`SimulatedRun`) and Run on VentureOS (`VentureOSRun`, over Common's vdb client) both take an `.app`
  folder and copy it; neither should need to know about projects.
- Roundhouse's formats are flat XML, read with `Venture.OS.Interface`'s `FlatXml`, as `Info.xml` is, and follow the
  forward-compatibility rules: a `version`, unknown attributes ignored and kept.
- `<Image>`s and `<String>`s are found beside a document, so an untitled one has none until it's saved.

## Approaches

How a project becomes something that runs, the choice that shapes the rest. Chosen by the owner: **A**.

- **A. Assemble the `.app` when it's needed.** The project keeps the app's files in `App/`, laid out as a bundle's
  `Contents/`. To run or export, Roundhouse copies `App/` into `<Name>.app/Contents/` in a temporary folder, with
  unsaved edits applied, and hands that to the simulator or vdb unchanged. A small assembling step, and edits under
  `App/` map to `Contents/`; in return an `.app` only exists where something runs it.
- **B. Keep a real `.app` inside the project** (`Dice.rproj/Dice.app/Contents/…`). Almost no new code, but it's still
  an `.app` in Documents one folder down, which macOS shows as launchable and VentureOS would run if copied over: the
  problem projects exist to fix.
- **C. Teach Roundhouse, the simulator, and vdb to read `App/` directly.** No copying, but it changes Common's vdb
  client and the simulator's bundle handling to save a copy that costs almost nothing.

The welcome window's layout was chosen from mock-ups (Rider's, Xcode's, and tiles): Xcode's arrangement, with
Rider's plain, centred action text, the icons kept, and the tiles' status bar. The Run button is a plain drop-down
rather than a split button, by the owner's choice: every run picks where.

## How it works

### Projects

```
Dice.rproj/
  Project.xml        Roundhouse's: the format's version and the project's name
  App/               optional; becomes Dice.app/Contents when run or exported
    Info.xml
    Lua/main.lua
    Resources/main.uix, _1_dice.cst, AppIcon.png
  Interfaces/        optional; loose .uix files, such as system components to wire later,
    open.uix         with their own images and _<id>_<name>.cst tables beside them
```

- **`Project.xml`** is flat XML: `<Project version="1"><Info name="Dice"/></Project>`. A newer `version` is refused
  with a clear message; an unknown attribute is kept on save. The project has an app when `App/` exists.
- **The `.app`'s name** comes from `App/Info.xml`'s `name`, as a bundle's does today.
- **Opening one** shows its folder in the navigator, as an `.app`'s files are shown now, with `App/` and
  `Interfaces/` at the top. A document in `App/Resources/` draws with its app's tables and the shared ones; one in
  `Interfaces/` draws with what's beside it and the shared tables.
- **Unsaved edits** are kept per file as they are for bundles now (`OpenBundle`), so Save, Save All, the ● marks,
  and the prompt before leaving work the same.
- **Running** assembles `<temp>/<Name>.app` from `App/`, with every unsaved edit under `App/` written in, and runs
  that: in the simulator as a user app, or on VentureOS through vdb. A designer document under `App/` maps to the
  same file in the assembled `.app`, so Run in Simulation opens the app at the window being designed, as it does for
  an `.app` today. A loose interface in `Interfaces/` runs as a window of its own, as a loose `.uix` does now. A
  project with neither says so.
- **Export…** (File) writes the assembled `.app` to a folder the user picks, such as `rootfs/user/apps/`, asking
  before replacing one that's there.
- **A stray `.app`.** Opening an `.app` outside a `rootfs/` offers to make a project from it (copying `Contents/` to a
  new project's `App/`), and File has Open as Project… for the same. Declining edits it in place, as now.

### New

- **File ▸ New…** is a submenu: New Interface…, New App…, New Project…. The welcome window has the same three.
- **New Project…** asks for a name and a folder, and creates `Project.xml` and nothing else.
- **New App…** adds `App/` to the open project from the template New App… uses today; when the project has one, it
  says so ("Dice.rproj already has its app"). With no project open, it asks for a project's name and folder first.
- **New Interface…** with a project open asks whether it's the app's window (into `App/Resources/`) or a loose one
  (into `Interfaces/`), and opens it untitled there. With nothing open, it opens an untitled canvas at once.

### Untitled interfaces

- An untitled interface starts from a small blank document: a `<Window>` and nothing else.
- The designer works on it as on any document: the canvas, outline, inspector, library, text, and Undo. Images and
  strings show as missing, and the status line says they're found once it's saved.
- **Save** on an untitled interface asks where it goes: into the open project (the app's resources or
  `Interfaces/`), into a new project, or as a loose `.uix` chosen with a file picker. After that it's an ordinary
  file.
- Leaving it, opening something else, or quitting with it unsaved asks first, as the unsaved-files dialog does now.
  Run in Simulation runs it as a loose window, from its text, without saving.

### The welcome window

```
┌───────────────────────── Welcome to Roundhouse ─────────────────────────┐
│                                  │ Dice              project             │
│            [ R ]                 │ ~/Documents/Dice.rproj                │
│          Roundhouse              │ Dialogs           project             │
│        Version 0.7.600           │ ~/Projects/Dialogs.rproj              │
│                                  │ Settings          app                 │
│        ✎ New Interface…          │ …/rootfs/system/apps/Settings.app     │
│        ▣ New App…                │ open.uix          interface           │
│        ◫ New Project…            │ …/rootfs/system/interfaces/open.uix   │
│        ↥ Open…                   │                                       │
├──────────────────────────────────┴───────────────────────────────────────┤
│ Choose a recent item, or start something new.        ☑ Show at startup  │
└──────────────────────────────────────────────────────────────────────────┘
```

- A window of its own, in Roundhouse's blue-to-teal bar. The left half is centred: logo, name, version, then the
  four actions as plain link text with their icons. The right half lists recent items, newest first, each with its
  name, a tag (project, app, or interface), and its folder.
- The status bar explains whatever's under the pointer (an action, or a recent item's full path) and otherwise says
  "Choose a recent item, or start something new." "Show at startup" sits at its right.
- Double-clicking a recent item opens it in the main window; one that's gone is shown dimmed and can be removed.
- It shows when Roundhouse starts with no file named and "Show at startup" is on; it closes when the main window
  opens something, and comes back when the last thing closes. With it off, Roundhouse starts on its main window as
  now. File (and the ☰ menu) has Welcome to Roundhouse to show it again.
- Recent items are projects, apps, and loose interfaces, as opened or saved. They and "Show at startup" live in
  `settings.xml` in Roundhouse's per-user folder (`Environment.SpecialFolder.ApplicationData`/`Roundhouse`: on
  Linux `~/.config/Roundhouse`), as flat XML with a `version`. A file that can't be read is reported and replaced
  with defaults, never a crash.

### Run

- The bar's Run button reads "▶ Run ▾" and opens a menu: Run in Simulation (Ctrl+R) and Run on VentureOS
  (Ctrl+Alt+R). The Run menu holds the same two with the same keys.
- While something runs, the button is Stop for that run, as now.

## Where the code goes

- `src/Venture.Roundhouse/Projects/`: the project format (`Project.xml`), opening a project for the navigator, and
  assembling and exporting the `.app`.
- `src/Venture.Roundhouse/Welcome/`: the welcome window and the settings file with recent items.
- `MainWindow`: untitled documents, File ▸ New…, Export…, Open as Project…, and the Run drop-down.
- `Bundles/OpenBundle`: learns `.rproj` as a root, or projects get a sibling of it; the builder's call.
- Docs: the architecture's Roundhouse section, the roadmap (0.7.6xx is this band, nesting moves to 0.7.7xx), the
  writing-apps guide's line about Roundhouse (New App… makes a project, and Export… puts the app where VentureOS
  installs it; the guide itself still builds Dice by hand in `rootfs/`, where an `.app` belongs), and the changelog.

## Risks

- **Two kinds of root in the navigator** (`.app` and `.rproj`), and edits that must map from `App/` to `Contents/`
  when assembling. Tests cover both and a run of each.
- **Untitled documents** touch code that assumes a document has a path (drawing, strings, images, Run). Each needs
  to cope with none.
- **Settings on three platforms.** The per-user folder differs on each; a missing or broken file must fall back
  quietly to defaults with a report in the Log.
- **Templates.** New App… makes an `.app` today (`AppTemplate`); it moves to making a project's `App/`, and its
  tests with it.

## Left to the builder

- The phases, within the roadmap's band.
- Whether projects reuse `OpenBundle` or get a sibling type.
- The blank document's window size, and the welcome window's exact sizes and spacing, within the layout above.
- The icons themselves: placeholders in the mock-up's spirit (✎ ▣ ◫ ↥), which the owner may replace.
- How many recent items are kept.

## Questions

None open.
