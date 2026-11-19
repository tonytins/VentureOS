# UIScript in VentureOS

VentureOS's interfaces are UIScript documents (`.uix`), and **VentureOS's UIScript is a superset of
[Hudini's](https://github.com/tonytins/Hudini/blob/main/docs/UISCRIPT.md)**. Hudini defines the format, and its spec
is the reference: VentureOS reads every document Hudini reads, the same way, and follows Hudini's rules for what it
doesn't know. On top of that, it reads a few things Hudini's spec doesn't have yet, each drafted here so Hudini can
take it in.

Write a document for Hudini's spec, and it works in VentureOS. Use one of VentureOS's additions, and it still works
in Hudini, in a plainer form, since each is safe to ignore (see [What VentureOS adds](#what-ventureos-adds)).

## What VentureOS reads

- **Hudini's core**, all of it, with the same rules: a newer `version` or an unknown `requires` module refuses a
  document, an unknown element refuses it, and an unknown attribute is skipped and listed.
- **A module of VentureOS's own**, [`shelf`](./shelf.md), drafted for Hudini.
- **Both of Hudini's modules**, [`menus`](https://github.com/tonytins/Hudini/blob/main/docs/UISCRIPT.md#menus) and
  [`sidebar`](https://github.com/tonytins/Hudini/blob/main/docs/UISCRIPT.md#sidebar). Hudini's own reader
  implements neither yet.
- **VentureOS's additions**, below.

VentureOS's reader is `src/Venture.OS.Interface/UIScript.cs`. Its tests read Hudini's own example documents, so the
two readers stay in step.

## What VentureOS adds

Each of these is an attribute a reader without it skips, giving a plainer but correct interface, so none needs a
`requires` name.

| Addition | What it does | Draft |
|---|---|---|
| `subtitle` on `<Window>` | A line under the window's title, in a taller bar | [Title bar](./title-bar.md) |
| `image` on `<ToolbarItem>` | Shows the item as a symbol in the text's color, its title as hover text | [Title bar](./title-bar.md) |
| `placement` on `<ToolbarItem>` | Puts the item at the bar's start, after the window's buttons | [Title bar](./title-bar.md) |
| `role` and `edge` on `<Window>` | Makes the window a panel, pinned to the top or bottom of the screen | [Panels](./panel.md) |

VentureOS also does things as a host that Hudini's spec leaves to each host, such as drawing a button's `image`,
ringed in the accent while checked; selecting text and using the clipboard in text areas; and giving a sidebar's
rows icons and groups, which apps set through `window:setItems` (see [Writing apps](../writing-apps.md)).

## Drafts

- [Title bar](./title-bar.md): `subtitle`, and toolbar items' `image` and `placement`. Not yet in Hudini's spec.
- [Panels](./panel.md): `role` and `edge` on `<Window>`. Not yet in Hudini's spec.
- [Shelf](./shelf.md): the `shelf` module and its `<Shelf>` control. Not yet in Hudini's spec.
- [Menus](./menus.md), [Sidebar](./sidebar.md), and [Styles](./styles.md): VentureOS's drafts of the `menus` and
  `sidebar` modules and of style roles. Hudini took all three into its spec on 2026-11-12, so these are kept as a
  record; Hudini's spec is the reference.

## Not yet

- A four-state button image holds normal, pressed, hovered, and disabled pictures, left to right, as Hudini's spec
  says. VentureOS shows the first and shades it for the other states.
