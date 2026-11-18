-- init.lua: runs once, after VentureOS finishes booting.
--
-- VentureOS installs this file to /mnt/system/init.lua at every boot, from
-- the copy built into the kernel, so edits made on the running system don't
-- survive a reboot. Change rootfs/system/init.lua instead.
--
-- Its output goes to the console and, prefixed with [Venture], to serial.
-- An error is reported and the shell starts anyway. Ctrl+C or Esc stops it,
-- and it stops on its own after 10 seconds.
--
-- Needs: system

print('Type "help" for a list of commands.')

-- A developer's own build may have developer.local.lua beside this file, to
-- turn on developer mode and trust their vdb at every boot. It's git-ignored,
-- so no build anyone else makes has it; `vdb identity --lua` prints its lines.
local developer = "/mnt/system/developer.local.lua"
local found = io.open(developer)
if found then
  found:close()
  dofile(developer)
end
