-- Terminal: a shell in a window, with the system's commands.

local function run(app, line)
  app:submit("command", line)
end

test.case("Opens with the version and a prompt", function(app)
  test.expect(app:text("output"):find("^VentureOS %d+%.%d+%.%d+ %(Build %d+%)\n"), "the version comes first")
  test.expect(app:text("output"):find('Type "help" for a list of commands', 1, true))
  test.equal(app:text("prompt"), "/mnt/user>")
  test.equal(app:focused(), "command")
end)

test.case("Runs a command and shows what it printed", function(app)
  run(app, "echo hello there")
  test.expect(app:text("output"):find("\n/mnt/user> echo hello there\nhello there$"), app:text("output"))
  test.equal(app:text("command"), "")
end)

test.case("Clear empties the window", function(app)
  run(app, "echo one")
  app:press("clearItem")
  test.equal(app:text("output"), "")
end)

test.case("Reports an unknown command and carries on", function(app)
  run(app, "nothing")
  run(app, "echo still here")
  test.expect(app:text("output"):find('"nothing" isn\'t a command', 1, true))
  test.expect(app:text("output"):find("still here$"))
end)

test.case("Keeps only its newest 500 lines", function(app)
  for line = 0, 299 do
    run(app, "echo " .. line)
  end
  local lines = {}
  for line in (app:text("output") .. "\n"):gmatch("(.-)\n") do
    lines[#lines + 1] = line
  end
  test.equal(#lines, 500)
  test.equal(lines[#lines], "299")
end)

test.case("New Window opens another in the user's folder", function(app)
  test.expect(app:newWindow())
  test.equal(app:windows(), 2)
  test.equal(app:text("prompt"), "/mnt/user>")
end)
