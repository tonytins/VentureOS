-- Settings: the desktop's look, light or dark, and its accent color. Each
-- choice applies at once, to every window, and is saved in
-- /mnt/user/settings/appearance.xml, as the appearance command's are.

local window = venture.ui.open("main.uix")

-- The swatches, in the order of the buttons and their styles in main.uix.
local SWATCHES = {
  { name = "Sky Blue", accent = "#1E78DC" },
  { name = "Blueberry", accent = "#1D63BF" },
  { name = "Grape", accent = "#8F4CBF" },
  { name = "Strawberry", accent = "#E0435D" },
  { name = "Tangerine", accent = "#F27E18" },
  { name = "Lime", accent = "#71BF39" },
  { name = "Teal", accent = "#21A6A1" },
  { name = "Graphite", accent = "#7E848C" },
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

show()
