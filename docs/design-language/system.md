# System

The system book is VentureOS's own: the palette's exact values, the parts of the screen the host draws, and the
words the desktop uses. Read it to change the desktop. [Foundations](./foundations.md) has the vocabulary it uses.

## The palette

VentureOS works out its whole palette from one accent (`DesktopPalette.Create`).

- **Surfaces and text** follow libadwaita's: on light, a near-white window (`#FAFAFB`), white views and header bars,
  a cooler sidebar (`#EBEBED`), and text at 80% of near-black (`#333338`). On dark, a `#222226` window, a darker
  view (`#1D1D20`), a lighter header bar and sidebar (`#2E2E32`), and white text.
- **Quieter text and lines** are text at lower strengths: secondary text at 55%, disabled text at 50%, borders at
  20%.
- **Hover and press** are text laid over a surface at 7% and 16%.
- **The accent** starts as GNOME's blue, `#3584E4`. The user can choose any color, or one of GNOME's nine swatches:
  blue, teal, green, yellow, orange, red, pink, purple, and slate. The system adjusts the chosen accent so it stays
  readable: darker on light, so it reads on white; brighter on dark. Text on the accent is near-black or white,
  whichever reads.
- **Selection** is the accent mixed into the view: 30% on light, 35% on dark.
- **The window buttons** are traffic lights: red close (`#EC5F56`), yellow minimize (`#F5B232`), green zoom
  (`#62C555`), each with a darker mark.
- **The sky**, the gradient shown when there's no wallpaper, is the only surface that takes the accent's hue.

## Spacing

| Where | Spacing |
| --- | --- |
| Between the menu bar and windows | 4 |
| Inside a menu, around its items | 4 |
| Between sidebar items | 6 |
| Inside a tooltip | 6 |
| Around a sidebar's items, and inside the dock | 8 |
| Between the traffic lights | 8 |
| Inside a sidebar item, and between dock icons | 10 |
| Between a message and its buttons | 12 |
| Around an alert's content | 16 |
| Between cascaded windows | 24 |

## Corners

| What | Radius |
| --- | --- |
| A window | 10 |
| A sidebar item | 5 |
| An app icon | 10 at 48 pixels, scaled with the icon's size |

## Sizes of things

| What | Size |
| --- | --- |
| The menu bar | 22 tall |
| A menu item | 20 tall, menus at least 160 wide |
| A title bar | 30 tall; 42 with a subtitle or toolbar icons |
| A traffic light | 12, inset 9 from the window's edge |
| A sidebar item | 22 tall; 30 with a subtitle |
| A sidebar or menu symbol | 16 |
| A dock icon | 48 |

## Windows

A window has a title bar, its content, and, if it has one, a sidebar down its left side, or its right.

```
┌─────────────────────────────────────────────────┐
│ ● ● ●   [toolbar]      (title)          [items] │  title bar
├────────────┬────────────────────────────────────┤
│ Places     │                                    │
│ ▌Selected  │   Content                          │
│  Another   │                                    │
│            │                                    │
│            │                                  ⋰ │  resize grip
└────────────┴────────────────────────────────────┘
```

- **The traffic lights** sit at the left of the title bar: close, minimize, zoom. Zoom only shows on a window that
  can be resized. Roundhouse shows it dimmed instead, since its windows can't always hide it. An app outside
  VentureOS may use its target system's controls instead; see [Apps ▸ Window controls](./apps.md#window-controls).
- **The title is optional.** A title bar is blank by default, since the menu bar already names the app. A window
  shows a title only when it says something the menu bar doesn't: TextEdit's document, Files' folder, Settings'
  page. A subtitle on a blank bar moves up into the title's place, in a 30-pixel bar. The desktop's own windows,
  such as alerts and About, keep their titles, so an alert always says who's asking.
- **The title bar is the toolbar.** Toolbar items and a title field (`<ToolbarItem>`, `<TitleField>`) sit in the
  title bar, so there's no separate strip of buttons under it. A title bar with toolbar icons or a subtitle grows
  from 30 to 42 pixels.
- **Leave room to grab.** A title bar keeps empty space beside the title, so the window can always be dragged.
- **A resizable window** has a grip in its bottom-right corner, the same on every window, over a right sidebar's
  shade when it has one. When it resizes, the sidebar keeps its width and the content takes the rest. A VentureOS
  sidebar is part of the window's layout, not a pane the person drags wider, as GNOME's split views are.
- **New windows cascade**, each 24 pixels down from the last, and stop above the dock when it's always shown.

## Sidebar and content

For anything with places to choose from, such as folders, settings pages, or packages, use a sidebar and a content
area. The sidebar is a list of places, a shade cooler than the content, from the top of the window to the bottom.
Its selection is softer than a list's selection, a neutral gray rather than the accent, because the content beside
it is what the user is looking at.

A sidebar of places goes on the left, where it leads to the content. One that goes with the content instead, such as
a radio's player or a file's details, goes on the right (`edge="right"`), as Shortwave's player does, and may hold
controls of its own in place of rows. The traffic lights stay on the title bar's left either way, and the title and
toolbar span only the content.

## The menu bar

The menu bar runs along the top of the screen and belongs to whichever app is in front.

- **The VentureOS menu** comes first, shown as an icon: About VentureOS, and Return to Shell (Ctrl+Shift+Q).
- **The app's menu** comes next, named after the app, with Quit and, for apps that open more than one window, New
  Window.
