-- Show or change the desktop's look: light, dark, or auto, and its accent color.
-- Usage: appearance [light|dark|auto] [--accent #RRGGBB] [--from HH:MM] [--until HH:MM]
-- Needs: system
local args = require("venture.args")

local options = args.parse({...}, {
  name = "appearance",
  summary = "Show or change the desktop's look, as Settings does. The choice is saved. "
    .. "Auto is dark from --from until --until, by the clock.",
  arguments = { { name = "mode", optional = true, summary = "light, dark, or auto." } },
  options = {
    accent = { short = "a", type = "string", summary = "The accent color, as #RRGGBB." },
    from = { short = "f", type = "string", summary = "When auto turns dark, as HH:MM, such as 19:00." },
    ["until"] = { short = "u", type = "string", summary = "When auto turns light again, as HH:MM, such as 07:00." },
  },
})
if not options then return end

if options.mode or options.accent or options.from or options["until"] then
  local ok, problem = venture.setAppearance({
    mode = options.mode,
    accent = options.accent,
    darkFrom = options.from,
    darkUntil = options["until"],
  })
  if not ok then
    print("appearance: " .. problem)
    return
  end
  if problem then print("appearance: " .. problem) end
end

local look = venture.appearance()
if look.mode == "auto" then
  print("Appearance: auto, dark from " .. look.darkFrom .. " until " .. look.darkUntil
    .. " (" .. (look.dark and "dark" or "light") .. " now)")
else
  print("Appearance: " .. look.mode)
end
print("Accent:     " .. look.accent)
