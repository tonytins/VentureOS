using Venture.OS.Bundles;
using Venture.OS.Desktop;
using Venture.OS.Interface;
using Venture.OS.Processes;
using Venture.OS.Scripting;
using Venture.OS.Shell;
using Venture.OS.Tests.Desktop;
using Venture.OS.Tests.Scripting;
using Venture.OS.Tests.Shell;

namespace Venture.OS.Tests.Bundles;

/// <summary>
/// The shipped <c>Dock.app</c>, run by a desktop as the kernel runs it, over a few apps of its own: Files, the
/// desktop's app, which opens windows; Chatty, which opens none; Writer, with an icon; and Clock, one of the user's.
/// </summary>
public sealed class DockAppTests : IDisposable
{
    private static readonly string ShippedDock = Path.Combine(AppContext.BaseDirectory, "system", "apps", "Dock.app");

    private readonly string root = Path.Combine(Path.GetTempPath(), $"venture-dock-{Guid.NewGuid():N}").Replace('\\', '/');
    private readonly HeldOutput held = new();
    private readonly WindowManager windows = new(1024, 768);
    private readonly AppManager apps;
    private readonly DesktopSession session;
    private readonly RecordingPainter painter = new(1024, 768);
    private TimeSpan now;

    public DockAppTests()
    {
        Copy(ShippedDock, $"{root}/apps/Dock.app");
        App("apps", "Files", """
            local function open() venture.ui.open("main.uix") end
            venture.ui.onNewWindow(open)
            """, window: true);
        App("apps", "Chatty", "");
        App("apps", "Writer", "", icon: true);
        App("user/apps", "Clock", "");

        AppWindows host = new(windows, held)
        {
            Text = TestDesktopText.Create(),
            LoadImage = path => File.Exists(path) ? new DesktopImage(1, 1, [unchecked((int)0xFF3060A0)]) : null,
        };
        CommandRegistry registry = new();
        apps = new AppManager(
            $"{root}/apps", new RuntimeCommands(registry.IsBuiltIn), new FakeSystemInfo(), windows: host,
            userAppsDirectory: $"{root}/user/apps", supervisor: new Supervisor(() => now));
        session = new DesktopSession(1024, 768, apps, root, held, windows)
        {
            FallbackApp = "Files",
            DockApp = "Dock",
            Clock = () => now,
        };
        Frame();
        Frame();
    }

    public void Dispose()
    {
        session.Leave();
        foreach (var app in apps.Running.ToList())
        {
            apps.Supervisor.StopKeeping(app.Bundle.Identifier);
            apps.CloseApp(app.Bundle.Identifier, new RecordingOutput());
        }

        Directory.Delete(root, recursive: true);
    }

    private DesktopWindow DockWindow => windows.Windows.Single(window => window.IsPanel);

    private Shelf Shelf => DockWindow.View.Shelf("apps")!;

    [Fact]
    public void ShowsTheDesktopsAppFirstThenTheSystemsThenTheUsers()
    {
        Assert.Equal(["Files", "Chatty", "Writer", "Clock"], Shelf.Items.Select(item => item.Title));
        Assert.Equal([0, 0, 0, 1], Shelf.Items.Select(item => item.Group));
        Assert.Equal([true, false, false, false], Shelf.Items.Select(item => item.IsOpen));
    }

    [Fact]
    public void IsAPanelAlongTheBottomThatNeverTakesFocus()
    {
        Assert.True(apps.IsOpen("dev.tonytins.ventureos.dock"));
        Assert.Equal((768 - 72, Shelf.Width), (DockWindow.Y, DockWindow.Width));
        Assert.NotSame(DockWindow, windows.Focused);
        Assert.Equal(768 - 72, (int)(windows.WorkArea.Y + windows.WorkArea.Height));
    }

    [Fact]
    public void ShowsAnAppsIconAndATileForOneWithout()
    {
        var writer = Shelf.Items.Single(item => item.Title == "Writer").Icon!;
        Assert.Equal((Shelf.IconSize, Shelf.IconSize), (writer.Image.Width, writer.Image.Height));
        Assert.Null(Shelf.Items.Single(item => item.Title == "Chatty").Icon);

        Frame();
        var bounds = Shelf.IconBounds(Shelf.Items.ToList().FindIndex(item => item.Title == "Writer"));
        Assert.Contains(painter.Images, drawn => ReferenceEquals(drawn.Image, writer.Image) && drawn.X == (int)bounds.X && drawn.Y == (int)bounds.Y);
        Assert.Contains(painter.Texts, text => text.Text == "C");
    }

