-- Settings: panes in a sidebar, as GNOME Settings has them. Appearance holds
-- the desktop's look, light, dark, or auto, its accent color, and its scroll
-- bars, each a grid of choices; Wallpaper, every wallpaper; Dock, how the dock
-- behaves; Clock holds the clock's offset from UTC; Developer turns on
-- developer mode, for vdb. Each look, dock, and clock choice applies at once
-- and is saved in /mnt/user/settings, as the appearance and clock commands'
-- are; developer mode isn't saved.

local window = venture.ui.open("main.uix")

-- GNOME's nine accents, by their items' names in main.uix, in the order of
-- DesktopPalette.Swatches; the items draw the same colors.
local SWATCHES = {
  blue = "#3584E4", teal = "#2190A4", green = "#3A944A", yellow = "#C88800", orange = "#ED5B00",
  red = "#E62D42", pink = "#D56199", purple = "#9141AC", slate = "#6F8396",
}

-- Where each choice sits in its grid, by its item's name.
local function indexOf(grid, name)
  local items = { styles = { "auto", "light", "dark" }, scrollBars = { "classic", "automatic" } }
  for index, item in ipairs(items[grid]) do
    if item == name then return index end
  end
end

-- The wallpapers, the sky first, each its picture, which the grid scales down.
local wallpapers = { { name = "sky", title = "Sky", image = "sky" } }
for _, wallpaper in ipairs(venture.wallpapers() or {}) do
  wallpapers[#wallpapers + 1] = { name = wallpaper.name, title = wallpaper.title, image = wallpaper.path }
end
local filled, problem = pcall(window.setItems, window, "wallpapers", wallpapers)
if not filled then
  -- A picture that can't be read leaves the sky alone to choose, and says why.
  window:setItems("wallpapers", { wallpapers[1] })
  window:setText("wallpaperStatus", tostring(problem):gsub("^.-window:setItems: ", ""))
  wallpapers = { wallpapers[1] }
end

-- Whether the custom field shows: while the custom color is chosen.
local customChosen = false

-- Shows the look in use in every grid, and Auto's times while Auto is chosen.
local function show()
  local look = venture.appearance()
  window:select("styles", indexOf("styles", look.mode))
  window:select("scrollBars", indexOf("scrollBars", look.scrollbars))
  window:setText("darkFrom", look.darkFrom)
  window:setText("darkUntil", look.darkUntil)
  for _, name in ipairs({ "darkFromLabel", "darkFrom", "darkUntilLabel", "darkUntil" }) do
    window:setHidden(name, look.mode ~= "auto")
  end

  -- The swatch whose color is the accent, or else the custom color, which shows its field.
  local accent = 10
  for index, name in ipairs({ "blue", "teal", "green", "yellow", "orange", "red", "pink", "purple", "slate" }) do
    if SWATCHES[name] == look.accent then accent = index end
  end
  window:select("accents", accent)
  customChosen = customChosen or accent == 10
  window:setHidden("customAccent", not customChosen)

  for index, wallpaper in ipairs(wallpapers) do
    if wallpaper.name == look.wallpaper then window:select("wallpapers", index) end
  end
end

-- Applies a change to the look, and says when it couldn't be used or saved.
local function apply(change, status)
  local ok, problem = venture.setAppearance(change)
  window:setText(status or "status", problem or "")
  show()
  return ok
end

window:on("styles", function(event)
  if event.kind == "selected" then apply({ mode = event.item }) end
end)

window:on("accents", function(event)
  if event.kind ~= "selected" then return end
  if event.item == "custom" then
    -- The field shows, to type a color in; the accent stays until one's typed.
    customChosen = true
    window:setHidden("customAccent", false)
    window:focus("customAccent")
    return
  end
  customChosen = false
  apply({ accent = SWATCHES[event.item] })
end)

window:on("customAccent", function()
  local typed = window:text("customAccent"):match("^%s*(.-)%s*$"):upper()
  if apply({ accent = typed }) then window:setText("customAccent", "") end
end)

window:on("scrollBars", function(event)
  if event.kind == "selected" then apply({ scrollbars = event.item }) end
end)

window:on("wallpapers", function(event)
  if event.kind == "selected" then apply({ wallpaper = event.item }, "wallpaperStatus") end
end)

-- Rounds or squares every window's corners, keeping the rest of the look.
-- window:on("rounded", function() apply({ corners = "rounded" }) end)
-- window:on("square", function() apply({ corners = "square" }) end)

-- Auto's times, typed HH:MM and set with Enter; a wrong one says why and the
-- field goes back to the time in use.
local function schedule(field, key)
  local typed = window:text(field):match("^%s*(.-)%s*$")
  apply({ [key] = typed })
end

window:on("darkFrom", function() schedule("darkFrom", "darkFrom") end)
window:on("darkUntil", function() schedule("darkUntil", "darkUntil") end)

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

-- The dock: always there, keeping windows above it; hiding until the pointer
-- reaches the bottom of the screen; or hiding while a window would cover it.
local DOCK_MODES = {
  { button = "dockAlways", mode = "always",
    about = "Zoomed and new windows stop above the dock." },
  { button = "dockAutoHide", mode = "autohide",
    about = "The dock slides away, and comes back when the pointer reaches the bottom of the screen." },
  { button = "dockDodge", mode = "dodge",
    about = "The dock slides away while a window would cover it, and comes back at the bottom of the screen too." },
}

local function showDock()
  local current = venture.appearance().dock
  for _, choice in ipairs(DOCK_MODES) do
    window:setChecked(choice.button, choice.mode == current)
    if choice.mode == current then window:setText("dockAbout", choice.about) end
  end
end

for _, choice in ipairs(DOCK_MODES) do
  window:on(choice.button, function()
    local ok, problem = venture.setAppearance{ dock = choice.mode }
    if ok then showDock() else window:setText("dockAbout", problem) end
  end)
end

-- Developer mode, for vdb: on or off, and what it's doing. It lasts until
-- VentureOS restarts, like everything else in memory; init.lua can turn it on
-- at every boot.
local function showDeveloper()
  local status = venture.developer.status()
  if not status then
    window:setChecked("developerOn", false)
    window:setChecked("developerOff", false)
    window:setText("developerStatus", "This system has no developer mode.")
    return
  end

  window:setChecked("developerOn", status.enabled)
  window:setChecked("developerOff", not status.enabled)
  local text
  if status.problem then
    text = "Not listening: " .. status.problem .. "."
  elseif not status.enabled then
    text = "Off."
  elseif status.connected then
    text = "A computer is connected."
  else
    text = "Listening on port " .. status.port .. "."
  end
  local trusted = status.trusted == 1 and "1 computer is" or (status.trusted .. " computers are")
  window:setText("developerStatus", text .. " " .. trusted .. " trusted.")
end

window:setText("developerAbout",
  "vdb sends apps to VentureOS from another computer, which you're asked to trust "
  .. "first. It listens while the desktop runs. Developer mode lasts until VentureOS restarts.")
window:on("developerOn", function() venture.developer.enable(); showDeveloper() end)
window:on("developerOff", function() venture.developer.disable(); showDeveloper() end)
window:every(2, function() showDeveloper() end)

-- The panes are main.uix's: the sidebar shows them, and only the chosen
-- pane's controls. The window's title follows the pane.
local function showPane(number)
  local title = window:items("panes")[number]
  window:setTitle(title)
  if title == "Developer" then showDeveloper() end
end

window:on("panes", function(event)
  if event.kind == "selected" then showPane(event.index) end
end)

show()
showDock()
showClock()
showDeveloper()
showPane(1)
