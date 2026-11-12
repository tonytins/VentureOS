-- init.lua: runs once, after VentureOS finishes booting.
--
-- VentureOS installs this file to /mnt/system/init.lua at every boot, from
-- the copy built into the kernel, so edits made on the running system don't
-- survive a reboot. Change rootfs/system/init.lua instead.
--
-- Its output goes to the console and, prefixed with [Venture], to serial.
-- An error is reported and the shell starts anyway. Ctrl+C or Esc stops it,
-- and it stops on its own after 10 seconds.

print('Type "help" for a list of commands.')
