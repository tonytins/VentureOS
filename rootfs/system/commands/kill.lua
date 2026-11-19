-- End a running app, by its ID from ps, its name, or its identifier.
-- Usage: kill <id|name>
-- Needs: system
local args = require("venture.args")

local options = args.parse({...}, {
  name = "kill",
  summary = "End a running app, by its ID from ps, its name, or its identifier.",
  arguments = { { name = "app" } },
})
if not options then return end

local ok, kept = venture.kill(options.app)
if not ok then
  print("kill: " .. kept)
elseif kept then
  print("Ended " .. options.app .. ". VentureOS keeps it running, so it starts again.")
end
