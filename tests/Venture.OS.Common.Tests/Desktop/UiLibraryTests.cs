using Venture.OS.Bundles;
using Venture.OS.Desktop;
using Venture.OS.Interface;
using Venture.OS.Scripting;
using Venture.OS.Shell;
using Venture.OS.Tests.Scripting;
using Venture.OS.Tests.Shell;

namespace Venture.OS.Tests.Desktop;

/// <summary>
/// <c>venture.ui</c> through a small app, Pad, written fresh for each test into a temporary apps folder.
/// </summary>
public sealed class UiLibraryTests : IDisposable
{
    private const string Document = """
        <UIScript version="1" requires="menus">
          <Window width="200" height="120"/>
          <Menu name="file" title="File"/>
          <MenuItem menu="file" name="save" title="Save"/>
          <Text name="label" x="10" y="10" width="100" height="20" text="Hello"/>
          <TextField name="field" x="10" y="35" width="100" height="20"/>
          <List name="items" x="10" y="60" width="100" height="40" selectable="true"/>
          <ProgressBar name="volume" x="120" y="10" width="60" height="12" minValue="0" maxValue="10"/>
          <Button name="ok" x="120" y="90" width="60" height="20" text="OK"/>
          <ToolbarItem name="refresh" title="Refresh"/>
          <TitleField name="address"/>
        </UIScript>
        """;

    private readonly string root = Path.Combine(Path.GetTempPath(), $"venture-ui-{Guid.NewGuid():N}");
    private readonly RecordingOutput output = new();
    private readonly RecordingOutput events = new();
    private readonly WindowManager windows = new(800, 600);
    private readonly AppManager manager;
    private readonly AppWindows host;

    public UiLibraryTests()
    {
        Directory.CreateDirectory(Path.Combine(root, "apps"));
        CommandRegistry registry = new();
        host = new AppWindows(windows, events);
        manager = new AppManager(
            Path.Combine(root, "apps"),
            new RuntimeCommands(registry.IsBuiltIn),
            new FakeSystemInfo(),
            windows: host);
    }

