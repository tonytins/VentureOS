# vdb

vdb sends an app to a running VentureOS and opens it, from your computer, so you can try a change on the real
system without rebuilding it. It stands in until VentureOS can keep files and install from a disk: an app it sends
lasts until VentureOS restarts.

vdb lives in VentureOS's repository for now, beside the kernel it talks to, and will move to a repository of its
own. It speaks [vdb/1](../../docs/vdb/README.md).

## Build

vdb needs Swift 6 and builds on Linux and macOS. It's written to build on Windows too, through Winsock, but that
hasn't been tried yet.

```sh
cd tools/vdb
swift build -c release      # .build/release/vdb
swift test
```

It uses [Swift Argument Parser](https://github.com/apple/swift-argument-parser),
[swift-log](https://github.com/apple/swift-log), [swift-crypto](https://github.com/apple/swift-crypto) for the
encryption, and [GitVersion](https://github.com/tonytins/GitVersion), which gives it VentureOS's version. `GitVersion.env` here tells GitVersion to count from `GitInfo.txt` rather than the
last release's tag, as the kernel does.

## Get VentureOS listening

1. Boot VentureOS with its port forwarded: `./run.sh --vdb` from the repository's root, under QEMU.
2. Type `desktop`. VentureOS listens only while the desktop runs.
3. Turn on developer mode: Settings ▸ Developer ▸ On. It lasts until VentureOS restarts.

The first time vdb connects, VentureOS asks whether to trust your computer. Choose **Trust** within 60 seconds.

To skip both steps on your own builds, run `vdb identity --lua` and put what it prints in
`rootfs/system/developer.local.lua`. Git ignores that file, so only your builds turn developer mode on at boot and
trust your computer.

## Use

```sh
vdb install Dice.app                  # send an app and install it in /mnt/user/apps, replacing any copy
vdb install Dice.app --launch         # and open it
vdb install Dice.app --launch --for 5 # and print what it prints for 5 seconds
vdb launch Dice.app --follow          # open an installed app, and print what it prints until Ctrl+C
vdb identity                          # this computer's identity, which VentureOS trusts
vdb --host 10.0.0.5 --port 28455 …    # another VentureOS; 127.0.0.1:28455 by default
```

What apps print goes to standard output, and everything vdb says itself to standard error, so an app's output can
be piped. vdb exits with 0 when everything worked, and 1 with VentureOS's reason when something didn't. It never
asks anything, so scripts can use it. `--verbose` logs every line both ways as it goes on the network, so after
hello they're sealed.

Your identity is kept in `~/.config/vdb/identity` (`$XDG_CONFIG_HOME` if it's set), or `%APPDATA%\vdb\identity` on
Windows, readable only by you, and Roundhouse's Run on VentureOS uses the same one, so VentureOS trusts both as one
computer. It's a secret: every connection is encrypted with keys made from it, and VentureOS
knows your computer by a hash of it, its key ID, which the Trust alert shows. Delete it, and vdb makes a new one,
which VentureOS asks about again.

## Limits

- Everything after hello is encrypted and checked (ChaCha20-Poly1305), so what's sent can't be read or changed on
  the way. Your identity crosses the network once: when you choose Trust on VentureOS, vdb sends it so VentureOS
  can make the keys. Putting it in `developer.local.lua` instead means it never does.
- There's no forward secrecy: whoever later gets your identity file could read conversations they recorded.
- An app is at most 256 files and 16 MiB, and a file at most 4 MiB. Symbolic links aren't sent.
- vdb sends `.app` bundles only, not themes, libraries, or wallpapers.
