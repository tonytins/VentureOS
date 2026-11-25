# Hudini's example documents

These UIScript documents are copied from
[Hudini](https://github.com/tonytins/Hudini) (`Demo/Sources/hudini-demo/Resources/`
and its `city/` folder, commit `c7e6037`), which reads a subset of the format. VentureOS's reader is
tested against them so every document Hudini reads reads the same here.

On 2026-11-24 the spec dropped scrolling by hand: a `<Slider>` or button with `scrolls`, and `direction`, since
lists and text areas show their own scroll bars. These copies were upgraded ahead of Hudini: `chatdialog`,
`movelot`, `optionspanel`, and `city/help` lose their scroll sliders and buttons, their lists and text areas widening
into the room. Hudini's own examples follow when it adopts the change.

On 2026-11-25 `<Slider>` came back for choosing a value, as Hudini reads it, so `optionspanel`'s volumes and
`city/budget`'s figures are value sliders again, as in Hudini; between the two dates they were a `<ProgressBar>`
between − and + buttons.

They're covered by Hudini's BSD 3-Clause license, in [LICENSE](./LICENSE), not by VentureOS's Unlicense.

Refresh them by copying the `.uix` files again when Hudini's examples change, once Hudini has dropped its scroll sliders
and buttons too, or the old copies come back and VentureOS refuses them; the tests read every `.uix` here, so a new one is tested
without a code change.
