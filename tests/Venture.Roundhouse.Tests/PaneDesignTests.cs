using Avalonia;
using Avalonia.VisualTree;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Venture.OS.Desktop;
using Venture.OS.Interface;
using Venture.Roundhouse.Designer;

namespace Venture.Roundhouse.Tests;

/// <summary>
/// A sidebar of panes in the designer, with the shipped Settings: its rows drawn, one pane's controls at a time, and
/// the pane on show switched from the canvas, the outline, or by adding to it.
/// </summary>
[Collection(nameof(PaletteCollection))]
public sealed class PaneDesignTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), $"roundhouse-panes-{Guid.NewGuid():N}");
    private readonly string document;

    public PaneDesignTests()
    {
        Directory.CreateDirectory(folder);
        document = Path.Combine(folder, "main.uix");
        var settings = Path.Combine(SkiaPainterTests.RepositoryRoot(), "rootfs", "system", "apps", "Settings.app", "Contents", "Resources", "main.uix");
        File.Copy(settings, document);
    }

    public void Dispose()
    {
        DesktopAppearance.Use(DesktopLook.Standard);
        Directory.Delete(folder, recursive: true);
    }

    [Fact]
    public void TheCanvasShowsTheFirstPaneAndMapsOnlyItsControls() => TestApp.Run(() =>
    {
        var window = Open();

        Assert.Null(window.ShownPane);
        Assert.NotNull(window.Map!.BoundsOf(Index(window, "light")));
        Assert.Null(window.Map.BoundsOf(Index(window, "utcOffset")));
        Assert.Null(window.Map.BoundsOf(Index(window, "developerOn")));
        window.Close();
    });

    [Fact]
    public void EachPaneIsItsRowInTheSidebar() => TestApp.Run(() =>
    {
        var window = Open();
        var appearance = window.Map!.BoundsOf(Index(window, "appearance"))!.Value;
        var dock = window.Map.BoundsOf(Index(window, "dock"))!.Value;
        var sidebar = window.Map.BoundsOf(window.Document!.Xml!.Children.ToList().FindIndex(element => element.Name == "Sidebar"))!.Value;

        Assert.True(dock.Y > appearance.Y);
        Assert.True(appearance.X >= sidebar.X && appearance.X + appearance.Width <= sidebar.X + sidebar.Width);
        Assert.Equal(Index(window, "dock"), window.Map.ElementAt(dock.X + 5, dock.Y + 5));
        window.Close();
    });

    [Fact]
    public void ClickingARowSelectsItsPaneAndShowsIt() => TestApp.Run(() =>
    {
        var window = Open();
        var clock = window.Map!.BoundsOf(Index(window, "clock"))!.Value;

        Click(window, clock.X + 5, clock.Y + 5);

        Assert.Equal(Index(window, "clock"), window.Selection);
        Assert.Equal("clock", window.ShownPane);
        Assert.NotNull(window.Map!.BoundsOf(Index(window, "utcOffset")));
        Assert.Null(window.Map.BoundsOf(Index(window, "light")));
        window.Close();
    });

    [Fact]
    public void SelectingAControlInAnotherPaneShowsThatPane() => TestApp.Run(() =>
    {
        var window = Open();

        window.Select(Index(window, "developerOn"));

        Assert.Equal("developer", window.ShownPane);
        Assert.NotNull(window.Map!.BoundsOf(Index(window, "developerOn")));
        window.Close();
    });

    [Fact]
    public void TheShownPaneOutlastsAnEdit() => TestApp.Run(() =>
    {
        var window = Open();
        window.Select(Index(window, "dockAlways"));

        window.Nudge(1, 0);

        Assert.Equal("dock", window.ShownPane);
        Assert.NotNull(window.Map!.BoundsOf(Index(window, "dockAlways")));
        window.Close();
    });

    [Fact]
    public void AControlFromTheLibraryJoinsThePaneOnShow() => TestApp.Run(() =>
    {
        var window = Open();
        window.Select(Index(window, "clock"));

        Assert.True(window.AddControl("Slider"));

        // Settings' panes hold their controls, so the slider goes inside Clock.
        var slider = window.Document!.Xml!.Children[window.Selection!.Value];
        Assert.Equal("Slider", slider.Name);
        Assert.Equal(Index(window, "clock"), slider.Parent);
        Assert.Null(slider["pane"]);
        Assert.NotNull(window.Map!.BoundsOf(window.Selection!.Value));
        window.Close();
    });

    [Fact]
    public void ASidebarOnTheRightIsMappedWhereItsDrawn() => TestApp.Run(() =>
    {
        File.WriteAllText(document, """
            <UIScript>
              <Window width="300" height="200"/>
              <Sidebar name="queue" width="100" edge="right"/>
              <Pane name="now" title="Now">
                <Button name="play" x="10" y="10" width="80" height="24" text="Play"/>
              </Pane>
              <Pane name="later" title="Later"/>
            </UIScript>
            """);
        var window = Open();
        var play = window.Map!.BoundsOf(Index(window, "play"))!.Value;
        var sidebar = window.Map.BoundsOf(window.Document!.Xml!.Children.ToList().FindIndex(element => element.Name == "Sidebar"))!.Value;
        var later = window.Map.BoundsOf(Index(window, "later"))!.Value;

        Assert.True(sidebar.X >= play.X + 300 - 10);
        Assert.Equal(100, (int)sidebar.Width);
        Assert.True(later.X >= sidebar.X && later.X + later.Width <= sidebar.X + sidebar.Width);
        Click(window, later.X + 5, later.Y + 5);
        Assert.Equal("later", window.ShownPane);
        window.Close();
    });

    [Fact]
    public void ASidebarThatHoldsControlsOpensWithThemMappedInIt() => TestApp.Run(() =>
    {
        File.WriteAllText(document, """
            <UIScript>
              <Window width="300" height="200"/>
              <Sidebar name="player" width="160" edge="right">
                <Button name="stop" x="40" y="40" width="80" height="24" text="Stop"/>
              </Sidebar>
              <List name="stations" x="10" y="10" width="280" height="180"/>
            </UIScript>
            """);
        var window = Open();
        var stop = window.Map!.BoundsOf(Index(window, "stop"))!.Value;
        var sidebar = window.Map.BoundsOf(Index(window, "player"))!.Value;

        Assert.True(stop.X >= sidebar.X && stop.X + stop.Width <= sidebar.X + sidebar.Width);
        Click(window, stop.X + 5, stop.Y + 5);
        Assert.Equal(Index(window, "stop"), window.Selection);
        window.Close();
    });

    [Fact]
    public void ControlsAreAddedToAndDraggedInsideTheSidebar() => TestApp.Run(() =>
    {
        File.WriteAllText(document, """
            <UIScript>
              <Window width="300" height="200"/>
              <Sidebar name="player" width="160" edge="right">
                <Button name="stop" x="40" y="40" width="80" height="24" text="Stop"/>
              </Sidebar>
              <List name="stations" x="10" y="10" width="280" height="180"/>
            </UIScript>
            """);
        var window = Open();

        // A click on the sidebar's own shade selects it, and Insert then adds there.
        var sidebar = window.Map!.BoundsOf(Index(window, "player"))!.Value;
        Click(window, sidebar.X + 5, sidebar.Y + sidebar.Height - 5);
        Assert.Equal(Index(window, "player"), window.Selection);
        Assert.True(window.AddControl("Text"));
        Assert.True(window.Document!.Script!.Control("text")!.InSidebar);

        // A drag moves a control by as much in the sidebar's own positions.
        var stop = window.Map!.BoundsOf(Index(window, "stop"))!.Value;
        var at = window.FindControl<Avalonia.Controls.Image>("PreviewImage")!;
        Avalonia.Point Screen(float x, float y) => at.TranslatePoint(new Avalonia.Point(x, y), window)!.Value;
        window.MouseDown(Screen(stop.X + 10, stop.Y + 10), MouseButton.Left);
        window.MouseMove(Screen(stop.X + 15, stop.Y + 12));
        window.MouseMove(Screen(stop.X + 20, stop.Y + 15));
        window.MouseUp(Screen(stop.X + 20, stop.Y + 15), MouseButton.Left);

        var moved = window.Document!.Script!.Control("stop")!;
        Assert.True(moved.InSidebar);
        Assert.Equal((50, 45), ((int)moved.Frame.X, (int)moved.Frame.Y));
        window.Close();
    });

    private MainWindow Open()
    {
        MainWindow window = new() { AskToLeave = _ => Task.FromResult(Bundles.LeaveChoice.DontSave) };
        window.Show();
        window.Open(document);
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static int Index(MainWindow window, string name) =>
        window.Document!.Xml!.Children.ToList().FindIndex(element => element["name"] == name);

    private static void Click(MainWindow window, float x, float y)
    {
        var at = window.FindControl<Avalonia.Controls.Image>("PreviewImage")!.TranslatePoint(new Avalonia.Point(x, y), window)!.Value;
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
    }
}