    public void Dispose()
    {
        if (manager.IsOpen("dev.example.pad"))
            manager.CloseApp("Pad", new RecordingOutput());
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void OpensAWindowFromTheAppsResources()
    {
        Open("""window = venture.ui.open("main.uix", { title = "My Pad" })""");

        var window = Assert.Single(windows.Windows);
        Assert.Equal("My Pad", window.Title);
        Assert.True(window.ShowsTitle);
        Assert.Equal("Pad", window.App);
        Assert.True(window.OpenedByApp);
    }

    [Fact]
    public void WithoutATitleTheBarIsBlankAndTheWindowGoesByTheAppsName()
    {
        Open("""venture.ui.open("main.uix")""");

        var window = Assert.Single(windows.Windows);
        Assert.Equal("Pad", window.Title);
        Assert.False(window.ShowsTitle);
    }

    [Fact]
    public void AnEmptyTitleIsNoTitle()
    {
        Open("""venture.ui.open("main.uix", { title = "" })""");

        var window = Assert.Single(windows.Windows);
        Assert.Equal("Pad", window.Title);
        Assert.False(window.ShowsTitle);
    }

    [Fact]
    public void SetTitleShowsATitleAndNilBlanksTheBarAgain()
    {
        Open("""
            local window = venture.ui.open("main.uix")
            window:setTitle("todo.txt")
            window:on("ok", function() window:setTitle(nil) end)
            window:on("save", function() window:setTitle("") end)
            """);

        var window = Assert.Single(windows.Windows);
        Assert.Equal("todo.txt", window.Title);
        Assert.True(window.ShowsTitle);

        Send(window, new UIEvent(UIEventKind.Pressed, "ok"));
        Assert.Equal("Pad", window.Title);
        Assert.False(window.ShowsTitle);

        Send(window, new UIEvent(UIEventKind.Pressed, "save"));
        Assert.False(window.ShowsTitle);
    }

    [Fact]
    public void SetsAndReadsControls()
    {
        Open("""
            local window = venture.ui.open("main.uix")
            window:setText("label", "Changed")
            window:setValue("volume", 42)
            window:setItems("items", { "one", 2, "three" })
            window:select("items", 3)
            window:setDisabled("ok", true)
            window:setChecked("save", true)
            window:setTitle("Renamed")
            print(window:text("label"), window:value("volume"), #window:items("items"), window:selected("items"))
            window:select("items", nil)
            print(window:selected("items"), window:text("items"), window:value("label"))
            """);

        var window = Assert.Single(windows.Windows);
        Assert.Equal(["Changed\t10.0\t3\t3", "nil\tnil\tnil"], output.Lines);
        Assert.Equal(["one", "2", "three"], window.View.List("items")!.Items);
        Assert.True(window.View.IsDisabled("ok"));
        Assert.True(window.View.IsSelected("save"));
        Assert.Equal("Renamed", window.Title);
    }

    [Fact]
    public void HandlersGetTheEvent()
    {
        Open("""
            local window = venture.ui.open("main.uix")
            local function show(event)
              print(event.kind, event.control, event.text, event.value, event.index)
            end
            window:on("field", show)
            window:on("items", show)
            window:on("save", show)
            """);
        var window = Assert.Single(windows.Windows);

        Send(window, new UIEvent(UIEventKind.Submitted, "field", Text: "typed"));
        Send(window, new UIEvent(UIEventKind.Changed, "field", Text: "typed!"));
        Send(window, new UIEvent(UIEventKind.Selected, "items", Text: "two", Index: 1));
        Send(window, new UIEvent(UIEventKind.Pressed, "save"));
        Send(window, new UIEvent(UIEventKind.Pressed, "ok"));

        Assert.Equal(
            [
                "submitted\tfield\ttyped\tnil\tnil",
                "changed\tfield\ttyped!\tnil\tnil",
                "selected\titems\ttwo\tnil\t2",
                "pressed\tsave\tnil\tnil\tnil",
            ],
            events.Lines);
    }

    [Fact]
    public void ReachesTheTitleBarsPartsByName()
    {
        Open("""
            window = venture.ui.open("main.uix")
            window:setText("address", "/mnt")
            window:setDisabled("refresh", true)
            window:setChecked("refresh", true)
            window:on("refresh", function(event) print(event.kind, event.control) end)
            window:on("address", function(event) print(event.kind, event.text) end)
            window:focus("address")
            print(window:text("address"))
            """);
        var window = Assert.Single(windows.Windows);

        Send(window, new UIEvent(UIEventKind.Pressed, "refresh"));
        Send(window, new UIEvent(UIEventKind.Submitted, "address", Text: "/mnt/user"));

        Assert.Equal(["/mnt"], output.Lines);
        Assert.True(window.IsEditingTitle);
        Assert.True(window.View.IsDisabled("refresh"));
        Assert.True(window.View.IsSelected("refresh"));
        Assert.Equal(["pressed\trefresh", "submitted\t/mnt/user"], events.Lines);
    }

    [Fact]
    public void ATimerRunsEveryIntervalWithoutMakingUpLostTime()
    {
        Open("""
            local window = venture.ui.open("main.uix")
            window:every(1, function() print("tick") end)
            """);

        foreach (var seconds in (double[])[0, 0.5, 1, 1.5, 2, 9])
            host.Tick(TimeSpan.FromSeconds(seconds));

        // Started at 0: due at 1, then 2, then 3; at 9 it runs once and waits until 10.
        Assert.Equal(["tick", "tick", "tick"], events.Lines);
        Assert.False(host.Tick(TimeSpan.FromSeconds(9.5)));
        Assert.True(host.Tick(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public void ATimerStopsWhenToldWhenItsWindowClosesOrWhenTheAppQuits()
    {
        Open("""
            local window = venture.ui.open("main.uix")
            local timer = window:every(1, function() print("one") end)
            window:on("ok", function() timer:stop() end)
            local other = venture.ui.open("main.uix")
            other:every(1, function() print("two") end)
            other:on("ok", function() other:close() end)
            venture.ui.open("main.uix"):every(1, function() print("three") end)
            """);
        Assert.Equal(3, host.TimerCount);

        Send(windows.Windows[0], new UIEvent(UIEventKind.Pressed, "ok"));
        Assert.Equal(2, host.TimerCount);

        Send(windows.Windows[1], new UIEvent(UIEventKind.Pressed, "ok"));
        windows.CloseRequested();
        host.Tick(TimeSpan.Zero);
        Assert.Equal(1, host.TimerCount);

        manager.CloseApp("Pad", output);
        Assert.Equal(0, host.TimerCount);
        Assert.Empty(events.Lines);
    }

    [Theory]
    [InlineData("""venture.ui.open("main.uix"):every(0.05, print)""", "from 0.1 seconds to a day, not 0.05")]
    [InlineData("""venture.ui.open("main.uix"):every("often", print)""", "needs a number of seconds")]
    [InlineData("""venture.ui.open("main.uix"):every(1)""", "needs a function to run")]
    public void ExplainsATimerMistake(string code, string expected)
    {
        Write(code);

        Assert.False(manager.OpenApp("Pad", root, output));
        Assert.Contains(output.Lines, line => line.Contains(expected));
        Assert.Equal(0, host.TimerCount);
    }

    [Fact]
    public void AHandlerErrorIsReportedAndTheWindowCarriesOn()
    {
        Open("""
            local window = venture.ui.open("main.uix")
            window:on("ok", function() error("broken") end)
            """);
        var window = Assert.Single(windows.Windows);

        Send(window, new UIEvent(UIEventKind.Pressed, "ok"));

        Assert.Contains(events.Lines, line => line.Contains("broken"));
        Assert.Single(windows.Windows);
    }

    [Fact]
    public void CloseAsksTheWindowManager()
    {
        Open("""
            local window = venture.ui.open("main.uix")
            window:on("ok", function() window:close() end)
            """);
        var window = Assert.Single(windows.Windows);

        Send(window, new UIEvent(UIEventKind.Pressed, "ok"));
        var closed = windows.CloseRequested();

        Assert.Equal([new WindowEvent(window, null, Closed: true)], closed);
    }

    [Fact]
    public void QuittingTheAppClosesItsWindowsAndStopsItsHandlers()
    {
        Open("""
            local window = venture.ui.open("main.uix")
            venture.ui.open("main.uix")
            window:on("ok", function() print("still here") end)
            """);
        var window = windows.Windows[0];

        manager.CloseApp("Pad", output);
        Send(window, new UIEvent(UIEventKind.Pressed, "ok"));

        Assert.Empty(windows.Windows);
        Assert.Empty(events.Lines);
    }

    [Theory]
    [InlineData("""venture.ui.open("main.uix"):setText("nothing", "x")""", "main.uix has no control or menu item called \"nothing\"")]
    [InlineData("""venture.ui.open("main.uix").setText("label", "x")""", "use window:setText(...), with a colon")]
    [InlineData("""venture.ui.open("main.uix"):items("label")""", "\"label\" in main.uix isn't one")]
    [InlineData("""venture.ui.open("main.uix"):on("ok", 5)""", "window:on needs a function")]
    [InlineData("""venture.ui.open("main.uix"):select("items", 1)""", "there's no item 1 in a list of 0")]
    [InlineData("""venture.ui.open("../Info.xml")""", "needs a path inside Contents/Resources")]
    [InlineData("""venture.ui.open("missing.uix")""", "venture.ui.open: missing.uix:")]
    [InlineData("""venture.ui.open("main.uix", "Pad")""", "options must be a table")]
    [InlineData("""venture.ui.launch("Pad", 5)""", "file must be a path")]
    public void ExplainsMistakes(string code, string expected)
    {
        Write(code);

        Assert.False(manager.OpenApp("Pad", root, output));
        Assert.Contains(output.Lines, line => line.Contains(expected));
        Assert.Empty(windows.Windows);
    }

    [Fact]
    public void LaunchExplainsAnUnknownAppAndOneThatCantOpenFiles()
    {
        Open("""
            print(venture.ui.launch("Nowhere"))
            print(venture.ui.launch("Pad", "notes.txt"))
            """);

        // Pad is still opening, so launching it again from its own main is refused rather than started twice.
        Assert.Equal(["nil\tthere's no app called Nowhere", "nil\tPad didn't open"], output.Lines);
        Assert.Equal(["Pad is still opening."], events.Lines);
    }

    [Fact]
    public void LaunchingAnAppWithAFileItCantOpenSaysSo()
    {
        Open("""
            local window = venture.ui.open("main.uix")
            window:on("ok", function() print(venture.ui.launch("Pad", "notes.txt")) end)
            """);

        Send(Assert.Single(windows.Windows), new UIEvent(UIEventKind.Pressed, "ok"));

        Assert.Equal(["nil\tPad can't open files"], events.Lines);
    }

    [Fact]
    public void OpenShellIsOnlyThereWhenTheSystemOffersIt()
    {
        Open("""print(venture.openShell)""");

        Assert.Equal(["nil"], output.Lines);
    }

    [Fact]
    public void AnAppOpensWindowsWithoutAskingForUi()
    {
        Write("""venture.ui.open("main.uix")""", needs: "");

        Assert.True(manager.OpenApp("Pad", root, output), string.Join('\n', output.Lines));
        Assert.Single(windows.Windows);
    }

    [Fact]
    public void AnAppsAlertShowsAWindowOrPrintsWithoutTheDesktop()
    {
        Open("""
            venture.ui.alert("Saved.")
            venture.ui.alert("Disk full.", "Problem")
            """);

        // The desktop isn't on the screen in this test, so both go to the app's messages.
        Assert.Contains("Pad: Saved.", events.Lines);
        Assert.Contains("Problem: Disk full.", events.Lines);

        windows.IsShowing = true;
        Assert.True(manager.CloseApp("Pad", new RecordingOutput()));
        Open("""venture.ui.alert("Saved.")""");
        Assert.Contains(windows.Windows, window => window.Title == "Pad" && window.ShowsTitle && window.App == "Pad" && window.View.Script.Control("ok") is not null);
    }

    [Fact]
    public void ExplainsThatThereIsNoDesktop()
    {
        Write("""venture.ui.open("main.uix")""");
        CommandRegistry registry = new();
        AppManager withoutDesktop = new(Path.Combine(root, "apps"), new RuntimeCommands(registry.IsBuiltIn));

        Assert.False(withoutDesktop.OpenApp("Pad", root, output));
        Assert.Contains(output.Lines, line => line.Contains("needs the desktop"));
    }

    [Fact]
    public void OnlyAppsHaveWindows()
    {
        LuaRunner.Run(output, root, lua => lua.DoString("venture.ui.open(\"main.uix\")"), new FakeSystemInfo());

        Assert.Contains(output.Lines, line => line.Contains(VentureLibrary.NotAppMessage));
    }

    [Fact]
    public void SetsAndClearsTheSubtitle()
    {
        Open("""
            local window = venture.ui.open("main.uix")
            window:setSubtitle("3 notes")
            window:on("ok", function() window:setSubtitle(nil) end)
            """);
        var window = Assert.Single(windows.Windows);

        Assert.Equal("3 notes", window.View.Subtitle);
        Assert.Equal(DesktopTheme.TallTitleBarHeight, window.BarHeight);

        Send(window, new UIEvent(UIEventKind.Pressed, "ok"));
        Assert.Null(window.View.Subtitle);
        Assert.Equal(DesktopTheme.TitleBarHeight, window.BarHeight);
    }

    [Fact]
    public void LoadsTheImagesADocumentNamesFromBesideIt()
    {
        List<string> asked = [];
        AppWindows withImages = new(windows, events)
        {
            LoadImage = path =>
            {
                asked.Add(path);
                return path.EndsWith("icons/add.png") ? new DesktopImage(1, 1, [unchecked((int)0xFF000000)]) : null;
            },
        };
        CommandRegistry registry = new();
        AppManager apps = new(Path.Combine(root, "apps"), new RuntimeCommands(registry.IsBuiltIn), new FakeSystemInfo(), windows: withImages);
        Write("""venture.ui.open("main.uix")""");
        var resources = Path.Combine(root, "apps", "Pad.app", "Contents", "Resources");
        File.WriteAllText(Path.Combine(resources, "main.uix"), """
            <UIScript version="1">
              <Window width="200" height="120"/>
              <Image name="add" path="icons/add.png"/>
              <Image name="gone" path="icons/gone.png"/>
              <Image name="outside" path="../Info.xml"/>
              <Image name="tso" assetID="0x1234"/>
              <ToolbarItem name="addItem" title="Add" image="add"/>
            </UIScript>
            """);

        Assert.True(apps.OpenApp("Pad", root, output), string.Join('\n', output.Lines));

        var view = Assert.Single(windows.Windows).View;
        Assert.Equal(["gone", "outside", "tso"], view.MissingImages);
        Assert.Equal([$"{resources}/icons/add.png", $"{resources}/icons/gone.png"], asked);
        Assert.Contains(events.Lines, line => line.Contains("\"gone\" couldn't be read from icons/gone.png"));
        Assert.Contains(events.Lines, line => line.Contains("\"outside\" has path=\"../Info.xml\", which isn't a path beside the document"));
        apps.CloseApp("Pad", new RecordingOutput());
    }

    [Fact]
    public void SetImageShowsPicturesFromWhereAnAppMayLook()
    {
        var system = Path.Combine(root, "system");
        Directory.CreateDirectory(system);
        File.WriteAllText(Path.Combine(system, "sky.png"), "a picture");
        File.WriteAllText(Path.Combine(root, "private.png"), "a picture");
        AppWindows withImages = new(windows, events)
        {
            SystemFolder = system,
            LoadImage = path => File.Exists(path) ? new DesktopImage(1, 1, [unchecked((int)0xFF000000)]) : null,
        };
        CommandRegistry registry = new();
        AppManager apps = new(Path.Combine(root, "apps"), new RuntimeCommands(registry.IsBuiltIn), new FakeSystemInfo(), windows: withImages);
        Write($$"""
            local window = venture.ui.open("main.uix")
            print(window:setImage("thumb", "{{system}}/sky.png"))
            print(window:setImage("thumb", "{{root}}/private.png"))
            print(window:setImage("thumb", "nowhere.png"))
            print(window:setImage("thumb", "../Info.xml"))
            print(pcall(window.setImage, window, "nope", "sky.png"))
            """);
        var resources = Path.Combine(root, "apps", "Pad.app", "Contents", "Resources");
        File.WriteAllText(Path.Combine(resources, "main.uix"), """
            <UIScript version="1">
              <Window width="200" height="120"/>
              <Image name="thumb" path="thumb.png"/>
              <Button name="pick" x="10" y="10" width="40" height="30" image="thumb"/>
            </UIScript>
            """);

        Assert.True(apps.OpenApp("Pad", root, output), string.Join('\n', output.Lines));

        Assert.Equal("true", output.Lines[0]);
        Assert.Contains("needs the files scope", output.Lines[1]);
        Assert.Contains("couldn't be read as a picture", output.Lines[2]);
        Assert.Contains("isn't a path beside the document", output.Lines[3]);
        Assert.StartsWith("false", output.Lines[4]);
        Assert.Contains("isn't an <Image>", output.Lines[4]);
        Assert.Empty(Assert.Single(windows.Windows).View.MissingImages);
        apps.CloseApp("Pad", new RecordingOutput());
    }

    [Fact]
    public void AGridsItemsAreSetChosenAndReportedFromLua()
    {
        var system = Path.Combine(root, "system");
        Directory.CreateDirectory(system);
        File.WriteAllText(Path.Combine(system, "pic.png"), "a picture");
        DesktopImage picture = new(1, 1, [unchecked((int)0xFF000000)]);
        AppWindows withImages = new(windows, events)
        {
            SystemFolder = system,
            LoadImage = path => File.Exists(path) ? picture : null,
        };
        CommandRegistry registry = new();
        AppManager apps = new(Path.Combine(root, "apps"), new RuntimeCommands(registry.IsBuiltIn), new FakeSystemInfo(), windows: withImages);
        Write($$"""
            local window = venture.ui.open("main.uix")
            window:on("g", function(event) print(event.kind, event.index, event.item, event.text) end)
            print(window:setItems("g", { "Plain", { title = "Red", name = "red", color = "#FF0000" },
              { title = "Sky", image = "sky", tooltip = "Blue" }, { title = "File", image = "{{system}}/pic.png" } }))
            local items = window:items("g")
            print(#items, items[1], items[4])
            window:select("g", 2)
            print(window:selected("g"))
            print(pcall(window.setItems, window, "g", { { title = "x", image = "sky", color = "#000000" } }))
            print(pcall(window.setItems, window, "g", { { color = "blue" } }))
            print(pcall(window.setItems, window, "g", { { tooltip = "t" } }))
            print(pcall(window.setItems, window, "g", { { title = "x", image = "nowhere.png" } }))
            print(pcall(window.select, window, "g", 9))
            print(#window:items("g"), window:selected("g"))
            """);
        var resources = Path.Combine(root, "apps", "Pad.app", "Contents", "Resources");
        File.WriteAllText(Path.Combine(resources, "main.uix"), """
            <UIScript version="1">
              <Window width="200" height="120"/>
              <Image name="sky" path="sky.png"/>
              <Grid name="g" x="10" y="10" width="180" height="100" itemWidth="30" itemHeight="30">
                <Item name="first" title="First"/>
              </Grid>
            </UIScript>
            """);

        Assert.True(apps.OpenApp("Pad", root, output), string.Join('\n', output.Lines));
        var window = Assert.Single(windows.Windows);
        var grid = window.View.Grid("g")!;
        Send(window, new UIEvent(UIEventKind.Selected, "g", Index: 1, Item: "red"));

        Assert.Equal("", output.Lines[0]);
        Assert.Equal("4\tPlain\tFile", output.Lines[1]);
        Assert.Equal("2", output.Lines[2]);
        Assert.Contains("an item shows one", output.Lines[3]);
        Assert.Contains("written #RRGGBB, not \"blue\"", output.Lines[4]);
        Assert.Contains("needs a title, an image, or a color", output.Lines[5]);
        Assert.Contains("couldn't be read as a picture", output.Lines[6]);
        Assert.Contains("there's no item 9 in a grid of 4", output.Lines[7]);
        Assert.Equal("4\t2", output.Lines[8]);
        Assert.Contains("selected\t2\tred\tnil", events.Lines);
        Assert.Equal(new UIColor(255, 0, 0), grid.Items[1].Color);
        Assert.Equal(("sky", null as DesktopImage, "Blue"), (grid.Items[2].Image, grid.Items[2].Picture, grid.Items[2].Tooltip));
        Assert.Same(picture, grid.Items[3].Picture);
        apps.CloseApp("Pad", new RecordingOutput());
    }

    [Fact]
    public void AnAppsWindowFindsTheSharedTables()
    {
        var system = Path.Combine(root, "system");
        Directory.CreateDirectory(Path.Combine(system, "strings"));
        File.WriteAllText(Path.Combine(system, "strings", "_100_common.cst"), "1 ^OK^\n");
        AppWindows withStrings = new(windows, events) { SystemFolder = system };
        CommandRegistry registry = new();
        AppManager apps = new(Path.Combine(root, "apps"), new RuntimeCommands(registry.IsBuiltIn), new FakeSystemInfo(), windows: withStrings);
        Write("""venture.ui.open("main.uix")""");
        var resources = Path.Combine(root, "apps", "Pad.app", "Contents", "Resources");
        File.WriteAllText(Path.Combine(resources, "_1_pad.cst"), "1 ^Mine^\n");
        File.WriteAllText(Path.Combine(resources, "main.uix"), """
            <UIScript version="1">
              <Window width="200" height="120"/>
              <String name="ok" table="100" index="1"/>
              <String name="mine" table="1" index="1"/>
              <Button name="done" x="10" y="10" width="60" height="30" string="ok"/>
              <Text name="label" x="10" y="50" width="100" height="20" string="mine"/>
            </UIScript>
            """);

        Assert.True(apps.OpenApp("Pad", root, output), string.Join('\n', output.Lines));

        var view = Assert.Single(windows.Windows).View;
        Assert.Equal("OK", view.Text("done"));
        Assert.Equal("Mine", view.Text("label"));
        Assert.Empty(view.MissingStrings);
        apps.CloseApp("Pad", new RecordingOutput());
    }

    private static void Send(DesktopWindow window, UIEvent uiEvent) => window.EventHandler!(window, uiEvent);

    private void Open(string main)
    {
        Write(main);
        Assert.True(manager.OpenApp("Pad", root, output), string.Join('\n', output.Lines));
    }

    private void Write(string main, string needs = "ui")
    {
        var contents = Path.Combine(root, "apps", "Pad.app", "Contents");
        Directory.CreateDirectory(Path.Combine(contents, "Lua"));
        Directory.CreateDirectory(Path.Combine(contents, "Resources"));
        File.WriteAllText(Path.Combine(contents, "Info.xml"),
            $"""<App version="1"><Info identifier="dev.example.pad" name="Pad" needs="{needs}"/></App>""");
        File.WriteAllText(Path.Combine(contents, "Lua", "main.lua"), main + "\n");
        File.WriteAllText(Path.Combine(contents, "Resources", "main.uix"), Document);
    }
}
