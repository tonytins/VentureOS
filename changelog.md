# Changelog

## 0.7.406

- vdb's connections are encrypted: everything after hello is sealed with ChaCha20-Poly1305, with keys made from your computer's identity, so what's sent can't be read or changed on the way. VentureOS knows your computer by its key ID, a hash of the identity, which the Trust alert now shows.
- Your identity crosses the network once, when you choose Trust; trusting it ahead of time in `developer.local.lua` means it never does.
- Breaking: vdb tools and VentureOS builds from before this can't talk to these. Rebuild the tool with `swift build` in `tools/vdb`.

## 0.7.404

- Apps can declare a sidebar's panes in their `.uix`: `requires="sidebar panes"`, a `<Pane name="…" title="…"/>` for each row, and `pane="…"` on each control. The sidebar shows the panes and only the chosen pane's controls, with no Lua.
- Settings is written this way, and looks and works as before.
- Roundhouse's designer draws a sidebar's rows and one pane at a time, where it drew every pane's controls on top of each other. Clicking a pane's row on the canvas, or choosing anything in a pane, shows that pane, and a control added from the library goes into it.

## 0.7.403

- Breaking, for apps that write their own panels: a panel is now `<Panel width="…" height="…" edge="top|bottom">` in place of `<Window role="panel" edge="…">`, which is read as an ordinary window now. The dock and the menu bar use it, and look and work as before.
- Roundhouse's designer resizes a panel by its handles, as it does a window.

## 0.7.402

- Updated to Cosmos 3.0.99.

## 0.7.304

- The dock can hide: Settings ▸ Dock offers Always visible, as before; Hide automatically, which slides it away until the pointer reaches the bottom of the screen; and Hide when a window covers it. A hidden dock lets windows zoom to the bottom of the screen. The choice is saved with the look.
- Scripts can read and set it as `dock` in `venture.appearance()` and `venture.setAppearance{ dock = "always" }`, `"autohide"`, or `"dodge"`.

## 0.7.303

- The menu bar is Files' top panel: it's there while Files runs, which VentureOS sees to, and it looks and works as before, menus, shortcuts, and Alt with a letter included. If Files stops, the bar is back as soon as Files is.
- Only one top panel can be open, the menu bar's; an app opening another is told why.

## 0.7.302

- The dock is an app, Dock, which VentureOS keeps running: it shows the same apps and windows, but as a panel, so zoomed and new windows now stop above it instead of running underneath. If it stops, it's back at once, and `ps` lists it.
- Apps with the `system` scope can do what the dock does with `venture.desktop`: list the apps (`apps()`), open one as the dock does (`open(path)`), list and bring back minimized windows (`minimized()`, `restore(id)`), and hear when any of that changes (`onChange`).

## 0.7.301

- Apps can open panels: a window whose `.uix` says `role="panel"` and `edge="bottom"` or `"top"` is pinned to that edge, with no title bar, above every other window, and never takes the keyboard. New windows open between the panels, and zoom fills the space between them.
- A `<Shelf>` (with `requires="shelf"`) shows a row of icons, as the dock does; an app fills it with `window:setItems`, each item a `title`, an `icon`, `open`, `group`, and `window`, and hears which was clicked.
- Roundhouse's library has a shelf, and its designer shows a panel as designed, with sample icons on an empty shelf.

## 0.7.204

- `ps` lists the running apps: each one's ID, how long it has run, and whether VentureOS keeps it running. `kill` ends one by its ID, name, or identifier, such as `kill 3` or `kill calculator`; an app VentureOS keeps, such as Files, starts again, and it says so.
- Scripts can do the same with `venture.processes()` and `venture.kill(target)`, under the `system` scope.

## 0.7.203

- An app whose code keeps failing, three times within a minute, is closed, and the desktop says it crashed, in front of the errors that led to it. An app that fails now and then carries on, as before.
- Files comes back at once when it's closed or crashes, and if it can't start, VentureOS keeps trying, waiting longer each time, as before.

## 0.7.105

