-- Show or change the desktop's look: light or dark, and its accent color.
-- Usage: appearance [light|dark] [--accent #RRGGBB]
-- Needs: system
local args = require("venture.args")

local options = args.parse({...}, {
  name = "appearance",
  summary = "Show or change the desktop's look. It lasts until reboot.",
  arguments = { { name = "mode", optional = true, summary = "light or dark." } },
  options = { accent = { short = "a", type = "string", summary = "The accent color, as #RRGGBB." } },
})
if not options then return end

if options.mode or options.accent then
  local mode = options.mode or venture.appearance().mode
  local ok, problem = venture.setAppearance(mode, options.accent)
  if not ok then
    print("appearance: " .. problem)
    return
  end
end

local look = venture.appearance()
print("Appearance: " .. look.mode)
print("Accent:     " .. look.accent)
