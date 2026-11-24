-- Files: browsing folders in a window, and handing what's opened to the app that opens it.

local function setUp(app)
  app:write("/mnt/user/work/notes.txt", "milk\neggs\n")
  app:write("/mnt/user/work/photos/beach.txt", "sand\n")
  app:write("/mnt/user/work/Dice.app/Contents/Info.xml", '<App version="1"><Info identifier="dev.example.dice" name="Dice"/></App>')
  app:write("/mnt/user/work/Dice.app/Contents/Lua/main.lua", "")
  test.expect(app:newWindow(), "Files opens a window")
  app:submit("address", "/mnt/user/work")
end

test.case("Starts with the menu bar and no window, until asked for one", function(app)
  test.equal(app:windows(), 1, "only the menu bar")
  test.expect(app:newWindow())
  test.equal(app:windows(), 2)
end)

test.case("Shows a folder's entries and how many", function(app)
  setUp(app)
  test.equal(app:title(), "work")
  test.equal(app:text("address"), "/mnt/user/work")
  test.equal(table.concat(app:rows("entries"), "|"), "Dice.app|notes.txt|photos/")
  test.equal(app:subtitle(), "3 items")
  test.expect(app:dimmed("openItem"), "Open is dimmed with nothing chosen")
  test.expect(app:dimmed("lookItem"), "Quick Look is dimmed with nothing chosen")
end)

test.case("Opens a folder and goes back up", function(app)
  setUp(app)
  app:choose("entries", "photos/")
  test.expect(not app:dimmed("openItem"))
  app:press("openItem")
  test.equal(table.concat(app:rows("entries"), "|"), "beach.txt")
  test.equal(app:title(), "photos")
  test.equal(app:subtitle(), "1 item")

  app:press("enclosing")
  test.equal(app:text("address"), "/mnt/user/work")
end)

test.case("The address field goes to a typed folder, relative or not", function(app)
  setUp(app)
  app:submit("address", "photos")
  test.equal(app:text("address"), "/mnt/user/work/photos")
  app:submit("address", "/mnt/user/work")
  test.equal(app:subtitle(), "3 items")
end)

test.case("A missing folder says why and keeps the current one", function(app)
  setUp(app)
  app:submit("address", "nowhere")
  test.equal(app:text("address"), "/mnt/user/work")
  test.expect(app:text("status"):find("nowhere", 1, true))
  test.equal(app:subtitle(), "3 items")

  -- The next folder shown clears the problem.
  app:submit("address", "/mnt/user/work")
  test.equal(app:text("status"), "")
end)

test.case("Every bookmark goes to a folder that's there", function(app)
  setUp(app)
  for _, row in ipairs(app:rows("bookmarks")) do
    app:choose("bookmarks", row)
    test.equal(app:text("status"), "", row .. " goes somewhere")
  end
end)

test.case("Opening a text file hands it to TextEdit", function(app)
  setUp(app)
  app:choose("entries", "notes.txt")
  app:press("openItem")
  test.equal(app:title(), "notes.txt")
  test.equal(app:text("text"), "milk\neggs\n")
end)

test.case("Quick Look shows a file in a window of its own", function(app)
  setUp(app)
  local before = app:windows()
  app:choose("entries", "notes.txt")
  app:press("lookItem")
  test.equal(app:windows(), before + 1)
  test.equal(app:title(), "notes.txt")
  test.equal(table.concat(app:rows("lines"), "|"), "milk|eggs")
end)

test.case("Opening an app launches it once", function(app)
  setUp(app)
  app:choose("entries", "Dice.app")
  app:press("openItem")
  app:press("openItem")
  for _, line in ipairs(app:printed()) do
    test.expect(not line:find("already open", 1, true), line)
  end
end)
