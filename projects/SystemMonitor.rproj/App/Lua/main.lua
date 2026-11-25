-- System Monitor: the version, how long the system has been up, memory in
-- use, and the network, kept current once a second while the desktop shows;
-- and every app running, which can be ended, since one may run out of sight.

local window = venture.ui.open("main.uix")

local MIB = 1024 * 1024

-- A reading that fails, such as from a device, shows as unknown rather than
-- stopping the window.
local function read(reading)
  local ok, value = pcall(reading)
  if ok then return value end
  return nil
end

local function formatUptime(seconds)
  if not seconds then return "unknown" end
  local days = seconds // 86400
  local clock = string.format("%02d:%02d:%02d", seconds % 86400 // 3600, seconds % 3600 // 60, seconds % 60)
  if days > 0 then return string.format("%dd %s", days, clock) end
  return clock
end

local function describeNetwork(net)
  if net == nil then return "No network device." end
  if not net.link_up then return net.device .. ", no link" end
  if not net.address then return net.device .. ", waiting for an address" end
  return net.device .. ", " .. net.address
end

local function refresh()
  window:setText("version", venture.version().display)
  window:setText("uptime", formatUptime(read(venture.uptime)))

  local memory = read(venture.memory)
  if memory and memory.total > 0 then
    local used = memory.total - memory.free
    window:setText("memory", string.format("%d of %d MiB used", used // MIB, memory.total // MIB))
    window:setValue("memoryBar", used / memory.total)
  else
    window:setText("memory", "unknown")
    window:setValue("memoryBar", 0)
  end

  local ok, net = pcall(venture.net)
  window:setText("network", ok and describeNetwork(net) or "unknown")
end

-- Processes: every app running, oldest first, with how long it's run and
-- whether VentureOS keeps it, which ending only restarts.
local shown = {}

local function formatRun(seconds)
  if seconds < 60 then return seconds .. "s" end
  if seconds < 3600 then return (seconds // 60) .. "m" end
  return (seconds // 3600) .. "h " .. (seconds % 3600 // 60) .. "m"
end

local function refreshProcesses()
  local processes = read(venture.processes)
  if not processes then
    shown = {}
    window:setItems("processList", { "Processes can't be read on this system." })
    return
  end

  shown = processes
  local rows = {}
  for _, process in ipairs(processes) do
    local row = process.name .. ", " .. formatRun(process.seconds)
    if process.kept then row = row .. ", kept" end
    if process.state ~= "running" then row = row .. ", " .. process.state end
    rows[#rows + 1] = row
  end
  window:setItems("processList", rows)
end

window:on("endProcess", function()
  local process = shown[window:selected("processList") or 0]
  if not process then return end
  local ended, kept = venture.kill(process.id)
  if ended == nil then
    window:setText("processStatus", "Couldn't end it: " .. tostring(kept) .. ".")
  elseif kept then
    window:setText("processStatus", process.name .. " ended, and starts again, since VentureOS keeps it.")
  else
    window:setText("processStatus", process.name .. " ended.")
  end
  refreshProcesses()
end)

local function showPane(number)
  local title = window:items("panes")[number]
  window:setTitle(title)
  if title == "Processes" then refreshProcesses() end
end

window:on("panes", function(event)
  if event.kind == "selected" then showPane(event.index) end
end)

refresh()
refreshProcesses()
showPane(1)
window:every(1, function()
  refresh()
  refreshProcesses()
end)
