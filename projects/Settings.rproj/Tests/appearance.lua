-- Appearance: the style, the accent, the dark schedule, and scroll bars, each a choice in a grid that changes the
-- look and saves it.

local function saved(app)
  return app:read("/mnt/user/settings/appearance.xml")
end

test.case("Opens showing the look in use", function(app)
  local mode = saved(app):match('mode="(%a+)"')
  test.equal(app:rows("styles")[app:chosen("styles")], ({ auto = "Auto", light = "Light", dark = "Dark" })[mode],
    "the saved style, " .. mode .. ", should be chosen")
  test.equal(app:rows("scrollBars")[app:chosen("scrollBars")], "Classic")
  test.equal(app:rows("accents")[app:chosen("accents")], "Blue")
  test.equal(app:pane(), "appearance")
  test.equal(#app:printed(), 0, "nothing printed")
end)

test.case("Dark switches the look and saves it", function(app)
  app:choose("styles", "dark")
  test.equal(app:chosen("styles"), 3)
  test.expect(saved(app):find('mode="dark"', 1, true), "appearance.xml says dark")
  test.equal(app:text("status"), "")
end)

test.case("Auto's times show only while Auto is chosen", function(app)
  app:choose("styles", "light")
  test.expect(not app:showing("darkFrom"), "light hides the times")
  app:choose("styles", "auto")
  test.expect(app:showing("darkFrom") and app:showing("darkUntil"), "auto shows them")
end)

test.case("Each swatch sets its accent and keeps the style", function(app)
  app:choose("styles", "dark")
  local names = app:rows("accents")
  test.equal(#names, 10, "nine swatches and the custom color")
  for index = 1, 9 do
    app:choose("accents", index)
    test.equal(app:chosen("accents"), index, names[index] .. " is chosen")
    test.expect(saved(app):find('mode="dark"', 1, true), names[index] .. " keeps dark")
    test.expect(not app:showing("customAccent"), names[index] .. " hides the custom field")
  end
  test.expect(saved(app):find('accent="#6F8396"', 1, true), "the last swatch, Slate, is saved")
end)

test.case("The custom color shows its field, and a typed color applies", function(app)
  test.expect(not app:showing("customAccent"), "the field waits for the custom color")
  app:choose("accents", "custom")
  test.expect(app:showing("customAccent"), "choosing it shows the field")
  test.equal(app:focused(), "customAccent")

  app:submit("customAccent", " #12abef ")
  test.equal(app:text("customAccent"), "")
  test.expect(saved(app):find('accent="#12ABEF"', 1, true), "appearance.xml has the accent")
  test.equal(app:chosen("accents"), 10, "a color that isn't a swatch is the custom one")
  test.expect(app:showing("customAccent"), "the field stays")
end)

test.case("A wrong custom color says why and changes nothing", function(app)
  app:choose("accents", "custom")
  app:submit("customAccent", "blue")
  test.expect(app:text("status"):find("#RRGGBB", 1, true), "the status shows the form an accent takes")
  test.equal(app:text("customAccent"), "blue", "what was typed stays")
  test.expect(saved(app):find('accent="#3584E4"', 1, true), "the accent is still blue")
end)

test.case("Auto and its times set the schedule", function(app)
  app:choose("styles", "auto")
  app:submit("darkFrom", "21:30")
  test.equal(app:chosen("styles"), 1)
  test.expect(saved(app):find('mode="auto"', 1, true))
  test.expect(saved(app):find('darkFrom="21:30"', 1, true), "appearance.xml has the new time")

  -- A time it can't read says why, and the field shows the time in use again.
  local until_ = app:text("darkUntil")
  app:submit("darkUntil", "7am")
  test.expect(app:text("status"):find("HH:MM", 1, true), "the status says how to write a time")
  test.equal(app:text("darkUntil"), until_)
end)

test.case("Scroll bars switch between classic and automatic and are saved", function(app)
  app:choose("scrollBars", "automatic")
  test.equal(app:chosen("scrollBars"), 2)
  test.expect(saved(app):find('scrollbars="automatic"', 1, true), "appearance.xml says automatic")
  test.equal(app:text("status"), "")

  app:choose("scrollBars", "Classic")
  test.equal(app:chosen("scrollBars"), 1)
  test.expect(saved(app):find('scrollbars="classic"', 1, true), "appearance.xml says classic")
end)
