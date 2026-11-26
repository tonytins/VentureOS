# Foundations

The vocabulary the other two books use. An interface names a role and draws with it; the host decides what that role
looks like. This book says what each role is for and how the parts of an interface behave, whatever draws them.

[System](./system.md) puts these to work in VentureOS itself, and [Apps](./apps.md) says how an app keeps them.

## Color roles

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

## Type

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

## Spacing

Spacing is forgiving. Gaps and padding between related things fall between **4 and 10 pixels**. Pick what looks
right within that range, rather than snapping to a strict grid. Larger steps separate a window's content from its
edges and groups from each other.

The gaps VentureOS uses today are in [System ▸ Spacing](./system.md#spacing).

## Corners

Corners are rounded by default. The user can turn rounding off, so a design must also look right with square
corners.

The radii VentureOS uses are in [System ▸ Corners](./system.md#corners).

## Depth

VentureOS uses few layers, and keeps them subtle.

- Windows cast a short drop shadow, 4 pixels.
- A window that isn't in front fades a little, so the one in front stands out.
- The menu bar, the window buttons, and the filled part of a slider's or progress bar's rail have a soft gloss, after
  Mac OS X Tiger: a bright band over the top half and a sheen along the top edge. Nothing else does.

The dock's shelf and the notifications' material are the system's; see [System ▸ Materials](./system.md#materials).

## Light and dark

The user chooses light, dark, or auto. Every interface must work on both, and switching must reach open windows
without reopening them. If you use roles, both come for free.

## States

| State | How it shows |
| --- | --- |
| Hover | A faint tint over the control: text at 7%. In a sidebar, a fainter version of its selection. |
| Pressed | A stronger tint: text at 16%. An icon darkens. |
| Disabled | Text in `disabledText`. A disabled control can't take focus. |
| Focused | The control's outline turns to the accent. |
| Selected | `selection` behind a list item; the sidebar's own selection in a sidebar. An image button is ringed in the accent while it's checked. |

## Focus and selection

Focus goes to the control the user clicked. A text field or list with focus shows it with an accent outline in
place of its border. Hiding or disabling a control takes its focus away.

## The keyboard

Shortcuts use Ctrl, with Shift for the reverse or a related action (Ctrl+Z undoes, Ctrl+Shift+Z redoes). The
standard menus' shortcuts are the same in every app, so an app must not take them for something else. Ctrl+C or Esc
stops a command that's running too long.

## Never crash

VentureOS has no host process under it, so an app's error can't be allowed to take the system down. The same applies
to the interface: when something fails, say so, keep going, and leave things as they were.

- An app's error is shown, and the app carries on.
- A missing image shows a red outline where the image should be, so the gap is visible rather than silent.
- A file that can't be read isn't half-loaded. The old state stays until the new one is valid.

An app that keeps failing is closed, and the system says why; [System ▸ Never crash](./system.md#never-crash) has
the rule.

## Writing

### Voice

VentureOS speaks like an announcer who trusts the people listening. It says what happened, calmly and in as few
words as the facts need, and leaves the rest to the reader.

- **State what happened, as a fact.** One clause if it fits: `Dice is already open.`
- **Trust the reader with what follows.** Don't add a consequence they can already see or work out. When an app
  crashes, its windows close in front of the user; the message doesn't need to say so.
- **Stay even, whatever the news.** A crash is reported in the same tone as a boot. No exclamation marks, no jokes,
  no blame, and no apologies that say nothing.
- **Name things rather than describing them:** the app, the file, the line, what the user typed.
- **Give a remedy only when the reader couldn't work it out.** `"roll" is already registered by Games.` already tells
  the reader what to do. The guides give the longer help, so messages don't have to.
- Use common words: "can't open" rather than "failed to initialize". Contractions are fine.

When something falls back, say what stands in, since the reader can't see that a default took its place:
`couldn't read appearance.xml: … Keeping the standard look.`

A "so" clause is fine when it explains something the reader couldn't guess, such as
`This VentureOS is a test's, so it can't halt, reboot, or shut down.`, and cut when it only restates what follows
from the first half.

### Labels

- Menu items and buttons use title case: Close Window, Select All, Return to Shell.
- A menu item that asks for more before it acts ends with an ellipsis: Open…, Go to Folder…
- A button says what it does. Prefer Install or Delete to OK where the question is about installing or deleting.
- Keep words in a table, not in code, so they can be changed without a rebuild. The system's table is in
  [System ▸ Words](./system.md#words); an app's is in [Apps ▸ Writing](./apps.md#writing).

### Errors

An error says what went wrong and with what. Some of VentureOS's own:

- `Dice.app has no Contents/Lua/main.lua to run`
- `"roll" is already registered by Games.`
- `Dice crashed: its code failed 3 times within a minute.`

Name the thing in the message, quote what the user typed when it's the problem, and give the line when a file is at
fault. [Troubleshooting](../tutorial/troubleshooting.md) lists the rest.