    [Fact]
    public void ClickingAnIconOpensTheAppAndMarksIt()
    {
        Click("Chatty");
        Frame();

        Assert.True(apps.IsOpen("dev.example.chatty"));
        Assert.Equal("Chatty", session.ActiveApp);
        Assert.True(Shelf.Items.Single(item => item.Title == "Chatty").IsOpen);
    }

    [Fact]
    public void ClickingAUserAppOpensItByItsPath()
    {
        Click("Clock");

        var clock = apps.Running.Single(app => app.Bundle.Name == "Clock");
        Assert.EndsWith("/user/apps/Clock.app", clock.Bundle.Path);
    }

    [Fact]
    public void ClickingTheDesktopsAppGivesItAWindow()
    {
        Assert.DoesNotContain(windows.Windows, window => window.App == "Files");

        Click("Files");

        Assert.Single(windows.Windows, window => window.App == "Files" && !window.IsPanel);
    }

    [Fact]
    public void MinimizedWindowsGoToTheDockAndComeBack()
    {
        Click("Files");
        var window = windows.Windows.Single(open => open.App == "Files" && !open.IsPanel);
        windows.Minimize(window);
        Frame();
        Frame();

        var tile = Shelf.Items[^1];
        Assert.Equal(("Files", 2, true), (tile.Title, tile.Group, tile.IsWindow));

        Click(Shelf.Items.Count - 1);
        Frame();

        Assert.False(window.IsMinimized);
        Assert.DoesNotContain(Shelf.Items, item => item.IsWindow);
    }

    [Fact]
    public void ZoomStopsAboveTheDock()
    {
        DesktopWindow big = new("Big", new UIView(UIScript.Parse(
            """<UIScript><Window width="300" height="200" minWidth="100" minHeight="100" resizable="true"/></UIScript>""")));
        windows.Open(big);

        windows.Zoom(big);

        Assert.Equal(DockWindow.Y, big.Y + big.Height);
    }

    [Fact]
    public void ComesBackWhenItsKilled()
    {
        var dock = apps.Supervisor.FindApp("dev.tonytins.ventureos.dock")!;
        apps.Supervisor.End(dock, EndingKind.Killed, "Killed.");
        Assert.DoesNotContain(windows.Windows, window => window.IsPanel);

        Frame();
        Frame();

        Assert.True(apps.IsOpen("dev.tonytins.ventureos.dock"));
        Assert.Equal(4, Shelf.Items.Count);
    }

    private void Click(string title) => Click(Shelf.Items.ToList().FindIndex(item => item.Title == title));

    private void Click(int index)
    {
        Assert.True(index >= 0);
        var icon = Shelf.IconBounds(index);
        var (x, y) = ((int)icon.X + 10, (int)icon.Y + 10);
        Frame(x, y, down: true);
        Frame(x, y);
    }

    private void Frame(int x = 500, int y = 300, bool down = false)
    {
        now += TimeSpan.FromMilliseconds(16);
        session.Update(new DesktopInput(x, y, down, Time: now));
        session.Draw(painter);
    }

    private void App(string folder, string name, string main, bool window = false, bool icon = false)
    {
        var contents = $"{root}/{folder}/{name}.app/Contents";
        Directory.CreateDirectory($"{contents}/Lua");
        Directory.CreateDirectory($"{contents}/Resources");
        var iconAttribute = icon ? """ icon="Contents/Resources/AppIcon.png" """ : "";
        File.WriteAllText($"{contents}/Info.xml",
            $"""<App version="1"><Info identifier="dev.example.{name.ToLowerInvariant()}" name="{name}"{iconAttribute}{(name == "Files" ? " kept=\"true\"" : "")}/></App>""");
        File.WriteAllText($"{contents}/Lua/main.lua", main + "\n");
        if (window)
            File.WriteAllText($"{contents}/Resources/main.uix", """<UIScript><Window width="200" height="100"/></UIScript>""");
        if (icon)
            File.WriteAllText($"{contents}/Resources/AppIcon.png", "a picture");
    }

    private static void Copy(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var file in Directory.GetFiles(from))
            File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
        foreach (var folder in Directory.GetDirectories(from))
            Copy(folder, Path.Combine(to, Path.GetFileName(folder)));
    }
}
