# Toasts and the notification history

**Status:** proposed for 0.8.4xx, Hokutosei. **Date:** 2026-11-25.

VentureOS tells a person about things that need no answer, such as an app that crashed and is starting again, in a
modal alert that takes over the screen until they press OK. This design gives those heads-ups a place of their own:
**toasts**, small notices that slide into the top-right corner and leave on their own, and a **history** of them,
opened from a bell beside the clock, so one nobody saw isn't lost.

Mac OS X had no notifications of its own until Mountain Lion; in the Tiger era, a third party, Growl, filled the gap.
VentureOS mixes the two eras: Growl's translucency and stacking, made of the material VentureOS already has, the
dock's shelf, with Mountain Lion's structure (icon, title, text) and its Notification Center, drawn as modern macOS
draws it.

It's the second part of 0.8.4xx, after `vdb mcp` and before a simpler `<Slider>`. Status items an app can put in the
menu bar, and apps that stay out of the dock, are 0.8.5xx's; the bell here is the system's own, and becomes the first
status item there.

## Why

- A crash notice is a heads-up, but today it's a modal alert, and a few at once stack up as windows to dismiss.
- Apps have no way to tell a person something in passing. `venture.ui.alert` interrupts.
- A notice nobody saw is gone: an alert must be dismissed, but a passing notice needs somewhere to be found again.

## Goals

- **Toasts are passive**, by the owner's decision: a heads-up, never a question. A click dismisses one; nothing else
  happens. Nothing calls an app back.
