-- Make folders.
-- Usage: mkdir [-p] <folder...>
-- Needs: files
local args = require("venture.args")

local options = args.parse({...}, {
  name = "mkdir",
  summary = "Make folders.",
  arguments = { { name = "folder", many = true } },
  options = { parents = { short = "p", summary = "Make missing folders above it too, and accept one that exists." } },
})
if not options then return end

for _, folder in ipairs(options.folder) do
  local ok, problem = venture.files.mkdir(folder, options.parents)
  if not ok then print("mkdir: " .. problem) end
end
