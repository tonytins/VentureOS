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

-- Shows the notes as they are in the file, so the window and `note` agree,
-- selecting the note numbered `selected` when it's given.
local function refresh(selected)
  local notes = load()
  window:setItems("notes", notes)
  if selected then window:select("notes", selected) end
  window:setText("count", #notes == 1 and "1 note" or (#notes .. " notes"))
  local nothingSelected = window:selected("notes") == nil
  window:setDisabled("delete", nothingSelected)
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

local function delete()
  local number = window:selected("notes")
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
window:on("delete", delete)
window:on("deleteNote", delete)
window:on("newNote", function() window:focus("text") end)
window:on("clearAll", function()
  if save({}) then refresh() end
end)

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