- **Everyone posts**: apps through `venture.ui.toast`, the system (crashes, apps that didn't start), and vdb.
- **The top-right corner**, under the menu bar's clock, stacked downward.
- **The dock's shelf** is the material, by the owner's choice (option C of the mockups): translucent, with its bright
  edge, following light and dark as the dock does.
- **A history**, after Mountain Lion's Notification Center, by the owner's choice: a column of floating shelf cards
  down the right edge, as modern macOS draws it, each app's notifications stacked with the newest on top, opened by
  **a bell** beside the clock, with a dot while something's unseen.
- Crash and start-failure notices become toasts. Alerts stay for what needs an answer, such as Trust.
- Everything stays readable on every palette and over any wallpaper.
- An agent can see that a toast was posted, through vdb.

## Non-goals

- **Actions**: buttons on a toast, replies, or opening the app from one. By the owner's decision, toasts are passive.
- **Status items for apps**, and apps that stay out of the dock: 0.8.5xx.
- **Keeping the history across a restart.** Nothing persists on VentureOS yet; it lasts until restart.
- **Per-app settings**, such as turning one app's toasts off, and Do Not Disturb.
- **Sounds**, badges on dock icons, and blur behind the shelf (the desktop draws in software).

## Anti-goals

- **A notice that depends on an app.** A crash toast must show even when the crashed app is the one that would have
  drawn it, so the desktop draws toasts, the bell, and the history itself.
- **An app speaking as another**, or as VentureOS. The title and icon of an app's toast are the app's, filled in by
  the system.
- **Unreadable text** over a bright or busy wallpaper. Translucency is a look, never at the cost of contrast.
- **A flood**: an app posting in a loop must not bury the screen or the history.

## Approaches considered

| | Approach | Trade-off |
|---|---|---|
| **Chosen** | **The desktop draws everything.** A notification list in Common, beside `DesktopSession`; toasts and the history drawn as a layer over the windows; the bell a built-in, clickable item beside the clock | Works when apps crash, tests in Common, leaves the work area alone. Can't be redesigned in Roundhouse or replaced as the dock can. |
| | **A system app**, `Notifications.app`, drawing toasts and the history in UIScript, as the dock is drawn | Follows 0.7's apps-for-the-shell direction, but UIScript has no floating, translucent window that leaves the work area alone, so it needs new panel kinds, stacked cards, and motion; and its own crash notice would have nowhere to go. |
| | The desktop draws toasts, an app draws the history | One feature split across two owners, for nothing A lacks. |

The owner chose the first, and asked that status items in general, with the safety of a normal app, follow in their
own band (0.8.5xx).

## How it works

### A notification

A notification is an icon, a title, a text, and when it came:

- **From an app**: the app's icon and its name, as the dock and About show them. The app supplies only the text.
- **From the system**: the VentureOS icon, titled for what it's about: a crash notice is titled with the app's name,
  as the alert is today, and shows that app's icon when it has one.
- **The text** is plain, at most 200 characters, its line breaks kept; longer text is cut with an ellipsis. Empty
  text is refused.

### Toasts

- A toast slides in from the right edge, below the menu bar, 8 pixels from the edge, 300 pixels wide, and stays 5
  seconds, then slides out (each slide 150 ms, as the dock's). Newer toasts go above older ones.
- **At most three show at once.** A fourth waits until one leaves; past ten waiting, the oldest waiting ones go
  straight to the history without showing, so a flood can't hold the corner for minutes.
- A click on a toast dismisses it at once. It stays in the history.
- A toast shows the icon (32 pixels), the title in bold, how long ago in secondary text, and the text in up to three
  lines, the rest cut with an ellipsis.
- While the history is open, toasts don't show; what arrives goes straight into it.
- They sit over every window and panel, and under open menus and tooltips. A toast never takes the keyboard, so the
  window in front keeps it.

### The history

- **The bell** sits left of the clock, drawn by the desktop as the clock is, in the bar's text color. It's the bar's
  first item that takes a click. A dot in the accent, beside it, shows while something arrived since the history was
  last opened.
- A click on the bell opens the history; a click on it again, Escape, or a click anywhere outside closes it.
- The history is a column down the right edge, from below the menu bar to above the dock's panel (the work area's
  height), 300 pixels wide. It has no backing: a heading, **Notifications**, and **Clear All**, then a card per app,
  newest app first, made of the same shelf as a toast.
- Each app's card shows its newest notification, with "2 more from Terminal" under it when there are more, and the
  edges of the ones beneath peeking out below, as on macOS. A click on a stacked card expands it to show all of that
  app's notifications; a click on its heading collapses it again.
- When the column holds more than fits, it scrolls with the automatic scroll bar, the sidebar's.
- **Clear All** empties the history. An empty history says **No notifications**.
- The history keeps the newest 50; older ones fall off. It lasts until VentureOS restarts, and survives leaving and
  reopening the desktop, as apps' windows do.

### Readable on every palette

The shelf alone is too faint to read over a bright wallpaper. A toast and a card are drawn as a nearly opaque tint
with the shelf's bright edge, so the wallpaper only shows at the edges and through the gaps between cards. The
shelf's translucent white isn't laid over the tint: in the dark palette it lifts the body enough to drop the
secondary text under 3 to 1 over a white wallpaper. New color roles carry it, system-only as the dock's are:

- `NotificationBackground`: the tint. Dark, 215 of 255 opaque, in the dark palette; opaque white in the light one,
  where the secondary text reaches 3 to 1 on white and only just (it was found below it on an off-white while
  building phase 1). `Text` reads on it as it does on a window, and it follows the mode as the dock does.
- `NotificationEdge`: the bright edge, the dock's.
- Title and text use `Text` and `SecondaryText`, which the tint makes readable.

Contrast is checked as `ShippedContrastTests` checks interfaces: the text over the tint, laid over pure white and pure
black, reaches 4.5 to 1 for the text and 3 to 1 for the secondary text, on every palette and accent.

### Who posts

- **Apps**: `venture.ui.toast(text)`, returning `true`, or `nil` and why (no text, or text that's only spaces). Any
  script may call it, as any script may show an alert, since nothing calls back. In an app, the title and icon are
  the app's; in a command, `init.lua`, or the `lua` prompt, they're VentureOS's. While the desktop isn't showing, the
  toast goes into the history and is printed, as `venture.ui.alert` prints.
- **The system**: an app that crashed (`ReportCrashes`) and one that didn't start (`ReportStartFailures`) post toasts
  rather than open alerts, with the same words. What apps print that nobody saw stays an alert
  (`ShowMessages`), since it can be many lines to read.
- **vdb**: "vdb installed Dice.app" when an app is installed, and "A computer connected over vdb" when a trusted one
  connects, titled VentureOS.

### vdb

The `ui` feature gains `notifications`, answering a `notification` line for each in the history, newest first
(`app`, `title`, `text`, `seconds` ago), then `ok`; and `vdb mcp` a read-only `notifications` tool over it, its
descriptions saying the text is untrusted. It's an addition to a feature already offered: a VentureOS without it
answers that it isn't a command, which an older vdb never sends.

### Words

**Notifications**, **Clear All**, **No notifications**, "{count} more from {app}", and the ages ("now", "1 min",
"{n} min", "{n} hr") go in `_101_desktop.cst`, through `DesktopText`, in the HIG's voice.

## Where the code goes

- `src/Venture.OS.Common/Desktop/`: the notification list (posting, limits, the waiting queue, the history), the
  toast layer and the history column drawn through `IPainter`, the bell and its dot in `MenuBar`, the palette's new
  roles, and `DesktopSession` posting crashes and start failures, opening and closing the history, and routing
  clicks and Escape. With tests: limits, timing, stacking, the flood rule, clicks, the bell, contrast.
- `src/Venture.OS.Common/Scripting/`: `venture.ui.toast`, for apps (`UiLibrary`) and for other scripts
  (`VentureLibrary`), with tests.
- `src/Venture.OS.Common/Vdb/`: the `notifications` command, posts from vdb, a recorded conversation.
- `src/Venture.OS.Kernel/`: nothing new beyond what the desktop does; the frame loop already ticks the session.
- `tools/vdb/`: `notifications` in VdbKit and as a tool.
- `rootfs/system/strings/_101_desktop.cst`: the words. `rootfs/system/icons/`: the bell, a symbolic icon.
- Docs: the HIG (a Notifications section; Depth gains the toasts' shelf), the architecture page, the scorecard, the
  roadmap, docs/vdb, and the tutorials' mention of `venture.ui.toast`.

## Risks

- **Timing on the real system.** Toasts need the frame loop to redraw while they move and to wake when one should
  leave; the desktop redraws only on change today, so it must ask for frames while a toast slides or waits. Checked
  by booting.
- **Drawing cost.** Blending three toasts and a column is more software blending per frame than the dock alone; it
  should be measured on a boot.
- **A crash loop** posts a toast every restart; the flood rule bounds what shows, and the history keeps 50.
- **The bell's icon** is a placeholder until the owner draws or approves one.

## Calls made in writing it

The owner decided what toasts are, where, their material, the history, and the bell. These were the writer's calls,
for the owner to check:

- Five seconds on screen, three at once, ten waiting before the rest go straight to the history, and 50 kept.
- A click dismisses a toast; toasts hide while the history is open.
- Crash and start-failure notices become toasts; held app output stays an alert.
- Scripts outside apps post as VentureOS; while the desktop is hidden, a toast is kept and printed.
- vdb posts on install and on a trusted connection, and gains `notifications`, in `ui` and as a tool.
- The light palette's toast is opaque white, following the mode as the dock does; the dark one is translucent.
- The history survives leaving the desktop, but not a restart.

## Left to whoever builds it

- Exact spacing inside a toast and a card, from the HIG's spacing scale.
- How the slide eases, within 150 ms.
- How expanding a stacked card animates, if at all.
- The phases of the rollout.
