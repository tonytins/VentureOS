-- Files: the desktop's own app, as Finder is on a Mac. It browses folders,
-- opens folders and apps, and shows anything else with Quick Look.
--
-- The desktop starts Files, starts it again if it stops, and never quits it,
-- so closing its last window leaves it running. New Window (Ctrl+N), or
-- clicking Files in the dock with no window open, opens another.

local files = venture.files

-- The folders down the left of every window.
local BOOKMARKS = {
  { name = "Home", path = "/mnt" },
  { name = "User", path = "/mnt/user" },
  { name = "Apps", path = "/mnt/user/apps" },
  { name = "System", path = "/mnt/system" },
}

-- New windows open where Files started: the folder the desktop opened from.
local START = files.resolve()

-- The folder above `path`, or nil at the top.
local function parent(path)
  if path == "/" then return nil end
  local up = path:match("^(.*)/[^/]+$")
  if up == nil or up == "" then return "/" end
  return up
end

local function join(folder, name)
  if folder == "/" then return "/" .. name end
  return folder .. "/" .. name
end

local function isApp(entry)
  return entry.type == "folder" and entry.name:lower():match("%.app$") ~= nil
end

local function openWindow(start)
  local window = venture.ui.open("main.uix")
  local folder = start
  local entries = {}

  local names = {}
  for number, bookmark in ipairs(BOOKMARKS) do names[number] = bookmark.name end
  window:setItems("bookmarks", names)

  local function selected()
    local number = window:selected("entries")
    return number and entries[number]
  end

  -- Open and Quick Look need something selected.
  local function update()
    local nothing = selected() == nil
    window:setDisabled("openItem", nothing)
    window:setDisabled("lookItem", nothing)
  end

  -- Shows a folder, or says why it can't and keeps the one it was showing.
  local function show(path)
    local list, problem = files.list(path)
    if not list then
      window:setText("status", problem)
      window:setText("address", folder)
      return
    end

    folder, entries = path, list
    local items = {}
    for number, entry in ipairs(list) do
      -- Folders end in a slash, as `ls` shows them, but apps are things, not places.
      items[number] = (entry.type == "folder" and not isApp(entry)) and (entry.name .. "/") or entry.name
    end

    window:setItems("entries", items)
    window:select("entries", nil)
    window:setText("address", path)
    window:setTitle(path == "/" and "/" or path:match("[^/]+$"))
    window:setText("status", #list == 1 and "1 item" or (#list .. " items"))
    window:setDisabled("enclosing", parent(path) == nil)

    -- The bookmark for this folder, if there is one, stays selected.
    window:select("bookmarks", nil)
    for number, bookmark in ipairs(BOOKMARKS) do
      if bookmark.path == path then window:select("bookmarks", number) end
    end
    update()
  end

  -- Goes to what's typed in the address field, from the current folder when it's relative.
  local function goTo()
    local typed = window:text("address"):match("^%s*(.-)%s*$")
    if typed == "" then
      window:setText("address", folder)
      return
    end
    show(files.resolve(typed:sub(1, 1) == "/" and typed or join(folder, typed)))
  end

  local function quickLook()
    local entry = selected()
    if not entry then return end
    local ok, problem = venture.ui.preview(join(folder, entry.name))
    if not ok then window:setText("status", problem) end
  end

  -- A folder opens in this window, an app launches, and anything else gets Quick Look.
  local function open()
    local entry = selected()
    if not entry then return end
    local path = join(folder, entry.name)
    if isApp(entry) then
      local ok, problem = venture.ui.launch(path)
      if not ok then window:setText("status", problem) end
    elseif entry.type == "folder" then
      show(path)
    else
      quickLook()
    end
  end

  -- A double-click opens what it lands on.
  window:on("entries", function(event)
    if event.kind == "submitted" then open() else update() end
  end)
  window:on("bookmarks", function(event)
    if event.kind == "selected" then show(BOOKMARKS[event.index].path) end
  end)
  window:on("address", goTo)
  window:on("go", goTo)
  window:on("goToFolder", function() window:focus("address") end)
  window:on("openItem", open)
  window:on("lookItem", quickLook)
  window:on("enclosing", function()
    local up = parent(folder)
    if up then show(up) end
  end)
  window:on("closeWindow", function() window:close() end)

  show(start)
end

venture.ui.onNewWindow(function() openWindow(START) end)
openWindow(START)
