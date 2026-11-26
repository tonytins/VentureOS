# Venture Design Language

_Draft, 2026-11-24. Split into three books, 2026-11-26._

The Venture Design Language (VDL) describes how Venture's software looks, behaves, and talks, and how apps on
other platforms share that design. VentureOS draws with it, Roundhouse follows it on Linux, macOS, and Windows,
and [S1PK](https://github.com/tonytins/s1pkdotnet) is the first app outside the family to use it.

The structure is like macOS: a menu bar at the top, windows with traffic-light buttons, a toolbar merged into the
title bar, and a dock. The colors come from GNOME's libadwaita: neutral surfaces, with one accent color. That pairing
is where VentureOS starts, not a limit. An app can grow a look of its own, as S1PK did, and still follow this design
language.

Most of the system book records what VentureOS already does. Where a value comes from the code, the book says where,
so the two can be kept in step. Where the system hasn't decided something yet, it says so under
[Open questions](#open-questions) rather than inventing an answer.

## The three books

| Book | For | Holds |
| --- | --- | --- |
| [Foundations](./foundations.md) | everyone | the words both sides use: color and type roles, spacing, corners, depth, light and dark, states, focus, the keyboard, and the voice |
| [System](./system.md) | VentureOS itself | what the host draws: the palette's values, windows, the menu bar, status items, the dock, alerts, and notifications |
| [Apps](./apps.md) | app authors, on VentureOS or elsewhere | what an app decides: the contract, roles in its toolkit, window controls, its own title bar, and its controls |

**Who implements it decides where it lives.** Foundations owns the words both sides use. The system book owns what
the host draws. The apps book owns what an app decides. When a section could fit two, it goes where the code does,
and the other book links to it.

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

## Open questions

These aren't settled yet. Until they are, follow what VentureOS does today, and don't treat it as a rule.

- **More roles for apps.** Opening the system's other colors, such as `sidebar`, `headerBar`, and `separator`, is a
  UIScript change, which Hudini would see. It's planned as a separate step.
- **Icons and symbols.** There's no guidance yet on drawing app icons or the 16-pixel symbols in sidebars and menus,
  beyond their sizes and the icon's corner radius.

The desktop's own open questions, such as keyboard focus and motion, are in [System ▸ Open questions](./system.md#open-questions).

## Revision history

| Date | Change |
| --- | --- |
| 2026-11-26 | Renamed the VentureOS HIG the Venture Design Language, since it guides apps outside the system too, and split it into three books: Foundations, System, and Apps. |
| 2026-11-25 | Status items, apps staying out of the dock, and apps at startup: what an item holds and when to use each kind, and asking rather than adding. |
| 2026-11-25 | Sliders and progress bars: the shared, glossed rail, the knob, and when to use a slider; Depth's gloss rule takes in the rail's fill, by the owner's choice. |
| 2026-11-25 | Notifications: toasts, the history, and the bell; when to post one rather than an alert; their translucency under Depth. |
| 2026-11-24 | The voice: calm, factual, and trusting the reader with what follows. Inspired by the System Voice of *ReBoot*, which announced what was happening in Mainframe in a few even words and left its audience to understand. |
| 2026-11-24 | Title bars are blank unless the app gives a title; a subtitle takes the title's place. |
| 2026-11-24 | First draft: principles, foundations, structure, behavior, writing, and using it outside VentureOS. |
