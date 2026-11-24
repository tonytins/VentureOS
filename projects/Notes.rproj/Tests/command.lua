-- The note command, which Notes adds to the shell while it runs.

test.case("Adds, lists, deletes, and clears notes", function(app)
  test.equal(app:command("note")[1], 'No notes yet. Add one with "note <text>".')
  test.equal(app:command("note buy milk")[1], "Noted (1).")
  test.equal(app:command('note call the "train" office')[1], "Noted (2).")
  local listed = app:command("note -l")
  test.equal(listed[1], "  1  buy milk")
  test.equal(listed[2], "  2  call the train office")
  test.equal(app:command("note --delete 1")[1], "Deleted: buy milk")
  test.equal(app:command("note")[1], "  1  call the train office")
  test.equal(app:command("note --clear")[1], "Deleted 1 note.")
  test.equal(app:command("note --list")[1], 'No notes yet. Add one with "note <text>".')
end)

test.case("Keeps notes in notes.txt in the folder it opened from", function(app)
  app:command("note first")
  app:command("note second")
  test.equal(app:read("/mnt/user/notes.txt"), "first\nsecond\n")
end)

test.case("Explains a missing note", function(app)
  app:command("note only one")
  for _, line in ipairs({ "note -d 5", "note -d 1.5" }) do
    local said = app:command(line)
    test.expect(said[#said]:find("^note: there's no note"), line .. " said: " .. tostring(said[#said]))
  end
end)
