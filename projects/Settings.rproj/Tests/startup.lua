-- Startup: the apps that open with the desktop, added from the installed apps and removed again.

test.case("Starts with nothing to open, and the installed apps to add", function(app)
  app:choose("panes", "Startup")
  test.equal(app:title(), "Startup")
  test.equal(#app:rows("startupItems"), 0)
  test.expect(#app:rows("installedApps") > 0)
  for _, name in ipairs(app:rows("installedApps")) do
    test.expect(name ~= "Settings", "Settings doesn't offer itself")
  end
end)

test.case("Add puts an app on the list and saves it, and Remove takes it off", function(app)
  app:choose("panes", "Startup")
  local name = app:rows("installedApps")[1]
  app:choose("installedApps", 1)
  app:press("addStartup")

  test.equal(app:rows("startupItems")[1], name)
  test.equal(app:text("startupStatus"), name .. " opens at startup.")
  for _, other in ipairs(app:rows("installedApps")) do
    test.expect(other ~= name, "an app on the list isn't offered again")
  end
  -- The rows moved up, so nothing is left chosen: Add again adds nothing.
  app:press("addStartup")
  test.equal(#app:rows("startupItems"), 1)
  local saved = app:read("/mnt/user/settings/startup.xml")
  test.expect(saved and saved:find("<App identifier="), saved)

  app:choose("startupItems", 1)
  app:press("removeStartup")
  test.equal(#app:rows("startupItems"), 0)
  test.equal(app:text("startupStatus"), name .. " won't open at startup.")
end)

test.case("An app that's gone is shown by its identifier and can still be removed", function(app)
  app:write("/mnt/user/settings/startup.xml",
    '<StartupItems version="1"><App identifier="dev.example.gone"/></StartupItems>')
  app:choose("panes", "Startup")
  -- The pane reads the list as it's shown.
  app:choose("panes", "Clock")
  app:choose("panes", "Startup")

  test.equal(app:rows("startupItems")[1], "dev.example.gone (not installed)")
  app:choose("startupItems", 1)
  app:press("removeStartup")
  test.equal(#app:rows("startupItems"), 0)
end)

test.case("Pressing Add or Remove with nothing chosen does nothing", function(app)
  app:choose("panes", "Startup")
  app:press("addStartup")
  app:press("removeStartup")
  test.equal(#app:rows("startupItems"), 0)
  test.equal(app:text("startupStatus"), "")
end)
