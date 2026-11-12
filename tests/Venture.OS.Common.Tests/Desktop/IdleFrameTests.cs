using Venture.OS.Bundles;
using Venture.OS.Desktop;
using Venture.OS.Scripting;
using Venture.OS.Shell;
using Venture.OS.Tests.Scripting;
using Venture.OS.Tests.Shell;

namespace Venture.OS.Tests.Desktop;

/// <summary>
/// The desktop's frame loop runs without pause, so a frame where nothing changes must not allocate, or memory churns
/// (BUG-6). The desktop here has a fallback app with a window, menus, lists, and a timer that isn't due.
/// </summary>
public sealed class IdleFrameTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"venture-idle-{Guid.NewGuid():N}");
    private readonly AppManager apps;
    private readonly DesktopSession session;
    private TimeSpan now;

    public IdleFrameTests()
    {
        var contents = Path.Combine(root, "apps", "Shelf.app", "Contents");
        Directory.CreateDirectory(Path.Combine(contents, "Lua"));
        Directory.CreateDirectory(Path.Combine(contents, "Resources"));
        File.WriteAllText(Path.Combine(contents, "Info.xml"), """
            <App version="1"><Info identifier="dev.example.shelf" name="Shelf" needs="ui"/></App>
            """);
        File.WriteAllText(Path.Combine(contents, "Resources", "main.uix"), """
            <UIScript version="1" requires="menus">
              <Window width="300" height="200"/>
              <ToolbarItem name="go" title="Go"/>
              <TitleField name="address"/>
              <Menu name="file" title="File"/>
              <MenuItem menu="file" name="open" title="Open" shortcut="Ctrl+O"/>
              <List name="items" x="10" y="10" width="200" height="100" selectable="true"/>
              <List name="log" x="10" y="120" width="200" height="60"/>
              <TextField name="field" x="220" y="10" width="70" height="20"/>
            </UIScript>
            """);
        File.WriteAllText(Path.Combine(contents, "Lua", "main.lua"), """
            local window = venture.ui.open("main.uix")
            window:setItems("items", { "one", "two", "three" })
            window:every(60, function() end)
            venture.ui.onNewWindow(function() venture.ui.open("main.uix") end)
            """);

        CommandRegistry registry = new();
        WindowManager windows = new(1024, 768);
        HeldOutput held = new();
        apps = new AppManager(
            Path.Combine(root, "apps"), new RuntimeCommands(registry.IsBuiltIn), new FakeSystemInfo(), windows: new AppWindows(windows, held));
        session = new DesktopSession(1024, 768, apps, root, held, windows)
        {
            FallbackApp = "Shelf",
            Clock = () => now,
        };
        session.StartFallbackApp();
    }

    public void Dispose()
    {
        apps.CloseApp("Shelf", new RecordingOutput());
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void AnIdleFrameAllocatesNothing()
    {
        RecordingPainter painter = new(1024, 768);
        DesktopInput still = new(500, 400, false);
        for (var frame = 0; frame < 50; frame++)
        {
            now += TimeSpan.FromMilliseconds(10);
            session.Update(still with { Time = now });
        }

        session.Draw(painter);

        // The runtime can allocate once in a while for its own reasons, such as recompiling hot code, so the frames
        // are measured in three batches. A frame that allocates would show in every batch.
        var changed = false;
        var fewest = long.MaxValue;
        for (var batch = 0; batch < 3; batch++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var frame = 0; frame < 1000; frame++)
            {
                now += TimeSpan.FromMilliseconds(10);
                changed |= session.Update(still with { Time = now });
            }

            fewest = Math.Min(fewest, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        Assert.False(changed);
        Assert.Equal(0, fewest);
    }
}
