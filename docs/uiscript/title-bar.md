# UIScript title bar additions (draft)

This is a draft addition to the **Title bar** section of
[Hudini's UIScript spec](https://github.com/tonytins/Hudini/blob/main/docs/UISCRIPT.md#title-bar). VentureOS reads it
first, since 0.5; it's written as a section of the spec so it can move there once it settles.

A window's title bar can carry a line under the title, and its toolbar items can show a symbol and sit at either end
of the bar, as GNOME's header bars do.

```xml
<UIScript version="1">
  <Window width="480" height="300" subtitle="3 notes"/>
  <Image name="addIcon" path="icons/add.png"/>
  <ToolbarItem name="back" title="Back" placement="start"/>
  <ToolbarItem name="add" title="Add" image="addIcon"/>
  ...
</UIScript>
```

## Attributes

| Element | Attribute | Required | Meaning |
|---|---|---|---|
| `<Window>` | `subtitle` | No | A line shown under the title. Literal text. |
| `<ToolbarItem>` | `image` | No | An `<Image>` the item shows in place of its title, as a symbol. |
| `<ToolbarItem>` | `placement` | No | `end`, the default, or `start`: which end of the bar the item sits at. |

## Rules

- `image` must name an `<Image>` in the document; one that doesn't refuses it.
- A `placement` other than `start` or `end` refuses the document.
- Items at each end run in document order: at the start, left to right after the window's buttons; at the end, the
  last at the right edge, as the spec has them.

## Host responsibilities

A host that implements these:

- Draws the subtitle under the title, smaller and quieter, and makes the bar taller for it. A host may let the app
  change it as it runs; VentureOS's apps call `window:setSubtitle`.
- Draws an item's image as a symbol in the bar's text color, so it follows a light or dark look, and shows the
  item's `title` as hover text. Draw an item whose image it can't supply with its title.
- Places items at the end they name, with the title between the two groups.

A reader that doesn't know these attributes skips them: the window has no subtitle, and every item is a text item at
the right end, which still works.
