-- List the files and folders in a folder.
-- Usage: ls [-l] [folder...]
-- Needs: files
local args = require("venture.args")
local files = venture.files

local options = args.parse({...}, {
  name = "ls",
  summary = "List the files and folders in a folder.",
  arguments = { { name = "folder", optional = true, many = true } },
  options = { long = { short = "l", summary = "Show each entry's type and size." } },
})
if not options then return end

local folders = options.folder
if #folders == 0 then folders = { "." } end

for index, folder in ipairs(folders) do
  local entries, problem = files.list(folder)
  if not entries then
    print("ls: " .. problem)
  else
    if #folders > 1 then
      if index > 1 then print("") end
      print(files.resolve(folder) .. ":")
    end
    for _, entry in ipairs(entries) do
      local name = entry.type == "folder" and (entry.name .. "/") or entry.name
      if options.long then
        local size = entry.type == "folder" and "-" or tostring(entry.size)
        print(string.format("%s %10s  %s", entry.type == "folder" and "d" or "f", size, name))
      else
        print(name)
      end
    end
  end
end
