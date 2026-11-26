# Apps

How to build an app in the Venture Design Language, on VentureOS or on another platform. These guidelines are
meant to travel: an app on Linux, macOS, or Windows can follow them, in whatever toolkit it's built with. Roundhouse
and S1PK use Avalonia.

[Foundations](./foundations.md) has the vocabulary; this book says what an app keeps, what it may change, and how to
meet the rest. [System](./system.md) is VentureOS's own side of it.

## What an app keeps

To follow the Venture Design Language, an app keeps:

- **The structure:** window controls in the title bar, the toolbar merged into the title bar, a sidebar and content
  where there are places to choose, and the standard menus.
- **The behavior:** the [states](./foundations.md#states), [focus](./foundations.md#focus-and-selection), and
  [keyboard shortcuts](./foundations.md#the-keyboard), and stating errors plainly.
- **The roles:** the app defines a value for every [color](./foundations.md#color-roles) and
  [type](./foundations.md#type) role, and draws with those, not with colors scattered through its code. That's what
  lets its look change later.
- **The contrast:** text reaches 3 to 1 against what's behind it, on every palette the app offers.
- **The writing:** the [voice](./foundations.md#voice), [labels](./foundations.md#labels), and
  [errors](./foundations.md#errors).

## What an app can change

The paint is the app's own: its palette, its accent, its type sizes, and its corners. It can offer only light or
only dark. A look of its own is welcome, as long as it keeps the structure and the roles.

## Roles in your toolkit

An app defines a value for every [color role](./foundations.md#color-roles) and
[type role](./foundations.md#type), and draws with those.

- **On VentureOS**, the host supplies the roles from its palette, so an app gets the look for free. It names them in
  a `.uix` [Style](../uiscript/README.md#styles) and never writes a hex value it doesn't have to.
- **Elsewhere**, the app defines them itself, in whatever its toolkit offers (Roundhouse and S1PK use Avalonia
  resources), including a value for every role on each palette it offers.

## Window controls

VentureOS's traffic lights are the default, but an app outside VentureOS may use the window controls its audience
expects instead. For an app aimed mostly at Windows users, that means minimize, maximize, and close on the right of
the title bar.

- Pick one style for the whole app, and use it on every platform the app runs on, so its windows look the same
  everywhere.
- Keep the controls in the title bar, beside the toolbar, rather than in a strip of their own.
- Whatever their style, they do the same things: close, minimize, and zoom or maximize. A window that can't be
  resized doesn't offer to grow.

## Drawing your own title bar

On the desktop, an app's own bar can only be its title bar where the window manager lets it. Roundhouse's rule,
which S1PK adapted (`TitleBars.cs`):

- On Windows and macOS, it always can.
- On Linux, it can when the desktop names itself (`XDG_CURRENT_DESKTOP`), except under a tiling window manager,
  which places windows itself, and under WSL, whose windows go through another system's frames.
- Otherwise, the app's bar sits under the title bar the window manager draws.

Roundhouse's `TitleBars.cs` and `TitleBarStyles.axaml` are the code to start from. VentureOS is public-domain
software (the Unlicense), so they can be copied and adapted freely. If a third app ends up copying them, they should
become a shared package instead.

## Sliders and progress bars

A slider chooses a value, such as a volume or a tax rate; a progress bar shows one. They share a rail: thin and
round-ended, its empty part sunk into the window, its filled part the accent, glossed. A slider's knob is round and
white, 18 pixels, with a thin edge so it stands out on a light window.

- Use a slider for a value somewhere in a range, where seeing roughly how much matters more than typing an exact
  number. Give it a `step` when only whole values make sense, such as whole percents.
- A slider runs along its longer side. Lay it out across, as it reads most naturally, unless the window's shape asks
  for it upright.
- It reports `changed` as it moves and `committed` when let go. Follow it live when the person should feel it, such
  as a volume; do slow work, such as recomputing a budget, on `committed`.
- Arrow keys move a focused slider a step, and Home and End to its ends.

On VentureOS, `step`, `changed`, and `committed` are UIScript's names for this; an app elsewhere follows the same
rules in its toolkit's terms.

## Status items

An app can put a small item in the menu bar, left of the bell. [System ▸ Status items](./system.md#status-items)
says what the bar draws and how a click behaves. An item belongs to its app: it leaves the bar when the app ends.
Give an app one item unless it truly does two unrelated things, and keep its text short, a few characters such as
`72°` or `5:00`.

## Writing

An app on VentureOS keeps the words its buttons share in `system/strings/_100_common.cst`, and its own in its
bundle. Change a label there, not in code. [Foundations ▸ Writing](./foundations.md#writing) has the voice, labels,
and errors.

## Contrast

Text reaches 3 to 1 against what's behind it, on every palette the app offers. VentureOS checks this for every
interface it ships (`ShippedContrastTests`). An app elsewhere checks each palette it offers the same way, with a
contrast tool or a test of its own.

## S1PK

S1PK shows how this works. It kept VentureOS's window structure: the title bar that's also the toolbar, a sidebar of
packages beside the package's details, and Roundhouse's rules for when an app can draw its own title bar. Its
audience is players of The Sims, where Windows has always come first, so it uses Windows-style minimize, maximize,
and close buttons on the right in place of the traffic lights. Then it painted it navy, after the Maxis-era tools for
The Sims (HomeCrafter, SimPaintShop, and RoofEdit). The result looks nothing like VentureOS, and still works like it.
