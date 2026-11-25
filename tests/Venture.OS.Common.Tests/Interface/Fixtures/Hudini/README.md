# Hudini's example documents

These UIScript documents are copied from
[Hudini](https://github.com/tonytins/Hudini) (`Demo/Sources/hudini-demo/Resources/`
and its `city/` folder, commit `c7e6037`), which reads a subset of the format. VentureOS's reader is
tested against them so every document Hudini reads reads the same here.

On 2026-11-24 the spec dropped `<Slider>`, and `scrolls` and `direction` on buttons, since lists and text areas
show their own scroll bars. These copies were upgraded ahead of Hudini: `chatdialog`, `movelot`, `optionspanel`, and
`city/help` lose their scroll sliders and buttons, their lists and text areas widening into the room; and
`optionspanel` and `city/budget` show each value in a `<ProgressBar>` between − and + buttons, in place of a value
slider. Hudini's own examples follow when it adopts the change.

They're covered by Hudini's BSD 3-Clause license, in [LICENSE](./LICENSE), not by VentureOS's Unlicense.

Refresh them by copying the `.uix` files again when Hudini's examples change, once Hudini has dropped sliders too,
or the old copies come back and VentureOS refuses them; the tests read every `.uix` here, so a new one is tested
without a code change.
