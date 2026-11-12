-- Look up the address a host name has, through the DNS server DHCP gave.
-- Usage: resolve <name>
-- Needs: network
local args = require("venture.args")

local options = args.parse({...}, {
  name = "resolve",
  summary = "Look up the address a host name has, through the DNS server DHCP gave.",
  arguments = { { name = "name", summary = "A host name, such as example.com." } },
})
if not options then return end

local address, problem = venture.resolve(options.name)
if address then
  print(options.name .. ": " .. address)
else
  print("resolve: " .. problem)
end
