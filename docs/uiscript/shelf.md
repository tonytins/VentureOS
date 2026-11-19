# UIScript shelf (draft feature)

This is a draft addition to [Hudini's UIScript spec](https://github.com/tonytins/Hudini/blob/main/docs/UISCRIPT.md),
as a module. VentureOS reads it first, since 0.7.3xx; it's written as a section of the spec so it can move there
once it settles.

A **shelf** is a row of icons, such as a dock's: each with a label shown under the pointer and a dot under the ones
that are open, in groups with a divider between.

```xml
<UIScript version="1" requires="shelf">
  <Panel width="400" height="72" edge="bottom"/>
  <Shelf name="apps" x="0" y="0" width="400" height="72"/>
</UIScript>
```

## Elements

`<Shelf>` is a control, and follows the rules for controls: a name, unique among the document's names, and a frame.
It takes nothing else.

| Attribute | Required | Value |
| --- | --- | --- |
| `name` | Yes | The name the host and the app use for it. |
| `x`, `y`, `width`, `height` | As any control | Where it sits. |

A shelf is a required feature: a document that uses one names `shelf` in `requires`, so a reader without shelves
refuses it, saying why, rather than tripping over an element it doesn't know.

## Items

What a shelf shows is the app's to give, as a list's items are. In VentureOS, `window:setItems(name, items)` takes
a table for each item: `title`, `icon` (a PNG), `open`, and `group`, a number; a divider goes between items whose
groups differ. A click on an item is a `pressed` event with its `index`.

## Older readers

None: a reader without shelves refuses the document, by design.