- `vdb`, in `tools/vdb`, sends an app from your computer to a running VentureOS: `vdb install Dice.app --launch` installs and opens it, and `--for 5` or `--follow` prints what it prints. It never asks anything, exits with 1 and VentureOS's reason when something's refused, and `vdb identity --lua` prints the lines that make your own builds trust your computer. See [its README](tools/vdb/README.md).
- The desktop opens without a Files window: Files starts for the menu bar, and its dock icon or New Window opens one.
- vdb says which folders it looked in when it can't find an app to open.

## 0.7.104

- Developer mode, for vdb: Settings ▸ Developer turns it on, and while the desktop runs, VentureOS listens on port 28455 for a computer sending an app. An app sent is checked as apps are at boot, installed in `/mnt/user/apps`, and opened, replacing an open copy, and what it prints goes back to the computer. A computer VentureOS doesn't know is asked about first, with Trust and Deny. Developer mode and trust last until VentureOS restarts.
- Scripts can turn developer mode on and off, trust a computer, and check it with `venture.developer.enable()`, `disable()`, `trust(identity)`, and `status()`, under the `system` scope.
- `init.lua` runs `developer.local.lua` beside it when a build has one, to turn developer mode on at every boot; it's git-ignored, so only your own builds have it.
- `./run.sh --vdb` forwards vdb's port into QEMU.

## 0.6.505

- VentureOS 0.6, Izumo, is released. See [the release notes](docs/releases/0.6.md).

## 0.6.504

- Opening another bundle while one is open in the designer no longer crashes Roundhouse.
- Leaving a bundle with unsaved edits, by opening another, making a new app, or closing Roundhouse, asks first: Save All, Don't Save, or Cancel.
- A bundle's `Info.xml` shows its form above its XML, in the same Text tab an interface's XML is in; each follows the other, and Undo takes back a change made in the form. An `Info.xml` VentureOS can't read shows why in the form's place, over its text to fix. Edit as Text is gone, since the text is always there.
- A Log tab beside Text shows what Roundhouse did and what VentureOS's apps printed in Run, and Roundhouse keeps the same log in a file. An error nothing caught is logged and reported in the status line instead of closing Roundhouse.

## 0.6.503

- Roundhouse's File ▸ New App (Ctrl+N) makes a window app to start from: give it a name, an identifier (suggested from the name), and a place, and it opens in the designer, ready for Run. Its button counts its presses. A name or identifier VentureOS wouldn't take is explained in the dialog, and nothing is made.

## 0.6.502

- Roundhouse shows a bundle's `Info.xml` as a form: name, identifier, version, and description; check boxes for the scopes an app needs; and its main script and icon, chosen from the bundle's own files, with the icon shown as VentureOS's dock shows it. A value VentureOS wouldn't accept, such as an identifier that isn't reverse-DNS, says why under its field, and every change edits only that attribute in the file.
- Edit as Text (or View ▸ Info.xml as Text) shows the file as text instead; an `Info.xml` VentureOS can't read opens as text with the reason.

## 0.6.501

- Roundhouse edits a bundle's other files: Lua in an editor with highlighting, other text as plain text, and pictures shown as they are. A file that isn't text says so.
- Files with unsaved edits are marked ● in the list. File ▸ Save (Ctrl+S) saves the file shown, and Save All (Ctrl+Shift+S) every file with edits.
- Run runs every unsaved edit of the bundle, not only the interface's. Undo and Redo work in whichever editor is showing.

## 0.6.500

- Roundhouse opens a whole bundle, such as an app: open its folder (File ▸ Open Bundle…, Ctrl+Shift+O) or any file in it. Its files are listed over the outline, and choosing one shows it: an interface in the designer. Edits stay while you move between files, until they're saved.
- Run runs the bundle's app from whichever of its files is open.

## 0.6.403

- Roundhouse's designer shows a document's menus above its window, in VentureOS's menu bar, as Xcode shows an app's: the app's name in bold, then its menus. Click a menu to open it and select it, and click an item to select that; Escape or a click elsewhere closes it. A document without menus shows no bar.

## 0.6.402

- Roundhouse's Run runs VentureOS: its desktop in a window beside the designer, at 1024x768, with the menu bar, the dock, Files, and the system's and user's apps, with the app being designed open as it's edited, saved or not. A document outside an app opens as a window of its own.
- Run ▸ Restart (Ctrl+Shift+R) starts a fresh run; Run ▸ Stop (Ctrl+.), closing the window, Return to Shell, or closing the designer ends it. Each run starts from a fresh copy of VentureOS's files, and nothing it changes outlasts it.
- Settings in a run changes the run's look, not the designer's, and the designer's Dark and accent don't change the run.

