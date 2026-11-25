-- Processes: every app running, and ending one. A test's VentureOS runs only System Monitor itself.

test.case("Lists the apps running, System Monitor among them", function(app)
  app:choose("panes", "Processes")
  test.equal(app:title(), "Processes")
  local rows = app:rows("processList")
  test.expect(#rows >= 1)
  test.expect(rows[1]:find("^System Monitor, %d+s$"), rows[1])
end)

test.case("Ending itself is refused, saying why", function(app)
  app:choose("panes", "Processes")
  app:choose("processList", 1)
  app:press("endProcess")
  test.equal(app:text("processStatus"),
    "Couldn't end it: System Monitor is running the code asking, and can't end from inside itself.")
end)

test.case("End with nothing chosen does nothing", function(app)
  app:choose("panes", "Processes")
  app:press("endProcess")
  test.equal(app:text("processStatus"), "")
end)

test.case("The sidebar shows one pane at a time and names the window", function(app)
  test.equal(app:title(), "System")
  test.expect(app:showing("version"))
  test.expect(not app:showing("processList"))
  app:choose("panes", "Processes")
  test.expect(not app:showing("version"))
  test.expect(app:showing("processList"))
end)
