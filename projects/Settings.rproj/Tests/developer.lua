-- Developer: developer mode, off at every start, turned on and off.

test.case("Starts off", function(app)
  app:choose("panes", "Developer")
  test.equal(app:title(), "Developer")
  test.expect(app:checked("developerOff"))
  test.equal(app:text("developerStatus"), "Off. 0 computers are trusted.")
end)

test.case("On turns developer mode on and Off turns it off", function(app)
  app:choose("panes", "Developer")
  app:press("developerOn")
  test.expect(app:checked("developerOn"))
  test.equal(app:text("developerStatus"), "Listening on port 28455. 0 computers are trusted.")
  app:press("developerOff")
  test.expect(app:checked("developerOff"))
end)
