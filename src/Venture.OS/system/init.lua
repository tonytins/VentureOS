-- init.lua: runs once, after VentureOS finishes booting.
--
-- VentureOS installs this file to /mnt/system/init.lua at every boot, from
-- the copy built into the kernel, so edits made on the running system don't
-- survive a reboot. Change src/Venture.OS/system/init.lua instead.
--
-- Its output goes to the console and, prefixed with [Venture], to serial.
-- An error is reported and the shell starts anyway. It can't be
-- interrupted, so a script that never finishes keeps the shell from starting.

print('Type "help" for a list of commands.')
