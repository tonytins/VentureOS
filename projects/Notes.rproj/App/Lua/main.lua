-- Notes: keeps short notes in notes.txt, in the folder Notes was opened from.
--
-- This runs once, when Notes opens. It opens Notes' window and registers the
-- `note` command; both work on the same notes, and both last until Notes
-- quits. Closing the window quits Notes, and everything here is undone.

local args = require("venture.args")
local files = venture.files

-- Decided now, so `note` keeps using the same file after a `cd`.
local path = files.resolve("notes.txt")

local function load()
  local text = files.read(path)
  local notes = {}
  if text then
    for line in text:gmatch("[^\n]+") do
      notes[#notes + 1] = line
    end
  end
  return notes
end

local function save(notes)
  local text = #notes > 0 and (table.concat(notes, "\n") .. "\n") or ""
  local ok, problem = files.write(path, text)
  if not ok then print("note: " .. problem) end
  return ok
end

local window = venture.ui.open("main.uix")

-- How the list shows the notes, from Note > Sort By. Only the list changes:
-- notes.txt, and `note`, keep the order they were added in.
local orders = { sortAdded = "added", sortNewest = "newest", sortTitle = "title" }
local order = "added"

-- The note number each row of the list shows, in the order the list shows them.
local shown = {}

local function arrange(notes)
  local numbers = {}
  for number = 1, #notes do numbers[number] = number end
  if order == "newest" then
    table.sort(numbers, function(a, b) return a > b end)
  elseif order == "title" then
    table.sort(numbers, function(a, b)
      local left, right = notes[a]:lower(), notes[b]:lower()
      if left == right then return a < b end
      return left < right
    end)
  end
  return numbers
end

-- The note the list has selected, by its number in notes.txt, or nil.
local function selectedNote()
  local row = window:selected("notes")
  return row and shown[row]
end

-- Shows the notes as they are in the file, so the window and `note` agree,
-- in the order chosen, keeping the note numbered `selected` selected when
-- it's given, or else the one selected before.
local function refresh(selected)
  local notes = load()
  selected = selected or selectedNote()
  shown = arrange(notes)
  local rows = {}
  local selectedRow = nil
  for row, number in ipairs(shown) do
    rows[row] = notes[number]
    if number == selected then selectedRow = row end
  end
  window:setItems("notes", rows)
  window:select("notes", selectedRow)
  for item, name in pairs(orders) do window:setChecked(item, name == order) end
  local nothingSelected = window:selected("notes") == nil
  window:setDisabled("remove", nothingSelected)
  window:setDisabled("deleteNote", nothingSelected)
  window:setDisabled("clearAll", #notes == 0)
end

local function add()
  local text = window:text("text"):match("^%s*(.-)%s*$")
  if text ~= "" then
    local notes = load()
    notes[#notes + 1] = text
    if save(notes) then
      window:setText("text", "")
      refresh(#notes)
    end
  end
  refresh()
  window:focus("text")
end

local function remove()
  local number = selectedNote()
  if not number then return end
  local notes = load()
  table.remove(notes, number)
  if save(notes) then window:select("notes", nil) end
  refresh()
end

window:on("add", add)
window:on("text", function(event)
  if event.kind == "submitted" then add() end
end)
window:on("notes", function() refresh() end)
window:on("remove", remove)
window:on("deleteNote", remove)
window:on("newNote", function() window:focus("text") end)
window:on("clearAll", function()
  if save({}) then refresh() end
end)
for item, name in pairs(orders) do
  window:on(item, function()
    order = name
    refresh()
  end)
end

local spec = {
  name = "note",
  summary = "Keep short notes.",
  arguments = { { name = "text", optional = true, many = true } },
  options = {
    list = { short = "l", summary = "List the notes." },
    delete = { short = "d", type = "number", summary = "Delete the note with this number." },
    clear = { summary = "Delete every note." },
  },
}

local function note(...)
  local options = args.parse({...}, spec)
  if not options then return end

  local notes = load()

  if options.clear then
    if save({}) then print("Deleted " .. #notes .. (#notes == 1 and " note." or " notes.")) end
  elseif options.delete then
    local number = options.delete
    if notes[number] == nil or number ~= math.floor(number) then
      print("note: there's no note " .. number .. ". Type \"note --list\" to see them.")
      return
    end
    local removed = table.remove(notes, number)
    if save(notes) then print("Deleted: " .. removed) end
  elseif #options.text > 0 then
    notes[#notes + 1] = table.concat(options.text, " ")
    if save(notes) then print("Noted (" .. #notes .. ").") end
  else
    -- Listing is what `note` does with nothing else to do; --list just asks for it plainly.
    if #notes == 0 then
      print("No notes yet. Add one with \"note <text>\".")
    end
    for number, text in ipairs(notes) do
      print(string.format("%3d  %s", number, text))
    end
  end

  refresh()
end

venture.command {
  name = "note",
  summary = "Keep short notes.",
  usage = "note [-l] [-d <number>] [--clear] [text...]",
  run = note,
}

refresh()
window:focus("text")
