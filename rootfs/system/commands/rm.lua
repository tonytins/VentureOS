-- Remove files and folders.
-- Usage: rm [-r] <path...>
-- Needs: files
local args = require("venture.args")

local options = args.parse({...}, {
  name = "rm",
  summary = "Remove files and empty folders.",
  arguments = { { name = "path", many = true } },
  options = { recursive = { short = "r", summary = "Remove folders and everything in them." } },
})
if not options then return end

for _, path in ipairs(options.path) do
  local ok, problem = venture.files.remove(path, options.recursive)
  if not ok then
    if problem:find("with things in it", 1, true) then problem = problem .. "; use -r to remove it all" end
    print("rm: " .. problem)
  end
end
