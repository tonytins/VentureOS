-- System Monitor: what VentureOS says about itself, kept current. A test's VentureOS has no measured memory or
-- network, so those read as a person would see them there.

test.case("Shows the system when it opens", function(app)
  test.expect(app:text("version"):find("^VentureOS %d+%.%d+%.%d+ %(Build %d+%)$"), app:text("version"))
  test.expect(app:text("uptime"):find("^%d%d:%d%d:%d%d$"), app:text("uptime"))
  test.equal(app:text("memory"), "unknown")
  test.equal(app:value("memoryBar"), 0)
  test.equal(app:text("network"), "No network device.")
  test.equal(#app:printed(), 0, "nothing printed")
end)

test.case("Keeps current every second", function(app)
  app:wait(1.5)
  test.expect(app:text("uptime"):find("^%d%d:%d%d:%d%d$"), app:text("uptime"))
  test.equal(app:text("network"), "No network device.")
end)