- **The window's own menus** come after.
- **The standard menus**, File, Edit, View, and Help, are shared. An app adds to them (UIScript's `role`) rather
  than making its own. The system fills in what every app needs:
  - File: Close Window (Ctrl+W).
  - Edit: Undo (Ctrl+Z), Redo (Ctrl+Shift+Z), Cut (Ctrl+X), Copy (Ctrl+C), Paste (Ctrl+V), and Select All (Ctrl+A),
    for whatever text has focus.
  - Help: About the app, or About VentureOS when no app is in front, so Help is never empty.

## Status items

An app can put a small item in the menu bar, left of the bell, as macOS's menu extras are: a reading, such as the
weather, or a control kept close at hand, such as a volume. It's a `.uix` whose root is `<StatusItem>`, shown as its
title, a 16-pixel symbol in the bar's text color, or both. What it holds decides what a click does:

- **Nothing:** it's a button, and the app hears it pressed. Use it for one action, such as refreshing a reading.
- **Menus:** they drop down under it, as an app's menus do from the bar. Use them for a few commands.
- **Controls:** a panel of them opens under it, on the shelf's material, as toasts are, and closes on Escape, a
  click outside, or the item again. Keep it small, a few controls; a panel that needs a title or a sidebar is a
  window.

Keep the item's text short, a few characters such as `72°` or `5:00`, since items share the bar with the app's menus,
and past the room there the newest don't show. Put a reading in the title and update it with `setTitle`. Give an app
one item unless it truly does two unrelated things. An item belongs to its app: it leaves the bar when the app ends.

## The dock

The dock shows apps as 48-pixel icons on a translucent shelf at the bottom of the screen, with a dot under each open
app and a label over the icon under the pointer. The user chooses whether it's always shown, hides itself, or gets
out of the way of windows.

An app that lives in the menu bar, or works out of sight, can stay out of the dock, with `dock="hidden"` in its
`Info.xml`. A hidden app's minimized windows still show in the dock, so they can come back. An app whose windows
are its point belongs in the dock.

## Apps at startup

The person chooses which apps open when the desktop starts, in Settings ▸ Startup; VentureOS calls this **Open at
Startup**. Such an app may open a window, put a status item in the bar, or show nothing at all and work in the
background. It opens once: if it quits, is closed, or crashes, it stays closed until it's opened again.

- **Ask, don't add.** An app can't put itself on the list. It may ask, with `venture.ui.askToOpenAtStartup`, which
  shows VentureOS's own alert; the person answers. Ask once, when the person turns on something that needs it, such
  as a reminder, and not on the app's first launch.
- **Show that it's running.** An app with no window and no status item can only be seen in System Monitor's
  Processes pane. Prefer a status item, so the person knows it's there and can reach it.
- Files and the Dock are **kept**: VentureOS restarts them whenever they end. Only system apps can be kept.

## Alerts

An alert is a small window, 380 pixels wide, with a message and its buttons in the bottom-right corner. Buttons are
74 by 24 pixels, 8 apart. The button that answers the question goes on the right: OK, or Yes beside No.

An alert is for something that needs an answer, such as whether to trust a computer. A heads-up that needs none, such
as an app that crashed and is starting again, is a notification.

## Notifications

A notification tells a person something in passing, and asks nothing back. Apps post them with
`venture.ui.toast(text)`; VentureOS posts its own, such as a crash.

- **A toast** slides into the top-right corner, below the menu bar, 300 pixels wide, and slides out after 5 seconds.
  Three show at most, newest on top. A click dismisses one; it never opens anything, and it never takes the keyboard.
- **Each shows** its app's icon (32 pixels) and name, how long ago it came, and up to three lines of text. The name
  and icon are always the app's own: an app supplies only the text.
- **The history** keeps the newest 50 until VentureOS restarts: a column of cards down the right edge, each app's
  notifications stacked with the newest on top. The bell beside the clock opens it, with a dot in the accent while
  something arrived unseen, and so does VentureOS ▸ Notifications (Ctrl+Shift+N).
- **Write** one or two short sentences that say what happened, in the [voice](./foundations.md#voice): "Saved 3
  notes.", not "Success!". Don't post what the person just did and can already see, and don't post in a loop: past
  three showing, the rest wait, and a flood only reaches the history.

## Materials

The dock's shelf is translucent, and so are toasts and the notification history in the dark palette, with the shelf's
bright edge. Their body is nearly opaque, so their words read over any wallpaper; in the light palette it's opaque
white, since the light secondary text needs it.

## Light and dark

The person chooses light, dark, or auto in Settings ▸ Appearance; auto is dark on a schedule, 7 PM to 7 AM to start
with. [Foundations ▸ Light and dark](./foundations.md#light-and-dark) has the rule every interface keeps.

## Never crash

An app whose code fails three times in a minute is closed, and the system says why. [Foundations ▸ Never
crash](./foundations.md#never-crash) has the stance.

## Words

VentureOS keeps the desktop's own words in one table, `system/strings/_101_desktop.cst`, read through `DesktopText`
by key. Change a label there, not in code. [Foundations ▸ Labels](./foundations.md#labels) has the rules.

## Open questions

These aren't settled yet. Until they are, follow what VentureOS does today, and don't treat it as a rule.

- **Keyboard focus.** Focus moves only by clicking. Tab doesn't move it between controls yet, and there's no focus
  ring beyond a field's accent outline.
- **Motion.** The dock and toasts slide in and out, in 150 ms, but there are no rules yet for how anything moves.
