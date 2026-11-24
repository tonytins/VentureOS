-- Wallpaper: the sky, then each of the system's wallpapers, the chosen one checked, named, and saved.

test.case("Choosing the sky saves it", function(app)
  app:choose("panes", "Wallpaper")
  app:press("wallpaper1")
  test.expect(app:checked("wallpaper1"))
  test.equal(app:text("wallpaperName"), "Sky")
  test.expect(app:read("/mnt/user/settings/appearance.xml"):find('wallpaper="sky"', 1, true))
end)

test.case("Exactly one wallpaper is checked, and choosing another moves the check", function(app)
  app:choose("panes", "Wallpaper")
  local slots = {}
  local slot = 1
  while app:has("wallpaper" .. slot) do
    if app:showing("wallpaper" .. slot) then
      slots[#slots + 1] = "wallpaper" .. slot
    end
    slot = slot + 1
  end
  test.expect(#slots > 1, "the sky and at least one wallpaper show")

  for _, chosen in ipairs(slots) do
    app:press(chosen)
    for _, other in ipairs(slots) do
      test.equal(app:checked(other), other == chosen, other .. " after choosing " .. chosen)
    end
    test.expect(app:text("wallpaperName") ~= "", chosen .. " is named")
  end
end)

test.case("Unused slots stay hidden after visiting another pane", function(app)
  app:choose("panes", "Wallpaper")
  local shown = {}
  local slot = 1
  while app:has("wallpaper" .. slot) do
    shown[slot] = app:showing("wallpaper" .. slot)
    slot = slot + 1
  end
  app:choose("panes", "Clock")
  app:choose("panes", "Wallpaper")
  for index, was in ipairs(shown) do
    test.equal(app:showing("wallpaper" .. index), was, "wallpaper" .. index)
  end
end)
