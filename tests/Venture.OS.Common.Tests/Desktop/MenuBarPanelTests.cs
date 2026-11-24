using Venture.OS.Bundles;
using Venture.OS.Desktop;
using Venture.OS.Interface;
using Venture.OS.Processes;
using Venture.OS.Scripting;
using Venture.OS.Shell;
using Venture.OS.Tests.Scripting;
using Venture.OS.Tests.Shell;

namespace Venture.OS.Tests.Desktop;

/// <summary>
/// The menu bar as Files' top panel: a desktop whose bar app is Files shows the bar only while Files' panel is open,
/// where the panel is, and the panel keeps windows below it.
/// </summary>
public sealed class MenuBarPanelTests : IDisposable
{
    private static readonly string ShippedBar = Path.Combine(AppContext.BaseDirectory, "system", "apps", "Files.app", "Contents", "Resources", "menubar.uix");

    private readonly string root = Path.Combine(Path.GetTempPath(), $"venture-bar-{Guid.NewGuid():N}").Replace('\\', '/');
    private readonly HeldOutput held = new();
    private readonly WindowManager windows = new(1024, 768);
    private readonly AppManager apps;
    private readonly DesktopSession session;
    private readonly RecordingPainter painter = new(1024, 768);
    private TimeSpan now;

    public MenuBarPanelTests()
    {
        App("Files", """
            venture.ui.open("menubar.uix")
            venture.ui.onNewWindow(function() venture.ui.open("main.uix") end)
            """);
        App("Other", """print(pcall(venture.ui.open, "menubar.uix"))""");

        CommandRegistry registry = new();
        apps = new AppManager(
            $"{root}/apps", new RuntimeCommands(registry.IsBuiltIn), new FakeSystemInfo(), windows: new AppWindows(windows, held) { Text = TestDesktopText.Create() },
            supervisor: new Supervisor(() => now));
        session = new DesktopSession(1024, 768, apps, root, held, windows)
        {
            FallbackApp = "Files",
            MenuBarApp = "Files",
            Clock = () => now,
        };
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

    [Fact]
    public void TheBarShowsOnlyOnceFilesPanelIsOpen()
    {
        Assert.False(session.BarShows);

        Frame();

        Assert.True(session.BarShows);
        Assert.Equal(new UIRect(0, 0, 1024, 26), windows.TopPanel!.Bounds);
        Assert.Equal(0, windows.TopInset);
        Assert.Equal(26, (int)windows.WorkArea.Y);
        Assert.Contains(painter.Texts, text => text.Text == "Files");
    }

    [Fact]
    public void TheBarTakesAPressAsItAlwaysHas()
    {
        Frame();
        var title = session.MenuBar.TitleBounds(1)!.Value;

        Frame((int)title.X + 4, (int)title.Y + 4, down: true);
        Frame((int)title.X + 4, (int)title.Y + 4);

        Assert.True(session.MenuBar.IsOpen);
    }

    [Fact]
    public void ABarAppThatEndsTakesTheBarAndAnOpenMenuWithIt()
    {
        Frame();
        var title = session.MenuBar.TitleBounds(1)!.Value;
        Frame((int)title.X + 4, (int)title.Y + 4, down: true);
        Frame((int)title.X + 4, (int)title.Y + 4);
        var files = apps.Supervisor.FindApp("dev.example.files")!;

        apps.Supervisor.StopKeeping("dev.example.files");
        apps.Supervisor.End(files, EndingKind.Killed, "Killed.");
        Frame();

        Assert.False(session.BarShows);
        Assert.False(session.MenuBar.IsOpen);
        Assert.Equal(0, (int)windows.WorkArea.Y);
        RecordingPainter after = new(1024, 768);
        session.Draw(after);
        Assert.DoesNotContain(after.Texts, text => text.Text == "Files");
    }

    [Fact]
    public void TheBarComesBackWithFiles()
    {
        Frame();
        var files = apps.Supervisor.FindApp("dev.example.files")!;

        apps.Supervisor.End(files, EndingKind.Killed, "Killed.");
        Frame();

        Assert.True(session.BarShows);
        Assert.NotSame(files, apps.Supervisor.FindApp("dev.example.files"));
    }

    [Fact]
    public void ASecondTopPanelIsRefused()
    {
        Frame();
        var output = new RecordingOutput();

        Assert.True(apps.OpenApp("Other", root, output));

        Assert.StartsWith("false", output.Lines.Single());
        Assert.Contains("the top edge holds the menu bar, which Files shows", output.Lines.Single(), StringComparison.Ordinal);
        Assert.Single(windows.Windows, window => window.Edge == UIEdge.Top);
    }

    [Fact]
    public void NewWindowsOpenBelowTheBar()
    {
        Frame();

        Assert.True(apps.Windows!.NewWindow("Files"));

        var browser = windows.Windows.Single(window => !window.IsPanel);
        Assert.True(browser.Y >= 26);
        Assert.Same(browser, windows.Focused);
    }

    private void Frame(int x = 500, int y = 300, bool down = false)
    {
        now += TimeSpan.FromMilliseconds(16);
        session.Update(new DesktopInput(x, y, down, Time: now));
        session.Draw(painter);
    }

    private void App(string name, string main)
    {
        var contents = $"{root}/apps/{name}.app/Contents";
        Directory.CreateDirectory($"{contents}/Lua");
        Directory.CreateDirectory($"{contents}/Resources");
        File.WriteAllText($"{contents}/Info.xml",
            $"""<App version="1"><Info identifier="dev.example.{name.ToLowerInvariant()}" name="{name}"/></App>""");
        File.WriteAllText($"{contents}/Lua/main.lua", main + "\n");
        File.Copy(ShippedBar, $"{contents}/Resources/menubar.uix");
        File.WriteAllText($"{contents}/Resources/main.uix", """<UIScript><Window width="300" height="200"/></UIScript>""");
    }
}
