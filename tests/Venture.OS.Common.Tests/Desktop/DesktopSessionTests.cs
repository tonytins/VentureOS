using Venture.OS.Bundles;
using Venture.OS.Desktop;
using Venture.OS.Interface;
using Venture.OS.Scripting;
using Venture.OS.Shell;
using Venture.OS.Tests.Scripting;
using Venture.OS.Tests.Shell;

namespace Venture.OS.Tests.Desktop;

/// <summary>
/// The desktop with its bar, dock, and apps from a temporary apps folder: Chatty, which says hello when it opens,
/// and Viewer, which opens a window whose OK button prints.
/// </summary>
public sealed class DesktopSessionTests : IDisposable
{
    private const string MenusDocument = """
        <UIScript version="1" requires="menus">
          <Window width="200" height="100"/>
          <Menu name="file" title="File" hotkey="F"/>
          <MenuItem menu="file" name="newNote" title="New Note" shortcut="Ctrl+N"/>
          <MenuItem menu="file" name="closeIt" title="Close"/>
        </UIScript>
        """;

    private const string AboutDocumentText = """
        <UIScript>
          <Window width="100" height="50"/>
          <Text name="name" x="0" y="0" width="100" height="10" text="VentureOS"/>
          <Text name="version" x="0" y="10" width="100" height="10"/>
          <Text name="blurb" x="0" y="20" width="100" height="10"/>
          <Button name="ok" x="0" y="30" width="40" height="10" text="OK"/>
        </UIScript>
        """;

    private readonly string root = Path.Combine(Path.GetTempPath(), $"venture-desktop-{Guid.NewGuid():N}");
    private readonly RuntimeCommands runtime;
    private readonly AppManager apps;
    private readonly HeldOutput held = new();
    private readonly WindowManager windows = new(1024, 768);
    private readonly DesktopSession session;
    private readonly RecordingPainter painter = new(800, 600);

    public DesktopSessionTests()
    {
        var contents = Path.Combine(root, "apps", "Chatty.app", "Contents");
        Directory.CreateDirectory(Path.Combine(contents, "Lua"));
        File.WriteAllText(Path.Combine(contents, "Info.xml"), """
            <App version="1"><Info identifier="dev.example.chatty" name="Chatty"/></App>
            """);
        File.WriteAllText(Path.Combine(contents, "Lua", "main.lua"), "print(\"Hello from Chatty\")\n");

        var viewer = Path.Combine(root, "apps", "Viewer.app", "Contents");
        Directory.CreateDirectory(Path.Combine(viewer, "Lua"));
        Directory.CreateDirectory(Path.Combine(viewer, "Resources"));
        File.WriteAllText(Path.Combine(viewer, "Info.xml"), """
            <App version="1"><Info identifier="dev.example.viewer" name="Viewer" needs="ui"/></App>
            """);
        File.WriteAllText(Path.Combine(viewer, "Resources", "main.uix"), """
            <UIScript><Window width="200" height="100"/><Button name="ok" x="10" y="10" width="60" height="20" text="OK"/></UIScript>
            """);
        File.WriteAllText(Path.Combine(viewer, "Lua", "main.lua"), """
            local window = venture.ui.open("main.uix")
            window:on("ok", function() print("OK pressed") end)
            """);

        CommandRegistry registry = new();
        runtime = new RuntimeCommands(registry.IsBuiltIn);
        apps = new AppManager(Path.Combine(root, "apps"), runtime, new FakeSystemInfo(), windows: new AppWindows(windows, held));
        session = new DesktopSession(800, 600, apps, root, held, windows)
        {
            AboutDocument = () => UIScript.Parse(AboutDocumentText),
        };
        Frame(400, 300);
    }

