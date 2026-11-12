-- Send echo requests to a host and show how long each reply took.
-- Usage: ping [-c count] <host>
-- Needs: network
local args = require("venture.args")

local options = args.parse({...}, {
  name = "ping",
  summary = "Send echo requests to a host and show how long each reply took. Each waits up to 2 seconds.",
  arguments = { { name = "host", summary = "A host name or an IPv4 address." } },
  options = { count = { short = "c", type = "number", default = 4, summary = "How many to send, from 1 to 20." } },
})
if not options then return end

local count = options.count
if count < 1 or count > 20 or count % 1 ~= 0 then
  print("ping: the count is a whole number from 1 to 20")
  return
end

-- Resolve once, so each request goes to the same address.
local address, problem = venture.resolve(options.host)
if not address then
  print("ping: " .. problem)
  return
end

if address == options.host then
  print("Pinging " .. address .. ":")
else
  print("Pinging " .. options.host .. " (" .. address .. "):")
end

local received = 0
for _ = 1, count do
  local time, why = venture.ping(address)
  if time then
    received = received + 1
    print(("Reply from %s: time=%d ms"):format(address, time))
  else
    print(("No reply from %s: %s"):format(address, why))
  end
end

print(("%d sent, %d received."):format(math.tointeger(count), received))
