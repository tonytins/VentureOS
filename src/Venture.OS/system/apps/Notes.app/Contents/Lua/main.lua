-- Notes: keeps short notes in notes.txt, in the folder Notes was opened from.
--
-- This runs once, when Notes opens. It registers the `note` command, which
-- lasts until Notes closes. Everything here is undone when it does.

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
end

venture.command {
  name = "note",
  summary = "Keep short notes.",
  usage = "note [-l] [-d <number>] [--clear] [text...]",
  run = note,
}

print(venture.app.name .. " is open. Type \"note --help\" to use it.")