## 0.6.400

- Roundhouse's bar shows the document and its app on one line, such as `main.uix — Calculator.app`.
- Run ▸ Run (▶ Run, Ctrl+R) opens the window VentureOS will run in, beside the designer. For now it only prepares a fresh copy of VentureOS's files, which goes when the window closes.

## 0.6.308

- Roundhouse comes as packages that need nothing installed: an AppImage and a `.tar.gz` for Linux, a zip for Windows, and `Roundhouse.app` for macOS, Apple silicon and Intel, built by the scripts in `packaging/`.
- Roundhouse has an icon, a placeholder until its own art arrives.

## 0.6.307

- Roundhouse's designer edits the rest of a window: click the title bar to select the window, and drag its right or bottom edge to resize it; click a toolbar item, the title field, or the sidebar to select it, and drag the sidebar's edge to set its width.
- Insert (and Add ▾) also adds a toolbar item, a title field, a menu, a menu item, a menu separator, or a sidebar. A menu or sidebar adds `requires="menus"` or `"sidebar"` for you; a menu item joins the selected menu. Forward and Backward move toolbar items along the bar.

## 0.6.306

- The document's name and its app in Roundhouse's bar are no longer cut off at the bottom.

## 0.6.305

- Roundhouse's bar is laid out like a VentureOS window's title bar: red, orange, and green buttons to close, minimize, and zoom at the left, then ☰, Add, Undo, and Redo; the document's name centred with its app, such as Calculator.app, under it; and 2× and Text at the right.
- On KDE and other Linux desktops, the bar replaces the window manager's title bar instead of sitting under it.

## 0.6.304

- Roundhouse has menus: File, Edit, Insert, Arrange, and View, with their shortcuts. On KDE and macOS they appear in the desktop's global menu bar; elsewhere, the ☰ button opens them.
- A new look, as a desktop IDE has: a blue-to-teal bar across the top, which is also the window's title bar where the desktop allows, with Add, Undo, Redo, 2×, and Text on it; compact panes headed OUTLINE and INSPECTOR; and a status bar showing the caret's line and column. Dark and the accent moved to the View menu, and Duplicate, Delete, Forward, and Backward to Edit and Arrange.

## 0.6.303

