-- System Monitor: the version, how long the system has been up, memory in
-- use, and the network, kept current once a second while the desktop shows.

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

refresh()
window:every(1, refresh)
