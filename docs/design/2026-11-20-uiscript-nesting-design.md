# UIScript nesting: design

> **Status:** approved 2026-11-20, built in 0.7.7xx, Seto · **Date:** 2026-11-20 · **Release:** VentureOS 0.7, Seto, band 0.7.7xx
>
> Built as written, with the corrections marked *Built:* below. How it works now is in
> [the architecture](../architecture.md) and [the spec](../uiscript/README.md).

UIScript elements learn to hold other elements where the [spec](../uiscript/README.md) says they may: a `<Menu>`
holds its items, separators, and submenus, and a `<Pane>` holds its controls. VentureOS reads, shows, and edits both,
and its apps move to the nested forms. `version` stays `1`, and a `<Window>`'s controls stay flat.

## Why

- **VentureOS doesn't read its own spec.** The spec has had nested menus since 2026-11-20, and VentureOS's reader
  refuses them.
- **Nested documents say what they mean.** A menu that holds its items reads as the menu it draws, and a pane that
  holds its controls reads as the page it shows, without `menu=` and `pane=` on every line.
- **Submenus.** The spec has them, and the desktop can't show one.

Prior art: macOS's and GNOME's menus (submenus open beside their entry, on hover or the right arrow); Xcode's and
Rider's outlines (a tree of what a document holds).

## Goals

1. `FlatXml` reads nested elements when its caller asks, and records where each one sits, so the editor can still
   change text in place. `Info.xml` stays flat.
2. `UIScript` reads nested menus and submenus to any depth, by the spec's rules, alongside the flat form.
3. The desktop's menu bar opens submenus: on hover and with the right arrow, back with the left arrow or Esc. Hotkeys
   and shortcuts reach inside them, and a disabled submenu blocks everything in it.
