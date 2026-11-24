-- $NAME$: opens its window, and counts presses of its button.
-- See docs/tutorial/desktop.md in VentureOS for what an app can do.

local window = venture.ui.open("main.uix")
local presses = 0

window:on("press", function()
  presses = presses + 1
  window:setText("count", presses == 1 and "Pressed once." or "Pressed " .. presses .. " times.")
end)
