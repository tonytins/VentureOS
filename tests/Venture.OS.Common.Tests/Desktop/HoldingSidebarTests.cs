using Venture.OS.Desktop;
using Venture.OS.Interface;

namespace Venture.OS.Tests.Desktop;

/// <summary>
/// A <c>&lt;Sidebar&gt;</c> that holds controls and visuals in place of rows, as Shortwave's player does: they're placed
/// from the sidebar's corner, level with the content's top, anchored to its edges, and drawn and clicked where the
/// sidebar is, on either edge.
/// </summary>
public sealed class HoldingSidebarTests
{
    private const int Red = unchecked((int)0xFFFF0000);

    private static string Document(string edge) => $"""
        <UIScript version="1">
          <Window width="300" height="200" resizable="true"/>
          <Image name="art" path="art.png"/>
          <Sidebar name="player" width="100" edge="{edge}">
            <Visual name="cover" image="art" x="10" y="10" width="2" height="2"/>
            <Button name="stop" x="10" y="40" width="80" height="24" text="Stop"/>
            <Slider name="volume" x="10" y="80" width="40" height="20" edgeOffsetLeft="10" edgeOffsetRight="10"/>
          </Sidebar>
          <List name="stations" x="10" y="10" width="280" height="180" selectable="true"/>
        </UIScript>
        """;

    private readonly WindowManager manager = new(800, 600);
    private readonly RecordingPainter painter = new(800, 600);
    private readonly List<UIEvent> events = [];

    [Theory]
    [InlineData("left", -100)]
    [InlineData("right", 300)]
    public void ItsControlsArePlacedFromTheSidebarsCorner(string edge, int sidebarX)
    {
        var window = Open(edge);

        Assert.Equal(new UIRect(sidebarX + 10, 40, 80, 24), window.View.Bounds("stop"));
        Assert.Equal(new UIRect(sidebarX + 10, 80, 80, 20), window.View.Bounds("volume"));
        Assert.Equal(new UIRect(10, 10, 280, 180), window.View.Bounds("stations"));
        Assert.Equal(400, window.Width);
    }

    [Theory]
    [InlineData("left")]
    [InlineData("right")]
    public void AClickOnOneOfItsControlsReachesIt(string edge)
    {
        var window = Open(edge);
        var (left, top) = window.ContentOrigin;
        var stop = window.View.Bounds("stop")!.Value;

        Click(left + (int)stop.X + 5, top + (int)stop.Y + 5);

        Assert.Contains(new UIEvent(UIEventKind.Pressed, "stop"), events);
    }

    [Fact]
    public void ItsVisualsDrawInTheSidebar()
    {
        var window = Open("right");
        window.View.SetImage("art", new DesktopImage(2, 2, [Red, Red, Red, Red]));
        manager.Draw(painter);
        var (left, top) = window.ContentOrigin;

        Assert.Contains(painter.Images, drawn => (drawn.X, drawn.Y) == (left + 310, top + 10));
    }

    [Fact]
    public void ResizingKeepsItsControlsWithTheSidebar()
    {
        var window = Open("right");

        window.Resize(500, window.Height);

        // The content grows; the sidebar, and what it holds, moves with its edge and keeps its width.
        Assert.Equal(new UIRect(410, 40, 80, 24), window.View.Bounds("stop"));
        Assert.Equal(new UIRect(410, 80, 80, 20), window.View.Bounds("volume"));
    }

    [Fact]
    public void ItHasNoRows()
    {
        var window = Open("left");

        Assert.True(window.View.SidebarHoldsContent);
        Assert.Equal("player", window.View.SidebarName);
        Assert.Null(window.View.List("player"));
        Assert.Null(window.View.SidebarRowBounds(0));
    }

    private DesktopWindow Open(string edge)
    {
        DesktopWindow window = new("Radio", new UIView(UIScript.Parse(Document(edge))))
        {
            EventHandler = (_, uiEvent) => events.Add(uiEvent),
        };
        manager.Open(window);
        manager.Draw(painter);
        return window;
    }

    private void Click(int x, int y)
    {
        manager.Update(new DesktopInput(x, y, true));
        manager.Update(new DesktopInput(x, y, false));
    }
}