4. The spec lets a `<Pane>` hold its controls, alongside the flat form with `pane`, and VentureOS reads both.
5. The shipped apps move to the nested forms: Files, Terminal, TextEdit, and Notes for menus, and Settings for panes.
   Notes gains **Note ▸ Sort By**, a real submenu (see [Notes' Sort By](#notes-sort-by)).
6. Roundhouse's designer shows the document as a tree, adds a menu's entries and a pane's controls inside it, and
   moves an element into or out of a menu or pane from commands. New panes and menus come out nested.

## Non-goals

- **A `<Window>` that holds its controls.** Core controls stay direct children of `<UIScript>`.
- **Dragging in the outline.** Moving into and out of a menu or pane is a command for now; dragging in the tree can
  come later as its own item.
- **Hudini reading nested documents.** Menus and panes are outside its subset.
- **Remembering Notes' sort order** between launches.

## Anti-goals

- **An editor that rewrites the file.** A document nobody edited must still save byte for byte, nested or not, and
  an edit must touch only the text it changes.
- **Breaking flat documents.** Every flat document that reads today reads the same afterwards, including Hudini's
  examples.
- **Partial reads.** A nested document reads completely and validly, or not at all, as now.

## Constraints

- Cosmos can't link `System.Xml`, so `FlatXml` stays the reader, and grows only as much as nesting needs.
- Everything new is kernel-free and goes in `Venture.OS.Interface` or `Venture.OS.Common`, with tests.
- Roundhouse finds elements by their position in the document, an `int`, in about 30 places; the change should keep
  that shape rather than rework every one.

## Approaches

**How elements are numbered once they nest.** Chosen: elements are numbered in document order, each element's
children straight after it (`Children` stays one list, in reading order), and each element knows its parent and its
depth. Every index Roundhouse holds keeps meaning "the element at this place in the text", and a flat document
numbers exactly as it does now. The other way, a tree with paths such as `[2, 0, 1]`, matches the structure but
changes every caller.

**How a submenu shows.** Chosen, by the owner: beside its entry, opened on hover or with the right arrow, as on macOS
and GNOME. The spec also allows showing it in place of its parent with a way back, which is simpler but feels worse
on a desktop.

**How much the designer does.** Chosen, by the owner: a tree outline, adding into the selected menu or pane, and Move
Into and Move Out commands. Dragging in the tree waits.

## How it works

### Reading

`FlatXml.Read` takes whether nesting is allowed. Without it, as for `Info.xml`, a nested element is refused with its
line, as now. With it, each `FlatXmlElement` gains `Parent` (an index, or `-1` for a child of the root) and `Depth`,
and its `FlatXmlElementSource` covers its children too, as the root's does. Text inside an element is still refused.

`UIScript` walks the elements in order and checks each one's parent against the spec:

- A `<Menu>` may sit in `<UIScript>` or in a `<Menu>`; `<MenuItem>` and `<MenuSeparator>` in either, taking `menu`
  only when flat. A submenu takes no `role`, and only nests. A menu that holds anything can't also be named by a
  flat item.
- With [panes nesting](#panes), a control may sit in a `<Pane>`, taking no `pane` of its own.
- Anything else inside an element refuses the document with `unexpected nesting`, naming both elements.

The model grows by one field: a `UIMenuEntry` may be a submenu, carrying its own `UIMenu`. Names stay unique across
the whole document, and hotkeys are checked within each menu, as the spec says.

### Submenus on the desktop

`MenuBar` keeps a stack of open menus rather than one. A submenu's entry shows a `▸` where an item's shortcut goes.
Resting on the entry, or the right arrow on it, opens the submenu beside its parent, flipped to the left when it
would run off the screen; the left arrow or Esc closes the innermost menu, and a click outside closes them all. The
pointer can cross from the entry into its submenu without the submenu closing. A hotkey picks within the innermost
open menu. `FindShortcut` looks through submenus. A disabled submenu (`setDisabled` with its name) can't be opened,
and its items' shortcuts don't fire. A chosen item reports `pressed` with its name, however deep.

The desktop's standard menus (File, Edit, View, Help) merge submenus like any entry. *Built:* a submenu opens at
once when the pointer rests on its entry, with no delay; a pointer resting off the menus no longer clears what the
keys highlighted, which fixed the same fault in top-level menus.

### Panes

The spec's Panes section gains a second form, as menus have: a `<Pane>` may hold its controls, each of which is then
in that pane and takes no `pane`. A pane uses one form or the other. `UIView` is unchanged, since the model, a
control with a pane, is the same either way.

### Notes' Sort By

Notes' Note menu gains a submenu:

```xml
<Menu name="noteMenu" title="Note" hotkey="N">
  <MenuItem name="newNote" title="New Note" hotkey="N" shortcut="Ctrl+Shift+N"/>
  <MenuItem name="deleteNote" title="Delete Note" hotkey="D" shortcut="Ctrl+D"/>
  <MenuSeparator/>
  <Menu name="sortMenu" title="Sort By" hotkey="S">
    <MenuItem name="sortAdded" title="Order Added" hotkey="O"/>
    <MenuItem name="sortNewest" title="Newest First" hotkey="N"/>
    <MenuItem name="sortTitle" title="A to Z" hotkey="A"/>
  </Menu>
  <MenuSeparator/>
  <MenuItem name="clearAll" title="Clear All Notes" hotkey="C"/>
</Menu>
```

The chosen order is checked (`setSelected`) and changes only how the list shows the notes; `notes.txt` keeps the
order they were added, so the `note` command and the file agree. Selecting and deleting map the shown row back to
the note it shows. Notes starts in Order Added. *Built:* selection and Delete follow the note, not the row.

### Roundhouse

The outline becomes a tree, indented by depth, with menus and panes able to fold. Adding a menu item, separator, or
submenu goes inside the selected menu, or the menu of the selected item, after its last entry; adding a control while
a pane is shown goes inside that pane, if the pane holds its controls. New menus and new panes are written nested.
Edit ▸ Move Into (the menu or pane just before the element) and Move Out (to after its parent) move an element and
its children as one. *Built:* Tab and Shift+Tab in the outline do the same, Left and Right fold, folds are kept by name and
cleared when another document opens, and Move Into drops a `menu` or `pane` that named the parent from outside. `FlatXmlEditor` gains moving an element in or out, keeping indentation as the document has it.
The canvas and Run show submenus as the desktop does, since they use its code.

## Where the code goes

| Piece | Where |
| --- | --- |
| Nested reading, parents, depth, moving in and out | `src/Venture.OS.Interface/Xml/FlatXml.cs`, `FlatXmlEditor.cs` |
| Nested menus, submenus, nested panes | `src/Venture.OS.Interface/UIScript.cs`, `UIDocument.cs`, `UIScriptSchema.cs` |
| Submenus in the bar | `src/Venture.OS.Common/Desktop/MenuBar.cs`, `DesktopSession.cs` |
| The spec | `docs/uiscript/README.md`: nested panes, and the change log |
| Apps | `rootfs/system/apps/{Files,Terminal,Settings}.app`, `rootfs/user/apps/{TextEdit,Notes}.app` |
| The tree outline and Move Into, Move Out | `src/Venture.Roundhouse/MainWindow.axaml(.cs)`, `Designer/DesignEdits.cs` |
| Tests | `tests/Venture.OS.Common.Tests/Interface/`, `Desktop/`, `Bundles/NotesAppTests.cs`; `tests/Venture.Roundhouse.Tests/` |

## Risks

- **Round trips.** Nested elements make the text positions harder to keep right. Every shipped `.uix`, Hudini's
  examples, and new nested fixtures are tested to save byte for byte, and to change only what an edit touches.
- **Index drift.** An edit that adds or moves an element with children shifts the indices after it by more than one.
  Roundhouse keeps its selection by finding the element again after each edit, which needs tests with children.
- **Pointer paths into submenus.** Crossing from an entry into its submenu over another entry can close it. Keep the
  submenu open while the pointer moves toward it, as macOS does, and test the path.
- **Pointer moves don't reach QEMU from the virtual display**, so hovering into a submenu is checked by tests, and on
  a boot by keyboard only.

## Phases

1. **Scaffolding:** `FlatXml` reads nesting when asked, with parents, depth, and source spans; `FlatXmlEditor` and
   `UIDocument` work on nested documents; round-trip tests.
2. **Nested menus in the reader:** submenus in the model, the spec's rules, and the spec's menu examples as tests.
3. **Submenus on the desktop**, and Notes' Sort By.
4. **Panes hold their controls:** the spec, the reader, and the shipped apps' nested menus and panes.
5. **Roundhouse:** the tree outline, adding inside, and Move Into and Move Out.

## Left to the builder

- How the outline folds, and whether it remembers what's folded.
- Exactly how long hover takes to open a submenu.
- The unexpected-nesting message's wording.
