# airo

airo is VentureOS's build tool. It builds a Roundhouse Project (`.rproj`) into the `.app` VentureOS runs, and
runs the project's tests against it. Roundhouse uses the same code to run and export one. It's named after Amtrak's
[Airo](https://en.wikipedia.org/wiki/Amtrak_Airo) trains.

airo runs on the computer you make apps on, under Linux, macOS, or Windows, and needs only the .NET SDK. It's in
`src/Venture.Airo`, and what it shares with Roundhouse is in `src/Venture.Airo.Kit`.

```
dotnet run --project src/Venture.Airo -- build Dice.rproj --output rootfs/user/apps
dotnet run --project src/Venture.Airo -- test Dice.rproj
```

## How it behaves

- It never asks anything, so scripts and Roundhouse can run it.
- What it makes or finds goes to standard output, a line each (a built app's path, a test's result), so another
  program can read it.
- What went wrong goes to standard error, starting `airo:`, and airo exits with 1. It exits with 0 when it did what
  was asked.
- A mistake on the command line, such as a missing `--output`, is reported the same way, without the help text.
  `airo --help` and `airo build --help` show it.
- `airo --version` prints VentureOS's version, which airo shares, as Roundhouse does.

## `airo build`

```
airo build <project> --output <folder> [--replace]
```

Builds the project's app, from its `App/` folder, into `<folder>/<Name>.app`, and prints that path.

- `<project>` is the `.rproj` folder, or its `Project.xml`.
- `--output` (`-o`) is required. It's where the app goes, such as `rootfs/user/apps/` for an app that's ready to ship,
  or a scratch folder to try it. The folder is made if it isn't there.
- `--replace` replaces an `.app` of the same name that's already in the folder. Without it, airo leaves that app as
  it is and says so. A replacement is built beside the old app first, so a failed build leaves the old one untouched.
- `<Name>` comes from `App/Info.xml`'s `name`, as a bundle's does.
- The `.app` is checked as VentureOS reads apps at boot. If VentureOS would refuse it, airo says why and leaves
  nothing behind.

airo refuses these, with the reason:

- a folder with no `Project.xml`, or one that isn't a project
- a `Project.xml` with a newer format `version` than airo reads, or something in it airo doesn't know
- a project without an app (`App/`)
- an `App/Info.xml` with no usable `name`

## `airo test`

```
airo test <project>
```

Runs the project's tests, the `.lua` files in its `Tests/` folder, each case on a fresh VentureOS with the
project's app built into it and opened. [Testing an app](testing.md) has how to write them.

- It prints a line a case, `pass` or `fail`, the file, and the case's name, with why it failed after a `fail`; then
  the counts, such as `3 passed, 1 failed`.
- It exits with 0 when every case passed, and 1 when any failed, or when there was nothing to run: a project with no
  app, or no `.lua` files in `Tests/`.
- What the tests `print` goes to standard error.
- The app runs with the system files from the `rootfs/` beside airo, or in the repository above it.

## Projects

A project is a folder:

```
Dice.rproj/
  Project.xml   <Project version="1"><Info name="Dice"/></Project>
  App/          the app, laid out as a bundle's Contents/
  Interfaces/   loose .uix files, which airo doesn't build
  Tests/        the app's tests, which airo test runs and the .app never holds
```

[The projects design](../design/2026-11-20-roundhouse-projects-design.md) has the rest. A project is where an app is
designed and worked on, and the `.app` is what it builds. A built-in app's project and its shipped `.app` in
`rootfs/` can differ until the project is built into `rootfs/`.
