-- Wallpaper: the sky, then every one of the system's wallpapers, in a grid; the chosen one is saved.

test.case("The sky comes first, then every wallpaper", function(app)
  app:choose("panes", "Wallpaper")
  local rows = app:rows("wallpapers")
  test.equal(rows[1], "Sky")
  test.expect(#rows > 2, "the sky and the system's wallpapers")
  test.equal(app:text("wallpaperStatus"), "", "every picture was read")
end)

test.case("Choosing a wallpaper saves it, and the grid shows it chosen", function(app)
  app:choose("panes", "Wallpaper")
  for index, title in ipairs(app:rows("wallpapers")) do
    app:choose("wallpapers", index)
    test.equal(app:chosen("wallpapers"), index, title .. " is chosen")
  end

  app:choose("wallpapers", "sky")
  test.expect(app:read("/mnt/user/settings/appearance.xml"):find('wallpaper="sky"', 1, true))
end)

test.case("The wallpaper in use is chosen when Settings opens", function(app)
  local name = app:read("/mnt/user/settings/appearance.xml"):match('wallpaper="([^"]+)"') or "Charger"
  app:choose("panes", "Wallpaper")
  test.equal(app:rows("wallpapers")[app:chosen("wallpapers")], name)
end)
