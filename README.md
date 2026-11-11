# VentureOS

<p align="center">
<img src="./ventureos-screenshot.png" width="450"/>
</p>
<hr>
<p align="center">
  <a href="https://github.com/tonytins/VentureOS/blob/main/UNLICENSE"><img src="https://img.shields.io/github/license/tonytins/VentureOS" alt="GitHub license"></a>
  <img src="https://img.shields.io/github/commit-activity/w/tonytins/VentureOS" alt="GitHub commit activity">
</p>

Named after the Siemens' [passenger cars](https://en.wikipedia.org/wiki/Siemens_Venture), VentureOS is my hobby operating
system written in C# and based on the [Cosmos](https://github.com/CosmosOS/Cosmos) framework. It is the spiritual successor to my earlier project, [TOMAS](https://github.com/tonytins/tomas/).


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
```

To run each step yourself, point the Cosmos CLI at the kernel project:

```
dotnet test --project src/Venture.OS.Common.Tests
cosmos check
cosmos build -p src/Venture.OS
cosmos run -p src/Venture.OS
```

### Using it

VentureOS boots to a shell. Type `help` to list the commands, and `desktop`
to open the desktop; VentureOS ▸ Return to Shell (Ctrl+Shift+Q) comes back.
[Writing apps](docs/writing-apps.md) shows how to add your own.

## ⚖️ No Copyright

Copyrights and any related rights for VentureOS are waived via the [UNLICENSE](UNLICENSE). Credit still appreciated.

The desktop's fonts keep their own licenses, which ship beside them in
`src/Venture.OS/system/fonts/`: Open Sans under the Apache License 2.0, and
Fira Code under the SIL Open Font License 1.1.
