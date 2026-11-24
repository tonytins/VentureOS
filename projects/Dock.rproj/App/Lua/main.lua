-- Dock: the row of apps along the bottom of the screen, as macOS's is. It
-- shows the desktop's own app first, then the system's apps, the user's, and
-- any minimized windows, with a divider between each group; a dot under the
-- open ones; and opens an app, or brings a window back, when it's clicked.
--
-- The desktop starts Dock and keeps it running, starting it again if it
-- stops, so the dock comes back on its own.

local window = venture.ui.open("main.uix")

-- What each icon on the shelf stands for, in the shelf's order: an app's path
-- or a window's ID.
local shown = {}

local function refresh()
  local items, targets = {}, {}
  for _, app in ipairs(venture.desktop.apps()) do
    if app.identifier ~= venture.app.identifier then
      items[#items + 1] = { title = app.name, icon = app.icon, open = app.open, group = app.user and 1 or 0 }
      targets[#targets + 1] = { app = app.path }
    end
  end

  for _, minimized in ipairs(venture.desktop.minimized()) do
    items[#items + 1] = { title = minimized.title, group = 2, window = true }
    targets[#targets + 1] = { window = minimized.id }
  end

  window:setItems("apps", items)
  shown = targets
end

window:on("apps", function(event)
  local target = shown[event.index]
  if not target then return end

  local ok, problem
  if target.window then
    ok, problem = venture.desktop.restore(target.window)
  else
    ok, problem = venture.desktop.open(target.app)
  end
  if not ok then print(problem) end
end)

venture.desktop.onChange(refresh)

-- Apps installed while the desktop runs, such as over vdb, show up within a few
-- seconds, since installing one isn't something the desktop hears about.
window:every(5, refresh)

refresh()
