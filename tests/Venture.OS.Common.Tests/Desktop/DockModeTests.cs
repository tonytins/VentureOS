using Venture.OS.Desktop;
using Venture.OS.Interface;

namespace Venture.OS.Tests.Desktop;

/// <summary>The dock's three modes, in the window manager: shown, hiding on its own, or hiding when covered.</summary>
public sealed class DockModeTests
{
    private readonly WindowManager windows = new(1000, 800);
    private readonly DesktopWindow dock;
    private TimeSpan now;

    public DockModeTests()
    {
        dock = new("Dock", new UIView(UIScript.Parse("""
            <UIScript requires="shelf">
              <Window width="400" height="72" role="panel" edge="bottom"/>
              <Shelf name="apps" x="0" y="0" width="400" height="72"/>
            </UIScript>
            """)));
        dock.View.Shelf("apps")!.SetItems([new("Files"), new("Notes")]);
        windows.Open(dock);
    }

    [Fact]
    public void AlwaysStaysAndKeepsWindowsAbove()
    {
        Frames(30, y: 300);

        Assert.Equal(1, dock.Shown);
        Assert.Equal(800 - 72, dock.Y);
        Assert.Equal(800 - 72, (int)(windows.WorkArea.Y + windows.WorkArea.Height));
    }

    [Fact]
    public void AutoHideSlidesAwayAndLetsWindowsRunToTheBottom()
    {
        windows.DockMode = DockMode.AutoHide;

        Frames(10, y: 300);
        Assert.True(windows.IsSliding);
        Assert.True(dock.Shown is > 0 and < 1, $"Shown {dock.Shown} after a sixth of a second.");
        Frames(100, y: 300);

        Assert.Equal(0, dock.Shown);
        Assert.Equal(800, dock.Y);
        Assert.False(windows.IsSliding);
        Assert.Equal(800, (int)(windows.WorkArea.Y + windows.WorkArea.Height));
        Assert.Null(windows.WindowAt(500, 799));
    }

    [Fact]
    public void AutoHideComesBackAtTheBottomEdgeAndStaysAWhileAfter()
    {
        windows.DockMode = DockMode.AutoHide;
        Frames(100, y: 300);

        Frames(20, y: 799);
        Assert.Equal(1, dock.Shown);

        // Over the dock, it stays; a second after the pointer leaves, it goes.
        Frames(100, y: 760);
        Assert.Equal(1, dock.Shown);
        Frames(30, y: 300);
        Assert.Equal(1, dock.Shown);
        Frames(100, y: 300);
        Assert.Equal(0, dock.Shown);
    }

    [Fact]
    public void DodgeHidesOnlyWhileAWindowCoversIt()
    {
        windows.DockMode = DockMode.Dodge;
        Frames(30, y: 300);
        Assert.Equal(1, dock.Shown);

        DesktopWindow tall = new("Tall", new UIView(UIScript.Parse("""<UIScript><Window width="300" height="700"/></UIScript>""")));
        windows.Open(tall);
        tall.Y = 60;
        Frames(30, y: 300);
        Assert.Equal(0, dock.Shown);

        windows.Minimize(tall);
        Frames(30, y: 300);
        Assert.Equal(1, dock.Shown);
    }

    [Fact]
    public void DodgeComesBackAtTheBottomEdgeEvenWhenCovered()
    {
        windows.DockMode = DockMode.Dodge;
        DesktopWindow tall = new("Tall", new UIView(UIScript.Parse("""<UIScript><Window width="300" height="700"/></UIScript>""")));
        windows.Open(tall);
        tall.Y = 60;
        Frames(30, y: 300);

        Frames(20, y: 799);

        Assert.Equal(1, dock.Shown);
    }

    // Frames 10 ms apart, with the pointer resting at (500, y).
    private void Frames(int count, int y)
    {
        for (var frame = 0; frame < count; frame++)
        {
            now += TimeSpan.FromMilliseconds(10);
            windows.Update(new DesktopInput(500, y, false, Time: now));
        }
    }
}
