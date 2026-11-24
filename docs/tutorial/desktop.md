# A desktop app

This tutorial gives **Dice**, from [the command-line tutorial](./command-line.md),
a window with a Roll button, then shows what else a window can do. Start
from that Dice: its `Info.xml`, `main.lua`, and project.

Roundhouse, VentureOS's IDE, is the easiest way to work on a window: it draws
a `.uix` file as VentureOS does while you edit it, runs the app without QEMU,
and runs its tests. Everything here also works by hand.

A window should look and speak like the rest of VentureOS: say what colors
are for rather than what they are, use the standard menus, and write
messages in [the HIG's voice](../hig/README.md#voice). The
[VentureOS HIG](../hig/README.md) has the rest.

## Give the app a window

An app can open windows on the desktop. Each window's layout is a `.uix`
file, in the [UIScript format](../uiscript/README.md),
in `Contents/Resources/`. Opening windows needs no scope, so `needs` stays
as it was. (Before 0.6, windows needed `ui`; it's still accepted, and means
nothing.)

Save this as `Contents/Resources/main.uix`:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<UIScript version="1">
  <Window width="240" height="100"/>
  <Text name="result" x="12" y="12" width="216" height="40" alignment="center" text="Press Roll."/>
  <Button name="roll" x="84" y="62" width="72" height="26" text="Roll"/>
</UIScript>
```

Then open it at the end of `main.lua`, and handle the button:

```lua
local window = venture.ui.open("main.uix")

window:on("roll", function()
  window:setText("result", "You rolled " .. math.random(1, 6) .. ".")
end)
```

Open Dice, type `desktop`, and its window is there; clicking **Roll** shows a
new number. `roll` still works in the shell, since both live in the same app.

- **Closing the window quits Dice.** An app quits when the last window it
  opened closes, and quitting closes its windows and removes its commands.
- **Events** call the function you gave `window:on` for that control, with a
  table: `kind` (`pressed`, `submitted`, `changed`, or `selected`),
  `control`, and, where they apply, `text`, `value`, and `index` (from 1). A
  text field reports `submitted` when you press Enter, and a selectable list
  when an item is double-clicked, after `selected` for the first click.
- **Changing the window:** `setText`, `setValue`, `setItems` (a list's
  items), `select` (a list's selection), `setDisabled`, `setHidden`,
  `setChecked`, `focus`, `setTitle`, `setSubtitle` (a line under the title,
  or `nil` for none), and `close`, with readers `text`,
  `value`, `items`, and `selected`. Use a colon: `window:setText(...)`.
- Anything a handler prints shows in a message window on the desktop.

## Test the window

A test presses buttons and reads the window as a person would. Add this to
the project as `Tests/window.lua`:

```lua
test.case("Roll shows a number from 1 to 6", function(app)
  app:press("roll")
  local rolled = tonumber(app:text("result"):match("^You rolled (%d+)%.$"))
  test.expect(rolled and rolled >= 1 and rolled <= 6, app:text("result"))
end)
```

Run it with `airo test Dice.rproj`, as before, or with Run ▸ Test (Ctrl+U) in
Roundhouse, which puts each result in the Log tab. [Testing an
app](../airo/testing.md) lists what a test can read and press.

## Roundhouse

- **Start an app:** File ▸ New ▸ New App makes a project with a window app
  in it, a button that counts its presses, and opens it in the designer.
- **Bring one in:** opening `Dice.app` from outside `rootfs/` offers to make
  it a project, and File ▸ Open as Project… does the same.
- **Try it:** Run ▸ Run in Simulation runs the app in VentureOS's own
  desktop code, without QEMU. Run on VentureOS sends it to a VentureOS
  started with `./run.sh --vdb`.
- **Ship it:** File ▸ Export… puts the finished `.app` in `rootfs/user/apps/`.

## The title bar

- **The title:** the title bar is blank unless you give it a title, since
  the menu bar already names your app. Give one when it says something the
  menu bar doesn't, such as the open file's name:
  `venture.ui.open("main.uix", { title = "todo.txt" })`, or
  `window:setTitle("todo.txt")` later, and `window:setTitle(nil)` to blank
  it again. A subtitle on a blank bar takes the title's place.
- **Toolbar items:** `<ToolbarItem name="add" title="Add"/>` puts a button
  in the window's title bar, which reports `pressed` like any button. Notes
  uses toolbar items for Add and Remove. Items sit at the right end; give one
  `placement="start"` to put it after the window's buttons instead. Give
  one `image="addIcon"`, naming an `<Image>`, and it shows that image as a
  symbol in the text's color, so it follows light and dark, with its title
  as hover text and a taller bar. Draw symbols as dark shapes, with
  `#00FF01` or transparency where they're clear.
- **A title field:** `<TitleField name="address"/>` makes the title
  editable, as Files' address bar is: it reads as the title until it's
  clicked or given focus, then becomes a field that reports `submitted` on
  Enter.
- **A subtitle:** `subtitle="3 notes"` on `<Window>` puts a smaller line
  under the title, as `window:setSubtitle` does.

## Images and styles

- **Images:** declare one with `<Image name="logo" path="images/logo.png"/>`,
  its path beside the `.uix` file, and show it with
  `<Visual name="banner" image="logo" x="10" y="10" width="64" height="64"/>`,
  scaled to the visual's size. A `<Button>` with `image="logo"` shows the
  picture in place of a label, ringed in the accent while it's checked
  (`setChecked`), as a picker marks its choice. Images are PNG. One that
  can't be read stays a red outline, and the reason shows in a message
  window. `window:setImage("logo", path)` changes a picture while the app
  runs, from beside the document, the app's bundle, or the system's
  folder; anywhere else needs the `files` scope. It returns `true`, or
  `nil` and why.
- **Styles:** say what a color or size is for, and your app follows the
  desktop's light or dark look and accent: `textColor="secondaryText"`,
  `backgroundColor="inputBackground"`, `fontSize="title"`. A hex color such
  as `#1E5AA0` stays exactly that in every look unless you give it a dark
  twin, such as `darkTextColor="#8CB8F0"`. See
  [UIScript styles](../uiscript/README.md#styles) for every role.

## Text

- **Hover text:** give a control `tooltip="saveTip"`, naming a
  `<String name="saveTip" table="1" index="3"/>`, and put its text in a
  table beside your `.uix`, named `_1_<anything>.cst`, as `3 ^Save the file^`.
  It shows after the pointer rests on the control for half a second.
  Settings' color swatches do this. Tables follow the
  [CST specification](https://github.com/tonytins/swift-cst/blob/main/doc/CST.md),
  and VentureOS tells you about anything wrong in one, as Roundhouse does
  while you edit it. Number your own tables below 100. A value can also
  borrow another table's text, as The Sims Online's do, with a reference:
  the control character U+0002, then
  `<cst><cst_id>100</cst_id><string_id>1</string_id></cst>` for table 100's
  "OK", all between the value's carets.
- **Shared text:** tables 100 and up are VentureOS's, in
  `/mnt/system/strings`, for words every app uses alike. Table 100 holds
  OK, Cancel, Save, Don't Save, Open, Close, and Delete, keyed 1 to 7, so
  `<String name="ok" table="100" index="1"/>` and `string="ok"` on a button
  gives it "OK".
- **Text from Lua:** `venture.cst.get(table, key)` gives a table's text as
  written, and `venture.cst.format(table, key, ...)` fills its placeholders
  (`%s`, `%d`, `%02d`, ...) in order, from your own tables and the shared
  ones. `window:string(name, ...)` does the same for a `<String>` your
  window's `.uix` names. Each gives `nil` and why when there's no text, so
  you can show it. Files writes its "3 items" this way, from
  `2 ^%d items^` in its `_1_files.cst`:
  `venture.cst.format(1, 2, #list)`.
- **Longer text:** `<TextArea>` is a text field with several lines: Enter
  adds a line, and it reports `changed` as you type. TextEdit uses one.

## Menus

Add `<Menu>` elements to the `.uix` file, each holding its `<MenuItem>`s and
`<MenuSeparator>`s (or with the items after it, each naming it with `menu`),
and they appear in the menu bar while the window is in front. A `<Menu>`
inside another is a submenu, which opens beside its entry, as Notes' Note ▸
Sort By does; disabling the submenu by its name disables everything in it,
shortcuts included. A chosen item reports `pressed` with its name, like a
button.

Every app already has File, with Close Window (Ctrl+W); Edit, with Cut,
Copy, Paste, and Select All; and Help, with About your app, which shows its
`name`, `version`, and `description`. A menu with `role="file"` (or the
title `File`) adds its items to the top of File, and the same goes for
`edit`, `view`, and `help`. Edit works on every text field and area with no
code of yours, Undo and Redo included; setting a field's text yourself
starts its undo history over. Don't add your own Close Window, or your own
copy and paste. See [UIScript menus](../uiscript/README.md#menus).

## Sidebars and panes

- **A sidebar:** add `<Sidebar name="places" width="120"/>`, and the window
  gets a list of places down its left, its full height, as Files has. Fill
  and read it like a selectable list: `setItems`, `select`, `selected`, and a
  `selected` event. A row can be a table with an icon, naming an `<Image>`,
  and `{ separator = true }` puts a line between groups, as Files sets
  System apart:

  ```lua
  window:setItems("places", {
    { text = "Home", icon = "homeIcon" },
    { separator = true },
    { text = "System", icon = "driveIcon" },
  })
  ```

  Separators aren't rows, so System is row 2. Icons show in the text's
  color, as toolbar icons do; draw them as dark shapes. Only a sidebar
  takes tables; a plain list takes strings. The sidebar sits beside the
  content, so the window's size and your controls' positions don't change.
  See [UIScript sidebar](../uiscript/README.md#sidebar).
- **Panes:** for a sidebar that switches between sets of controls, as
  Settings has, add a `<Pane name="general" title="General">` for each row,
  in order, holding its controls (or `pane="general"` on each control
  instead, outside any pane). The sidebar shows the panes' titles and only
  the chosen pane's controls, with no code of yours; a control in no pane
  shows on every pane. The rows are the document's, so `setItems` on that
  sidebar is an error, but `select` and the `selected` event work as usual.
  See [UIScript panes](../uiscript/README.md#panes).

## Resizing

`resizable="true"` on `<Window>` puts a grip in the window's bottom-right
corner. The window starts at its `width` and `height` and shrinks no further
than `minWidth` and `minHeight` (or its starting size, without them). Give a
control edge offsets to keep it to the window's edges: `edgeOffsetRight`
alone pins it to the right, and `edgeOffsetLeft` with `edgeOffsetRight`
stretches it, as Files' list does. Leave the bottom right 16 pixels clear for
the grip.

## More from Lua

- **A shell of its own:** `local shell = venture.openShell()` (with `shell` in
  `needs`), then `shell:run("ls")` returns what the command printed, and
  `shell:directory()` its current folder. Terminal is built on it.
- **Opening files:** `venture.ui.onOpenFile(function(path) ... end)` lets
  Files hand your app a file, through `venture.ui.launch("YourApp", path)`.
  If your app opens no window when it starts, also give it
  `venture.ui.onNewWindow`, which the dock uses to open one.
- **Timers:** `window:every(1, refresh)` runs `refresh` every second while
  the desktop shows, until the window closes or the app quits; keep the
  timer it returns and call `timer:stop()` to stop sooner. Intervals start
  at 0.1 seconds. System Monitor uses one.
- **More:** `venture.ui.preview(path)` opens a file in Quick Look,
  `venture.ui.launch(path)` opens another app, and
  `venture.ui.onNewWindow(fn)` adds New Window (Ctrl+N) to your app's menu;
  the dock calls it too when your app has no window open. `Files.app` uses
  all three.

`Notes.app` has a fuller window: a text field, a list, and a menu.

## Panels and shelves

An app's interface can be a **panel**, pinned to an edge of the screen with
no title bar, as the dock is. Write `<Panel>`, with an `edge`, in place of
`<Window>`:

```xml
<UIScript version="1">
  <Panel width="400" height="72" edge="bottom"/>
  <Shelf name="apps" x="0" y="0" width="400" height="72"/>
</UIScript>
```

- A panel sits above every other window, never takes the keyboard, and
  can't be moved, closed by a button, minimized, or zoomed. A bottom panel
  is as wide as its shelf and centred, and follows the dock's setting in
  Settings; a top panel spans the screen.
- The top edge holds the menu bar, Files' top panel, and there's one top
  panel at a time, so opening another is an error.
- A **shelf** (`<Shelf>`) is a row of icons. Fill
  it with `window:setItems`, each item a table: `title`, and if you like an
  `icon` (a PNG, in your app or any app's bundle), `open = true` for a dot
  under it, a `group` number, with a divider between groups, and
  `window = true` for a window's pale tile. A click is a `pressed` event with
  the item's `index`.
- See [panels](../uiscript/README.md#panel) and
  [the shelf](../uiscript/README.md#shelf) for the rules.

An app with the `system` scope can do what the dock does through
`venture.desktop`:

| Function | Does |
|---|---|
| `venture.desktop.apps()` | Every app, Files first, then the system's and the user's: `{ name, path, icon, identifier, user, open }` each |
| `venture.desktop.open(path)` | Opens an app as the dock does: brings it forward if it's open, with a new window if it has none |
| `venture.desktop.minimized()` | Minimized windows, `{ id, title, app }` each |
| `venture.desktop.restore(id)` | Brings a minimized window back |
| `venture.desktop.onChange(fn)` | Calls `fn` when apps open, close, start, or end, or windows are minimized or restored, at most once a frame |

`Dock.app` in `rootfs/system/apps` is all of this in about fifty lines.

When something's off, [Troubleshooting](./troubleshooting.md) lists what
VentureOS says and what to do.
