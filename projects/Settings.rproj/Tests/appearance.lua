-- Appearance: the mode, the accent, the dark schedule, and corners, each changing the look and saving it.

local function saved(app)
  return app:read("/mnt/user/settings/appearance.xml")
end

test.case("Opens showing the look in use", function(app)
  local mode = saved(app):match('mode="(%a+)"')
  test.expect(app:checked(mode), "the saved mode, " .. mode .. ", should be checked")
  test.equal(app:pane(), "appearance")
  test.equal(#app:printed(), 0, "nothing printed")
end)

test.case("Dark switches the look and saves it", function(app)
  app:press("dark")
  test.expect(app:checked("dark"))
  test.expect(not app:checked("light"))
  test.expect(saved(app):find('mode="dark"', 1, true), "appearance.xml says dark")
  test.equal(app:text("status"), "")
end)

test.case("Each swatch sets its accent and keeps the mode", function(app)
  app:press("dark")
  local count = 0
  while app:has("swatch" .. (count + 1)) do
    count = count + 1
    app:press("swatch" .. count)
    test.expect(app:checked("dark"), "swatch " .. count .. " keeps dark")
    test.expect(saved(app):find('mode="dark"', 1, true))
  end
  test.expect(count > 1, "there are swatches")
end)

test.case("A custom accent applies", function(app)
  app:submit("custom", " #12abef ")
  test.equal(app:text("accentName"), "Custom, #12ABEF")
  test.equal(app:text("custom"), "")
  test.expect(saved(app):find('accent="#12ABEF"', 1, true), "appearance.xml has the accent")
end)

test.case("A wrong custom accent says why and changes nothing", function(app)
  local before = app:text("accentName")
  app:submit("custom", "blue")
  test.expect(app:text("status"):find("#RRGGBB", 1, true), "the status shows the form an accent takes")
  test.equal(app:text("custom"), "blue", "what was typed stays")
  test.equal(app:text("accentName"), before)
end)

test.case("Auto and its times set the schedule", function(app)
  app:press("auto")
  app:submit("darkFrom", "21:30")
  test.expect(app:checked("auto"))
  test.expect(not app:checked("light"))
  test.expect(saved(app):find('mode="auto"', 1, true))
  test.expect(saved(app):find('darkFrom="21:30"', 1, true), "appearance.xml has the new time")

  -- A time it can't read says why, and the field shows the time in use again.
  local until_ = app:text("darkUntil")
  app:submit("darkUntil", "7am")
  test.expect(app:text("status"):find("HH:MM", 1, true), "the status says how to write a time")
  test.equal(app:text("darkUntil"), until_)
end)
