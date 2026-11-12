-- Terminal: the shell in a window. Each window has a shell of its own, with
-- its own current folder; type a command and press Enter. `exit` closes the
-- window, and Shell ▸ Clear (Ctrl+K) empties it.
--
-- Commands that need the text console, such as `desktop` and the `lua`
-- prompt, say so here. Ctrl+C or Esc stops a script that runs too long.

-- The most lines a window keeps, so a long session can't fill memory.
local MAX_LINES = 500

local function openWindow()
  local window = venture.ui.open("main.uix")
  local shell = venture.openShell()
  local lines = {}

  local function add(text)
    for line in (text .. "\n"):gmatch("(.-)\n") do
      lines[#lines + 1] = line
    end
    while #lines > MAX_LINES do
      table.remove(lines, 1)
    end
  end

  local function show()
    window:setText("output", table.concat(lines, "\n"))
    window:setText("prompt", shell:directory() .. ">")
  end

  local function clear()
    lines = {}
    show()
  end

  window:on("command", function(event)
    if event.kind ~= "submitted" then return end
    local line = event.text
    window:setText("command", "")
    add(shell:directory() .. "> " .. line)

    if line:match("^%s*exit%s*$") then
      window:close()
      return
    end

    local output, cleared = shell:run(line)
    if cleared then lines = {} end
    if output ~= "" then add(output) end
    show()
  end)
  window:on("clearItem", clear)

  add(venture.version().display)
  add('Type "help" for a list of commands, and "exit" to close this window.')
  show()
  window:focus("command")
end

venture.ui.onNewWindow(openWindow)
openWindow()
