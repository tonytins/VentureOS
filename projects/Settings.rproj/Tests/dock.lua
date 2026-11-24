-- Dock: always visible, hiding on its own, or hiding when a window would cover it, saved with the look.

test.case("Choosing a mode checks it, explains it, and saves it", function(app)
  app:choose("panes", "Dock")
  test.equal(app:title(), "Dock")
  for _, mode in ipairs({ { "dockDodge", "dodge" }, { "dockAutoHide", "autohide" }, { "dockAlways", "always" } }) do
    app:press(mode[1])
    test.expect(app:checked(mode[1]), mode[1] .. " is checked")
    test.expect(app:read("/mnt/user/settings/appearance.xml"):find('dock="' .. mode[2] .. '"', 1, true), mode[2] .. " is saved")
    test.expect(app:text("dockAbout") ~= "", "it says what " .. mode[2] .. " does")
  end
end)
