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
