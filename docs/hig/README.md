# VentureOS HIG

_Working title. Draft, 2026-11-24._

The VentureOS Human Interface Guidelines describe how VentureOS looks, behaves, and talks, and how other apps can
share that design. VentureOS draws with them, Roundhouse follows them on Linux, macOS, and Windows, and
[S1PK](https://github.com/tonytins/s1pkdotnet) is the first app outside the system to use them.

The structure is like macOS: a menu bar at the top, windows with traffic-light buttons, a toolbar merged into the
title bar, and a dock. The colors come from GNOME's libadwaita: neutral surfaces, with one accent color. That pairing
is where VentureOS starts, not a limit. An app can grow a look of its own, as S1PK did, and still follow these
guidelines.

Most of this guide records what VentureOS already does. Where a value comes from the code, the guide says where, so
the two can be kept in step. Where the system hasn't decided something yet, the guide says so under
[Open questions](#open-questions) rather than inventing an answer.

## Contents

1. [Principles](#principles)
2. [Foundations](#foundations): color, type, spacing, corners, depth, light and dark
3. [Structure](#structure): windows, sidebars, the menu bar, the dock, alerts
4. [Behavior](#behavior): states, focus, selection, the keyboard
5. [Writing](#writing): voice, labels, errors
6. [Using it outside VentureOS](#using-it-outside-ventureos)
7. [Open questions](#open-questions)

## Principles

**Say what a color is for, not what it is.** An interface asks for `secondaryText` or `accent`, not `#8F8F92`. The
system decides what that role looks like today. This is how VentureOS can change its look, from light to dark, to a
new accent, or to a future theme, and every app keeps up without being edited. Mac apps that used semantic colors got
Dark Mode the same way.

**Old apps take on new looks.** A change to the look reaches every app through the roles, never through edits to the
apps. If a new look only works when apps change, the change isn't finished.

**Everything stays readable on every palette.** Text must stand out from what's behind it, on light and dark, with
every accent the system offers. VentureOS tests this: every interface it ships must reach a contrast of at least 3 to
1, the WCAG minimum for interface parts, on every palette (`ShippedContrastTests`).

**Only the accent carries color.** Surfaces, text, and lines are neutral. Color marks something: focus, selection,
progress. If everything is colored, nothing stands out.

**Say what went wrong.** An error names what failed and what to do about it. Never show success for something that
didn't happen, and never hide a failure behind a silent default.

**Use plain words.** Write like you're talking to a person, not like a system log.

## Foundations

### Color roles

An interface names these roles in a [UIScript `<Style>`](../uiscript/README.md#styles). Each host chooses what they
look like.

| Role | For |
| --- | --- |
| `text` | Ordinary text. |
| `secondaryText` | Quieter text, such as a status line or a placeholder. |
| `disabledText` | Text on something turned off. |
| `highlightedText` | Text on the accent color. |
| `accent` | Focus, selection marks, and fills. |
| `panel` | A window's background. |
| `inputBackground` | The inside of text fields and lists. |
| `border` | Lines around controls. |
| `selection` | A selected item's background. |

VentureOS draws with more colors than these: the header bar, the sidebar and its selection, menus, separators,
button hover and press, the dock's shelf, and shadows. For now only the system uses them. Opening them to apps would
be a change to UIScript, which Hudini also reads, so it's left as a separate step.

Use a hex color only when it has to be that exact color, such as a brand's, and give it a `dark` twin. A hex color
doesn't follow the look.

### The palette

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

### Type

| Role | For | VentureOS | Hudini |
| --- | --- | --- | --- |
| `caption` | Small text, such as a hint or a subtitle | 11 | 12 |
| `body` | Ordinary text, and text without a style | 13 | 14 |
| `title` | A heading | 17 | 16 |
| `largeTitle` | A large heading | 22 | 22 |

Sizes are in pixels. Each host picks its own sizes for the roles; ask for a role, not a number, unless the text
really must be a certain size.

There are two typefaces: `interface`, for everything, and `monospace`, for code and terminals. The host picks the
actual fonts.

### Spacing

Spacing is forgiving. Gaps and padding between related things fall between **4 and 10 pixels**. Pick what looks
right within that range, rather than snapping to a strict grid. Larger steps separate a window's content from its
edges and groups from each other.

What VentureOS uses today:

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

### Corners

Corners are rounded by default. The user can turn rounding off, so a design must also look right with square
corners.

| What | Radius |
| --- | --- |
| A window | 10 |
| A sidebar item | 5 |
| An app icon | 10 at 48 pixels, scaled with the icon's size |

### Depth

VentureOS uses few layers, and keeps them subtle.

- Windows cast a short drop shadow, 4 pixels.
- A window that isn't in front fades a little, so the one in front stands out.
- The menu bar and the window buttons have a soft gloss, after Mac OS X Tiger. Nothing else does.

### Light and dark

The user chooses light, dark, or auto. Auto is dark on a schedule, 7 PM to 7 AM to start with. Every interface must
work on both, and switching must reach open windows without reopening them. If you use roles, both come for free.

### Sizes of things

| What | Size |
| --- | --- |
| The menu bar | 22 tall |
| A menu item | 20 tall, menus at least 160 wide |
| A title bar | 30 tall; 42 with a subtitle or toolbar icons |
| A traffic light | 12, inset 9 from the window's edge |
| A sidebar item | 22 tall; 30 with a subtitle |
| A sidebar or menu symbol | 16 |
| A dock icon | 48 |

## Structure

### Windows

A window has a title bar, its content, and, if it has one, a sidebar down its left side.

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
  VentureOS may use its target system's controls instead; see
  [Window controls](#window-controls).
- **The title is optional.** A title bar is blank by default, since the menu bar already names the app. A window
  shows a title only when it says something the menu bar doesn't: TextEdit's document, Files' folder, Settings'
  page. A subtitle on a blank bar moves up into the title's place, in a 30-pixel bar. The desktop's own windows,
  such as alerts and About, keep their titles, so an alert always says who's asking.
- **The title bar is the toolbar.** Toolbar items and a title field (`<ToolbarItem>`, `<TitleField>`) sit in the
  title bar, so there's no separate strip of buttons under it. A title bar with toolbar icons or a subtitle grows
  from 30 to 42 pixels.
- **Leave room to grab.** A title bar keeps empty space beside the title, so the window can always be dragged.
- **A resizable window** has a grip in its bottom-right corner. When it resizes, the sidebar keeps its width and the
  content takes the rest.
- **New windows cascade**, each 24 pixels down from the last, and stop above the dock when it's always shown.

### Sidebar and content

For anything with places to choose from, such as folders, settings pages, or packages, use a sidebar and a content
area. The sidebar is a list of places, a shade cooler than the content, from the top of the window to the bottom.
Its selection is softer than a list's selection, a neutral gray rather than the accent, because the content beside
it is what the user is looking at.

### The menu bar

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

### The dock

The dock shows apps as 48-pixel icons on a translucent shelf at the bottom of the screen, with a dot under each open
app and a label over the icon under the pointer. The user chooses whether it's always shown, hides itself, or gets
out of the way of windows.

### Alerts

An alert is a small window, 380 pixels wide, with a message and its buttons in the bottom-right corner. Buttons are
74 by 24 pixels, 8 apart. The button that answers the question goes on the right: OK, or Yes beside No.

## Behavior

### States

| State | How it shows |
| --- | --- |
| Hover | A faint tint over the control: text at 7%. In a sidebar, a fainter version of its selection. |
| Pressed | A stronger tint: text at 16%. An icon darkens. |
| Disabled | Text in `disabledText`. A disabled control can't take focus. |
| Focused | The control's outline turns to the accent. |
| Selected | `selection` behind a list item; the sidebar's own selection in a sidebar. An image button is ringed in the accent while it's checked. |

### Focus and selection

Focus goes to the control the user clicked. A text field or list with focus shows it with an accent outline in
place of its border. Hiding or disabling a control takes its focus away.

### The keyboard

Shortcuts use Ctrl, with Shift for the reverse or a related action (Ctrl+Z undoes, Ctrl+Shift+Z redoes). The
standard menus' shortcuts are the same in every app, so an app must not take them for something else. Ctrl+C or Esc
stops a command that's running too long.

### Never crash

VentureOS has no host process under it, so an app's error can't be allowed to take the system down. The same applies
to the interface: when something fails, say so, keep going, and leave things as they were.

- An app's error is shown, and the app carries on. An app whose code fails three times in a minute is closed, and
  the system says why.
- A missing image shows a red outline where the image should be, so the gap is visible rather than silent.
- A file that can't be read isn't half-loaded. The old state stays until the new one is valid.

## Writing

### Voice

VentureOS talks like a helpful person: plainly, briefly, and without blame. It says what happened and what to do.

- Use common words. "Can't open" rather than "failed to initialize".
- Use the names the user sees: the app's name, the file's name, the menu's title.
- Contractions are fine.
- No exclamation marks, no jokes in errors, no apologies that say nothing.

### Labels

- Menu items and buttons use title case: Close Window, Select All, Return to Shell.
- A menu item that asks for more before it acts ends with an ellipsis: Open…, Go to Folder…
- A button says what it does. Prefer Install or Delete to OK where the question is about installing or deleting.

### Errors

An error says what went wrong, about what, and, where it can, what to do. Some of VentureOS's own:

- `Dice.app has no Contents/Lua/main.lua to run`
- `"roll" is already registered by Games.`
- `Dice crashed: its code failed 3 times within a minute, so VentureOS closed it.`

Name the thing in the message, quote what the user typed when it's the problem, and give the line when a file is at
fault. [Writing apps](../writing-apps.md#when-an-app-wont-open) lists the rest.

## Using it outside VentureOS

These guidelines are meant to travel. An app on Linux, macOS, or Windows can follow them, in whatever toolkit it's
built with. Roundhouse and S1PK use Avalonia.

### What an app keeps

To follow the VentureOS HIG, an app keeps:

- **The structure:** window controls in the title bar, the toolbar merged into the title bar, a sidebar and content
  where there are places to choose, and the standard menus.
- **The behavior:** the states, focus, and keyboard shortcuts above, and stating errors plainly.
- **The roles:** the app defines a value for every color and type role, and draws with those, not with colors
  scattered through its code. That's what lets its look change later.
- **The contrast:** text reaches 3 to 1 against what's behind it, on every palette the app offers.
- **The writing:** the voice, labels, and errors above.

### What an app can change

The paint is the app's own: its palette, its accent, its type sizes, and its corners. It can offer only light or
only dark. A look of its own is welcome, as long as it keeps the structure and the roles.

### Window controls

VentureOS's traffic lights are the default, but an app outside VentureOS may use the window controls its audience
expects instead. For an app aimed mostly at Windows users, that means minimize, maximize, and close on the right of
the title bar.

- Pick one style for the whole app, and use it on every platform the app runs on, so its windows look the same
  everywhere.
- Keep the controls in the title bar, beside the toolbar, rather than in a strip of their own.
- Whatever their style, they do the same things: close, minimize, and zoom or maximize. A window that can't be
  resized doesn't offer to grow.

### S1PK

S1PK shows how this works. It kept VentureOS's window structure: the title bar that's also the toolbar, a sidebar of
packages beside the package's details, and Roundhouse's rules for when an app can draw its own title bar. Its
audience is players of The Sims, where Windows has always come first, so it uses Windows-style minimize, maximize,
and close buttons on the right in place of the traffic lights. Then it painted it navy, after the Maxis-era tools for
The Sims (HomeCrafter, SimPaintShop, and RoofEdit). The result looks nothing like VentureOS, and still works like it.

### Drawing your own title bar

On the desktop, an app's own bar can only be its title bar where the window manager lets it. Roundhouse's rule,
which S1PK adapted (`TitleBars.cs`):

- On Windows and macOS, it always can.
- On Linux, it can when the desktop names itself (`XDG_CURRENT_DESKTOP`), except under a tiling window manager,
  which places windows itself, and under WSL, whose windows go through another system's frames.
- Otherwise, the app's bar sits under the title bar the window manager draws.

Roundhouse's `TitleBars.cs` and `TitleBarStyles.axaml` are the code to start from. VentureOS is public-domain
software (the Unlicense), so they can be copied and adapted freely. If a third app ends up copying them, they should
become a shared package instead.

## Open questions

These aren't settled yet. Until they are, follow what VentureOS does today, and don't treat it as a rule.

- **More roles for apps.** Opening the system's other colors, such as `sidebar`, `headerBar`, and `separator`, is a
  UIScript change, which Hudini would see. It's planned as a separate step.
- **Keyboard focus.** Focus moves only by clicking. Tab doesn't move it between controls yet, and there's no focus
  ring beyond a field's accent outline.
- **Icons and symbols.** There's no guidance yet on drawing app icons or the 16-pixel symbols in sidebars and menus,
  beyond their sizes and the icon's corner radius.
- **Motion.** The dock slides in and out, but there are no rules yet for how anything moves.
- **The name.** "VentureOS HIG" is a working title.

## Revision history

| Date | Change |
| --- | --- |
| 2026-11-24 | Title bars are blank unless the app gives a title; a subtitle takes the title's place. |
| 2026-11-24 | First draft: principles, foundations, structure, behavior, writing, and using it outside VentureOS. |
