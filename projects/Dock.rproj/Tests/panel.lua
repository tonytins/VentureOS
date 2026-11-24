-- Dock: it opens as one panel along the bottom without an error. What it shows and does needs the desktop around
-- it, which airo test doesn't have yet; VentureOS's own tests drive it in one for now.

test.case("Opens as a panel without an error", function(app)
  test.equal(app:windows(), 1)
  for _, line in ipairs(app:printed()) do
    test.expect(not line:find("error", 1, true), line)
  end
end)
