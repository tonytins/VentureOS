using Venture.OS.Desktop;
using Venture.OS.Interface;

namespace Venture.OS.Tests.Desktop;

/// <summary>
/// The green zoom button on resizable windows: it fills the screen below the menu bar, over the dock, as macOS does,
/// and a second click puts the window back. It draws in the palette in use, so it runs with the appearance tests.
/// </summary>
[Collection(nameof(AppearanceCollection))]
public sealed class ZoomTests
{
    private const int Inset = 26;
    private readonly WindowManager manager = new(800, 600) { TopInset = Inset };
    private readonly RecordingPainter painter = new(800, 600);

    [Fact]
    public void OnlyAResizableWindowHasIt()
    {
        var resizable = Open(resizable: true);
        var fixedSize = Open(resizable: false);

        Assert.True(resizable.HasZoom);
        Assert.False(fixedSize.HasZoom);
        Assert.Equal((FramePart.Zoom, -1), resizable.Hit((int)resizable.ZoomButton.X + 6, (int)resizable.ZoomButton.Y + 6));
        Assert.NotEqual(FramePart.Zoom, fixedSize.Hit((int)fixedSize.ZoomButton.X + 6, (int)fixedSize.ZoomButton.Y + 6).Part);
    }

    [Fact]
    public void FillsTheScreenBelowTheMenuBarThenGoesBack()
    {
        var window = Open(resizable: true);
        var before = window.Bounds;

        Click(window.ZoomButton);

        Assert.True(window.IsZoomed);
        Assert.Equal(new UIRect(0, Inset, 800, 600 - Inset), window.Bounds);

        Click(window.ZoomButton);

        Assert.False(window.IsZoomed);
        Assert.Equal(before, window.Bounds);
    }

    [Fact]
    public void MovingAZoomedWindowForgetsTheOldFrame()
    {
        var window = Open(resizable: true);
        Click(window.ZoomButton);

        // Drag it by the bar, beside the title.
        var (x, y) = (window.X + 300, window.Y + 10);
        manager.Update(new DesktopInput(x, y, true));
        manager.Update(new DesktopInput(x + 30, y + 40, true));
        manager.Update(new DesktopInput(x + 30, y + 40, false));

        Assert.False(window.IsZoomed);
        Assert.Equal((30, Inset + 40), (window.X, window.Y));

        // So the next zoom fills the screen again.
        Click(window.ZoomButton);
        Assert.Equal(new UIRect(0, Inset, 800, 600 - Inset), window.Bounds);
    }

    [Fact]
    public void AFixedWindowDoesntZoom()
    {
        var window = Open(resizable: false);
        var before = window.Bounds;

        Assert.False(manager.Zoom(window));
        Assert.Equal(before, window.Bounds);
    }

    [Fact]
    public void ShowsAPlusUnderThePointerWithTheOtherMarks()
    {
        var window = Open(resizable: true);
        var zoom = window.ZoomButton;

        manager.Update(new DesktopInput((int)zoom.X + 6, (int)zoom.Y + 6, false));
        manager.Draw(painter);

        var mark = DesktopImage.Pack(255, DesktopTheme.ZoomMark.Red, DesktopTheme.ZoomMark.Green, DesktopTheme.ZoomMark.Blue);
        Assert.Equal(mark, painter.At((int)zoom.X + 6, (int)zoom.Y + 4));
        Assert.Equal(mark, painter.At((int)zoom.X + 4, (int)zoom.Y + 6));
    }

    [Fact]
    public void TheBarsRoomStartsAfterTheThirdButton()
    {
        DesktopWindow window = new("Files", new UIView(UIScript.Parse("""
            <UIScript><Window width="400" height="100" resizable="true"/><ToolbarItem name="back" title="Back" placement="start"/></UIScript>
            """)));
        manager.Open(window);
        manager.Draw(painter);

        Assert.Equal(window.X + 73, (int)window.ToolbarItemBounds(0).X);
    }

    private DesktopWindow Open(bool resizable)
    {
        DesktopWindow window = new("Pad", new UIView(UIScript.Parse(
            $"<UIScript><Window width='300' height='100' resizable='{(resizable ? "true" : "false")}'/></UIScript>")));
        manager.Open(window);
        manager.Draw(painter);
        return window;
    }

    private void Click(UIRect button)
    {
        var (x, y) = ((int)button.X + 6, (int)button.Y + 6);
        manager.Update(new DesktopInput(x, y, true));
        manager.Update(new DesktopInput(x, y, false));
    }
}
