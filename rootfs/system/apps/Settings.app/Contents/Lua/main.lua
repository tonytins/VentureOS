-- Settings: the desktop's look, light or dark, and its accent color, and the
-- clock's offset from UTC. Each choice applies at once and is saved in
-- /mnt/user/settings, as the appearance and clock commands' are.

local window = venture.ui.open("main.uix")

-- GNOME's nine accents, in the order of the buttons and their styles in main.uix.
local SWATCHES = {
  { name = "Blue", accent = "#3584E4" },
  { name = "Teal", accent = "#2190A4" },
  { name = "Green", accent = "#3A944A" },
  { name = "Yellow", accent = "#C88800" },
  { name = "Orange", accent = "#ED5B00" },
  { name = "Red", accent = "#E62D42" },
  { name = "Pink", accent = "#D56199" },
  { name = "Purple", accent = "#9141AC" },
  { name = "Slate", accent = "#6F8396" },
}

-- Shows the look in use: which mode is checked, and the accent's name.
local function show()
  local look = venture.appearance()
  window:setChecked("light", look.mode == "light")
  window:setChecked("dark", look.mode == "dark")

  local name = "Custom, " .. look.accent
  for _, swatch in ipairs(SWATCHES) do
    if swatch.accent == look.accent then name = swatch.name end
  end
  window:setText("accentName", name)
end

-- Applies a look, and says when it couldn't be used or saved.
local function choose(mode, accent)
  local ok, problem = venture.setAppearance(mode, accent)
  window:setText("status", problem or "")
  if ok then show() end
  return ok
end

window:on("light", function() choose("light") end)
window:on("dark", function() choose("dark") end)
for number, swatch in ipairs(SWATCHES) do
  window:on("swatch" .. number, function() choose(venture.appearance().mode, swatch.accent) end)
end
window:on("custom", function()
  local typed = window:text("custom"):match("^%s*(.-)%s*$"):upper()
  if choose(venture.appearance().mode, typed) then window:setText("custom", "") end
end)

-- Shows the clock: the time and its offset from UTC, or that the hardware clock isn't set.
local function showClock(problem)
  if problem then
    window:setText("clockShown", problem)
    return
  end

  local time = venture.time()
  if not time then
    window:setText("clockShown", "This system has no clock.")
  elseif time.set then
    window:setText("clockShown", time.text .. ", " .. time.zone)
  else
    window:setText("clockShown", "Not set, " .. time.zone)
  end
end

window:on("utcOffset", function()
  local ok, problem = venture.setUtcOffset(window:text("utcOffset"))
  if ok then window:setText("utcOffset", "") end
  showClock(problem)
end)

-- The time shown keeps up, as the menu bar's does.
window:every(30, function() showClock() end)

show()
showClock()
