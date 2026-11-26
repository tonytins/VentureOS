# Hudini's example documents

These UIScript documents are copied from
[Hudini](https://github.com/tonytins/Hudini) (`Demo/Sources/hudini-demo/Resources/`
and its `city/` folder, commit `5f5e565`), which reads a subset of the format. VentureOS's reader is
tested against them so every document Hudini reads reads the same here.

Hudini follows the spec's changes of 2026-11-24 and 2026-11-25: its lists and text areas show their own scroll bars,
with no slider or buttons beside them to scroll them, and its sliders choose values. Between those dates these
copies were upgraded ahead of Hudini; they're Hudini's own again. `city/help`'s text area stops 16 pixels above the
window's bottom, so the resize grip doesn't cover its scroll bar's down arrow.

`movelot` defines a `scrollThumb` image it doesn't use here. Hudini's demo reads its windows with an appearance, a
Hudini host feature that gives them shared scroll bar art, and Move Lot's image overrides the shared thumb. Read
alone, as here, it's an unused image, which the spec allows.

They're covered by Hudini's BSD 3-Clause license, in [LICENSE](./LICENSE), not by VentureOS's Unlicense.

Refresh them by copying the `.uix` files again when Hudini's examples change. The tests read every `.uix` here, so a
new one is tested without a code change.
