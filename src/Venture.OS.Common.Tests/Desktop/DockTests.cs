using Venture.OS.Desktop;

namespace Venture.OS.Tests.Desktop;

public sealed class DockTests
{
    private readonly Dock dock = new(800, 600);

    public DockTests() =>
        dock.SetItems([new DockItem("Notes", "Notes", IsOpen: true), new DockItem("Terminal", "Terminal", IsOpen: false)]);

    [Fact]
    public void SitsCenteredAlongTheBottom()
    {
        // Two 48-pixel icons, a 10-pixel gap, and 8 pixels of padding each side.
        Assert.Equal(new UIRect((800 - 122) / 2, 600 - Dock.Height, 122, Dock.Height), dock.Bounds);
        Assert.Equal(new UIRect(347, 600 - Dock.Height + 8, 48, 48), dock.IconBounds(0));
    }

    [Fact]
    public void AClickOnAnIconReportsItsApp()
    {
        var icon = dock.IconBounds(1);
        var (x, y) = ((int)icon.X + 5, (int)icon.Y + 5);

        Assert.Null(dock.Update(x, y, owned: true, justPressed: true, released: false));
        Assert.Equal("Terminal", dock.Update(x, y, owned: true, justPressed: false, released: true)?.Name);
    }

    [Fact]
    public void APressThatLeavesTheIconDoesNothing()
    {
        var icon = dock.IconBounds(0);
        dock.Update((int)icon.X + 5, (int)icon.Y + 5, owned: true, justPressed: true, released: false);

        Assert.Null(dock.Update(10, 10, owned: true, justPressed: false, released: true));
    }

    [Fact]
    public void NamesTheIconUnderThePointerAndMarksOpenApps()
    {
        var icon = dock.IconBounds(0);
        dock.Update((int)icon.X + 5, (int)icon.Y + 5, owned: true, justPressed: false, released: false);
        RecordingPainter painter = new(800, 600);

        dock.Draw(painter);

        Assert.Equal("Notes", dock.Hovered?.Name);
        Assert.Contains(painter.Texts, text => text.Text == "Notes" && text.Y < dock.Bounds.Y);
        var dot = DesktopBackground.Pack(DesktopTheme.OpenAppDot.Red, DesktopTheme.OpenAppDot.Green, DesktopTheme.OpenAppDot.Blue);
        Assert.Equal(dot, painter.At((int)icon.X + 24, (int)icon.Y + 48 + 4));
        Assert.NotEqual(dot, painter.At((int)dock.IconBounds(1).X + 24, (int)icon.Y + 48 + 4));
    }

    [Fact]
    public void AnEmptyDockTakesNoRoom()
    {
        dock.SetItems([]);

        Assert.False(dock.Contains(400, 590));
        Assert.Equal(default, dock.Bounds);
    }
}
