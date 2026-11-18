# Packaging

Scripts that build Roundhouse, VentureOS's IDE, into the packages people
download, one folder for each platform, after CityLimits' `packaging/`.
Every script runs on Linux or macOS with the .NET 10 SDK, since .NET
publishes for every platform from any of them, and writes to
`dist/release/`, which git ignores.

| Script | Builds |
|---|---|
| `linux/package.sh` | `roundhouse-<version>-linux-x86_64.AppImage` and `.tar.gz` |
| `windows/package.sh` | `roundhouse-<version>-windows-x64.zip`, Roundhouse in a folder |
| `macos/package.sh` | `roundhouse-<version>-macos-arm64.zip` and `-macos-x64.zip`, each holding `Roundhouse.app` |

```sh
packaging/linux/package.sh
packaging/windows/package.sh
packaging/macos/package.sh
```

Each takes `--framework-dependent` for a smaller package that needs the
.NET 10 runtime installed, and `--help`. Without it, the package carries
the runtime and needs nothing installed.

VentureOS itself isn't packaged here: it's an ISO, built with
`cosmos build`.

## What they share

`env.sh` sets up every script:

- It reads the version once, from GitInfo, the same major.minor.patch
  Roundhouse shows in its title and the changelog uses.
- It publishes Roundhouse without debug symbols and checks VentureOS's
  fonts came with it, in `fonts/`, with their licenses.
- It copies the readme, the UNLICENSE, and the changelog in.
- It runs the packaged Roundhouse with no window: `Roundhouse --render`
  draws Calculator's interface into a PNG, which needs the fonts, Skia,
  and VentureOS's own code, but no display.
- It downloads tools only at pinned versions, refusing a file whose
  SHA-256 isn't the one published for it.

| Variable | Default | Meaning |
|---|---|---|
| `ROUNDHOUSE_VERSION` | GitInfo's | The version the packages are named for |
| `DIST` | `dist/release` | Where the packages go |
| `WORK` | `dist/work` | Where they are put together, and the downloaded tools kept |

## The icon

Roundhouse's icon is a placeholder until it has its own art: an R on
VentureOS's blue running into its teal. Its one source is
`src/Venture.Roundhouse/Assets/Roundhouse.png`, square and at least
512 pixels. Replace it, then run `packaging/icons.sh`, which needs
ImageMagick 7 and python3, to remake the Windows `.ico` (in the
executable) and the macOS `.icns`, and commit all three. The Linux
packages and the window use the PNG itself.

## Linux

The AppImage carries its own runtime, so it runs without FUSE. It is
built with appimagetool 1.9.1 and the type 2 runtime from 2025-11-08,
CityLimits' pins, downloaded on the first run. `AppRun` starts Roundhouse
from inside the image without changing the folder, so a document's path
on the command line still means what it did, and `Roundhouse.desktop` is
its menu entry. The script runs the AppImage itself, as well as the
folder inside it.

## Windows

`Roundhouse.exe` carries the icon. The zip unzips anywhere. There is no
installer yet. The script checks the executable is built for x64, but
doesn't run it.

## macOS

`Roundhouse.app` holds the published Roundhouse in `Contents/MacOS`,
beside its `fonts`, the icon in `Contents/Resources/Roundhouse.icns`, and
`Info.plist`, from `Info.plist.in`. It needs macOS 14 or later, as .NET 10
does.

The app is signed ad hoc, not with a Developer ID, so Gatekeeper asks
before a downloaded copy first opens: Control-click the app and choose
Open. On a Mac, the script signs the whole bundle with `codesign`;
elsewhere, .NET has already signed the executable as it published it,
which Apple silicon needs before it runs anything. Signing with a
Developer ID and notarizing need a Mac and an Apple developer account.
