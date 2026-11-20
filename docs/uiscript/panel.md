# UIScript panels (draft)

> **Adopted.** Hudini took this draft into its spec's core on 2026-11-20, and added `minWidth` and `minHeight`, for a
> panel stretched across the screen, which VentureOS reads as a window's. Hudini's
> [spec](https://github.com/tonytins/Hudini/blob/main/docs/UISCRIPT.md#panel) is now the reference; this draft is kept
> as a record of how VentureOS first built it. See [UIScript in VentureOS](./README.md).

This is a draft addition to [Hudini's UIScript spec](https://github.com/tonytins/Hudini/blob/main/docs/UISCRIPT.md),
beside the **Window** section. VentureOS reads it first, since 0.7.3xx, and as its own element since 0.7.4xx; it's
written as a section of the spec so it can move there once it settles.

A document can describe a **panel** instead of a window: pinned to an edge of the screen, with no title bar, such as
a dock or a menu bar. The host keeps it above ordinary windows, doesn't let it be moved or focused, and keeps other
windows clear of the edge it's on.

```xml
<UIScript version="1" requires="shelf">
  <Panel width="400" height="72" edge="bottom"/>
  <Shelf name="apps" x="0" y="0" width="400" height="72"/>
</UIScript>
```

## Elements

`<Panel>` takes the place of `<Window>`: a document has exactly one of the two.

| Attribute | Required | Meaning |
|---|---|---|
| `width` | Yes | The panel's width, as a window's. |
| `height` | Yes | The panel's height, as a window's. |
| `edge` | Yes | `top` or `bottom`: the edge of the screen the panel is pinned to. |

## Rules

- An `edge` other than `top` or `bottom` refuses the document, so a panel never lands somewhere it wasn't meant to;
  a later edge needs a newer reader.
- A panel has no title bar and isn't resized by hand, so `<Panel>` has no `subtitle`, `resizable`, `minWidth`, or
  `minHeight`. Written anyway, they're skipped and listed, as unknown attributes are.
- A panel can't have toolbar items, a title field, or a sidebar. Any of these refuses the document.
- A panel may have menus. How the host shows them is the host's: VentureOS shows the menu bar in its top panel.
- A document with both a `<Window>` and a `<Panel>`, or two of either, is refused.

## Older readers

A reader without panels refuses `<Panel>`, as it refuses any element it doesn't know. That's deliberate: a dock or
a menu bar shown as an ordinary, movable window isn't a working fallback.

Until 0.7.4xx, VentureOS wrote panels as `<Window role="panel" edge="…">`. That form is gone, with no alias: on a
`<Window>`, `role` and `edge` are now unknown attributes, skipped and listed, and the window is an ordinary one.

## In VentureOS

- A top panel is the menu bar, and there's one at a time.
- A bottom panel is as wide as its content, centred, and follows the dock's setting: always visible, hiding on its
  own, or hiding when a window would cover it.
- See [the design](../design/2026-11-18-dock-app-design.md), written before panels had their own element.
