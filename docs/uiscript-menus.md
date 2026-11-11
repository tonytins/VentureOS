# UIScript menus (draft feature)

This is a draft addition to the [UIScript format](https://github.com/tonytins/Hudini/blob/main/doc/UISCRIPT.md),
which Hudini defines. VentureOS reads it first; it's written as a section of
the UIScript spec so it can move there once it settles.

A document can describe the menus its interface puts in a global menu bar, as
a desktop shows the focused app's menus along the top of the screen. Menus
are a required feature: a document that uses them names `menus` in
`requires`, so a reader without menus refuses it rather than showing an
interface whose commands are missing.

```xml
<?xml version="1.0" encoding="UTF-8"?>
<UIScript version="1" requires="menus">
  <Window width="400" height="300"/>

  <Menu name="notesMenu" title="Notes" hotkey="N"/>
  <MenuItem menu="notesMenu" name="newNote" title="New Note" hotkey="N" shortcut="Ctrl+N"/>
  <MenuSeparator menu="notesMenu"/>
  <MenuItem menu="notesMenu" name="clearAll" title="Clear All Notes" hotkey="C"/>

  <List name="notes" x="10" y="10" width="380" height="250" selectable="true"/>
</UIScript>
```

## Elements

Menu elements follow the document rules: they're direct children of
`<UIScript>`, don't nest, and hold no text. An item names the menu it belongs
to, as a slider names the list it scrolls. Items and separators appear in a
menu in document order, and menus appear in the bar in document order.

`<Menu>` is one drop-down menu.

| Attribute | Required | Value |
| --- | --- | --- |
| `name` | Yes | The name items use to join it. |
| `title` | Yes | The text in the menu bar. |
| `hotkey` | No | One letter or digit. With Alt held, it opens the menu. |

`<MenuItem>` is a choice in a menu.

| Attribute | Required | Value |
| --- | --- | --- |
| `menu` | Yes | The name of a `<Menu>`. |
| `name` | Yes | The name the host uses for it, as for a control. |
| `title` | Yes | The text in the menu. |
| `hotkey` | No | One letter or digit that picks it while its menu is open. |
| `shortcut` | No | A key combination that picks it from anywhere, such as `Ctrl+N` or `Ctrl+Shift+S`. |

`<MenuSeparator>` is a line between groups of items. It takes `menu` only.

## Rules

- Menu and item names share the namespace of controls and visuals, so a
  host can refer to any of them by name.
- A menu may be empty.
- Menus use different `hotkey`s from each other, as do the items in one menu,
  ignoring case.
- A `shortcut` is `Ctrl`, `Alt`, and `Shift` in any combination, each at
  most once and joined with `+`, then one letter or digit. It needs `Ctrl` or
  `Alt`, so typing into a text field never triggers it. No two items in a
  document use the same shortcut, ignoring case.
- An item that names an undeclared menu, or a menu element in a document
  without `requires="menus"`, refuses the document.

## Host responsibilities

- Show the focused interface's menus in the bar, after any menus the host
  supplies itself, such as an application menu.
- Report a chosen item as an event carrying the item's name, as a pressed
  button reports its own.
- Disable or check items by name, as for controls: `setDisabled(_:named:)`
  greys an item out, and `setSelected(_:named:)` marks it, such as for a
  choice that's on.
- Draw `hotkey` and `shortcut` in the menu so people can learn them.
