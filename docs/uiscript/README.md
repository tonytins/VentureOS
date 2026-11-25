# UIScript Format

A UIScript document is an XML file describing one interface: its window size, resources, styles, decorative images,
and controls. A reader turns it into a model, and a host builds, updates, and draws an interface from that model,
supplying the live data, actions, images, and localized text. The format describes presentation only. It's inspired
by The Sims Online's `.uis` files.

This is the official spec. VentureOS keeps it, and two readers follow it:

- **VentureOS** reads all of it (see [Readers](#readers) for what it adds as a host), in
  `src/Venture.OS.Interface/UIScript.cs`.
- **[Hudini](https://github.com/tonytins/Hudini)**, a Swift interface library for raylib games, reads a subset: the
  parts a game needs. Its [UISCRIPT.md](https://github.com/tonytins/Hudini/blob/main/docs/UISCRIPT.md) covers its
  Swift API and its reader for Maxis' `.uis` files.

The spec text is in the public domain under [CC0](./LICENSE), so anyone may implement it.

## Status

**Draft.** This spec changes on VentureOS's `main` branch as hosts need it, rather than in numbered releases.
Additions land here once a host has built and tested them, and the [change log](#change-log) dates each one so other
readers can catch up.

While the format is a draft, `version` stays at `1`. A reader that implements only part of the format refuses a
document that uses the rest, through the [compatibility rules](#compatibility), so a subset never draws an interface
wrong.

Hudini kept this spec until 2026-11-20. Its change log is carried over below.

## Files

UIScript documents use the `.uix` extension. The contents are XML, so `.uix` only says which format it is, and keeps
documents apart from Maxis' `.uis` files. A reader must not depend on the extension: documents named `.xml` still
load.

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!-- Comments are welcome anywhere. -->
<UIScript version="1">
  <Window width="400" height="255"/>

  <Image name="send" path="ui/send.bmp" stateCount="4"/>
  <Style name="entry" textColor="#FFF99D" backgroundColor="#000000" fontSize="14"/>

  <List name="history" x="40" y="45" width="310" height="150"/>
  <TextField name="message" x="40" y="216" width="299" height="22"
             style="entry" placeholder="Type a message..." maximumLength="120"/>
  <Button name="sendButton" x="345" y="212" image="send"/>
</UIScript>
```

The interfaces VentureOS ships, in `rootfs/system/` and `rootfs/user/apps/`, are complete examples, as are Hudini's
demo documents.

## Document rules

- Documents must be UTF-8 and well-formed XML.
- `DOCTYPE` declarations are not allowed, which also rules out custom entities. The five standard entities, such as
  `&lt;`, work as usual.
- Every element is a direct child of `<UIScript>`, in any order, and holds no children, except where its section
  says it may hold others, as a [`<Menu>`](#menus), a [`<Pane>`](#panes), and a [`<Sidebar>`](#sidebar) do. An element anywhere else refuses the
  document.
- No element holds text. Text goes in attributes.
- Controls draw in document order, with later controls on top, whether they sit in `<UIScript>` or in a pane.
  Visuals draw before all controls.
- Names are unique among controls, visuals, title bar elements, menus and their items, and panes, and within each
  kind of resource. A control and an image may share a name.

## Root

`<UIScript>` takes two optional attributes:

| Attribute | Value |
| --- | --- |
| `version` | The format version, which defaults to `1`. It stays `1` while the format is a draft. |
| `requires` | Space-separated names of parts of the format the document needs. See [Compatibility](#compatibility). |

## Window

Exactly one `<Window>` is required, unless the document describes a [panel](#panel) instead. It gives the
interface's size in pixels, not counting its title bar or its [sidebar](#sidebar).

| Attribute | Required | Value |
| --- | --- | --- |
| `width`, `height` | Yes | The size. Both must be positive. |
| `minWidth`, `minHeight` | No | The smallest size the view lays out at. Give both or neither. |
| `resizable` | No | `true` or `false`, which defaults to `false`. The window has a grip in its bottom-right corner that resizes its content, which controls follow by their edge offsets. It never goes below `minWidth` and `minHeight`, or below the window's size without them. A reader that ignores it shows the window at its size. |
| `subtitle` | No | A line shown under the title, in the [title bar](#title-bar). Literal text. |

## Panel

A document can describe a panel in place of a window: a strip pinned to the top or bottom edge of the screen, with
no title bar. A game's HUD bars are panels, such as the control panel along the bottom of The Sims Online's screen
and the status bar along CityLimits'. So are a desktop's dock and menu bar.

```xml
<UIScript version="1">
  <Panel width="1024" height="40" edge="bottom"/>

  <Button name="home" x="8" y="6" width="80" height="28" text="Home"/>
  <Text name="clock" x="956" y="12" width="60" height="16" edgeOffsetRight="8"
        text="12:00" alignment="right"/>
</UIScript>
```

`<Panel>` takes the place of `<Window>`: a document has exactly one of the two.

| Attribute | Required | Value |
| --- | --- | --- |
| `width`, `height` | Yes | The panel's size, as a window's. Both must be positive. |
| `minWidth`, `minHeight` | No | The smallest size the panel lays out at, as a window's. Give both or neither. |
| `edge` | Yes | `top` or `bottom`, in lowercase: the edge of the screen the panel is pinned to. |

Controls lay out in a panel as they do in a window, and edge offsets measure from the panel's size, or the size the
host gives it, such as the screen's width. A panel stretched across the screen uses `minWidth` so its anchored
controls don't run into each other on a narrow one. A reader that ignores `minWidth` and `minHeight` lays the panel
out at any size, which is plainer but still shows it.

Rules:

- An `edge` other than `top` or `bottom` refuses the document, so a panel never lands somewhere it wasn't meant to.
  Another edge needs a reader that knows it.
- A panel has no title bar and isn't resized by a grip, so `resizable` and `subtitle` mean nothing on it. Written
  anyway, they're ignored and listed, as unknown attributes are.
- A panel can't have toolbar items, a title field, a sidebar, or panes. Any of these refuses the document.
- A panel may have menus. How the host shows them is the host's choice.
- A document with both a `<Window>` and a `<Panel>`, or with two panels, refuses the document.
- `<Panel>` holds nothing. Its controls sit in `<UIScript>`, as a window's do, so the rest of a document reads the
  same whichever it has.

A reader from before panels refuses `<Panel>` as an unknown element. That's the right failure: a HUD bar or a dock
shown as an ordinary, movable window isn't a working fallback.

A host that shows panels:

- Draws a panel along its edge, above the game or the windows it manages, and doesn't let it be dragged or framed as
  a window.
- Keeps windows clear of the edge a panel is on, or, in a game, keeps the panel above whatever the game draws there.
- Decides where along its edge a panel sits, and how many it allows on each edge. VentureOS allows one top panel, its
  menu bar, and centres a bottom panel, its dock, which can hide itself.

A panel is where a document's interface goes on the screen. The views a host draws inside it, or inside each other,
are up to the host.

## Resources

`<Image>` refers to an image the host loads. It needs a `path`, an `assetID`, or both. The host decides which to
use. An asset ID stays exactly as written.

| Attribute | Required | Value |
| --- | --- | --- |
| `name` | Yes | The name other elements use. |
| `path` | One of these | A host-defined path. |
| `assetID` | One of these | A host-defined identifier. |
| `stateCount` | No | `1` (the default), `4`, or `6`. |

A four-state image holds four equal-width slices, left to right: normal, pressed, hovered, and disabled. An image with
one state shows it whatever the button's state. Some Maxis images hold six slices, and readers use the first four.

`<String>` refers to localized text the host supplies.

| Attribute | Required | Value |
| --- | --- | --- |
| `name` | Yes | The name other elements use. |
| `table` | Yes | A host-defined string table. |
| `index` | Yes | The string's index in that table. |

Both hosts read The Sims Online's `.cst` string tables: VentureOS with [CST.NET](https://github.com/tonytins/cstdotnet),
and Hudini with [SwiftCST](https://github.com/tonytins/swift-cst).

## Styles

`<Style>` is a named set of visual properties that controls refer to.

A style should say what a color or size is *for*, not what it is, so a document follows the host's look: light or
dark, any accent, and whatever the host's theme becomes later. The same idea let Mac apps that used semantic colors
take on Dark Mode with no changes. Colors written as hex still work, and each has a `dark` twin for a dark theme.

```xml
<Style name="heading" fontSize="title"/>
<Style name="quiet" textColor="secondaryText"/>
<Style name="display" fontSize="24" backgroundColor="inputBackground"/>
<Style name="brand" textColor="#1E5AA0" darkTextColor="#8CB8F0"/>
```

| Attribute | Value |
| --- | --- |
| `name` | Required. The name controls use. |
| `textColor`, `backgroundColor`, `borderColor` | Colors used when drawing. |
| `selectedTextColor`, `highlightedTextColor`, `disabledTextColor` | Button label colors while pressed, hovered, and disabled. |
| `fillColor` | The filled part of a progress bar and the thumb of a slider. |
| `darkTextColor`, `darkBackgroundColor`, and the rest | A color's `dark` twin: the same attribute with `dark` before it, used in place of it on a dark theme. |
| `fontSize` | A size role, or a positive size in pixels. Without one, text uses the `body` size. |
| `font` | `interface` (the default) or `monospace`, a fixed-width typeface for code and terminals. The host picks the actual fonts. Any other value refuses the document. |

A reader that ignores `font` draws the text in the interface font, which is plainer but still correct.

### Colors

A color is one of:

- **A role**, the color the host's theme gives that purpose. Prefer roles: they follow light and dark, the accent,
  and later themes.
- **`#RRGGBB` or `#RRGGBBAA`**, for a color that must be exactly that, such as a brand's. It's the same on every
  theme unless a `dark` twin replaces it.

| Role | For |
| --- | --- |
| `text` | Ordinary text. |
| `secondaryText` | Quieter text, such as a status line. |
| `disabledText` | Text on something turned off. |
| `highlightedText` | Text on the accent color. |
| `accent` | The accent: focus, selection marks, and fills. |
| `panel` | A window's background. |
| `inputBackground` | The inside of text fields and lists. |
| `border` | Lines around controls. |
| `selection` | A selected item's background. |

### Sizes

| Role | For | VentureOS | Hudini |
| --- | --- | --- | --- |
| `caption` | Small text, such as a hint | 11 | 12 |
| `body` | Ordinary text, and the size text has without a style | 13 | 14 |
| `title` | A heading | 17 | 16 |
| `largeTitle` | A large heading | 22 | 22 |

A host chooses its own sizes for the roles; the table shows each host's defaults.

### Rules

- A color that's neither a role nor `#` and six or eight hex digits refuses the document, as does a `fontSize`
  that's neither a role nor a positive whole number.
- A `dark` twin may name a role or a hex color. It's optional, and may be given without its light color, in which
  case a light theme uses the host's default.
- On a light theme, `dark` twins are ignored. On a dark one, each twin that is set replaces its color; the others
  apply as written.
- A reader from before roles refuses a document that uses one as an invalid color or size, which says plainly that
  it needs a newer reader.

A host that implements styles:

- Resolves roles against the theme in use each time it draws, or again when the theme changes, so switching light,
  dark, or the accent reaches every open window without the document changing.
- Keeps the shipped roles readable together: each text role against each background role it's used on, on every
  theme the host offers. VentureOS tests every interface it ships for a contrast of at least 3 to 1 on every palette.
- Uses a document's hex colors exactly as written, `dark` twins included; the document has asked for that color.

## Position and size

Visuals and controls share these attributes:

| Attribute | Required | Value |
| --- | --- | --- |
| `x`, `y` | Yes | The position within the window, which may be negative. |
| `width`, `height` | See below | The size, which must not be negative. |
| `edgeOffsetLeft`, `edgeOffsetRight`, `edgeOffsetTop`, `edgeOffsetBottom` | No | Distances to the window edges. |

An edge offset replaces the position on its axis. `edgeOffsetRight` alone anchors a control to the right edge, and
`edgeOffsetLeft` with `edgeOffsetRight` stretches it between both. Top and bottom work the same way. Offsets measure
from the document's window size until the host gives the view another size, and never from less than `minWidth` and
`minHeight`.

A button or visual with an image may leave out `width`, `height`, or both to take them from one slice of the image.
Any other control needs both. If the host cannot supply the image, it draws a 24-pixel stand-in with a red outline.

## Visuals

`<Visual>` is a decorative image. It takes `name` and `image`, which names an `<Image>`, plus the position and size
attributes.

## Controls

Every control takes `name` and the position and size attributes. All of these are optional:

| Attribute | Value |
| --- | --- |
| `style` | The name of a `<Style>`. |
| `legacyID` | An integer identifier for host use or for `.uis` conversion. |
| `tooltip` | The name of a `<String>`, shown when the pointer rests on the control. |
| `pane` | The [pane](#panes) the control is in. |

| Element | What it is | Extra attributes |
| --- | --- | --- |
| `<Text>` | Text at its position | `text` or `string`, `alignment` |
| `<FormattedText>` | Text on a background, with an inset | `text` or `string`, `alignment` |
| `<Button>` | A clickable button | `text` or `string`, `image`, `scrolls`, `direction` |
| `<TextField>` | A one-line text entry | `text` or `string`, `placeholder` or `placeholderString`, `maximumLength`, `readOnly`, `secure`, `alignment` |
| `<TextArea>` | A multi-line text entry | `text` or `string`, `placeholder` or `placeholderString`, `maximumLength`, `readOnly`, `follows`, `alignment` |
| `<List>` | A scrollable list the host fills | `text` or `string`, shown as its first item, `selectable` |
| `<Slider>` | A draggable thumb on a track | `scrolls`, `minValue`, `maxValue`, `orientation` |
| `<ProgressBar>` | A bar filled in proportion to a value | `minValue`, `maxValue` |
| `<Region>` | A rectangle the host draws into | None |
| `<Shelf>` | A row of icons the host fills, such as a dock's | None. See [Shelf](#shelf). |

- **`text` and `string`:** `text` is literal, and `string` names a `<String>`. A control takes one or the other, and
  `placeholder` and `placeholderString` work the same way.
- **`alignment`:** `left`, `center`, or `right`, which defaults to `left`. A text area aligns each of its lines, and a
  text field its text while all of it fits; a field's text that doesn't fit fills it from the left and scrolls, as
  before. Either places its caret, selection, and placeholder with the text.
- **`maximumLength`:** the most characters a text field or text area accepts, which defaults to 256 for a field and
  65,536 for an area.
- **`readOnly`:** `true` or `false`, which defaults to `false`. A read-only text field or text area never takes focus
  or typing, and the host can still set its text.
- **`secure`:** `true` or `false`, which defaults to `false`. A secure text field draws `*` in place of each
  character, as a password field does. The host still reads the real text from it.
- **`selectable`:** `true` or `false`, which defaults to `false`. A list without it is a log: it opens on its newest
  items and stays at the end as items arrive. A selectable list is a pick list: it opens on its first items, stays
  where it is when items change, highlights one item, and reports a click on a different item as `selected`.
  Double-clicking an item reports `submitted` with the item's text, after the first click has selected it, so a host
  reads the index from the list's selection. Two presses on the same item count as a double-click when they come
  close enough together; the host picks the time, and 500 ms matches common desktops. A reader that ignored this
  attribute would still show the items.
- **`follows`:** `true` or `false`, which defaults to `false`, and only on a `<TextArea>`. When its text changes
  while it's scrolled to its last line, it stays at the last line, as a log list does. Scrolled back, it stays put. A
  terminal uses it.
- **`scrolls`:** names the `<List>` or `<TextArea>` a slider or button scrolls. A slider with `scrolls` always runs
  top to bottom and has no value of its own.
- **`minValue` and `maxValue`:** the values a slider or progress bar covers, which default to `0` and `1`. It starts
  at the lowest.
- **`orientation`:** `horizontal` or `vertical`. Without it, a slider runs along its longer side.
- **`direction`:** `up` or `down`. A button with `scrolls` needs it, and a slider must not have it.

### Text areas

A `<TextArea>` takes typing across several lines. Enter adds a line break rather than submitting, and the starting
`text` may hold line breaks, written `&#10;`. It wraps at spaces, scrolls with the wheel and with a bound slider or
buttons, and keeps the caret in view. `secure` and `selectable` refuse the document on it.

The host reads and sets an area's text by name. A reader may also report typing, such as with `changed` and the
text, but hosts shouldn't rely on it: Hudini doesn't report it.

It's an element rather than a `multiline` attribute on `<TextField>` because an older reader ignores unknown
attributes. It would show a one-line field where Enter submits, which silently changes behavior. An unknown element
refuses the document instead.

### Shelf

A **shelf** is a row of icons, such as a dock's: each with a label shown under the pointer and a dot under the ones
that are open, in groups with a divider between.

```xml
<UIScript version="1">
  <Panel width="400" height="72" edge="bottom"/>
  <Shelf name="apps" x="0" y="0" width="400" height="72"/>
</UIScript>
```

`<Shelf>` is a control, and follows the rules for controls. It takes nothing else.

What a shelf shows is the host's to give, as a list's items are: for each item a title, an icon, whether it's open,
and a group, with a divider between items whose groups differ. A click on an item reports `pressed` with its index.
In VentureOS, apps give them through `window:setItems(name, items)`.

A reader without shelves refuses the document as it does any unknown element, by design.

## Title bar

A document can describe what goes in its window's title bar. The host's window frame draws the bar: its height, its
close and minimize buttons, the title, and dragging. Positions in the document are still the content's, below the
bar, so adding these moves nothing.

```xml
<UIScript version="1">
  <Window width="480" height="300" subtitle="3 notes"/>
  <Image name="addIcon" path="icons/add.png"/>
  <ToolbarItem name="back" title="Back" placement="start"/>
  <ToolbarItem name="go" title="Go"/>
  <ToolbarItem name="add" title="Add" image="addIcon"/>
  <TitleField name="address" placeholder="Path"/>
  ...
</UIScript>
```

`<ToolbarItem>` is a button on the bar.

| Attribute | Required | Value |
| --- | --- | --- |
| `name` | Yes | The name the host uses for it, as for a control. |
| `title` | Yes | The item's text, or its hover text when it shows an image. |
| `image` | No | An `<Image>` the item shows in place of its title, as a symbol. |
| `placement` | No | `end`, the default, or `start`: which end of the bar the item sits at. |

Items at each end run in document order: at the start, left to right after the window's buttons; at the end, the last
at the right edge. A click reports `pressed` with its name, as a button does. A host decides whether that happens on
the press or the release, as it does for its buttons: Hudini reports both on the press, and VentureOS on the release.
A host can dim an item, and show it held down, so items can work as tabs.

`<TitleField>` makes the title editable, as an address bar is. It takes `name`, which is required, and
`placeholder`. A document has at most one. While the title field is empty, the bar shows the placeholder in place of
a title.

The `title` and `placeholder` of these elements, and a window's `subtitle`, are literal text. They don't take string
references yet.

Rules:

- `image` must name an `<Image>` in the document; one that doesn't refuses it.
- A `placement` other than `start` or `end` refuses the document.

A host that draws a title bar:

- Draws a window's `subtitle` under the title, smaller and quieter, and makes the bar taller for it. A host may let
  the app change it as it runs; VentureOS's apps call `window:setSubtitle`.
- Draws an item's image as a symbol in the bar's text color, so it follows a light or dark look, and shows the item's
  `title` as hover text. It draws an item whose image it can't supply with its title.
- Places items at the end they name, with the title, if the window has one, between the two groups. A host may
  leave a window's title out; VentureOS does unless the app gives one. A subtitle on a bar without a title takes the
  title's place.
- For the title field: a click starts an edit. Enter reports `submitted` with the text, and Esc or a click elsewhere
  puts the old text back. A click on one of the same window's toolbar items keeps the edit, so an item such as Go
  acts on what was typed. The host reads and sets it by name, as it does a text field; while an edit is under way,
  reading it gives what has been typed so far. A disabled title field can't be edited.

A reader that doesn't know `subtitle`, `image`, or `placement` skips them: the window has no subtitle, and every item
is a text item at the right end, which still works. A host that shows a document without a title bar doesn't draw
the bar's elements. They aren't errors there.

## Menus

A document can describe the menus its interface puts in a global menu bar, as a desktop shows the focused app's menus
along the top of the screen.

```xml
<UIScript version="1">
  <Window width="400" height="300"/>

  <Menu name="notesMenu" title="Notes" hotkey="N">
    <MenuItem name="newNote" title="New Note" hotkey="N" shortcut="Ctrl+N"/>
    <Menu name="sortMenu" title="Sort By" hotkey="S">
      <MenuItem name="sortByDate" title="Date" hotkey="D"/>
      <MenuItem name="sortByTitle" title="Title" hotkey="T"/>
    </Menu>
    <MenuSeparator/>
    <MenuItem name="clearAll" title="Clear All Notes" hotkey="C"/>
  </Menu>

  <List name="notes" x="10" y="10" width="380" height="250" selectable="true"/>
</UIScript>
```

A menu holds its items, separators, and submenus. A `<Menu>` directly inside `<UIScript>` goes in the menu bar, and a
`<Menu>` inside another is a submenu: an entry in its parent that opens another menu beside it. Submenus nest to any
depth. Menus appear in the bar in document order, and everything in a menu appears in the order it's written.

A menu's items and separators may instead sit directly in `<UIScript>`, each naming its menu with `menu`, as a slider
names the list it scrolls. This is the form menus had before they nested, and it still reads the same:

```xml
<Menu name="notesMenu" title="Notes" hotkey="N"/>
<MenuItem menu="notesMenu" name="newNote" title="New Note" hotkey="N" shortcut="Ctrl+N"/>
<MenuSeparator menu="notesMenu"/>
<MenuItem menu="notesMenu" name="clearAll" title="Clear All Notes" hotkey="C"/>
```

A menu uses one form or the other. One that holds anything can't also be named by a `menu` attribute, and a submenu
only nests, so a menu with submenus holds all of its contents.

`<Menu>` is one drop-down menu, or a submenu.

| Attribute | Required | Value |
| --- | --- | --- |
| `name` | Yes | The name a flat item uses to join it. |
| `title` | Yes | The text in the menu bar, or a submenu's entry in its parent. |
| `hotkey` | No | One letter or digit. With Alt held, it opens a menu in the bar. On a submenu, it opens the submenu while its parent is open. |
| `role` | No | `file`, `edit`, `view`, or `help`: the host's standard menu this one adds to. Only a menu in the bar takes it. |

`<MenuItem>` is a choice in a menu.

| Attribute | Required | Value |
| --- | --- | --- |
| `menu` | Only when flat | The name of the `<Menu>` it joins. An item inside a menu must not have it. |
| `name` | Yes | The name the host uses for it, as for a control. |
| `title` | Yes | The text in the menu. |
| `hotkey` | No | One letter or digit that picks it while its menu is open. |
| `shortcut` | No | A key combination that picks it from anywhere, such as `Ctrl+N` or `Ctrl+Shift+S`. |

`<MenuSeparator>` is a line between groups of items. It takes `menu` when flat, and nothing when nested.

Rules:

- A menu may be empty, and so may a submenu.
- Menus in the bar use different `hotkey`s from each other. Within one menu, its items and submenus use different
  `hotkey`s from each other. Both ignore case.
- A `<MenuItem>` or `<MenuSeparator>` inside a menu that also has a `menu` attribute, a `role` on a submenu, a flat
  item naming a menu that holds anything, or a menu element inside anything other than `<UIScript>` or a `<Menu>`,
  refuses the document.
- `name`s stay unique across the whole document, however deep they nest.
- A `shortcut` is `Ctrl`, `Alt`, and `Shift` in any combination, each at most once and joined with `+`, then one
  letter or digit. It needs `Ctrl` or `Alt`, so typing into a text field never triggers it. No two items in a
  document use the same shortcut, ignoring case.
- An item that names an undeclared menu refuses the document.
- A `role` the reader doesn't know is ignored and listed, so the menu shows as the document's own.

A host that shows menus:

- Shows the focused interface's menus in the bar, after any menus of its own.
- Reports a chosen item as `pressed` with the item's name, however deep its submenu. Opening a menu or submenu
  reports nothing.
- Opens a submenu from its entry, such as on hover or with the right arrow, and marks the entry so it reads as a
  submenu rather than an item. A host that can't show a submenu beside its parent may show it in place of the parent,
  with a way back.
- Treats a disabled submenu's entry as it does a disabled item: it can't be opened, and nothing in it can be picked,
  shortcuts included.
- Lets the host disable and check items, as for controls.
- Draws `hotkey` and `shortcut` in the menu so people can learn them.
- May keep shortcuts for its own menus, such as Ctrl+Q to quit. Those win over a document's, so a host should list
  the ones it keeps.
- If it has standard menus, merges a document's menu into the one its `role` names, the document's items first. A
  menu without a `role` whose title is exactly `File`, `Edit`, `View`, or `Help` merges the same way. A standard item
  yields to a document's item with the same shortcut.

## Sidebar

A document can give its window a sidebar: a list of places down the left, such as a file manager's bookmarks, or
down the right, such as a radio's queue beside its stations.

```xml
<UIScript version="1">
  <Window width="360" height="300"/>

  <Sidebar name="places" width="130"/>

  <List name="entries" x="10" y="10" width="340" height="280" selectable="true"/>
</UIScript>
```

`<Sidebar>` takes `name` and `width`, both required, and a document has at most one. It has no position: it sits
beside the content rather than in it, so `<Window>`'s size and every control's position are the content's, and adding
a sidebar, on either edge, moves nothing. A second `<Sidebar>`, or a width that isn't positive, refuses the document.

| Attribute | Required | Value |
| --- | --- | --- |
| `name` | Yes | The name code uses for it, as for a `<List>`. |
| `width` | Yes | How wide it is, a positive number. |
| `edge` | No | `left`, the default, or `right`: the window's edge it runs down. Anything else refuses the document. |

A host that shows sidebars:

- Draws it at the window's `edge`, the window's full height, and makes the window wider by its width. When a
  resizable window resizes, the sidebar keeps its width and stays on its edge, and the content takes the rest.
- Treats an empty one as a selectable list of one-line rows under its name: it fills it, selects a row, and reports
  `selected`, as for `<List selectable="true">`.
- Sets it apart from the content, such as with its own shade, and marks the selected row.

A reader from before `edge` ignores it and lists it, drawing the sidebar on the left: plainer, but still correct, since
nothing in the content moves with the sidebar.

### A sidebar that holds controls

A sidebar may hold controls and visuals in place of rows, as a radio's player sits beside its stations:

```xml
<Image name="art" path="art.png"/>
<Sidebar name="player" width="200" edge="right">
  <Visual name="cover" image="art" x="52" y="16" width="96" height="96"/>
  <Text name="station" x="10" y="122" width="180" height="24" alignment="center" text="Jazz Radio"/>
  <Button name="stop" x="60" y="178" width="80" height="26" text="Stop"/>
  <Slider name="volume" x="16" y="218" width="168" height="20" edgeOffsetLeft="16" edgeOffsetRight="16"/>
</Sidebar>
```

Rules:

- What a sidebar holds is placed from the sidebar's own corner, level with the content's top, and its edge offsets are
  the sidebar's: its width, and the content's height. Its visuals' `<Image>`s stay in `<UIScript>`.
- A sidebar that holds anything has no rows, so it can't also have panes. Either refuses the document, as does a
  control in it naming a `pane`, or anything in it other than controls and visuals.
- Its controls and visuals share the document's names, report events, and are shown, hidden, and set by name, as any
  others are.

A host that shows them:

- Draws and hit-tests them where the sidebar is, on either edge, over its shade, and keeps them with it when a
  resizable window resizes.
- Has no rows to fill or select. VentureOS refuses `window:setItems` and the other list calls on such a sidebar.

A reader from before sidebars could hold anything refuses one that does, rather than draw its controls over the
content.

## Panes

A sidebar can hold **panes**, as GNOME Settings does: each row is a pane, and choosing a row shows that pane's
controls in place of the others'. The document says which controls and visuals each pane holds, so the host shows one pane at a
time with no code of the app's, and a designer that runs no code shows them the same way.

```xml
<UIScript version="1">
  <Window width="380" height="200"/>

  <Sidebar name="panes" width="130"/>
  <Pane name="general" title="General">
    <Text name="nameLabel" x="12" y="12" width="80" height="20" text="Name"/>
    <TextField name="name" x="96" y="10" width="200" height="26"/>
  </Pane>
  <Pane name="advanced" title="Advanced">
    <Button name="reset" x="12" y="10" width="120" height="26" text="Reset"/>
  </Pane>
</UIScript>
```

A pane holds its controls and visuals, and one directly in `<UIScript>` shows on every pane. A visual's `<Image>`
stays in `<UIScript>`, with the other resources, so any pane's visuals can show it:

```xml
<Image name="logo" path="logo.png"/>
<Sidebar name="panes" width="108"/>
<Pane name="about" title="About">
  <Visual name="icon" image="logo" x="138" y="16" width="64" height="64"/>
  <Text name="name" x="20" y="88" width="300" height="30" text="VentureOS"/>
</Pane>
```

A pane's controls may instead sit directly in `<UIScript>`, each naming its pane with `pane`, as menu items can name
their menu. This is the form panes had before they nested, and it still reads the same. Visuals have no such form: one
in a pane is always inside it.

```xml
<Pane name="general" title="General"/>
<Text name="nameLabel" x="12" y="12" width="80" height="20" text="Name" pane="general"/>
```

A pane uses one form or the other: one that holds anything can't also be named by a control's `pane`.

`<Pane>` is one of the sidebar's rows, in document order.

| Attribute | Required | Value |
| --- | --- | --- |
| `name` | Yes | The name controls use for it. |
| `title` | Yes | The row's text. |

Rules:

- Panes need a `<Sidebar>`. Panes without one refuse the document.
- A pane holds controls and visuals only, not resources, other panes, or anything else, and a control in a pane takes
  no `pane` of its own. Either refuses the document.
- A `pane` that names no pane, or a pane that holds anything, refuses the document.
- Positions in a pane are the window's content's, as for any control: a pane is a set of controls, not a box.
- The first pane starts selected.

A host that shows panes:

- Shows each pane's `title` as a sidebar row, in order, and shows a control or visual in a pane only while that pane's
  row is selected, as well as when the host hasn't hidden it. A host hiding one and a pane hiding it are separate:
  switching panes never shows what the host hid.
- Reports a row's selection as it does any sidebar's, so an app can follow it, such as by retitling its window.
- Treats the rows as the document's. VentureOS refuses `window:setItems` on a sidebar of panes; selecting rows still
  works.
- With no row selected, shows only the controls and visuals in no pane.

A reader without panes refuses `<Pane>` as an unknown element. That's deliberate: a reader that drew the document
without them would draw every pane's controls over each other. A reader from before visuals could sit in panes
refuses a `<Visual>` inside one, rather than draw it on every pane.

## Events

Controls report what the person does as events carrying the control's name.

| Event | Reported when |
| --- | --- |
| `pressed` | A button, toolbar item, menu item, or shelf item is chosen. Opening a menu or submenu reports nothing. |
| `submitted` | Enter is pressed in a text field or title field, with its text, or an item in a selectable list is double-clicked, with the item's text. |
| `changed` | A slider without a scroll binding moves, with its value. |
| `selected` | A click on a selectable list or sidebar picks a different row, with its index. |

A handler checks the kind, so `submitted` from a list and from a field are told apart by name.

## Compatibility

Every element and attribute in this spec is part of one format. Some hosts have no use for parts of it, such as a
game with no menu bar, so a reader may implement a **subset**. The rules below make a subset safe: a document that
uses what a reader lacks is refused, or drawn plainer but still correct, never drawn wrong.

A reader treats documents it doesn't fully understand like this:

| What the reader finds | What it does |
| --- | --- |
| A `version` newer than it supports | Refuses the document. |
| A `requires` name it does not know | Refuses the document. |
| An element it does not know | Refuses the document. |
| An attribute it does not know | Ignores it and lists it. |

These rules set how the format grows:

- A new attribute must be safe for an older reader to ignore, giving a plainer but still correct interface. If
  ignoring it would silently change behavior, add an element instead.
- New elements always stop older readers, because skipping one could leave an interface silently incomplete.
- An element's section says what it may hold. Letting an element hold others is a change older readers can't follow,
  so where an element already exists flat, the flat form keeps working, as menus' does.
- `version` only increases when existing documents or meanings change, which the draft avoids.

`requires` names parts of the format a document needs, for an addition that an unknown element alone wouldn't stop
an older reader from misreading. None are defined today. Before 2026-11-20, menus, sidebars, shelves, and panes were
modules named `menus`, `sidebar`, `shelf`, and `panes`; a reader accepts those four names and they mean nothing, so
documents written then still read. New documents leave them out.

A misspelled attribute also lands in the reader's list of ignored attributes rather than failing, so hosts should log
that list during development.

## Errors

Loading a document gives a complete, valid model or an error explaining the problem, with the line number when it
relates to one element. There is no partial result.

## Host responsibilities

The format leaves to the host everything about live data and drawing. A host:

- Supplies images and strings, and reports which it couldn't. A missing string shows its table and index instead.
- Shows and hides controls, disables buttons and items, and marks toggles or choices, by name.
- Sets text at runtime, the value of a slider or progress bar, and a list's items and selection. A host's own items
  are the source of what a selection's index means, and setting a selection reports no event.
- Gives the view a new size when the space around anchored controls changes, and never below the document's
  minimum on either axis.
- Places what the document leaves to it, such as a region's contents.
- Keeps a click on the interface from also reaching whatever is behind it, and decides which view gets the pointer
  when views overlap.
- Draws any window frame or background behind the view.
- Chooses the fonts, default text sizes, and corners. A document sets sizes through `<Style>`, and asks for a
  fixed-width font with `font="monospace"`, but can't name a font file.

## Readers

### VentureOS

VentureOS reads version 1, the whole spec, nested menus and submenus included. Its menu bar opens a submenu beside
its entry, on the right or, without room, the left, when the pointer rests on the entry or with the right arrow, and
keeps it open while the pointer crosses toward it. Left and Esc close the innermost submenu.

As a host, it also:

- Draws a button's `image`, ringed in the accent while checked, and shows a four-state image's first slice, shading
  it for the other states.
- Selects text and uses the clipboard in text areas.
- Gives a sidebar's rows icons and groups, which apps set through `window:setItems` (see
  [the desktop tutorial](../tutorial/desktop.md#sidebars-and-panes)).

Roundhouse, VentureOS's IDE, edits documents through `UIDocument` and checks attributes against `UIScriptSchema`,
both in `src/Venture.OS.Interface`; both must match this spec.

### Hudini

Hudini reads a subset, the parts a raylib game needs: the root, `<Window>` and `<Panel>`, resources, styles,
visuals, every control but `<Shelf>`, and the title bar's `<ToolbarItem>` and `<TitleField>`. It refuses menus,
sidebars, shelves, and panes as unknown elements, and skips `subtitle` and toolbar items' `image` and `placement`. Its
[UISCRIPT.md](https://github.com/tonytins/Hudini/blob/main/docs/UISCRIPT.md) says how a Swift host uses it, and how it
reads `.uis` files into the same model.

## Change log

Newest first. Hudini kept the spec before 2026-11-20, and changes before the draft are in Hudini's history.

- **2026-11-24:** `<TextArea>` and `<TextField>` list `alignment`: an area aligns each line, and a field its text
  while it fits. Readers already accepted it there; one that draws them on the left is plainer but still correct.
- **2026-11-24:** A `<Sidebar>` may hold controls and visuals in place of rows, placed from its own corner. One that
  does has no rows and no panes. A reader from before refuses it.
- **2026-11-24:** `<Sidebar>` takes `edge`, `left` or `right`, the window's edge it runs down; left is the default. A
  reader from before ignores it and draws the sidebar on the left.
- **2026-11-24:** A `<Pane>` may hold visuals as well as controls; their `<Image>`s stay in `<UIScript>`. Visuals
  have no flat form. A reader from before refuses a visual in a pane.
- **2026-11-24:** A host may leave a window's title out of its title bar, and a subtitle then takes the title's
  place. The format is unchanged: the title was never the document's.
- **2026-11-20:** A `<Pane>` may hold its controls, which then take no `pane`. The flat form, with `pane` on each
  control, still reads the same, and a pane uses one form or the other.

- **2026-11-20:** VentureOS keeps the spec, and Hudini reads a subset of it. Modules are gone: menus, sidebars,
  shelves, and panes are ordinary elements, and a subset reader refuses them as unknown elements, as it would any
  other. `requires` stays, and accepts the old module names, which mean nothing. The rule that only modules' elements
  nest becomes each element's section saying what it may hold. Added from VentureOS's drafts: `<Shelf>`, `<Pane>`
  and `pane` on controls, a window's `subtitle`, and toolbar items' `image` and `placement`.
- **2026-11-20:** Modules may let their elements nest, while core elements stay flat, so nesting needs no new
  `version`. `menus` nests first: a `<Menu>` holds its items, separators, and submenus, and the flat form with `menu`
  still reads the same. Submenus are new, and nest only.
- **2026-11-20:** `<Panel>` in place of `<Window>`, from VentureOS: a strip pinned to the top or bottom of the
  screen, such as a HUD bar, a dock, or a menu bar. It's core, needing no `requires`, as VentureOS wrote it. Unlike
  VentureOS's draft, it takes `minWidth` and `minHeight`, for a panel stretched across the screen.
- **2026-11-12:** `resizable` on `<Window>`, from VentureOS: a grip in the corner resizes the window within its
  smallest size.
- **2026-11-12:** Styles take color roles, size roles, and `dark` twins, from VentureOS's draft. Hex colors and pixel
  sizes work as before.
- **2026-11-12:** A toolbar item's click follows the host's buttons, on the press or the release, rather than always
  on the release. The title bar's `title` and `placeholder` are literal text for now.
- **2026-11-12:** Added from VentureOS, which built and tested each of them first: `<TextArea>` and `follows`, `font`
  on `<Style>`, double-clicking a selectable list, the title bar's `<ToolbarItem>` and `<TitleField>`, the `menus`
  and `sidebar` modules, and an Events section.
- **2026-11-12:** Marked the format as a draft that changes on `main`, with `version` frozen at `1`. Documents use
  `.uix`. `requires` now names optional modules a host may never implement, rather than features newer than the
  reader.
