-- Show the free space where a folder is stored.
-- Usage: df [folder]
-- Needs: files
local args = require("venture.args")

local options = args.parse({...}, {
  name = "df",
  summary = "Show the free space where a folder is stored.",
  arguments = { { name = "folder", optional = true } },
})
if not options then return end

local function size(bytes)
  if bytes >= 1024 * 1024 then return string.format("%.1f MiB", bytes / (1024 * 1024)) end
  if bytes >= 1024 then return string.format("%.1f KiB", bytes / 1024) end
  return bytes .. " bytes"
end

local folder = options.folder or "."
local space, problem = venture.files.space(folder)
if not space then
  print("df: " .. problem)
  return
end
print(string.format("%s: %s free of %s", venture.files.resolve(folder), size(space.free), size(space.total)))
