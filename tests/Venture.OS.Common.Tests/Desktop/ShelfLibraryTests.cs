using Venture.OS.Bundles;
using Venture.OS.Desktop;
using Venture.OS.Interface;
using Venture.OS.Scripting;
using Venture.OS.Shell;
using Venture.OS.Tests.Scripting;
using Venture.OS.Tests.Shell;

namespace Venture.OS.Tests.Desktop;

/// <summary><c>window:setItems</c> for a shelf, from an app.</summary>
public sealed class ShelfLibraryTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"venture-shelf-{Guid.NewGuid():N}").Replace('\\', '/');
    private readonly RecordingOutput output = new();
    private readonly RecordingOutput events = new();
    private readonly WindowManager windows = new(800, 600);
    private readonly List<string> loaded = [];
    private readonly AppManager manager;

    public ShelfLibraryTests()
    {
        AppWindows host = new(windows, events)
        {
            LoadImage = path =>
            {
                loaded.Add(path);
                return File.Exists(path) ? new DesktopImage(2, 2, [unchecked((int)0xFF3060A0), unchecked((int)0xFF3060A0), unchecked((int)0xFF3060A0), unchecked((int)0xFF3060A0)]) : null;
            },
        };
        CommandRegistry registry = new();
        manager = new AppManager($"{root}/apps", new RuntimeCommands(registry.IsBuiltIn), new FakeSystemInfo(), windows: host);

        // Another app, whose icon a shelf may show.
        var notes = $"{root}/apps/Notes.app/Contents";
        Directory.CreateDirectory($"{notes}/Resources");
        File.WriteAllText($"{notes}/Resources/Notes.png", "a picture");
        File.WriteAllText($"{root}/private.png", "a picture");
    }

    public void Dispose()
    {
        if (manager.IsOpen("dev.example.shelf"))
            manager.CloseApp("Shelf", new RecordingOutput());
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void FillsAShelfWithTitlesDotsGroupsAndWindows()
    {
        Open("""
            local window = venture.ui.open("main.uix")
            window:setItems("apps", {
              { title = "Files", open = true },
              { title = "Notes", group = 1 },
              { title = "Report", group = 2, window = true },
            })
            """);

        var shelf = Shelf();
        Assert.Equal(
            [new ShelfItem("Files", null, true, 0), new ShelfItem("Notes", null, false, 1), new ShelfItem("Report", null, false, 2, true)],
            shelf.Items);
    }

    [Fact]
    public void ShowsAnotherAppsIconAndReadsItOnce()
    {
        Open($$"""
            local window = venture.ui.open("main.uix")
            local items = { { title = "Notes", icon = "{{root}}/apps/Notes.app/Contents/Resources/Notes.png" } }
            window:setItems("apps", items)
            window:setItems("apps", items)
            """);

        Assert.NotNull(Assert.Single(Shelf().Items).Icon);
        Assert.Single(loaded, path => path.EndsWith("Notes.png", StringComparison.Ordinal));
    }

    [Fact]
    public void AnIconThatCannotBeReadShowsTheInitial()
    {
        Open($$"""
            local window = venture.ui.open("main.uix")
            window:setItems("apps", { { title = "Notes", icon = "{{root}}/apps/Notes.app/Contents/Resources/Gone.png" } })
            """);

        Assert.Null(Assert.Single(Shelf().Items).Icon);
    }

    [Theory]
    [InlineData("""{ "Files" }""", "item 1 of the shelf \"apps\" needs to be a table")]
    [InlineData("""{ { icon = "x.png" } }""", "item 1 needs a title")]
    [InlineData("""{ { title = "Files", group = 1.5 } }""", "item 1's group must be a whole number")]
    [InlineData("""{ { title = "Files", icon = 3 } }""", "item 1's icon must be a path to a PNG")]
    [InlineData("""{ { title = "Private", icon = "ROOT/private.png" } }""", "needs the files scope")]
    public void RefusesAWrongItemAndChangesNothing(string items, string problem)
    {
        Open($$"""
            local window = venture.ui.open("main.uix")
            window:setItems("apps", { { title = "Kept" } })
            print(pcall(window.setItems, window, "apps", {{items.Replace("ROOT", root, StringComparison.Ordinal)}}))
            """);

        Assert.StartsWith("false", output.Lines[^1]);
        Assert.Contains(problem, output.Lines[^1], StringComparison.Ordinal);
        Assert.Equal("Kept", Assert.Single(Shelf().Items).Title);
    }

    [Fact]
    public void APressGivesTheItemsIndexFromOne()
    {
        Open("""
            local window = venture.ui.open("main.uix")
            window:setItems("apps", { { title = "Files" }, { title = "Notes" } })
            window:on("apps", function(event) print(event.kind, event.index, event.text) end)
            """);
        var window = Assert.Single(windows.Windows);

        window.EventHandler!(window, new UIEvent(UIEventKind.Pressed, "apps", "Notes", Index: 1));

        Assert.Equal("pressed\t2\tNotes", events.Lines[^1]);
    }

    private Shelf Shelf() => Assert.Single(windows.Windows).View.Shelf("apps")!;

    private void Open(string main)
    {
        var contents = $"{root}/apps/Shelf.app/Contents";
        Directory.CreateDirectory($"{contents}/Lua");
        Directory.CreateDirectory($"{contents}/Resources");
        File.WriteAllText($"{contents}/Info.xml", """<App version="1"><Info identifier="dev.example.shelf" name="Shelf"/></App>""");
        File.WriteAllText($"{contents}/Lua/main.lua", main + "\n");
        File.WriteAllText($"{contents}/Resources/main.uix", """
            <UIScript requires="shelf">
              <Window width="400" height="72" role="panel" edge="bottom"/>
              <Shelf name="apps" x="0" y="0" width="400" height="72"/>
            </UIScript>
            """);
        Assert.True(manager.OpenApp("Shelf", root, output), string.Join('\n', output.Lines));
    }
}
