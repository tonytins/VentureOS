-- Show the network adapter, its address, and its DNS server.
-- Usage: net
-- Needs: network

local net = venture.net()
if not net then
  print("No network device.")
  return
end

local function yes_no(value)
  return value and "yes" or "no"
end

print("Device:   " .. net.device)
print("MAC:      " .. net.mac)
print("Link up:  " .. yes_no(net.link_up))
print("Ready:    " .. yes_no(net.ready))

if not net.address then
  print("IPv4:     not configured")
  return
end

print("Address:  " .. net.address)
print("Subnet:   " .. (net.subnet or "unknown"))
print("Gateway:  " .. (net.gateway or "unknown"))
print("DNS:      " .. (net.dns or "none"))
