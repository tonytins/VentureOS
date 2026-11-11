using Venture.OS.Bundles;
using Venture.OS.Desktop;
using Venture.OS.Interface;
using Venture.OS.Scripting;
using Venture.OS.Shell;
using Venture.OS.Tests.Scripting;
using Venture.OS.Tests.Shell;

namespace Venture.OS.Tests.Desktop;

/// <summary>
/// The desktop's fallback app, as Files is: Shelf, a temporary app that opens a window and offers new ones. It fails
/// to start while a file called <c>broken</c> sits next to it.
/// </summary>
public sealed class FallbackAppTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"venture-fallback-{Guid.NewGuid():N}");
    private readonly WindowManager windows = new(1024, 768);
    private readonly HeldOutput held = new();
    private readonly AppManager apps;
    private readonly DesktopSession session;
    private readonly RecordingPainter painter = new(800, 600);
    private TimeSpan now;

    public FallbackAppTests()
    {
        var contents = Path.Combine(root, "apps", "Shelf.app", "Contents");
        Directory.CreateDirectory(Path.Combine(contents, "Lua"));
        Directory.CreateDirectory(Path.Combine(contents, "Resources"));
        File.WriteAllText(Path.Combine(contents, "Info.xml"), """
            <App version="1"><Info identifier="dev.example.shelf" name="Shelf" needs="ui files"/></App>
            """);
        File.WriteAllText(Path.Combine(contents, "Resources", "main.uix"), """
            <UIScript><Window width="200" height="100"/></UIScript>
            """);
        File.WriteAllText(Path.Combine(contents, "Lua", "main.lua"), $$"""
            if venture.files.info("{{root.Replace("\\", "/")}}/broken") then error("Shelf is broken") end
            local function open() venture.ui.open("main.uix") end
            venture.ui.onNewWindow(open)
            open()
            """);

        CommandRegistry registry = new();
        apps = new AppManager(
            Path.Combine(root, "apps"), new RuntimeCommands(registry.IsBuiltIn), new FakeSystemInfo(), windows: new AppWindows(windows, held));
        session = new DesktopSession(800, 600, apps, root, held, windows)
        {
            FallbackApp = "Shelf",
            Clock = () => now,
        };
    }

    public void Dispose()
    {
        if (apps.IsOpen("dev.example.shelf"))
            apps.CloseApp("Shelf", new RecordingOutput());
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void StartsFirstInTheDockWithNewWindowButNoQuit()
    {
        session.StartFallbackApp();
        Frame();

        Assert.True(apps.IsOpen("dev.example.shelf"));
        Assert.Equal(new DockItem("Shelf", "Shelf", IsOpen: true), session.Dock.Items[0]);
        Assert.Equal(["VentureOS", "Shelf"], Titles());
        Assert.Equal(["newWindow"], Items(session.MenuBar.Menus[1]));

        Frame(keys: [new KeyPress(DesktopKey.Character, 'q', Control: true)]);
        Assert.True(apps.IsOpen("dev.example.shelf"));
    }

    [Fact]
    public void ClosingItsLastWindowLeavesItRunning()
    {
        session.StartFallbackApp();
        var window = Assert.Single(windows.Windows);

        var close = window.CloseButton;
        Frame((int)close.X + 6, (int)close.Y + 6, down: true);
        Frame((int)close.X + 6, (int)close.Y + 6);

        Assert.Empty(windows.Windows);
        Assert.True(apps.IsOpen("dev.example.shelf"));
        Assert.Equal(["VentureOS", "Shelf"], Titles());
    }

    [Fact]
    public void NewWindowAndTheDockOpenWindows()
    {
        session.StartFallbackApp();
        Frame(keys: [new KeyPress(DesktopKey.Character, 'n', Control: true)]);
        Assert.Equal(2, windows.Windows.Count);

        foreach (var window in (DesktopWindow[])[.. windows.Windows])
            windows.Close(window);
        var icon = session.Dock.IconBounds(0);
        Frame((int)icon.X + 10, (int)icon.Y + 10, down: true);
        Frame((int)icon.X + 10, (int)icon.Y + 10);

        Assert.Single(windows.Windows);
    }

    [Fact]
    public void ItsMenuShowsWhenNoOtherAppIsInFront()
    {
        session.StartFallbackApp();
        windows.Open(new DesktopWindow("Preview", new UIView(UIScript.Parse("""<UIScript><Window width="100" height="50"/></UIScript>"""))));
        Frame();

        Assert.Null(session.ActiveApp);
        Assert.Equal(["VentureOS", "Shelf"], Titles());
    }

    [Fact]
    public void StartsAgainAfterFailingWaitingLongerEachTime()
    {
        File.WriteAllText(Path.Combine(root, "broken"), "");

        Frame();
        var alert = Assert.Single(windows.Windows);
        Assert.StartsWith("Shelf didn't start. VentureOS will keep trying, next in 2 seconds.", alert.View.Text("message"));
        Assert.Contains("Shelf is broken", alert.View.Text("message"));

        // Too soon: no attempt. Then a second failure for the same reason adds no second message.
        now = TimeSpan.FromSeconds(1);
        Frame();
        now = TimeSpan.FromSeconds(2);
        Frame();
        Assert.Single(windows.Windows);
        Assert.False(apps.IsOpen("dev.example.shelf"));

        File.Delete(Path.Combine(root, "broken"));
        now = TimeSpan.FromSeconds(6);
        Frame();
        Assert.False(apps.IsOpen("dev.example.shelf"));
        now = TimeSpan.FromSeconds(7);
        Frame();

        Assert.True(apps.IsOpen("dev.example.shelf"));
        Assert.True(session.Dock.Items[0].IsOpen);
    }

    [Fact]
    public void StartsAgainWhenSomethingClosesIt()
    {
        session.StartFallbackApp();
        apps.CloseApp("Shelf", new RecordingOutput());

        Frame();

        Assert.True(apps.IsOpen("dev.example.shelf"));
    }

    private void Frame(int x = 400, int y = 300, bool down = false, KeyPress[]? keys = null)
    {
        session.Update(new DesktopInput(x, y, down, Keys: keys));
        session.Draw(painter);
    }

    private List<string> Titles()
    {
        List<string> titles = [];
        foreach (var menu in session.MenuBar.Menus)
            titles.Add(menu.Menu.Title);
        return titles;
    }

    private static List<string?> Items(BarMenu menu)
    {
        List<string?> names = [];
        foreach (var entry in menu.Menu.Entries)
            names.Add(entry.Name);
        return names;
    }
}
