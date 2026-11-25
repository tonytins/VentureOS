-- The sidebar: each pane shows its own controls alone, and the window is titled after it.

test.case("Every pane names the window and hides the others' controls", function(app)
  for _, row in ipairs(app:rows("panes")) do
    app:choose("panes", row)
    test.equal(app:title(), row)
    test.equal(app:showing("accents"), row == "Appearance", "the accents on " .. row)
    test.equal(app:showing("utcOffset"), row == "Clock", "utcOffset on " .. row)
  end
end)
