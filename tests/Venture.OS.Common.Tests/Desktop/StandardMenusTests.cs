using Venture.OS.Bundles;
using Venture.OS.Desktop;
using Venture.OS.Interface;
using Venture.OS.Scripting;
using Venture.OS.Shell;
using Venture.OS.Tests.Scripting;
using Venture.OS.Tests.Shell;

namespace Venture.OS.Tests.Desktop;

/// <summary>
/// The desktop's standard menus, which every app gets without asking: File, with Close Window, and Help, with About
/// the app, with an app's own menus of the same role merged in. Writer has a File menu, a Tools menu, and a help
/// menu named otherwise; Keeper has its own Ctrl+W; Plain has no menus and no description.
/// </summary>
public sealed class StandardMenusTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"venture-menus-{Guid.NewGuid():N}");
    private readonly HeldOutput held = new();
    private readonly WindowManager windows = new(1024, 768);
    private readonly AppManager apps;
    private readonly DesktopSession session;

    public StandardMenusTests()
    {
        App("Writer", """version="1.0" description="Writes things down." """, """
            <Menu name="fileMenu" title="File" hotkey="F"/>
            <MenuItem menu="fileMenu" name="saveItem" title="Save" shortcut="Ctrl+S"/>
            <Menu name="tools" title="Tools" hotkey="T"/>
            <MenuItem menu="tools" name="count" title="Count"/>
            <Menu name="assist" title="Assist" role="help"/>
            <MenuItem menu="assist" name="tips" title="Tips"/>
            """);
        App("Keeper", "", """
            <Menu name="keep" title="File"/>
            <MenuItem menu="keep" name="closeAll" title="Close All" shortcut="Ctrl+W"/>
            """);
        App("Plain", "", "");

        CommandRegistry registry = new();
        apps = new AppManager(
            Path.Combine(root, "apps"), new RuntimeCommands(registry.IsBuiltIn), new FakeSystemInfo(),
            windows: new AppWindows(windows, held));
        var about = Path.Combine(AppContext.BaseDirectory, "system", "interfaces", "about.uix");
        session = new DesktopSession(1024, 768, apps, root, held, windows)
        {
            AboutDocument = () => UIScript.Load(about),
            SystemVersion = "Version 9.9.9 (Build 1)",
        };
        Frame();
    }

    public void Dispose()
    {
        foreach (var app in new[] { "Writer", "Keeper", "Plain" })
        {
            if (apps.IsOpen($"dev.example.{app.ToLowerInvariant()}"))
                apps.CloseApp(app, new RecordingOutput());
        }

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void WithNothingOpenOnlyTheSystemMenuShows()
    {
        Assert.Equal(["VentureOS"], Titles());
    }

    [Fact]
    public void AnAppsMenusMergeIntoTheStandardOnes()
    {
        Open("Writer");

        Assert.Equal(["VentureOS", "Writer", "File", "Tools", "Help"], Titles());
        Assert.Equal(["Save", null, "Close Window"], Items("File"));
        Assert.Equal(["Tips", null, "About Writer"], Items("Help"));
    }

    [Fact]
    public void AnAppWithoutMenusStillGetsFileAndHelp()
    {
        Open("Plain");

        Assert.Equal(["VentureOS", "Plain", "File", "Help"], Titles());
        Assert.Equal(["Close Window"], Items("File"));
        Assert.Equal(["About Plain"], Items("Help"));
    }

    [Fact]
    public void AnAppsItemInAStandardMenuReportsToItsWindow()
    {
        Open("Writer");

        Frame(keys: [new KeyPress(DesktopKey.Character, 's', Control: true)]);

        Assert.Contains(session.LastEvents, happened => happened.Event == new UIEvent(UIEventKind.Pressed, "saveItem"));
    }

    [Fact]
    public void CloseWindowClosesTheFrontWindow()
    {
        Open("Writer");

        Frame(keys: [new KeyPress(DesktopKey.Character, 'w', Control: true)]);

        Assert.Empty(windows.Windows);
        Assert.False(apps.IsOpen("dev.example.writer"));
    }

    [Fact]
    public void AnAppsOwnShortcutTakesTheStandardItemsPlace()
    {
        Open("Keeper");

        Assert.Equal(["Close All"], Items("File"));
        Frame(keys: [new KeyPress(DesktopKey.Character, 'w', Control: true)]);

        Assert.Contains(session.LastEvents, happened => happened.Event == new UIEvent(UIEventKind.Pressed, "closeAll"));
        Assert.Single(windows.Windows);
    }

    [Fact]
    public void AboutShowsTheAppsNameVersionAndDescriptionOnce()
    {
        Open("Writer");

        ChooseAbout();
        ChooseAbout();

        var about = Assert.Single(windows.Windows, window => window.Title == "About Writer");
        Assert.Same(about, windows.Focused);
        Assert.Equal("Writer", about.View.Text("name"));
        Assert.Equal("Version 1.0", about.View.Text("version"));
        Assert.Equal("Writes things down.", about.View.Text("blurb"));
        Assert.False(about.View.IsHidden("blurb"));

        // It belongs to the app, so the app's menus stay, and it goes when the app quits.
        Assert.Equal("Writer", session.ActiveApp);
        session.MenuBar.OpenAt(1);
        Frame(keys: [KeyPress.Typed('q')]);
        Assert.Empty(windows.Windows);
    }

    [Fact]
    public void AboutLeavesOutWhatTheAppDoesntSay()
    {
        Open("Plain");

        ChooseAbout();

        var about = windows.Focused!;
        Assert.Equal("About Plain", about.Title);
        Assert.Equal("", about.View.Text("version"));
        Assert.True(about.View.IsHidden("blurb"));
    }

    [Fact]
    public void AboutVentureOSShowsTheSystemsVersion()
    {
        session.MenuBar.OpenAt(0);
        Frame(keys: [new KeyPress(DesktopKey.Enter)]);

        var about = windows.Focused!;
        Assert.Equal("About VentureOS", about.Title);
        Assert.Equal("Version 9.9.9 (Build 1)", about.View.Text("version"));
        Assert.False(about.View.IsHidden("blurb"));
        Assert.Equal(["VentureOS", "File"], Titles());
    }

    private void App(string name, string info, string menus)
    {
        var contents = Path.Combine(root, "apps", $"{name}.app", "Contents");
        Directory.CreateDirectory(Path.Combine(contents, "Lua"));
        Directory.CreateDirectory(Path.Combine(contents, "Resources"));
        File.WriteAllText(Path.Combine(contents, "Info.xml"), $"""
            <App version="1"><Info identifier="dev.example.{name.ToLowerInvariant()}" name="{name}" needs="ui" {info}/></App>
            """);
        File.WriteAllText(Path.Combine(contents, "Resources", "main.uix"), $"""
            <UIScript version="1" requires="menus">
              <Window width="200" height="100"/>
              {menus}
            </UIScript>
            """);
        File.WriteAllText(Path.Combine(contents, "Lua", "main.lua"), "venture.ui.open(\"main.uix\")\n");
    }

    private void Open(string app)
    {
        RecordingOutput output = new();
        Assert.True(apps.OpenApp(app, root, output), string.Join('\n', output.Lines));
        Frame();
    }

    private void ChooseAbout()
    {
        var help = Titles().IndexOf("Help");
        session.MenuBar.OpenAt(help);
        Frame(keys: [KeyPress.Typed('a')]);
    }

    private void Frame(KeyPress[]? keys = null) => session.Update(new DesktopInput(5, 400, false, Keys: keys));

    private List<string> Titles() => [.. session.MenuBar.Menus.Select(menu => menu.Menu.Title)];

    private List<string?> Items(string menu) =>
        [.. session.MenuBar.Menus.Single(bar => bar.Menu.Title == menu).Menu.Entries.Select(entry => entry.Title)];
}
