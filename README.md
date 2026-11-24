
<p align="center">
<img src="./screenshot.png" width="450"/>
</p>
<hr>
<p align="center">
  <b>a fun hobby operating system written in C# using the Cosmos framework</b><br>
  macOS-style interface with <a href="https://en.wikipedia.org/wiki/Bundle_(macOS)">bundled</a> Lua desktop applications
</p>

<p align="center">
  <a href="https://github.com/tonytins/VentureOS/blob/main/UNLICENSE"><img src="https://img.shields.io/github/license/tonytins/VentureOS" alt="GitHub license"></a>
  <img src="https://img.shields.io/github/commit-activity/w/tonytins/VentureOS" alt="GitHub commit activity">
</p>

## 🚀Getting Started

See the Cosmos [installation guide](https://cosmosos.github.io/articles/user/install.html).

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) or later
- [Visual Studio Code](https://code.visualstudio.com/) or [Passenger IDE](https://passenger-ide.net)
- [Homebrew](https://brew.sh/) on macOS

#### Windows

Go to the [Releases](https://github.com/CosmosOS/Cosmos/releases) page on Cosmos' Github and download:

```
CosmosSetup-<version>-windows.exe
```

#### macOS/Linux

```
dotnet tool install -g Cosmos.Tools
cosmos install
```

### Building

From the repository root, `run.sh` checks the Cosmos tools, builds the kernel, and boots it in QEMU:

```
./run.sh              # boot with a window
./run.sh --headless   # boot with serial output only
./run.sh --vdb        # also forward vdb's port, 28455, to send apps in
```

To run each step yourself, point the Cosmos CLI at the kernel project:

```
dotnet test --project tests/Venture.OS.Common.Tests
cosmos check
cosmos build -p src/Venture.OS.Kernel
cosmos run -p src/Venture.OS.Kernel
```

### Using it

VentureOS boots to a shell. Type `help` to list the commands, and `desktop`
to open the desktop; VentureOS ▸ Return to Shell (Ctrl+Shift+Q) comes back.
[The tutorials](docs/tutorial/README.md) show how to add your own, from a shell
command to a desktop app.

### Roundhouse

Roundhouse is VentureOS's IDE, for Linux, macOS, and Windows. It needs only the .NET SDK, not the Cosmos tools. Open an app's folder, or any file in it, and its files are listed: a `.uix` interface opens in a designer that draws it as VentureOS does, beside its text; Lua and other text in an editor; `Info.xml` as a form. File ▸ New App makes a window app to start from:

```
dotnet run --project src/Venture.Roundhouse -- rootfs/user/apps/Calculator.app/Contents/Resources/main.uix
dotnet test --project tests/Venture.Roundhouse.Tests
```

Run ▸ Test (Ctrl+U) runs the open project's tests, its `Tests/*.lua`, against its app as you've edited it, saved or not, as [`airo test`](docs/airo/testing.md) does: each result goes to the Log tab, and the counts to the status line.

Run ▸ Run on VentureOS (Ctrl+Alt+R) sends the app you're editing, unsaved edits included, to a VentureOS running under `./run.sh --vdb`, opens it, and shows what it prints, as the [vdb tool](tools/vdb/README.md) does; it's trusted as the same computer.

Roundhouse keeps a log, shown in its Log tab and written to a file a day, kept for a week, in its own folder of your local application data (`~/.local/share/Roundhouse/logs` on Linux): what it opened, saved, and ran, what VentureOS's apps printed in Run, and any error.

Run ▸ Run (Ctrl+R) runs VentureOS's desktop in a window beside the designer, with the app you're designing open as you've edited it, saved or not. Each run starts from a fresh copy of VentureOS's files, and an app's files stay inside that copy.

To draw a document into a PNG file without opening a window:

```
dotnet run --project src/Venture.Roundhouse -- --render main.uix main.png --dark --accent "#3A944A"
```

To build Roundhouse into packages for Linux (an AppImage and a `.tar.gz`), Windows (a zip), and macOS (`Roundhouse.app` in a zip), each needing nothing installed, see [packaging](packaging/README.md):

```
packaging/linux/package.sh
packaging/windows/package.sh
packaging/macos/package.sh
```

### airo

airo is VentureOS's build tool. `airo build` builds a Roundhouse Project (`.rproj`) into the `.app` VentureOS runs, and writes it to the folder you name, such as `rootfs/user/apps/` when it's ready to ship. `airo test` runs the app's tests, Lua files in the project's `Tests/` that use the app as a person would ([how to write them](docs/airo/testing.md)). airo never asks anything, and exits with 1 and the reason when it can't do what it's asked or a test fails. See [its README](docs/airo/README.md).

```
dotnet run --project src/Venture.Airo -- build Dice.rproj --output rootfs/user/apps
dotnet run --project src/Venture.Airo -- test Dice.rproj
dotnet test --project tests/Venture.Airo.Tests
```

### vdb

[vdb](tools/vdb/README.md) sends an app to a running VentureOS and opens it, so you can try a change without rebuilding the kernel. Boot with `./run.sh --vdb`, open the desktop, and turn on Settings ▸ Developer; then, with Swift 6:

```
cd tools/vdb
swift run vdb install ../../rootfs/user/apps/Calculator.app --launch
```

VentureOS asks whether to trust your computer the first time. An app sent this way lasts until VentureOS restarts.

## ⚖️ No Copyright

Copyrights and any related rights for VentureOS are waived via the [UNLICENSE](UNLICENSE). Credit still appreciated.

The desktop's fonts keep their own licenses, which ship beside them in
`rootfs/system/fonts/`: Open Sans under the Apache License 2.0, and
Fira Code under the SIL Open Font License 1.1.

The Charger wallpaper is a photo by Tony Bark of Amtrak 315 at New Orleans,
[under CC0](https://commons.wikimedia.org/wiki/File:Amtrak_315_at_New_Orleans,_December_2024.jpg).
