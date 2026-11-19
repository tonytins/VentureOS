# UIScript panels (draft)

This is a draft addition to the **Window** section of
[Hudini's UIScript spec](https://github.com/tonytins/Hudini/blob/main/docs/UISCRIPT.md). VentureOS reads it first,
since 0.7.3xx; it's written as a section of the spec so it can move there once it settles.

A window can be a **panel**: pinned to an edge of the screen, with no title bar, such as a dock or a menu bar. The
host keeps it above ordinary windows, doesn't let it be moved or focused, and keeps other windows clear of the edge
it's on.

```xml
<UIScript version="1" requires="shelf">
  <Window width="400" height="72" role="panel" edge="bottom"/>
  <Shelf name="apps" x="0" y="0" width="400" height="72"/>
</UIScript>
```

## Attributes

| Element | Attribute | Required | Meaning |
|---|---|---|---|
| `<Window>` | `role` | No | `window`, the default, or `panel`. |
| `<Window>` | `edge` | For a panel | `top` or `bottom`: the edge of the screen the panel is pinned to. |

## Rules

- A panel needs an `edge`. An `edge` other than `top` or `bottom` refuses the document, so a panel never lands
  somewhere it wasn't meant to; a later edge needs a newer reader.
- A `role` the reader doesn't know is skipped and listed, as unknown attributes are, and the window is an ordinary
  one.
- A panel has no title bar, so it can't have a `subtitle`, toolbar items, or a title field; and it can't be
  `resizable`, or have a sidebar. Any of these refuses the document.
- A panel may have menus. How the host shows them is the host's: VentureOS shows the menu bar in its top panel.

## Older readers

Both attributes are safe to skip, so neither needs a `requires` name. A reader without panels shows an ordinary
window with the panel's contents: plainer, and not pinned, but working.

## In VentureOS

- A top panel is the menu bar, and there's one at a time.
- A bottom panel is as wide as its content, centred, and follows the dock's setting: always visible, hiding on its
  own, or hiding when a window would cover it.
- See [the design](../design/2026-11-18-dock-app-design.md).
