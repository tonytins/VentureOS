using Venture.OS.Desktop;
using Venture.OS.Interface;

namespace Venture.OS.Tests.Desktop;

/// <summary>
/// Resizable windows: the grip in the bottom-right corner resizes the window, no smaller than the document's
/// <c>minWidth</c> and <c>minHeight</c> and no larger than the screen, and controls follow their edge offsets.
/// </summary>
public sealed class ResizeTests
{
    private const string Document = """
        <UIScript version="1">
          <Window width="300" height="200" minWidth="200" minHeight="150" resizable="true"/>
          <List name="entries" x="0" y="0" width="280" height="180" edgeOffsetLeft="10" edgeOffsetTop="10" edgeOffsetRight="10" edgeOffsetBottom="10"/>
          <Button name="ok" x="0" y="10" width="50" height="20" edgeOffsetRight="10"/>
        </UIScript>
        """;

    private const int Bar = DesktopTheme.TitleBarHeight;

    private readonly WindowManager manager = new(800, 600);
    private readonly RecordingPainter painter = new(800, 600);

    [Fact]
    public void TheGripGrowsTheWindowAndControlsFollowTheirEdges()
    {
        var window = Open(Document);

        Drag(window.ResizeGrip, 100, 50);

        Assert.Equal((400, 250 + Bar), (window.Width, window.Height));
        Assert.Equal(new UIRect(10, 10, 380, 230), window.View.Bounds("entries"));
        Assert.Equal(new UIRect(340, 10, 50, 20), window.View.Bounds("ok"));
    }

    [Fact]
    public void ItStopsAtTheSmallestSize()
    {
        var window = Open(Document);

        Drag(window.ResizeGrip, -300, -300);

        Assert.Equal((200, 150 + Bar), (window.Width, window.Height));
    }

    [Fact]
    public void ItStopsAtTheScreensEdge()
    {
        var window = Open(Document);

        Drag(window.ResizeGrip, 2000, 2000);

        Assert.Equal((800, 600), (window.X + window.Width, window.Y + window.Height));
    }

    [Fact]
    public void WithoutAMinimumItStopsAtItsFirstSize()
    {
        var window = Open("""<UIScript><Window width="300" height="200" resizable="true"/></UIScript>""");

        Drag(window.ResizeGrip, -100, -100);

        Assert.Equal((300, 200 + Bar), (window.Width, window.Height));
    }

    [Fact]
    public void ASidebarKeepsItsWidth()
    {
        var window = Open("""
            <UIScript requires="sidebar">
              <Window width="300" height="200" resizable="true"/>
              <Sidebar name="places" width="100"/>
            </UIScript>
            """);

        Drag(window.ResizeGrip, 60, 0);

        Assert.Equal(460, window.Width);
        Assert.Equal(360, window.View.WindowSize.Width);
    }

    [Fact]
    public void TheGripIsDrawnAsDots()
    {
        var window = Open(Document);
        var dot = DesktopBackground.Pack(DesktopTheme.SecondaryText.Red, DesktopTheme.SecondaryText.Green, DesktopTheme.SecondaryText.Blue);

        Assert.Equal((FramePart.Grip, -1), window.Hit(window.X + window.Width - 3, window.Y + window.Height - 3));
        Assert.Equal(dot, painter.At(window.X + window.Width - 6, window.Y + window.Height - 6));
    }

    [Fact]
    public void AFixedWindowHasNoGrip()
    {
        var window = Open("""<UIScript><Window width="300" height="200"/></UIScript>""");
        var corner = new UIRect(window.X + window.Width - 16, window.Y + window.Height - 16, 16, 16);

        Assert.Equal((FramePart.None, -1), window.Hit(window.X + window.Width - 3, window.Y + window.Height - 3));
        Drag(corner, 100, 100);

        Assert.Equal((300, 200 + Bar), (window.Width, window.Height));
    }

    private DesktopWindow Open(string document)
    {
        DesktopWindow window = new("Window", new UIView(UIScript.Parse(document)));
        manager.Open(window);
        manager.Draw(painter);
        return window;
    }

    private void Drag(UIRect from, int byX, int byY)
    {
        var (x, y) = ((int)from.X + 8, (int)from.Y + 8);
        manager.Update(new DesktopInput(x, y, true));
        manager.Update(new DesktopInput(x + (byX / 2), y + (byY / 2), true));
        manager.Update(new DesktopInput(x + byX, y + byY, true));
        manager.Update(new DesktopInput(x + byX, y + byY, false));
        manager.Draw(painter);
    }
}
