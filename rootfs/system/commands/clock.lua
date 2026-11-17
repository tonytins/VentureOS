-- Show the time, or set how far the clock is from UTC.
-- Usage: clock [offset]
-- Needs: system
local args = require("venture.args")

local options = args.parse({...}, {
  name = "clock",
  summary = "Show the time, or set the clock's offset from UTC, as Settings does. The choice is saved. "
    .. "There's no daylight saving, so change the offset when the clocks change.",
  arguments = {
    { name = "offset", optional = true, summary = "From UTC, such as -05:00, 5:30, -5, or 0." },
  },
})
if not options then return end

if options.offset then
  local ok, problem = venture.setUtcOffset(options.offset)
  if not ok then
    print("clock: " .. problem)
    return
  end
  if problem then print("clock: " .. problem) end
end

local time = venture.time()
if not time then
  print("clock: this system has no clock.")
  return
end

if time.set then
  print(string.format("Time:   %s, %04d-%02d-%02d", time.text, time.year, time.month, time.day))
else
  print("Time:   unknown; the hardware clock isn't set")
end
print("Offset: " .. time.zone)
