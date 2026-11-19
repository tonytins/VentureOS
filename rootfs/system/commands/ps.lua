-- List the running apps, as processes: their IDs, how long they've run, and whether VentureOS keeps them running.
-- Usage: ps
-- Needs: system
local args = require("venture.args")

local options = args.parse({...}, {
  name = "ps",
  summary = "List the running apps, as processes.",
})
if not options then return end

local processes = venture.processes()
if not processes then
  print("ps: this system has no apps.")
  return
end
if #processes == 0 then
  print("No apps are running.")
  return
end

-- How long, as the shortest of 42s, 3m 05s, or 2h 07m.
local function took(seconds)
  if seconds < 60 then return seconds .. "s" end
  if seconds < 3600 then return string.format("%dm %02ds", seconds // 60, seconds % 60) end
  return string.format("%dh %02dm", seconds // 3600, seconds // 60 % 60)
end

local width = #"NAME"
for _, process in ipairs(processes) do width = math.max(width, #process.name) end

print(string.format("%4s  %-" .. width .. "s  %-8s  %8s  %s", "ID", "NAME", "STATE", "UP", "KEPT"))
for _, process in ipairs(processes) do
  print(string.format("%4d  %-" .. width .. "s  %-8s  %8s  %s",
    process.id, process.name, process.state, took(process.seconds), process.kept and "yes" or ""))
end
