# vdb/1

vdb sends an app to a running VentureOS and opens it, from another computer. This is the protocol both sides
follow: VentureOS's side is `Venture.OS.Common/Vdb`, and the computer's is the `vdb` tool in `tools/vdb`, or
Roundhouse's Run on VentureOS, through Common's `VdbClient`. Why it's
built this way is in [its design](../design/2026-11-18-vdb-design.md), and why and how it's encrypted in
[that design](../design/2026-11-19-vdb-encryption-design.md).

VentureOS listens while the desktop runs and developer mode is on (Settings ▸ Developer, or
`venture.developer.enable()`); under QEMU, `./run.sh --vdb` forwards the port. The [`vdb` tool](../../tools/vdb/README.md) sends apps.

## Lines

A connection is lines of UTF-8 text, each ending with `\n`; a `\r` before it is dropped. A line is at most 8,192
bytes, not counting its line break. Empty lines are ignored, but count as being heard from, so a computer that's
only listening can send one to keep the connection open. A line that's too long or isn't UTF-8 closes the
connection, since there's no telling where the next one starts.

A line is a command, a space, and the rest. The computer sends commands; VentureOS answers.

## Hello and the seal

Only hello, and pairing, are plain text. Everything after VentureOS's `hello` is sealed.

1. The computer says `hello vdb/1 <key-id> <nonce> [features…]`. The **identity** is 32 lowercase hexadecimal
   characters the computer made for itself, and a secret. The **key ID** names it without giving it away: the first
   16 hexadecimal characters of SHA-256 of the ASCII text `vdb key-id ` and the identity. The **nonce** is 16 random
   bytes, as 32 lowercase hexadecimal characters, new for every connection.