    public void Dispose()
    {
        if (apps.IsOpen("dev.example.chatty"))
            apps.CloseApp("Chatty", new RecordingOutput());
        if (apps.IsOpen("dev.example.viewer"))
            apps.CloseApp("Viewer", new RecordingOutput());
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void TheDockShowsTheSystemsApps()
    {
        Assert.Equal([new DockItem("Chatty", "Chatty", IsOpen: false), new DockItem("Viewer", "Viewer", IsOpen: false)], session.Dock.Items);
        Assert.Equal(["VentureOS"], Titles());
    }

    [Fact]
    public void ClickingAnIconOpensTheAppAndShowsWhatItSaid()
    {
        ClickIcon(0);

        Assert.True(apps.IsOpen("dev.example.chatty"));
        Assert.True(session.Dock.Items[0].IsOpen);
        Assert.Equal("Chatty", session.ActiveApp);
        // What Chatty said is in a message in front, which File's Close Window closes; Help has About Chatty.
        Assert.Equal(["VentureOS", "Chatty", "File", "Help"], Titles());

        var alert = session.Windows.Focused!;
        Assert.Equal("Chatty", alert.Title);
        Assert.Equal("Hello from Chatty", alert.View.Text("message"));
        Assert.False(held.HasLines);
    }

    [Fact]
    public void QuitClosesTheActiveApp()
    {
        ClickIcon(0);

        Frame(400, 300, keys: [new KeyPress(DesktopKey.Character, 'q', Control: true)]);

        Assert.False(apps.IsOpen("dev.example.chatty"));
        Assert.False(session.Dock.Items[0].IsOpen);
        Assert.Null(session.ActiveApp);
        Assert.Equal(["VentureOS"], Titles());
    }

    [Fact]
    public void AboutOpensOnceFromTheMenu()
    {
        for (var time = 0; time < 2; time++)
        {
            session.MenuBar.OpenAt(0);
            Frame(400, 300, keys: [new KeyPress(DesktopKey.Enter)]);
        }

        Assert.Single(session.Windows.Windows);
        Assert.Equal("About VentureOS", session.Windows.Focused?.Title);
    }

    [Fact]
    public void TheFocusedWindowsMenusFollowTheSystemMenuAndReportChoices()
    {
        List<string> chosen = [];
        DesktopWindow window = new("Menus", new UIView(UIScript.Parse(MenusDocument)))
        {
            EventHandler = (self, uiEvent) =>
            {
                chosen.Add(uiEvent.Control);
                if (uiEvent.Control == "closeIt")
                    self.RequestClose();
            },
        };
        session.Windows.Open(window);
        Frame(400, 300);

        Assert.Equal(["VentureOS", "File"], Titles());

        Frame(400, 300, keys: [new KeyPress(DesktopKey.Character, 'n', Control: true)]);
        Assert.Equal(["newNote"], chosen);
        Assert.Contains(session.LastEvents, happened => happened.Event == new UIEvent(UIEventKind.Pressed, "newNote"));

        session.MenuBar.OpenByHotkey('f');
        Frame(400, 300, keys: [new KeyPress(DesktopKey.Down), new KeyPress(DesktopKey.Enter)]);
        Assert.Equal(["newNote", "closeIt"], chosen);
        Assert.Empty(session.Windows.Windows);
        Assert.Equal(["VentureOS"], Titles());
    }

    [Fact]
    public void AnOpenMenuTakesTheClickThatClosesIt()
    {
        DesktopWindow window = new("Under", new UIView(UIScript.Parse(MenusDocument)));
        session.Windows.Open(window);
        DesktopWindow other = new("Other", new UIView(UIScript.Parse(MenusDocument)));
        session.Windows.Open(other);
        Frame(400, 300);
        session.MenuBar.OpenAt(0);

        // A click on the window behind would normally bring it forward.
        Frame(window.X + 5, window.Y + 40, down: true);
        Frame(window.X + 5, window.Y + 40);

        Assert.False(session.MenuBar.IsOpen);
        Assert.Same(other, session.Windows.Focused);
    }

    [Fact]
    public void ClosingAnAppsLastWindowQuitsIt()
    {
        ClickIcon(1);
        var window = Assert.Single(session.Windows.Windows);
        Assert.Equal("Viewer", session.ActiveApp);
        Assert.True(session.Dock.Items[1].IsOpen);

        var close = window.CloseButton;
        Frame((int)close.X + 6, (int)close.Y + 6, down: true);
        Frame((int)close.X + 6, (int)close.Y + 6);

        Assert.False(apps.IsOpen("dev.example.viewer"));
        Assert.False(session.Dock.Items[1].IsOpen);
        Assert.Null(session.ActiveApp);
    }

    [Fact]
    public void WhatAHandlerPrintsShowsInAMessage()
    {
        ClickIcon(1);
        var window = Assert.Single(session.Windows.Windows);
        var ok = window.View.Bounds("ok")!.Value;
        var (x, y) = (window.ContentOrigin.X + (int)ok.X + 5, window.ContentOrigin.Y + (int)ok.Y + 5);

        Frame(x, y, down: true);
        Frame(x, y);

        var message = session.Windows.Focused!;
        Assert.NotSame(window, message);
        Assert.Equal("OK pressed", message.View.Text("message"));

        // Closing the message isn't closing Viewer's window, so Viewer stays open.
        message.RequestClose();
        Frame(x, y);
        Assert.True(apps.IsOpen("dev.example.viewer"));
    }

    [Fact]
    public void AppsWindowsOutlastTheDesktopAndItsOwnDont()
    {
        ClickIcon(1);
        session.ShowAbout();

        session.CloseDesktopWindows();

        var left = Assert.Single(windows.Windows);
        Assert.Equal("Viewer", left.App);

        DesktopSession next = new(800, 600, apps, root, held, windows)
        {
            AboutDocument = () => UIScript.Parse(AboutDocumentText),
        };
        Assert.Same(left, next.Windows.Focused);
        Assert.Equal("Viewer", next.ActiveApp);

        // About opening in front, before the first frame, still takes the bar from Viewer, leaving the VentureOS
        // menu and File, whose Close Window closes About.
        next.ShowAbout();
        next.Update(new DesktopInput(400, 300, false));
        Assert.Null(next.ActiveApp);
        Assert.Equal(["VentureOS", "File"], next.MenuBar.Menus.Select(menu => menu.Menu.Title));
    }

    [Fact]
    public void AnAlertShowsItsMessageAndOkClosesIt()
    {
        var alert = DesktopAlert.Create("Chatty", "First line\nSecond line", "Chatty");
        session.Windows.Open(alert);
        Frame(400, 300);

        Assert.Equal("First line\nSecond line", alert.View.Text("message"));
        Assert.Equal("Chatty", alert.App);

        var ok = alert.View.Bounds("ok")!.Value;
        var (x, y) = (alert.ContentOrigin.X + (int)ok.X + 5, alert.ContentOrigin.Y + (int)ok.Y + 5);
        Frame(x, y, down: true);
        Frame(x, y);

        Assert.Empty(session.Windows.Windows);
        Assert.Contains(session.LastEvents, happened => happened.Window == alert && happened.Closed);
    }

    [Fact]
    public void HeldOutputPassesLinesOnOnceTold()
    {
        HeldOutput output = new();
        output.WriteLine("one");
        Assert.Equal(["one"], output.Take());

        RecordingOutput shell = new();
        output.PassTo = shell;
        output.WriteLine("two");
        Assert.False(output.HasLines);
        Assert.Equal(["two"], shell.Lines);

        output.PassTo = null;
        for (var line = 0; line < HeldOutput.Limit + 5; line++)
            output.WriteLine($"line {line}");
        var kept = output.Take();
        Assert.Equal(HeldOutput.Limit, kept.Count);
        Assert.Equal("line 5", kept[0]);
    }

    private void ClickIcon(int index)
    {
        var icon = session.Dock.IconBounds(index);
        var (x, y) = ((int)icon.X + 10, (int)icon.Y + 10);
        Frame(x, y, down: true);
        Frame(x, y);
    }

    private void Frame(int x, int y, bool down = false, KeyPress[]? keys = null)
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
}
