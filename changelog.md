# Changelog

## 0.1.117

- Commands now take arguments. Quotes group words (`help "net"`), and a missing quote or the wrong number of arguments is explained instead of ignored.
- `help` lists every command with a short description, and `help <command>` shows how to use one.
- New `info` command: version, build, commit, Cosmos version, uptime, and memory.
- New `net` command: the network adapter, its MAC address and link, and its IP address, subnet, and gateway.
- New `reboot` and `shutdown` commands, beside `halt`.
- Command names ignore case, so `HELP` works like `help`.

## 0.1.116

- If a part of the system it can't run without fails at boot (filesystem or timer), VentureOS now stops with a panic screen that names the failed step and the reason, instead of crashing without explanation.
- If networking fails at boot, VentureOS reports it and carries on without the network.
- Boot messages say which step failed, such as the RAM disk's format or mount step, a DHCP timeout, or a missing network card.
- A failing command reports its error and returns to the prompt instead of taking the system down.
- Boot messages, the version, and any panic report are also written to the serial log, prefixed with `[Venture]`, so a headless boot can be followed.
- The version and build are shown at the start of boot as well as when boot finishes.
- Updated to Cosmos 3.0.94.

## 0.1.102

- Filesystem
- Networking

## 0.1.101

Start of the project!