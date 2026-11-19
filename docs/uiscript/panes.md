# UIScript panes (draft feature)

This is a draft addition to [Hudini's UIScript spec](https://github.com/tonytins/Hudini/blob/main/docs/UISCRIPT.md),
as a module beside `sidebar`. VentureOS reads it first, since 0.7.4xx; it's written as a section of the spec so it
can move there once it settles.

A sidebar can hold **panes**, as GNOME Settings does: each row is a pane, and choosing a row shows that pane's
controls in place of the others'. The document says which controls each pane holds, so the host shows one pane at
a time with no code of the app's, and a designer that runs no code shows them the same way.

```xml
<?xml version="1.0" encoding="UTF-8"?>
<UIScript version="1" requires="sidebar panes">
  <Window width="380" height="200"/>

  <Sidebar name="panes" width="130"/>
  <Pane name="general" title="General"/>
  <Pane name="advanced" title="Advanced"/>

  <Text name="nameLabel" x="12" y="12" width="80" height="20" text="Name" pane="general"/>
  <TextField name="name" x="96" y="10" width="200" height="26" pane="general"/>
  <Button name="reset" x="12" y="10" width="120" height="26" text="Reset" pane="advanced"/>
</UIScript>
```

Elements are flat, as everywhere in UIScript, so a pane doesn't contain its controls: each control names its pane.

## Elements

`<Pane>` follows the document rules: a direct child of `<UIScript>`, with no text and no children. Panes are the
sidebar's rows, in document order.

| Attribute | Required | Value |
| --- | --- | --- |
| `name` | Yes | The name controls use for it. It shares the namespace of controls, visuals, and menus. |
| `title` | Yes | The row's text. |

Every control takes one more attribute:

| Attribute | Required | Value |
| --- | --- | --- |
| `pane` | No | The `name` of the pane it's in. A control without one shows on every pane. |

## Rules

- Panes need the `panes` feature and a `<Sidebar>`. A `<Pane>` without the feature, or panes without a sidebar,
  refuses the document.
- A `pane` that names no pane refuses the document. Without the `panes` feature, `pane` is an unknown attribute,
  skipped and listed.
- The first pane starts selected.

## Host responsibilities

- Show each pane's `title` as a sidebar row, in order, and show a control in a pane only while that pane's row is
  selected, as well as when the app hasn't hidden it. An app hiding a control and a pane hiding it are separate:
  switching panes never shows what the app hid.
- Report a row's selection as it does any sidebar's, so an app can follow it, such as by retitling its window.
- Treat the rows as the document's. VentureOS refuses `window:setItems` on a sidebar of panes; `select` and
  `selected` still work.
- With no row selected, show only the controls in no pane.

## Older readers

A reader without panes refuses the document, as it refuses any feature it doesn't know. That's deliberate: a reader
that skipped `pane` would draw every pane's controls over each other.

## In VentureOS

- Settings is written with panes: Appearance, Dock, Clock, and Developer.
- Roundhouse's designer draws a sidebar's rows and the selected pane's controls. Selecting a pane's row on the
  canvas, or anything in a pane in the outline, shows that pane, and a control added from the library joins it.