2. A computer VentureOS trusts is answered `hello vdb/1 <nonce>`, VentureOS's own nonce, then, sealed,
   `ok vdb/1 install launch`, and `ui` after them when VentureOS can offer [its commands](#ui).
3. One it doesn't is answered `wait trust` while the person is asked. If they choose **Trust**, VentureOS sends
   `pair`, and the computer sends `pair <identity>`, the one time the identity crosses the network. If it gives the
   key ID, VentureOS trusts it and answers as in step 2; if not, `error closing`.

From VentureOS's `hello` on, every line in both directions is `sealed <base64>`:

- The base64 holds a ChaCha20-Poly1305 ciphertext (RFC 8439) of the line's UTF-8, without its line break, then its
  16-byte tag. There's no additional data.
- Each direction has its own 32-byte key, from HKDF-SHA-256 (RFC 5869): the identity's 16 bytes are the secret,
  the computer's nonce then VentureOS's are the salt, and the info is `vdb/1 to venture` for the computer's lines
  and `vdb/1 to computer` for VentureOS's.
- A line's 12-byte nonce is four zero bytes and how many lines were sent that way before it, as a little-endian
  64-bit number. It isn't sent, so a line dropped, repeated, or moved fails to open.
- A line that doesn't open ends the connection at once, with nothing sent back, and an app being sent is thrown
  away. A sealed line is at most 10,951 bytes, an 8,192-byte line sealed.
- The empty line that keeps a connection open is sealed too.

This keeps what's sent from being read or changed on the way. It doesn't give forward secrecy: whoever later has a
computer's identity file can read that computer's earlier conversations, if they recorded them. VentureOS's nonces
come from its timestamp counter hashed with a count, since Cosmos has no random generator; the computer's nonce,
from its own system's generator, keeps the keys fresh regardless.

## Answers

- `ok …` when a command worked.
- `error <reason>` when it didn't. The connection goes on, but an error during an install ends the install, and the
  reason ends `; <Name.app> was thrown away`.
- `error closing <reason>`, after which VentureOS closes the connection.
- `wait trust` while the person at VentureOS is asked whether to trust the computer, and `pair` once they have.
- `out <text>`, a line an app printed, at any time once the computer is trusted. Control characters in it are
  spaces, and text past the line's length is cut off.

## Commands

| Command | Answer | Does |
|---|---|---|
| `hello vdb/1 <key-id> <nonce> [features…]` | `hello vdb/1 <nonce>`, then `ok vdb/1 install launch`, perhaps after `wait trust` and pairing | Must come first, in plain text. See [Hello and the seal](#hello-and-the-seal). |
| `pair <identity>` | as `hello` | Only when VentureOS has asked with `pair`, in plain text. |
| `install <Name.app>` | `ok` | Starts sending an app. One at a time. |
| `file <path> <size>` | at its `end` | Starts one of its files: a path inside the app, such as `Contents/Lua/main.lua`, and its size in bytes. The path may hold spaces; the size is the last word. |
| `data <base64>` | at its file's `end` | Some of the file's bytes, in whole base64: a multiple of four characters. A line holds up to 6,138 bytes. |
| `end` | `ok` | Ends the file, which must be exactly its size. |
| `commit` | `ok installed <path>` | Checks the app as VentureOS checks apps at boot, and installs it in `/mnt/user/apps`, closing and replacing any copy there. |
| `abort` | `ok` | Throws the app away. |
| `launch <Name.app>` | `ok` | Opens an app, as the dock would. |
| `bye` | `ok` | Ends the connection. |
| `windows`, `snapshot`, `act`, `key`, `screenshot` | see [ui](#ui) | Only when VentureOS offers `ui`; otherwise they aren't commands. |

After an error inside a file, the rest of that file's `data` lines and its `end` are passed over, so each file is
answered once: a computer can send a whole file and then read one answer.

## ui

With the `ui` feature, a computer sees and uses app windows by name, with the words app tests use
([`app:press`, `app:choose`, ...](../airo/testing.md#the-app)), rather than by pointing. It reaches only windows an
app opened, its alerts among them: never a panel, such as the dock or the menu bar, or one of VentureOS's own, such
as the Trust alert.

Its lines are words separated by spaces. A word is bare, or quoted in `"`, with `\\`, `\"`, `\n`, `\r`, and `\t`
escaped; other control characters become spaces. Answers are `key=value` words, a bare key being a flag that's on,
and **a reader skips keys it doesn't know**, so later versions can say more without a new feature. A quoted value
holds at most 600 characters, so every line fits; a line with a value cut short ends with the flag `cut`.

| Command | Answer |
|---|---|
| `windows` | A `window` line for each app window, front first: `id`, `app`, `title`, and the flags `front` and `minimized`. Then `ok`. |
| `snapshot <id>` | A `window` line (`id`, `title`, `subtitle`, `pane`, `focused`), then a `control` line for each control, the sidebar, the title field, each toolbar item, and each menu item, in that order: `name`, `kind`, `text`, `value`, `chosen` (from 1), `rows` (how many), and the flags `hidden`, `dimmed`, `checked`, and `focused`. A list's, sidebar's, or grid's first 100 rows follow its line, as `row "<text>"`. Then `ok`. |
| `act <id> press <name>` | Presses a button, toolbar item, or menu item. |
| `act <id> submit <name> "<text>"` | Puts the text in a field, or the title bar's field, and presses Enter. |
| `act <id> type <name> "<text>"` | Puts the text in a field or text area, without Enter. |
| `act <id> choose <name> <row>` | Chooses a list's or sidebar's row, or a grid's item: its number, from 1, or its text or a grid item's name, quoted. |
| `key <id> <key>…` | Presses up to 32 keys: a character, or `Space`, `Enter`, `Escape`, `Tab`, `Backspace`, `Delete`, `Left`, `Right`, `Up`, `Down`, `Home`, or `End`, after any of `Ctrl+`, `Shift+`, and `Alt+`, such as `Ctrl+Shift+Z`. |
| `screenshot` | `screen width=<w> height=<h>`, then the pixels in `data` lines, three bytes each, red, green, then blue, a row at a time from the top, then `ok`. |

- A `kind` is the element, as a document writes it, starting lowercase (`button`, `textField`, `grid`, ...), or
  `sidebar`, `titleField`, `toolbarItem`, or `menuItem`. `kind` is never quoted.
- `act` and `key` bring their window to the front, restoring it if it was minimized, as a click would. An action
  is answered `ok` once the app has handled it, in the same frame, or `error` with why a person couldn't do it,
  such as `"ok" is dimmed, so a person couldn't use it`; nothing is done then. Keys reach the window on the
  desktop's next frame, so a `snapshot` right after may not show them yet.
- A `<id>` is a window's number, as `windows` gives it; one that isn't an app window's is `error`.
- A screenshot goes out 16 lines a frame, so a large one takes a few frames. Commands sent meanwhile are answered
  after its `ok`, and the wait doesn't count toward the 30 seconds of silence.

## Limits

| What | Most |
|---|---|
| A line | 8,192 bytes, and 10,951 sealed |
| A file | 4 MiB |
| An app | 256 files, 16 MiB |
| A path | 255 characters, with no empty, `.`, or `..` part, and no `\` or `:` |
| Silence | 30 seconds, then `error closing` |
| Waiting to be trusted | 60 seconds, which counts as being refused |

## Conversations

[`conversations/`](conversations/) holds whole conversations, which both sides' tests replay. In each:

- `>` is a line the computer sends, and `<` one VentureOS sends back, in order. After VentureOS's `hello`, lines are
  written as they read inside the seal, and each side's replay seals and opens them; the computer's nonce is in its
  `hello`, and VentureOS's is always `f0f1…feff`.
- `! identity <identity>` names the computer's identity, which the replay keys the connection from, and `! raw` says
  the lines from there on are written as they go on the network, already sealed.
- `!` also sets up or checks VentureOS's side: `trusted <identity>`, `answer trust|deny <key-id>`, `wait <seconds>`,
  `system-app <identifier>`, `print <text>` (an app prints it), `disconnect`, and the checks `closed`,
  `installed <Name.app>`, `not-installed <Name.app>`, `staging-empty`, `launched <Name.app>`, and
  `closed-app <identifier>`.
- `! desktop`, anywhere, gives VentureOS a desktop, so it offers `ui`. Then `app-window <app> <title>` opens a sample
  window, `panel <app>` an app's panel, and `system-alert <title>` one of VentureOS's own, numbered from 1 in the
  order they open; `screen <width> <height>` sets the screen, its bytes counting up from 0; and `event <id> <kind> <control> [text or row]`, `keys <id> <key>, …`, and `front <id>` check what a window was
  handed and which is in front. The sample's controls are in `FakeVdbDesktop`, in Common's tests.
- `#` starts a comment.
- Paths VentureOS sends back are shown as they are on VentureOS, under `/mnt/user`.

[`sealed-lines.txt`](conversations/sealed-lines.txt) is written raw by [`sealed-lines.py`](sealed-lines.py), with
Python's `cryptography` package, so VentureOS and the tool are each checked against sealed lines neither of them
made.
