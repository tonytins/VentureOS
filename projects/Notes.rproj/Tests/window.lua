-- Notes' window, which shares its notes with the command.

local function notes(app, ...)
  for _, text in ipairs({ ... }) do
    app:command("note " .. text)
  end
end

test.case("Opens a window that shares the notes with the command", function(app)
  notes(app, "buy milk")
  test.equal(app:title(), nil)
  test.equal(table.concat(app:rows("notes"), "|"), "buy milk")
  test.expect(app:dimmed("remove"), "remove is dimmed with nothing chosen")
  test.equal(app:focused(), "text")

  app:submit("text", "  call the office  ")
  test.equal(app:read("/mnt/user/notes.txt"), "buy milk\ncall the office\n")
  test.equal(app:text("text"), "")
  test.equal(app:chosen("notes"), 2)
  test.expect(not app:dimmed("remove"))
end)

test.case("Deletes and clears from the window", function(app)
  notes(app, "one", "two")
  app:choose("notes", "one")
  app:press("deleteNote")
  test.equal(app:read("/mnt/user/notes.txt"), "two\n")
  test.equal(app:chosen("notes"), nil)

  app:press("clearAll")
  test.equal(#app:rows("notes"), 0)
  test.expect(app:dimmed("clearAll"))
end)

test.case("Sort By changes only the list's order, and checks the order chosen", function(app)
  notes(app, "banana", "Apple", "cherry")
  test.equal(table.concat(app:rows("notes"), "|"), "banana|Apple|cherry")
  test.expect(app:checked("sortAdded"))

  app:press("sortNewest")
  test.equal(table.concat(app:rows("notes"), "|"), "cherry|Apple|banana")
  test.expect(app:checked("sortNewest"))
  test.expect(not app:checked("sortAdded"))

  app:press("sortTitle")
  test.equal(table.concat(app:rows("notes"), "|"), "Apple|banana|cherry")

  -- The file, and the command, keep the order the notes were added in.
  test.equal(app:read("/mnt/user/notes.txt"), "banana\nApple\ncherry\n")
end)

test.case("Deleting in a sorted list deletes the note shown, and the choice survives a sort", function(app)
  notes(app, "banana", "Apple", "cherry")
  app:press("sortTitle")
  app:choose("notes", "banana")

  app:press("sortNewest")
  test.equal(app:rows("notes")[app:chosen("notes")], "banana")

  app:press("deleteNote")
  test.equal(app:read("/mnt/user/notes.txt"), "Apple\ncherry\n")
  test.equal(table.concat(app:rows("notes"), "|"), "cherry|Apple")
end)
