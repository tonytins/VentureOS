# VentureOS roadmap

> **Last updated:** 2026-11-12 · **Release in development:** 0.4, Pere Marquette

Where VentureOS is going: the release in development, the ones after it, and
ideas not yet given a release. What has shipped is in
[docs/releases](./releases/), and verified status is in the
[scorecard](./scorecard.md). The roadmap VentureOS was planned with until 0.4
is archived in [docs/history](./history/roadmap-2026.md).

## How VentureOS releases

VentureOS is a hobby operating system with its own apps, so it's planned in
releases, which are what people get.

- **No schedule.** A release ships when its milestones land, not on a date.
- **Versions** follow the .NET SDK's feature bands: `0.4` is the release, each
  milestone in it takes a band (`0.4.1xx`, `0.4.2xx`, ...), and the last two
  digits count builds within the band. A build's version is the one the kernel
  reports, from `GitInfo.txt` and the commits since. `GitInfo.txt` moves to a
  milestone's band (`0.4.500`) when work on it begins; a new release
  (`0.5.100`) waits for my word.
- **Stages.** A release is *considered* (a theme and candidate features),
  then *in development* (bands with checklists), then *released*: tagged
  (`v0.3.18`), with notes in [docs/releases](./releases/). Published ISOs come
  later.
- **Support.** Until 1.0, only the latest release is supported.
- **1.0** is whenever I feel VentureOS is ready. There's no gate for it.
- **Names.** Each release has a code name, and the milestones in it plain
  ones. From 0.5, code names are Japanese sleeper trains; when those run out,
  Japanese limited expresses. 0.4, Pere Marquette, is the last named after an
  Amtrak route that runs Siemens Venture cars, the trains VentureOS is named
  after.
- **Apps** don't declare which VentureOS they need yet.

Keep it current: tick a band's items as they land, move a release from one
stage to the next, and add a line to the revision history.

## Releases

| Release | Code name | Stage | Holds |
|---|---|---|---|
| 0.3 | (the desktop) | released 2026-11-11, `v0.3.18`; [notes](./releases/0.3.md) | The desktop, its apps, Settings, and the look |
| 0.4 | Pere Marquette | in development; every band done, release is my call | Standard menus, text selection, Edit, networking, and Undo and Redo |
| 0.5 | (a sleeper train, when it's planned) | considered | Nothing chosen yet; see Ideas |

## 0.4, Pere Marquette

The bridge into releasing as an OS. It gives every app the menus a global
menu bar implies, edits text the same way everywhere, and brings
networking. Whatever a standard menu offers, every app gets, with no code of
its own.

**0.4.1xx, standard menus**

- [x] File, with Close Window, and Help, with About the app from its
  `Info.xml`, including an optional `description`.
- [x] `role` on `<Menu>`, or a menu's exact title, merges an app's menu into
  the standard one; an app's own shortcut wins over a standard item's.
- [x] The shipped apps drop their own Close Window, and describe themselves.

**0.4.2xx, text selection**

- [x] Select text in fields, areas, and the title field by dragging, with
  Shift and the arrows, and all at once.

**0.4.3xx, Edit**

- [x] A clipboard, and Cut, Copy, Paste, and Select All for whatever text
  has focus, in every app.

**0.4.4xx, menus always there, and networking**

- [x] File, Edit, and Help always on the bar, their items dimmed when they
  can't act.
- [x] `net` shows the DNS server DHCP registered, and `resolve` and `ping`
  use Cosmos's DNS and ICMP clients.

**0.4.5xx, Undo and Redo**

- [x] Edits recorded as values (where, what was there, what replaced it) at
  the one place every change to text passes through, so Undo applies the
  inverse and Redo the edit again.
- [x] Typing grouped into steps, broken by a space, a caret jump, or a paste.
- [x] A history for each text field, area, and title field, capped at 100
  steps, and cleared when the app sets the text itself.
- [x] Edit's Undo (Ctrl+Z) and Redo (Ctrl+Shift+Z), dimmed when there's
  nothing to undo or redo.

## Ideas

Worth doing someday, in no order, and not yet given a release.

- **Persistence:** keeping files, apps, and settings across reboots on a
  real disk, which the RAM disk can't. Installing apps from outside the
  system waits on it.
- **Undo for apps:** apps registering their own steps, such as deleting a
  note, beside text's.
- **Published ISOs** from tagged releases.
- **Apps declaring the VentureOS they need,** with a rule for deprecating
  what they use.
- **The full CST specification,** through CST.NET once it follows it, in
  place of the transitional reader.
- **Text:** selecting from read-only text, such as Terminal's output;
  dragging to select in the title field; double-clicking a word.
- **Windows:** resizing Terminal; collapsing a sidebar in a narrow window.
- **Lua:** the clipboard for apps.
- **ARM64:** booting it under QEMU, not only building it.
- **Hudini:** adopting VentureOS's UIScript drafts (menus, sidebars, and
  styles) in its own spec.

## Risks

- Cosmos renames APIs between releases (3.0.87 to 3.0.94 moved the
  filesystem, timer, network, and storage namespaces), and its docs follow
  the latest release only.
- Only `Venture.OS.Common`, `Venture.OS.Interface`, and the Lua system files
  are tested; kernel code is checked by booting by hand.
- `Cosmos.Executable.Lua` is young (4.0.1, from 2026-10); a bug there lands
  in every script.
- Nothing has run on real hardware, and the Cosmos docs warn against testing
  filesystem code there.
- The Cosmos network stack doesn't retransmit TCP, and its ARP wait is
  bounded by a count of tries rather than a time.

## Revision history

| Date | Change | Summary |
|---|---|---|
| 2026-11-12 | minor | 0.4.5xx done: Undo and Redo for every text field, area, and title field. Every band of 0.4 is done. |
| 2026-11-12 | major | The roadmap starts over for an evolving OS, by my decision: releases and their stages, bands without hour estimates, an Ideas list, and 1.0 whenever I'm ready. The route milestones and their history moved to [docs/history](./history/roadmap-2026.md). 0.4 gains a fifth band, Undo and Redo. Code names from 0.5 are Japanese sleeper trains, then Japanese limited expresses. |
