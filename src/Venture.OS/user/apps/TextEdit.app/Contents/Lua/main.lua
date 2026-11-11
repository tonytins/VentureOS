-- TextEdit: opens, edits, and saves text files, one per window.
--
-- The path in the title bar is the file: type one and press Enter to open
-- it, or to name a new file, then Save. Files hands TextEdit the text files
-- you open there. TextEdit opens no window of its own when it starts; the
-- desktop asks it for a new one, unless it was started to open a file.

local files = venture.files

-- A text area holds up to this much, so a larger file can't be edited here.
local MAXIMUM = 65536

local function trim(text) return text:match("^%s*(.-)%s*$") end
local function nameOf(path) return path:match("[^/]+$") or path end

local function openWindow(path)
  local window = venture.ui.open("main.uix", { title = "Untitled" })
  local current = nil -- the file this window holds, once opened or saved
  local edited = false

  local function show(message) window:setText("status", message) end

  local function setEdited(value)
    edited = value
    window:setTitle((current and nameOf(current) or "Untitled") .. (edited and " (edited)" or ""))
  end

  local function load(file)
    local text, problem = files.read(file)
    if not text then
      show(problem)
      window:setText("path", current or "")
      return
    end
    if #text > MAXIMUM then
      show(nameOf(file) .. " is too large to edit here.")
      window:setText("path", current or "")
      return
    end

    current = file
    window:setText("text", (text:gsub("\r\n", "\n")))
    window:setText("path", file)
    setEdited(false)
    show("Opened " .. nameOf(file) .. ".")
    window:focus("text")
  end

  local function save()
    local typed = trim(window:text("path"))
    if typed == "" then
      show("Type a path in the title bar, then Save.")
      window:focus("path")
      return
    end

    local file = files.resolve(typed)
    local ok, problem = files.write(file, window:text("text"))
    if not ok then
      show(problem)
      return
    end

    current = file
    window:setText("path", file)
    setEdited(false)
    show("Saved " .. nameOf(file) .. ".")
  end

  -- Enter in the title bar opens that file, or names a new one for Save.
  window:on("path", function()
    local typed = trim(window:text("path"))
    if typed == "" then
      window:setText("path", current or "")
      return
    end

    local file = files.resolve(typed)
    if files.info(file) then
      load(file)
    else
      current = nil
      window:setText("path", file)
      setEdited(edited)
      show("A new file: Save creates " .. nameOf(file) .. ".")
    end
  end)

  window:on("text", function(event)
    if event.kind == "changed" and not edited then
      setEdited(true)
      show("Edited.")
    end
  end)
  window:on("save", save)
  window:on("saveItem", save)
  window:on("openItem", function() window:focus("path") end)
  window:on("closeWindow", function() window:close() end)

  if path then
    load(path)
  else
    window:focus("text")
  end
end

venture.ui.onNewWindow(function() openWindow(nil) end)
venture.ui.onOpenFile(openWindow)
