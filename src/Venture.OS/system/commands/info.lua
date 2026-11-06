-- Show the version, uptime, and memory.
-- Usage: info

local version = venture.version()
print(version.display)
print("Commit:  " .. version.commit)
print("Cosmos:  " .. version.cosmos)

local function format_uptime(seconds)
  if not seconds then
    return "unknown"
  end

  local days = seconds // 86400
  local clock = string.format("%02d:%02d:%02d", seconds % 86400 // 3600, seconds % 3600 // 60, seconds % 60)
  if days > 0 then
    return string.format("%dd %s", days, clock)
  end
  return clock
end

print("Uptime:  " .. format_uptime(venture.uptime()))

local memory = venture.memory()
if memory then
  local mebibyte = 1024 * 1024
  print(string.format("Memory:  %d MiB free of %d MiB", memory.free // mebibyte, memory.total // mebibyte))
else
  print("Memory:  unknown")
end
