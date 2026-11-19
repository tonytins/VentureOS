# vdb: sending apps to VentureOS

**Status:** built in 0.7.1xx, Seto; where this differs from what was built, [docs/vdb](../vdb/README.md) and the [tool's README](../../tools/vdb/README.md) say how it works. **Date:** 2026-11-18.

> 0.7.4xx encrypts the conversation and stops sending the identity; see [its design](./2026-11-19-vdb-encryption-design.md). Where they differ, that one wins.

vdb is a way to put an app on a running VentureOS from another computer, and open it, while VentureOS can't yet
keep files across reboots or install from a disk. It's named after Android's adb, which it borrows from in spirit:
a developer mode, a computer that has to be trusted first, and a command-line tool on the computer. It's the first
part of 0.7; the native Roundhouse, a text editor first and a designer later, comes after it and is designed
separately.

## Why

- Today an app reaches VentureOS only by being added to `rootfs/` and built into the kernel. Trying a change on the
  real system means a rebuild and a reboot.
- Roundhouse's Run runs VentureOS's desktop code, not its kernel. vdb closes the gap: what Run shows, the real
  system can run a moment later.
- Probes, the throwaway apps used to check something on the running system, can be sent in instead of built in.
- The native IDE will be built on VentureOS itself, and needs a quick way to get each build of it there.

## Goals

- `vdb install Dice.app` sends an app bundle to VentureOS, which checks it as it checks apps at boot and puts it in
  `/mnt/user/apps`, replacing an older copy.
- `vdb launch Dice.app`, or `vdb install --launch`, opens it, and what the app prints comes back to the terminal.
- Nothing happens unless developer mode is on, and nothing happens from a computer VentureOS hasn't been told to
  trust.
- A bad, partial, or hostile transfer is refused with a reason and leaves nothing behind. vdb never stops the system.

## Non-goals, for now

- A remote shell, pushing or pulling arbitrary files, or streaming the system's log. The protocol leaves room for
  them; none is built.
- Themes, libraries, and wallpapers. Only `.app` bundles are sent.
- Listening from the shell. VentureOS listens only while the desktop runs (see [Where it listens](#where-it-listens)).
- Keeping installed apps or trust across a reboot. That waits on persistence, like everything else in memory.
- Roundhouse sending apps itself. It can later, by following this document; it doesn't in this band.

## Anti-goals

- **Security theatre.** Cosmos has no TLS and no cryptography to sign with. Trust here guards against accidents, such
  as the wrong VM or a stray connection, not against someone who can watch the network. The docs say so plainly.
- **A second way to read bundles.** VentureOS checks a sent app with the same `Bundle.Read` it uses at boot; vdb adds
  no rules of its own about what an app is.
- **A prompt that can't be answered.** A headless or automated boot must be able to use vdb with no one at the
  screen, by trusting a computer ahead of time on that machine's own builds.

## Approaches considered

1. **Plain-text commands, with each file's bytes in base64 lines** (chosen). Every message is a line of text, so the
   protocol can be read by eye, tested from recorded conversations, and parsed with a line reader and nothing else.
   Base64 costs a third more bytes, which doesn't matter on a local link, and keeps binary data from ever being
   mistaken for a command.
2. **A tar archive sent at once.** One transfer, but the kernel would need a tar reader, one more format crossing the
   boundary; zip would also need deflate, which Cosmos may not have.
3. **HTTP.** Usable from `curl`, but the kernel would need an HTTP parser, far more to validate than a line reader.

The owner chose 1, with base64 for file contents.

## Developer mode

- Off at every boot. While it's off, VentureOS doesn't listen at all.
- On from **Settings ▸ Developer**, a third pane with one switch, and the port vdb listens on. Like every setting, it
  lasts until the next reboot.
- On from a script: `venture.developer.enable()`, under the `system` scope, so `init.lua` can turn it on at each boot.
- `venture.developer.trust(identity)` trusts a computer ahead of time (see below), and `venture.developer.status()`
  says whether it's on, the port, and how many computers are trusted.

### Trusting a computer ahead of time

`rootfs/system/init.lua` ends by running `rootfs/system/developer.local.lua` when the build has one. That file is
git-ignored (`*.local.*`, added to the repository's `.gitignore`, since contributors don't share the owner's global
one), so it's built into the owner's own kernels and never into anyone else's. It holds, for example:

```lua
venture.developer.enable()
venture.developer.trust("4f1c…")  -- printed by `vdb identity`
```

`vdb identity --lua` prints those two lines, ready to paste.

## Trust

- vdb makes an **identity** the first time it runs: 32 random hex characters, kept in the computer's config folder
  (`~/.config/vdb/identity` on Linux and macOS, `%APPDATA%\vdb\identity` on Windows). `vdb identity` prints it.
- The first time an untrusted identity connects, the desktop shows an alert: *"Trust this computer? It can install
  and open apps until VentureOS restarts."*, with the identity's first eight characters, and **Trust** and **Deny**.
  vdb says it's waiting. No answer in 60 seconds counts as Deny.
- Trusted identities are kept in memory only.

## Where it listens

Sockets in Cosmos can be used from one thread only, and the shell blocks in `Console.ReadLine()` while it waits, so
nothing else runs then. The desktop's frame loop already runs every frame; the listener is checked there, once a
frame, without blocking: accept a waiting connection, read what has arrived, and handle whole lines. Opening an app
needs the desktop anyway, and so does the Trust alert.

- One connection at a time; a second is answered `error busy` and closed.
- A connection idle for 30 seconds is closed.
- Leaving the desktop closes the connection and stops listening; opening it again starts again if developer mode is on.
- The port is **28455** by default. Under QEMU, `run.sh --vdb` forwards the same port on the host with
  `--hostfwd tcp::28455-:28455`.

## The protocol, vdb/1

Lines of UTF-8 text, each ending with `\n` (a `\r` before it is ignored), at most 8,192 bytes. The full reference, as built, is [docs/vdb](../vdb/README.md). Words are split by
single spaces; the last field of a line may contain spaces where noted. The computer sends commands; VentureOS
answers each with `ok …` or `error <reason>`, and an error ends the command, not the connection, unless it says
`error closing <reason>`.

```text
> hello vdb/1 4f1c0d2e9a7b44c18e3f5a6b7c8d9e0f
< wait trust                      (only while the Trust alert is up)
< ok vdb/1 install launch
> install Dice.app
< ok
> file Contents/Info.xml 241
> data PD94bWwgdmVyc2lvbj0iMS4wIj8+Cjxbm…
> end
< ok
> file Contents/Lua/main.lua 812
> data …
> data …
> end
< ok
> commit
< ok installed /mnt/user/apps/Dice.app
> launch Dice.app
< ok
< out Rolled a 4
< out Rolled a 2
> bye
< ok
```

- **`hello vdb/<n> <identity> [features…]`**: must come first. VentureOS answers with the version it speaks and the
  features it supports. An unknown version is `error closing unsupported version, this VentureOS speaks vdb/1`.
  Features are how later additions, such as `shell`, are offered without a new version; a computer uses only those
  VentureOS lists.
- **`install <Name.app>`**: starts a bundle. The name is a folder name ending in `.app`, with no `/`, `\`, or leading
  dot. Only one install at a time.
- **`file <path> <size>`**, then any number of **`data <base64>`** lines, then **`end`**: one file, by its path inside
  the bundle and its size in bytes once decoded. Each `data` line is whole base64 (a multiple of four characters).
  At `end`, the decoded bytes must be exactly `size`.
- **`commit`**: VentureOS checks the bundle and installs it (see below). **`abort`** throws it away.
- **`launch <Name.app>`**: opens the app as the dock would. While the connection stays open, everything apps print is
  sent back as `out <text>` lines.
- **`bye`**: ends the connection.

### What's checked

Everything that crosses the connection is checked before it's used, and a failure is an `error` with the reason:

- A line longer than 8,192 bytes, text that isn't UTF-8, or an unknown command.
- A path that's empty, absolute, has a `\`, an empty, `.`, or `..` segment, a control character, or is longer than 255
  characters; a path sent twice.
- A size that isn't a whole number from 0 to 4 MiB; base64 that doesn't decode; more bytes than the size said, or fewer
  at `end`.
- More than 256 files or 16 MiB in one bundle.
- Commands out of order, such as `file` outside an install, or `commit` while a file is open.

### Installing

1. Files are written to a staging folder, `/mnt/user/.vdb/<Name.app>`, emptied when an install starts.
2. At `commit`, `Bundle.Read` checks the staged bundle as boot checks apps. A refusal is sent back with its reason
   and the staging folder is removed.
3. An app whose identifier belongs to a system app, in `/mnt/system/apps`, is refused: vdb can't replace those.
4. If the app is open, it's closed. The old copy in `/mnt/user/apps`, if any, is removed, and the staged folder is
   moved there.
5. A connection that ends mid-install, or `abort`, removes the staging folder.

## The tool

A Swift package in `tools/vdb/`, in this repository until it's moved to one of its own. It builds with Swift 6 on
Linux, macOS, and Windows, and uses Swift Argument Parser and swift-log, both approved by the owner on 2026-11-18.

```sh
vdb identity [--lua]                  # this computer's identity, or the lines for developer.local.lua
vdb install Dice.app [--launch]       # send an app, and open it
vdb launch Dice.app [--follow|--for N] # open an installed app; print what apps say until Ctrl+C, or for N seconds
vdb --host 10.0.0.5 --port 28455 …     # default: 127.0.0.1:28455, which run.sh --vdb forwards to QEMU
```

- It talks TCP through the platform's own sockets (POSIX, or Winsock on Windows) behind one small type, rather than
  adding a networking package.
- It exits 0 on success and non-zero with VentureOS's reason on stderr otherwise, so scripts can use it. Logging
  (`--verbose`) goes to stderr through swift-log.
- It never prompts; it waits up to 70 seconds for the Trust alert and says it's waiting.
- As built, following output is opt-in (`--follow`, `--for`), so a plain `launch` returns once the app opens; while
  following, vdb sends an empty line every 10 seconds, which VentureOS ignores but counts, so the idle limit doesn't
  close the connection. After a refusal, it says `bye` before exiting. See [its README](../../tools/vdb/README.md).

## Where the code goes

- **`Venture.OS.Common/Vdb/`**: the protocol, with no sockets: a line reader, the session's state (hello, install,
  file, launch), checking, and staging through `FileRoot`. It's given bytes and returns lines to send, so it's tested
  with xUnit like everything else in Common, including recorded conversations.
- **`docs/vdb/`**: the protocol's reference, and the recorded conversations in `docs/vdb/conversations/*.txt`, read by
  both the C# tests and the Swift tests, so the two sides can't drift apart.
- **The kernel**: a small adapter that wraps `TcpListener` and `NetworkStream` without blocking and hands bytes to the
  session from the desktop's frame loop; the Trust alert; `venture.developer` in `VentureLibrary`.
- **`rootfs/system/apps/Settings.app`**: the Developer pane.

## Unverified, to settle first

- That Cosmos 3.0.97's `TcpListener.Pending()` and `NetworkStream.DataAvailable` (or a zero-timeout read) work
  without blocking. Its plugs include `TcpListener`, `AcceptTcpClient`, and `NetworkStream`, but the non-blocking
  calls aren't confirmed. The first phase is a throwaway boot that checks them. If they block, the fallback is the
  connection's own socket with `Poll(0, SelectRead)`, and if that blocks too, the design comes back to the owner.
- That `Convert.FromBase64String` (or `Convert.TryFromBase64Chars`) works under Cosmos's compiler.
- That a large transfer survives Cosmos's TCP, which doesn't resend lost data. Over QEMU's port forwarding it should;
  a lost segment would stall the transfer, which the 30-second idle limit ends with an error rather than a hang.
- Whether app output can be told apart by app. If not, `out` lines carry everything apps print while connected.

## Left to the builder

- Exact names of types and Lua functions, and the Developer pane's layout.
- How the Swift tests read the shared conversations.
- How the 30-second idle limit and the 60-second trust limit are timed, within the frame loop's clock.
