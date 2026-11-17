using Venture.OS.Desktop;
using Venture.OS.Interface;

namespace Venture.OS.Tests.Desktop;

/// <summary>
/// The flat header bar, after GNOME's: one color that blends with the content, a faint line under it, and a faint
/// line beside the sidebar. It draws in the palette in use, so it runs with the appearance tests.
/// </summary>
[Collection(nameof(AppearanceCollection))]
public sealed class FlatHeaderBarTests
{
    private readonly WindowManager manager = new(800, 600);
    private readonly RecordingPainter painter = new(800, 600);
    private readonly DesktopWindow window;

    public FlatHeaderBarTests()
    {
        window = new DesktopWindow("Files", new UIView(UIScript.Parse("""
            <UIScript version="1" requires="sidebar"><Window width="300" height="100"/><Sidebar name="places" width="100"/></UIScript>
            """)));
        manager.Open(window);
        manager.Draw(painter);
    }

    [Fact]
    public void TheBarIsOneFlatColor()
    {
        var bar = Pack(DesktopTheme.HeaderBar);

        // The top and the bottom of the bar, away from the title, are the same color.
        Assert.Equal(bar, painter.At(window.X + 380, window.Y + 1));
        Assert.Equal(bar, painter.At(window.X + 380, window.Y + window.BarHeight - 2));
    }

    [Fact]
    public void FaintLinesSeparateTheBarAndTheSidebar()
    {
        var line = Pack(DesktopTheme.Separator);

        Assert.Equal(line, painter.At(window.X + 380, window.Y + window.BarHeight - 1));
        Assert.Equal(line, painter.At(window.X + 99, window.Y + 60));
        Assert.NotEqual(Pack(DesktopTheme.Border), line);
    }

    private static int Pack(UIColor color) => DesktopImage.Pack(255, color.Red, color.Green, color.Blue);
}
