-- Settings: panes in a sidebar, as GNOME Settings has them. Appearance holds
-- the desktop's look, light or dark, and its accent color; Clock holds the
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

-- The wallpapers in the picker's slots, the sky first, then as many of the
-- system's as there are slots for.
local WALLPAPER_SLOTS = 5
local wallpapers = { { name = "sky", title = "Sky" } }
for _, wallpaper in ipairs(venture.wallpapers() or {}) do
  if #wallpapers < WALLPAPER_SLOTS then wallpapers[#wallpapers + 1] = wallpaper end
end

-- Shows the look in use: which mode is checked, and the accent's name.
local function show()
  local look = venture.appearance()
  window:setChecked("light", look.mode == "light")
  window:setChecked("dark", look.mode == "dark")
  window:setChecked("auto", look.mode == "auto")
  window:setText("darkFrom", look.darkFrom)
  window:setText("darkUntil", look.darkUntil)
  window:setChecked("rounded", look.corners == "rounded")
  window:setChecked("square", look.corners == "square")

  local shown = look.wallpaper
  for slot, wallpaper in ipairs(wallpapers) do
    window:setChecked("wallpaper" .. slot, wallpaper.name == look.wallpaper)
    if wallpaper.name == look.wallpaper then shown = wallpaper.title end
  end
  window:setText("wallpaperName", shown)

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

-- Rounds or squares every window's corners, keeping the rest of the look.
local function corners(shape)
  local ok, problem = venture.setAppearance({ corners = shape })
  window:setText("status", problem or "")
  if ok then show() end
end

-- Each slot shows its wallpaper's thumbnail, and choosing it shows it on the
-- desktop; a thumbnail that can't be read says so, and the slot stays.
for slot = 1, WALLPAPER_SLOTS do
  local wallpaper = wallpapers[slot]
  if wallpaper then
    if wallpaper.thumbnail then
      local ok, problem = window:setImage("thumb" .. slot, wallpaper.thumbnail)
      if not ok then window:setText("status", problem) end
    end
    window:on("wallpaper" .. slot, function()
      local ok, problem = venture.setAppearance({ wallpaper = wallpaper.name })
      window:setText("status", problem or "")
      if ok then show() end
    end)
  end
end

window:on("rounded", function() corners("rounded") end)
window:on("square", function() corners("square") end)
window:on("light", function() choose("light") end)
window:on("dark", function() choose("dark") end)
window:on("auto", function() choose("auto") end)

-- Auto's times, typed HH:MM and set with Enter; a wrong one says why and the
-- field goes back to the time in use.
local function schedule(field, key)
  local typed = window:text(field):match("^%s*(.-)%s*$")
  local ok, problem = venture.setAppearance({ [key] = typed })
  window:setText("status", problem or "")
  show()
end

window:on("darkFrom", function() schedule("darkFrom", "darkFrom") end)
window:on("darkUntil", function() schedule("darkUntil", "darkUntil") end)
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

-- The panes, in the sidebar's order, and the controls each one shows; every
-- pane's controls sit in the same place, and only the chosen pane's show.
local PANES = {
  {
    name = "Appearance",
    controls = {
      "styleHeading", "light", "dark", "auto", "darkFromLabel", "darkFrom", "darkUntilLabel", "darkUntil", "accentHeading", "swatch1", "swatch2", "swatch3", "swatch4", "swatch5",
      "swatch6", "swatch7", "swatch8", "swatch9", "accentName", "custom", "status", "cornersHeading", "rounded",
      "square", "wallpaperHeading", "wallpaper1", "wallpaper2", "wallpaper3", "wallpaper4", "wallpaper5",
      "wallpaperName",
    },
  },
  { name = "Clock", controls = { "offsetHeading", "utcLabel", "utcOffset", "clockShown" } },
}

-- Wallpaper slots past the last wallpaper stay hidden on every pane.
local function unused(control)
  local slot = tonumber(control:match("^wallpaper(%d)$"))
  return slot ~= nil and wallpapers[slot] == nil
end

local function showPane(number)
  for index, pane in ipairs(PANES) do
    for _, control in ipairs(pane.controls) do window:setHidden(control, index ~= number or unused(control)) end
  end
  window:setTitle(PANES[number].name)
  window:select("panes", number)
end

local names = {}
for number, pane in ipairs(PANES) do names[number] = pane.name end
window:setItems("panes", names)
window:on("panes", function(event)
  if event.kind == "selected" then showPane(event.index) end
end)

show()
showClock()
showPane(1)
