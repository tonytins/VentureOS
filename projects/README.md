# Projects

Roundhouse Projects (`.rproj`) for VentureOS's built-in apps, and other work in progress. A project is where an app
is designed, in Roundhouse, and tested, with [airo](../docs/airo/README.md). The apps VentureOS ships are the `.app`
folders in `rootfs/`, built from these.

| Project | Ships as |
|---|---|
| `Dock.rproj`, `Files.rproj`, `Settings.rproj`, `SystemMonitor.rproj`, `Terminal.rproj` | `rootfs/system/apps/` |
| `Calculator.rproj`, `Notes.rproj`, `TextEdit.rproj` | `rootfs/user/apps/` |
| `about.rproj` | `rootfs/system/interfaces/about_system.uix`, About VentureOS, copied there by hand, since it isn't an app |

## Test an app

```
dotnet run --project src/Venture.Airo -- test projects/Settings.rproj
```

Each app's tests are in its `Tests/` folder, written in Lua. [Testing an app](../docs/airo/testing.md) has how to
write them. VentureOS's own tests, in `tests/`, cover the system rather than these apps, so a change to an app's
design is checked by its own tests alone.

## Ship an app

A project and its shipped app may differ while it's being worked on. When it's ready, build it into `rootfs/`,
replacing the one there:

```
dotnet run --project src/Venture.Airo -- build projects/Settings.rproj --output rootfs/system/apps --replace
```

Then build VentureOS as usual, and the new app is in it.
