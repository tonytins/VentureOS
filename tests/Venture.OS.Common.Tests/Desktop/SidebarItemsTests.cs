using Venture.OS.Bundles;
using Venture.OS.Desktop;
using Venture.OS.Scripting;
using Venture.OS.Shell;
using Venture.OS.Tests.Scripting;
using Venture.OS.Tests.Shell;

namespace Venture.OS.Tests.Desktop;

/// <summary>
/// A sidebar's rows from <c>window:setItems</c>: text, an optional icon, and separators between groups, which aren't
/// rows. A plain list takes strings only.
/// </summary>
public sealed class SidebarItemsTests : IDisposable
{
    private const string Document = """
        <UIScript version="1" requires="sidebar">
          <Window width="300" height="200"/>
          <Image name="homeIcon" path="home.png"/>
          <Image name="driveIcon" path="drive.png"/>
          <Sidebar name="places" width="120"/>
          <List name="files" x="10" y="10" width="160" height="150" selectable="true"/>
        </UIScript>
        """;

    private readonly string root = Path.Combine(Path.GetTempPath(), $"venture-sidebar-{Guid.NewGuid():N}");
    private readonly RecordingOutput output = new();
    private readonly WindowManager windows = new(800, 600);
    private readonly AppManager manager;

    public SidebarItemsTests()
    {
        Directory.CreateDirectory(Path.Combine(root, "apps"));
        CommandRegistry registry = new();
        manager = new AppManager(
            Path.Combine(root, "apps"), new RuntimeCommands(registry.IsBuiltIn), new FakeSystemInfo(),
            windows: new AppWindows(windows, new RecordingOutput()));
    }

    public void Dispose()
    {
        if (manager.IsOpen("dev.example.pad"))
            manager.CloseApp("Pad", new RecordingOutput());
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void RowsCarryTextIconsAndGroups()
    {
        Open("""
            window:setItems("places", {
              { text = "Home", icon = "homeIcon" },
              "User",
              { separator = true },
              { text = "System", icon = "driveIcon" },
            })
            window:select("places", 3)
            print(#window:items("places"), window:items("places")[3])
            """);

        var places = View.List("places")!;
        Assert.Equal(["Home", "User", "System"], places.Items);
        Assert.Equal(["homeIcon", null, "driveIcon"], places.Icons);
        Assert.Equal([2], places.GroupStarts);
        Assert.Equal(2, places.SelectedIndex);
        Assert.Contains("3\tSystem", output.Lines);
    }

    [Fact]
    public void SeparatorsThatSplitNothingAreDropped()
    {
        Open("""
            window:setItems("places", {
              { separator = true }, "Home", { separator = true }, { separator = true }, "User", { separator = true },
            })
            """);

        Assert.Equal([1], View.List("places")!.GroupStarts);
    }

    [Fact]
    public void SettingItemsAgainReplacesIconsAndGroups()
    {
        Open("""
            window:setItems("places", { { text = "Home", icon = "homeIcon" }, { separator = true }, "User" })
            window:setItems("places", { "Apps" })
            """);

        var places = View.List("places")!;
        Assert.Equal(["Apps"], places.Items);
        Assert.Equal([null], places.Icons);
        Assert.Empty(places.GroupStarts);
    }

    [Theory]
    [InlineData("""window:setItems("places", { { icon = "homeIcon" } })""", "item 1 needs text")]
    [InlineData("""window:setItems("places", { { text = "Home", icon = "nope" } })""", "icon \"nope\" isn't an <Image>")]
    [InlineData("""window:setItems("places", { { text = "Home", icon = 5 } })""", "item 1's icon must be the name of an <Image>")]
    [InlineData("""window:setItems("files", { { text = "a.txt" } })""", "only a sidebar's items can have icons or separators")]
    public void RefusesWhatItCantShowAndChangesNothing(string call, string reason)
    {
        Open($$"""
            window:setItems("places", { "Home" })
            window:setItems("files", { "notes.txt" })
            print(pcall(function() {{call}} end))
            """);

        Assert.Contains(output.Lines, line => line.StartsWith("false") && line.Contains(reason));
        Assert.Equal(["Home"], View.List("places")!.Items);
        Assert.Equal(["notes.txt"], View.List("files")!.Items);
    }

    [Fact]
    public void ASidebarOfPanesKeepsItsRows()
    {
        Open("""
            print(pcall(function() window:setItems("places", { "Home" }) end))
            """, """
            <UIScript version="1" requires="sidebar panes">
              <Window width="300" height="200"/>
              <Sidebar name="places" width="120"/>
              <Pane name="general" title="General"/>
            </UIScript>
            """);

        Assert.Contains(output.Lines, line => line.StartsWith("false") && line.Contains("its rows are the document's panes"));
        Assert.Equal(["General"], View.List("places")!.Items);
    }

    [Fact]
    public void ASidebarThatHoldsControlsHasNoRowsToSet()
    {
        Open("""
            print(pcall(function() window:setItems("player", { "Home" }) end))
            window:setText("nowPlaying", "Jazz Radio")
            print(window:text("nowPlaying"))
            """, """
            <UIScript version="1">
              <Window width="300" height="200"/>
              <Sidebar name="player" width="160" edge="right">
                <Text name="nowPlaying" x="10" y="10" width="140" height="20"/>
              </Sidebar>
            </UIScript>
            """);

        Assert.Contains(output.Lines, line => line.StartsWith("false") && line.Contains("is a sidebar that holds controls, not rows"));
        Assert.Contains("Jazz Radio", output.Lines);
        Assert.Null(View.List("player"));
    }

    private UIView View => Assert.Single(windows.Windows).View;

    private void Open(string code, string document = Document)
    {
        var contents = Path.Combine(root, "apps", "Pad.app", "Contents");
        Directory.CreateDirectory(Path.Combine(contents, "Lua"));
        Directory.CreateDirectory(Path.Combine(contents, "Resources"));
        File.WriteAllText(Path.Combine(contents, "Info.xml"),
            """<App version="1"><Info identifier="dev.example.pad" name="Pad" needs="ui"/></App>""");
        File.WriteAllText(Path.Combine(contents, "Resources", "main.uix"), document);
        File.WriteAllText(Path.Combine(contents, "Lua", "main.lua"), $"window = venture.ui.open(\"main.uix\")\n{code}\n");
        Assert.True(manager.OpenApp("Pad", root, output), string.Join('\n', output.Lines));
    }
}