- Roundhouse's library: Add ▾ puts a text, button, text field, text area, list, slider, progress bar, or region in the middle of the window, in front, named after its kind, and selects it.
- Duplicate (Ctrl+D) copies the selection just in front of it, set off a little; Delete removes it with the comment above it; Forward (Ctrl+]) and Backward (Ctrl+[) move a control in front of or behind its neighbour. The shortcuts work while the canvas or outline has focus, so they don't get in the way of typing. Anything VentureOS wouldn't read, such as deleting the window, is refused with the reason.

## 0.6.302

- Roundhouse's outline lists every element of the document in order, and selecting one there selects it on the canvas, and back. Styles, strings, and menus, which aren't drawn, are selected here.
- The inspector shows every attribute the selected element can have, grouped into Identity, Frame, Text, Appearance, and Behaviour, with attributes VentureOS doesn't read under Other. Type a value and press Enter, or leave the field; empty it to remove the attribute. A value VentureOS wouldn't accept is explained under its field and changes nothing, and Escape puts the field back.

## 0.6.301

- Roundhouse's designer: click a control on the canvas to select it, drag it to move it, and drag its handles to resize it. The arrow keys move it a pixel at a time, or eight with Shift. Each drag is one change, which Undo takes back, and the text follows it. Clicking the window's background selects the window.
- View the canvas at 100% or 200%.

## 0.6.300

- Roundhouse's window makes room for the designer: an outline on the left, an inspector on the right, and the canvas over the text, which the Text button hides and shows again.

## 0.6.203

- Roundhouse saves a `.uix` exactly as it was opened when you haven't changed it, byte order mark and line endings included, and keeps everything you didn't change when you have.
- When VentureOS can't read a document, Roundhouse's status line also says that only its text can be edited until it can.

## 0.6.103

- Roundhouse opens a `.uix` (`Roundhouse document.uix`, or Open… with Ctrl+O) and shows its text, highlighted, beside the window VentureOS would draw. The window redraws a moment after you stop typing. Text VentureOS wouldn't read keeps the last drawing, and the status line names the line that's wrong. Switch Dark and the accent to see either look, and Save (Ctrl+S) writes the text back.

## 0.6.102

- Roundhouse: `Roundhouse --render document.uix picture.png` draws a `.uix` as VentureOS shows it, in a window over the sky, into a PNG file, in light or `--dark` and with any `--accent`.

## 0.6.100

- Apps open windows without listing `ui` in `needs`. An app or script that still lists it works as before.
- Any script, not just an app, can show a message with `venture.ui.alert(message, title)`, Quick Look a file with `venture.ui.preview(path)`, and open an app with `venture.ui.launch(app, file)`. Without the desktop on the screen, an alert is printed.

## 0.5.506

- VentureOS 0.5, Cassiopeia, is released. See [the release notes](docs/releases/0.5.md).

## 0.5.505

- Automatic dark mode: choose Auto in Settings ▸ Appearance ▸ Style, and the desktop is dark from 7 PM until 7 AM, by the clock, switching on the minute. Change the times beside it, or with `appearance auto --from 20:00 --until 06:30`.

## 0.5.504

- Choose the wallpaper in Settings ▸ Appearance ▸ Wallpaper, from thumbnails: the sky, which follows the accent, or Earthset. The desktop changes at once.
- Scripts: `venture.wallpapers()` lists the wallpapers, and `venture.setAppearance{ wallpaper = "sky" }` chooses one.

## 0.5.502

- Apps: buttons with an `image` show it, ringed in the accent while checked, and `window:setImage(name, path)` changes a picture while the app runs.

## 0.5.501

- Windows have rounded corners, all four, and rounded shadows. Settings ▸ Appearance ▸ Window corners switches them to square.

## 0.5.500

- Scripts: `venture.appearance()` also gives `dark` (whether the look showing is dark), `corners`, `wallpaper`, `darkFrom`, and `darkUntil`, and its `mode` can be `auto`; `venture.setAppearance{ ... }` changes any of them. VentureOS doesn't use the new ones yet.
- Wallpapers are bundles, such as `Earthset.wallpaper`, and can carry a light and a dark picture.

## 0.5.403

- Settings has a sidebar with two panes, Appearance and Clock, as GNOME Settings does, and opens on Appearance.
- In Settings' Clock pane, type only the offset after the "UTC" label, such as `-05:00` or `5:30`.
- An offset without a sign is ahead of UTC, so `clock 5:30` sets UTC+05:30.

## 0.5.402

- Sidebars mark the selected row, and the one under the pointer, with a rounded shade, as Nautilus does.
- Files sets System apart from Home, User, and Apps with a faint line.
- Apps: a sidebar's rows can have icons, drawn in the text's color, and be split into groups, with tables in `window:setItems`, such as `{ text = "Home", icon = "homeIcon" }` and `{ separator = true }`.

## 0.5.300

- Title bars are 30 pixels tall, from 28, which leaves more room to grab and drag a window.
- The desktop takes GNOME's colors: neutral windows, sidebars, and menus in light and dark, with faint lines, and a neutral menu bar. Only the accent carries color.
- Settings offers GNOME's nine accent colors, from Blue, the new standard, to Slate. An accent you chose before still works, shown as Custom.
- Title bars are flat and blend with the window, with a faint line under them and beside the sidebar, as GNOME's do.
- Resizable windows have a green zoom button: it fills the screen below the menu bar, and a second click puts the window back.
- Files shows how many items a folder has under its address.
- Toolbar items are flat, as GNOME's are, shaded only while the pointer's over one or it's pressed.
- Apps: a subtitle under the window's title, with `subtitle` on `<Window>` or `window:setSubtitle(text)`; the bar grows taller for it.
- Apps: toolbar items can sit at the start of the bar (`placement="start"`), and show an icon (`image`) drawn in the text's color, so it follows light and dark, with the item's title as hover text.
- Apps: images in an app's `.uix` files show, from PNG files beside them, such as `<Image name="logo" path="images/logo.png"/>` shown with a `<Visual>`.

## 0.5.201

- The menu bar shows the time on its right, as Tiger did: `Tue 3:45 PM`.
- Set how far your clock is from UTC in Settings ▸ Clock, or with the new `clock` command (`clock -05:00`); the choice is saved. There's no daylight saving, so change it when the clocks change.
- Commands take values that start with a dash and a digit, such as `-05:00`, as arguments rather than options.
- Scripts: `venture.time()` and `venture.setUtcOffset(offset)`, with `system` in `-- Needs:`.

## 0.5.200

- The VentureOS menu shows VentureOS's icon in place of its name, as Tiger showed the Apple, and takes less room. Open, it turns white.
- About VentureOS shows the icon too.
- The menu bar has Tiger's gloss: a bright band over its top half and a sheen along its top edge, in light and dark.
- Icons smoothed against `#00FF01` come out clean: their edges keep the shape's color, partly clear, with no green fringe. Files and Terminal now have shapes of their own.

## 0.5.100

- The desktop has a wallpaper: Earthset, the Earth setting behind the Moon.
- Apps show their icons in the dock and in their About window. Files, Terminal, Notes, and TextEdit have icons; other apps keep their colored tile.
- Icons with a full background are rounded to a tile, as the letter tiles are. An icon can have a shape of its own, such as a folder, by painting what should be clear in exactly `#00FF01`; it gets a soft shadow.
- Apps: `icon` in `Info.xml` names a PNG inside the bundle, drawn at 48 pixels in the dock and 64 in About.

## 0.4.500

- VentureOS 0.4, Pere Marquette, is released. See [the release notes](docs/releases/0.4.md).
- Undo (Ctrl+Z) and Redo (Ctrl+Shift+Z) in the Edit menu, for any text field, text area, or window address. Typing comes back a word at a time; a paste or cut, one at a time.

## 0.4.401

- New commands: `resolve example.com` looks up a host name's address, and `ping 10.0.2.2` sends echo requests and shows how long each reply took (`-c` sets how many, up to 20).
- `net` shows the DNS server the network gave.
- Scripts: `venture.resolve(name)` and `venture.ping(address)`, with `network` in `-- Needs:`.

## 0.4.400

- File, Edit, and Help are always in the menu bar, whatever's open. Items that can't do anything at the moment are dimmed, such as Copy with no text selected.

## 0.4.300

- Every app has Cut (Ctrl+X), Copy (Ctrl+C), Paste (Ctrl+V), and Select All (Ctrl+A) in its Edit menu while you're typing in a text field, a text area, or a window's address. What you copy in one app pastes into any other, until you leave the desktop.
- Pasting into a one-line field turns line breaks into spaces, and stops at the field's length. Password fields can't be cut or copied.

## 0.4.200

- Select text in any text field or text area: drag across it, hold Shift with the arrow keys, Home, or End, or press Ctrl+A for all of it. Typing replaces what's selected, and Backspace or Delete removes it.
- Clicking in a text field puts the caret where you click, rather than at the end.
- Clicking a window's address, or pressing Ctrl+L in Files, selects the whole path, so you can type a new one; press End or an arrow key to edit it instead.

## 0.4.102

- Every app has a File menu with Close Window (Ctrl+W), and a Help menu with About the app, showing its name, version, and what it's for. An app's own File items appear at the top of File.
- App authors: `role="file"`, `"edit"`, `"view"`, or `"help"` on a `<Menu>` adds it to that standard menu, and `description` in `Info.xml` shows in About. A menu titled `File` merges even without a role. Leave out your own Close Window; if you keep one with Ctrl+W, yours is used instead.

## 0.3.18

- VentureOS 0.3 is released. See [the release notes](docs/releases/0.3.md).

## 0.3.15

- Hover text: rest the pointer on a control for half a second to see what it is, such as the name of each accent color in Settings.
- App authors: a control's `tooltip` names a `<String>`, whose text comes from a CST table beside the app's `.uix`, such as `_1_strings.cst`. See docs/writing-apps.md.

## 0.3.14

- New app: Settings, in the dock. Choose light or dark, and an accent color from eight swatches or your own `#RRGGBB`. Changes apply to every window at once.
- The desktop's look is saved in `/mnt/user/settings/appearance.xml`, whether it's changed in Settings or with `appearance`, and restored when VentureOS starts. Like other files, a change lasts until reboot for now; the file that ships sets the look a boot starts with.

## 0.3.13

- Calculator and About read properly in the dark appearance; quieter text in Files, TextEdit, Terminal, and System Monitor follows the appearance too.
- App authors: styles can say what a color or size is for, such as `textColor="secondaryText"` or `fontSize="title"`, and follow the desktop's look; a hex color can have a dark twin, such as `darkTextColor`. See docs/uiscript-styles.md.

## 0.3.12

- The desktop has a dark appearance, and any accent color: `appearance dark`, `appearance light`, or `appearance --accent "#E8922A"` in the shell or Terminal. Windows, the sky, the menu bar, and the dock change at once. The choice lasts until reboot.
- Scripts: `venture.appearance()` and `venture.setAppearance(mode, accent)`, with `system` in `-- Needs:`.

## 0.3.11

- The desktop shows what the pointer rests on: the close and minimize buttons show their × and −, a window's address turns blue, buttons lighten, and sidebar places are shaded.
- Files and TextEdit windows can be resized: drag the dots in the bottom-right corner.
- App authors: `resizable="true"` on `<Window>` makes a window resizable, no smaller than its `minWidth` and `minHeight`; controls follow the edges their edge offsets name.

## 0.3.10

- Files has a sidebar: Home, User, Apps, and System run down the window's left, its full height, with the close and minimize buttons on top.
- App authors: `<Sidebar>`, with `requires="sidebar"`, gives a window a sidebar of its own.

## 0.3.9

- Window titles sit in the middle of the title bar.
- Files' address reads as the window's title until you click it or press Ctrl+L, then becomes a field. Press Enter to go there; the Go button is gone. A long path keeps its end in view.

## 0.3.8

- New app: Terminal, the shell in a window, in Fira Code. Each window has its own current folder; `exit` closes it, and Shell ▸ Clear (Ctrl+K) empties it. `desktop` and the interactive `lua` prompt only work on the text console, and say so.
- A window gets its typing back where it was when you bring it forward again.

## 0.3.7

- New app: TextEdit, in your apps. Type a path in its title bar and press Enter to open a file, or Save (Ctrl+S) to create one.
- Double-clicking a text file in Files opens it in TextEdit.
- App authors: `<TextArea>` is a multi-line text field, and `venture.ui.launch(app, file)` with `venture.ui.onOpenFile` hands a file to an app.

## 0.3.5

- New app: System Monitor shows the version, how long the system has been up, memory in use, and the network, updated every second.
- App authors: `window:every(seconds, fn)` runs code on a schedule while the desktop shows.

## 0.3.4

- Windows have a new title bar: close and minimize on the left, the title, and the window's own buttons on the right. Minimize sends a window to the dock; click its tile, or its app's icon, to bring it back.
- Files has an address bar: type a folder and press Enter or Go. Bookmarks down the left go to Home, User, Apps, and System.
- Notes moved to your apps, in `/mnt/user/apps`. Add and Remove are in its title bar.
- Apps no longer say what they need when they open from the desktop or from your apps.

## 0.3.3

- New app: Calculator, for adding, subtracting, multiplying, and dividing.
- Your own apps live in `/mnt/user/apps`, next to the system's in `/mnt/system/apps`. Unlike system apps, you can change or delete them, and they say what they need each time they open. `apps` shows which is which, and the dock puts yours after a divider.

## 0.3.2

- Double-click a folder, app, or file in Files to open it.
- App authors: double-clicking an item in a selectable list reports `submitted`.

## 0.3.1

- New app: Files. The desktop opens with a Files window. Select something and press Open (Ctrl+O): a folder opens in the window, an app starts, and anything else shows in Quick Look (Ctrl+Y). Go ▸ Enclosing Folder (Ctrl+U) goes back up.
- Quick Look shows a file without opening it: text, an interface (`.uix`), an app's details, or a file's size.
- Files is always there. Closing its last window leaves it running; click it in the dock, or choose Files ▸ New Window (Ctrl+N), for another. If it stops, the desktop starts it again.
- About VentureOS is now in the VentureOS menu, rather than opening with the desktop.

## 0.3.0

VentureOS has a desktop. Type `desktop` at the prompt to open it, and choose VentureOS ▸ Return to Shell (Ctrl+Shift+Q) to come back.

- A Tiger-style desktop at 1024x768: a menu bar, a dock with the system's apps, and windows you can drag, bring to the front, and close.
- Apps open from the dock and can have windows. Notes has one, with a Note menu; closing an app's last window quits it.
- Interfaces are `.uix` files in Hudini's UIScript format, with menus. `desktop <file.uix>` previews one.
- App authors: `venture.ui` and the `ui` scope; see [Writing apps](docs/writing-apps.md).

The 0.2.20 to 0.2.23 entries below describe each step.

## 0.2.23

- Apps can have windows. Notes opens one: type a note and press Enter to add it, select one and press Delete (or Ctrl+D) to remove it, and use the Note menu. The `note` command still works, on the same notes.
- Closing an app's last window quits the app.
- An app's windows stay open when you leave the desktop and come back, and an app opened with `open` shows its window the next time you type `desktop`.
- App authors: `venture.ui` opens windows from `.uix` files, under the new `ui` scope. See [Writing apps](docs/writing-apps.md).

## 0.2.22

- The desktop has a menu bar and a dock. The dock shows the system's apps: click one to open it, and a dot marks the open ones. The app's menu has Quit (Ctrl+Q).
- An interface's own menus appear in the menu bar while its window is in front. Alt and the underlined letter open a menu; shortcuts such as Ctrl+N work from anywhere.
- To leave the desktop, choose VentureOS ▸ Return to Shell, or press Ctrl+Shift+Q. Esc no longer leaves; it closes an open menu.
- Messages from an app you open in the desktop show in a window.

## 0.2.21

- The desktop has windows. It opens with About VentureOS; drag a window by its title bar, click one to bring it to the front, and click its red button to close it.
- `desktop <file.uix>` opens a `.uix` interface in a window, to preview it as you write it. A file that can't be read is reported in the shell, and the desktop doesn't open.

## 0.2.20

- New `desktop` command: a blue desktop with the pointer, drawn at 1024x768. Press Esc to go back to the shell. Windows, the menu bar, and the dock come next.
- The screen is now 1024x768 in the shell too.

## 0.2.19

- VentureOS ships its first app, Notes: `open notes`, then `note <text>` to add a note, `note` to list them, `note -d <number>` to delete one, and `note --clear` to start over. Notes are kept in `notes.txt` in the folder you opened it from.
- New guide: [Writing apps](docs/writing-apps.md).
- When a script or app uses something it hasn't declared, the message now says where to declare it: a script's `-- Needs:` line, or an app's `needs` in `Info.xml`.

## 0.2.18

- Apps: `open <app>` opens an `.app` bundle, `close <app>` closes it and removes everything it added, and `apps` lists the apps and which are open. An app opened from outside the system's apps says what it needs first.

## 0.2.17

- File commands: `ls` (with `-l`), `cat`, `write` (with `-a` to add to the end), `mkdir` (with `-p`), `rm` (with `-r`), `df`, and `pwd`. `--help` explains each one's options.
- `/mnt/system`, the system's own folder, can be read but not changed, so `rm -r` there is refused.
- Lua scripts can work with files through `venture.files`, after declaring `-- Needs: files`.

## 0.2.16

- The shell has a current folder, shown in the prompt (`/mnt> `). `cd <folder>` changes it, `cd ..` goes up, and `cd` on its own goes back to `/mnt`.
- Scripts and Lua code run from the current folder, so relative paths such as `dofile('notes.lua')` start there.

## 0.2.15

- Commands made at the `lua` prompt with `venture.command` can be tried right there: a line starting with `!` runs a shell command, such as `!greet Tony`. They vanish when you `exit`.
- At the prompt, a command can stand in for one of the system's script commands, such as `echo`, until you `exit`. VentureOS says so when it happens.
- `venture.shell("line")` runs a shell command from Lua, in `init.lua` and at the prompt.

## 0.2.14

- `init.lua` can add commands to the shell with `venture.command`, after declaring `-- Needs: shell`. They last until shutdown, and if `init.lua` fails, the commands it added are removed again.
- `help <command>` shows who added a command, such as `From: init.lua`.

## 0.2.13

- VentureOS ships a Lua library in `/mnt/system/lib` that scripts load with `require`. Its first module, `venture.args`, lets a command declare its arguments and options (`--loud`, `-l`, `--times 3`) and handles the rest: clear messages for mistakes, and a generated `--help`.

## 0.2.12

- Typing `lua` on its own opens a Lua prompt, `lua>`, for trying things out. Values carry over between lines, an expression prints its value (`1 + 1` prints `2`), and an unfinished statement continues on the next line. Ctrl+C or Esc stops the line that's running, and `exit` returns to the shell.

## 0.2.11

- Lua scripts declare what they use with a `-- Needs:` line in their header, like an app's permissions: `system` for `venture.uptime()` and `venture.memory()`, and `network` for `venture.net()`. Every script can call `venture.version()`.
- Calling an undeclared function names the line that's missing, and a script that needs a scope VentureOS doesn't offer won't run. `help <command>` shows what a script needs.
- `lua` can still use everything.

## 0.2.10

- Ctrl+C or Esc stops a Lua script that's running, from `lua` or a script command, with `Interrupted.`, and returns to the prompt. This works even if the script catches errors with `pcall` or runs in a coroutine.
- `init.lua` also stops on its own after 10 seconds, so a stuck script can't keep the shell from starting.
- Lua scripts can no longer replace the hook with `debug.sethook`; it reports that it's reserved by VentureOS.

## 0.2.9

- After boot, VentureOS runs `/mnt/system/init.lua`. If it fails, the error is shown and the shell starts anyway. For now it prints the "Type help" hint, which used to be built into the kernel.

## 0.2.8

- `info` and `net` are now Lua scripts in `/mnt/system/commands`, with the same output, except that `net` shows `yes` or `no` for the link and readiness instead of `True` or `False`.
- Lua code, in `lua` and in script commands, can read the system through a `venture` table: `venture.version()`, `venture.uptime()`, `venture.memory()`, and `venture.net()`.

## 0.2.7

- Commands can be written in Lua. A file at `/mnt/system/commands/<name>.lua` runs as the command `name`, with its arguments as `...`, and shows up in `help`. Its first comment line is its description, and a `-- Usage:` comment line its usage.
- `echo` is now one of these Lua scripts, and works as before.
- Lua output, from `lua` and from script commands, now goes through the shell like every other command.
- Boot installs VentureOS's own system files to `/mnt/system`, and says so in the boot messages. They come from the kernel, so changes made to them are lost at reboot.

## 0.2.5

- New `lua` command: runs a line of Lua 5.5, such as `lua "print(6 * 7)"`. Relative paths start at `/mnt`, so `lua "dofile('script.lua')"` runs a script from the RAM disk. Lua errors are shown with a traceback, and the shell carries on.

## 0.2.2

- New `echo` command: prints its arguments on one line, so `echo "a  b" c` shows `a  b c`.

## 0.2.0

- VentureOS reaches alpha: it boots reliably, explains failures, and has a shell with arguments and commands to inspect and control the system. No other changes since 0.1.117.

## 0.1.117

- Commands now take arguments. Quotes group words (`help "net"`), and a missing quote or the wrong number of arguments is explained instead of ignored.
- `help` lists every command with a short description, and `help <command>` shows how to use one.
- New `info` command: version, build, commit, Cosmos version, uptime, and memory.
- New `net` command: the network adapter, its MAC address and link, and its IP address, subnet, and gateway.
- New `reboot` and `shutdown` commands, beside `halt`.
- Command names ignore case, so `HELP` works like `help`.

## 0.1.116

- If a part of the system it can't run without fails at boot (filesystem or timer), VentureOS now stops with a panic screen that names the failed step and the reason, instead of crashing without explanation.
- If networking fails at boot, VentureOS reports it and carries on without the network.
- Boot messages say which step failed, such as the RAM disk's format or mount step, a DHCP timeout, or a missing network card.
- A failing command reports its error and returns to the prompt instead of taking the system down.
- Boot messages, the version, and any panic report are also written to the serial log, prefixed with `[Venture]`, so a headless boot can be followed.
- The version and build are shown at the start of boot as well as when boot finishes.
- Updated to Cosmos 3.0.94.

## 0.1.102

- Filesystem
- Networking

## 0.1.101

Start of the project!