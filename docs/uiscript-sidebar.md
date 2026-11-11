# UIScript sidebar (draft feature)

This is a draft addition to the [UIScript format](https://github.com/tonytins/Hudini/blob/main/doc/UISCRIPT.md),
which Hudini defines. VentureOS reads it first; it's written as a section of
the UIScript spec so it can move there once it settles.

A document can give its window a sidebar: a list of places down the left,
such as a file manager's bookmarks, as Nautilus and Finder have. A sidebar
is a required feature: a document that uses one names `sidebar` in
`requires`, so a reader without sidebars refuses it rather than showing a
window that has lost its way around.

VentureOS reads this feature today. Its desktop draws sidebars from
Downeaster phase 3 onward.

```xml
<?xml version="1.0" encoding="UTF-8"?>
<UIScript version="1" requires="sidebar">
  <Window width="360" height="300"/>

  <Sidebar name="places" width="130"/>

  <List name="entries" x="10" y="10" width="340" height="280" selectable="true"/>
</UIScript>
```

## Elements

`<Sidebar>` follows the document rules: it's a direct child of
`<UIScript>`, doesn't nest, and holds no text. A document has at most one.

| Attribute | Required | Value |
| --- | --- | --- |
| `name` | Yes | The name the host uses for it, as for a control. |
| `width` | Yes | Its width, a positive number. |

## Rules

- The sidebar's name shares the namespace of controls, visuals, and menus.
- A sidebar has no position. It sits beside the content rather than in it,
  so `<Window>`'s size, and every control's `x` and `y`, are the content's.
  Adding a sidebar to a document doesn't move anything in it.
- A `<Sidebar>` in a document without `requires="sidebar"`, a second
  `<Sidebar>`, or a width that isn't positive refuses the document.

## Host responsibilities

- Draw the sidebar at the window's left, the window's full height, and make
  the window wider by its width.
- Treat it as a selectable list of one-line rows under its name: a host
  fills it, selects a row, and reports a selection, as it would for a
  `<List selectable="true">`.
- Set it apart from the content, such as with its own shade, and mark the
  selected row.
