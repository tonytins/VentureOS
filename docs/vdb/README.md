# vdb/1

vdb sends an app to a running VentureOS and opens it, from another computer. This is the protocol both sides
follow: VentureOS's side is `Venture.OS.Common/Vdb`, and the computer's is the `vdb` tool in `tools/vdb`. Why it's
built this way is in [its design](../design/2026-11-18-vdb-design.md).

VentureOS's side is written and tested; it doesn't listen for connections yet, and the tool doesn't send yet.

## Lines

A connection is lines of UTF-8 text, each ending with `\n`; a `\r` before it is dropped. A line is at most 8,192
bytes, not counting its line break. Empty lines are ignored. A line that's too long or isn't UTF-8 closes the
connection, since there's no telling where the next one starts.

A line is a command, a space, and the rest. The computer sends commands; VentureOS answers.

## Answers

- `ok …` when a command worked.
- `error <reason>` when it didn't. The connection goes on, but an error during an install ends the install, and the
  reason ends `; <Name.app> was thrown away`.
- `error closing <reason>`, after which VentureOS closes the connection.
- `wait trust` while the person at VentureOS is asked whether to trust the computer.
- `out <text>`, a line an app printed, at any time once the computer is trusted. Control characters in it are
  spaces, and text past the line's length is cut off.

## Commands

| Command | Answer | Does |
|---|---|---|
| `hello vdb/1 <identity> [features…]` | `ok vdb/1 install launch`, perhaps after `wait trust` | Must come first. The identity is 32 lowercase hexadecimal characters the computer made for itself. |
| `install <Name.app>` | `ok` | Starts sending an app. One at a time. |
| `file <path> <size>` | at its `end` | Starts one of its files: a path inside the app, such as `Contents/Lua/main.lua`, and its size in bytes. The path may hold spaces; the size is the last word. |
| `data <base64>` | at its file's `end` | Some of the file's bytes, in whole base64: a multiple of four characters. A line holds up to 6,138 bytes. |
| `end` | `ok` | Ends the file, which must be exactly its size. |
| `commit` | `ok installed <path>` | Checks the app as VentureOS checks apps at boot, and installs it in `/mnt/user/apps`, closing and replacing any copy there. |
| `abort` | `ok` | Throws the app away. |
| `launch <Name.app>` | `ok` | Opens an app, as the dock would. |
| `bye` | `ok` | Ends the connection. |

After an error inside a file, the rest of that file's `data` lines and its `end` are passed over, so each file is
answered once: a computer can send a whole file and then read one answer.

## Limits

| What | Most |
|---|---|
| A line | 8,192 bytes |
| A file | 4 MiB |
| An app | 256 files, 16 MiB |
| A path | 255 characters, with no empty, `.`, or `..` part, and no `\` or `:` |
| Silence | 30 seconds, then `error closing` |
| Waiting to be trusted | 60 seconds, which counts as being refused |

## Conversations

[`conversations/`](conversations/) holds whole conversations, which both sides' tests replay. In each:

- `>` is a line the computer sends, and `<` one VentureOS sends back, in order.
- `!` sets up or checks VentureOS's side: `trusted <identity>`, `answer trust|deny <identity>`, `wait <seconds>`,
  `system-app <identifier>`, `print <text>` (an app prints it), `disconnect`, and the checks `closed`,
  `installed <Name.app>`, `not-installed <Name.app>`, `staging-empty`, `launched <Name.app>`, and
  `closed-app <identifier>`.
- `#` starts a comment.
- Paths VentureOS sends back are shown as they are on VentureOS, under `/mnt/user`.
