using Venture.OS.Desktop;
using Venture.OS.Interface;

namespace Venture.OS.Tests.Desktop;

/// <summary>
/// A <c>&lt;Sidebar edge="right"&gt;</c>: down the window's right at full height, as Shortwave's player is, with the
/// content and the buttons on the left. Rows are placed as on the left (see <see cref="SidebarTests"/>).
/// </summary>
public sealed class RightSidebarTests
{
    private const string Document = """
        <UIScript version="1">
          <Window width="300" height="200" resizable="true"/>
          <Sidebar name="queue" width="100" edge="right"/>
          <List name="stations" x="10" y="10" width="280" height="180" selectable="true"/>
        </UIScript>
        """;

    private const int FirstRow = 7;
    private const int Row = 26;

    private readonly WindowManager manager = new(800, 600);
    private readonly RecordingPainter painter = new(800, 600);
    private readonly List<UIEvent> events = [];
    private readonly DesktopWindow window;

    public RightSidebarTests()
    {
        window = new DesktopWindow("Radio", new UIView(UIScript.Parse(Document)))
        {
            EventHandler = (_, uiEvent) => events.Add(uiEvent),
        };
        window.View.List("queue")!.Items.AddRange(["Now", "Next", "Later"]);
        manager.Open(window);
        manager.Draw(painter);
    }

    [Fact]
    public void TheContentStartsAtTheWindowsLeftAndTheSidebarFollowsIt()
    {
        Assert.Equal(400, window.Width);
        Assert.Equal((window.X, window.Y + DesktopTheme.TitleBarHeight), window.ContentOrigin);
        Assert.Equal(new UIRect(window.X + 300, window.Y, 100, window.Height), window.SidebarBounds);
        Assert.Equal(new UIRect(10, 10, 280, 180), window.View.Bounds("stations"));
    }

    [Fact]
    public void RunsTheWindowsFullHeightDownTheRight()
    {
        var shade = Pack(DesktopTheme.SidebarBackground);

        Assert.Equal(shade, painter.At(window.X + 360, window.Y + 4));
        Assert.Equal(shade, painter.At(window.X + 360, window.Y + window.Height - 4));
        Assert.NotEqual(shade, painter.At(window.X + 60, window.Y + 4));
        Assert.Equal(Pack(DesktopTheme.Separator), painter.At(window.X + 300, window.Y + 40));
        Assert.Equal((FramePart.Close, -1), window.Hit((int)window.CloseButton.X + 6, (int)window.CloseButton.Y + 6));
    }

    [Fact]
    public void CentresTheTitleOverTheContent()
    {
        // "Radio" is 40 wide, in the middle of the 300 left of the sidebar.
        Assert.Contains(painter.Texts, text => text.Text == "Radio" && text.X == window.X + ((300 - 40) / 2));
    }

    [Fact]
    public void AClickOnARowSelectsItAndSaysSo()
    {
        var (_, top) = window.ContentOrigin;

        Click(window.X + 340, top + FirstRow + Row + 5);
        manager.Draw(painter);

        Assert.Equal([new UIEvent(UIEventKind.Selected, "queue", Index: 1)], events);
        Assert.Contains(painter.Texts, text => text.Text == "Next" && text.X == window.X + 300 + 12);
    }

    [Fact]
    public void ResizingGrowsTheContentAndKeepsTheSidebarOnTheRight()
    {
        window.Resize(500, window.Height);
        manager.Draw(painter);
        var (_, top) = window.ContentOrigin;

        Assert.Equal(new UIRect(window.X + 400, window.Y, 100, window.Height), window.SidebarBounds);
        Assert.Equal(400, (int)window.View.WindowSize.Width);
        Click(window.X + 440, top + FirstRow + 5);
        Assert.Equal([new UIEvent(UIEventKind.Selected, "queue", Index: 0)], events);
    }

    [Fact]
    public void TheWindowKeepsItsGripInItsCornerOverTheSidebarAndItResizes()
    {
        Assert.Equal(new UIRect(window.X + window.Width - 16, window.Y + window.Height - 16, 16, 16), window.ResizeGrip);
        var (x, y) = ((int)window.ResizeGrip.X + 8, (int)window.ResizeGrip.Y + 8);

        manager.Update(new DesktopInput(x, y, true));
        manager.Update(new DesktopInput(x + 50, y + 20, true));
        manager.Update(new DesktopInput(x + 100, y + 40, true));
        manager.Update(new DesktopInput(x + 100, y + 40, false));

        // The content takes the growth; the sidebar keeps its width on the right.
        Assert.Equal(500, window.Width);
        Assert.Equal(new UIRect(window.X + 400, window.Y, 100, window.Height), window.SidebarBounds);
        Assert.Empty(events);
    }

    private static int Pack(UIColor color) => DesktopBackground.Pack(color.Red, color.Green, color.Blue);

    private void Click(int x, int y)
    {
        manager.Update(new DesktopInput(x, y, true));
        manager.Update(new DesktopInput(x, y, false));
    }
}
