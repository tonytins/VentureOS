using Venture.OS.Desktop;
using Venture.OS.Interface;

namespace Venture.OS.Tests.Desktop;

/// <summary>
/// A window's <c>&lt;Sidebar&gt;</c>: down the left at full height, beside the content, and a selectable list to its
/// owner. Rows start 7 pixels below the content's top (an inset of 6 and the list's 1) and are 26 tall (a 13-point
/// line of 18, and 8). The recording painter measures 8 pixels a character.
/// </summary>
public sealed class SidebarTests
{
    private const string Document = """
        <UIScript version="1" requires="sidebar">
          <Window width="300" height="200"/>
          <Sidebar name="places" width="100"/>
          <List name="entries" x="10" y="10" width="280" height="180" selectable="true"/>
        </UIScript>
        """;

    private const int FirstRow = 7;
    private const int Row = 26;

    private readonly WindowManager manager = new(800, 600);
    private readonly RecordingPainter painter = new(800, 600);
    private readonly List<UIEvent> events = [];
    private readonly DesktopWindow window;

    public SidebarTests()
    {
        window = new DesktopWindow("Files", new UIView(UIScript.Parse(Document)))
        {
            EventHandler = (_, uiEvent) => events.Add(uiEvent),
        };
        window.View.List("places")!.Items.AddRange(["Home", "User", "Apps"]);
        manager.Open(window);
        manager.Draw(painter);
    }

    [Fact]
    public void SitsBesideTheContentRatherThanInIt()
    {
        Assert.Equal(400, window.Width);
        Assert.Equal((window.X + 100, window.Y + DesktopTheme.TitleBarHeight), window.ContentOrigin);
        Assert.Equal(new UIRect(10, 10, 280, 180), window.View.Bounds("entries"));
    }

    [Fact]
    public void RunsTheWindowsFullHeightUnderTheButtons()
    {
        var shade = Pack(DesktopTheme.SidebarBackground);

        Assert.Equal(shade, painter.At(window.X + 60, window.Y + 4));
        Assert.Equal(shade, painter.At(window.X + 60, window.Y + window.Height - 4));
        Assert.NotEqual(shade, painter.At(window.X + 160, window.Y + 4));
        Assert.Equal((FramePart.Close, -1), window.Hit((int)window.CloseButton.X + 6, (int)window.CloseButton.Y + 6));
        Assert.Equal((FramePart.Bar, -1), window.Hit(window.X + 60, window.Y + 10));
    }

    [Fact]
    public void CentresTheTitleOverTheContent()
    {
        // "Files" is 40 wide, in the middle of the 300 beside the sidebar.
        Assert.Contains(painter.Texts, text => text.Text == "Files" && text.X == window.X + 100 + ((300 - 40) / 2));
    }

    [Fact]
    public void AClickOnARowSelectsItAndSaysSo()
    {
        var (_, top) = window.ContentOrigin;

        Click(window.X + 40, top + FirstRow + Row + 5);
        manager.Draw(painter);

        Assert.Equal([new UIEvent(UIEventKind.Selected, "places", Index: 1)], events);
        Assert.Equal(1, window.View.List("places")!.SelectedIndex);
        Assert.Equal(Pack(DesktopTheme.SidebarSelection), painter.At(window.X + 20, top + FirstRow + Row + 3));
        Assert.Contains(painter.Texts, text => text.Text == "User" && text.X == window.X + 12);
    }

    [Fact]
    public void AClickBelowTheRowsDoesNothing()
    {
        var (_, top) = window.ContentOrigin;

        Click(window.X + 40, top + FirstRow + (3 * Row) + 5);

        Assert.Empty(events);
        Assert.Equal(-1, window.View.List("places")!.SelectedIndex);
    }

    [Fact]
    public void TheWheelScrollsALongSidebar()
    {
        var places = window.View.List("places")!;
        for (var number = 0; number < 20; number++)
            places.Items.Add($"Place {number}");
        var (_, top) = window.ContentOrigin;

        manager.Update(new DesktopInput(window.X + 40, top + 50, false, Scroll: 3));

        Assert.Equal(3, places.FirstVisible);
    }

    private static int Pack(UIColor color) => DesktopBackground.Pack(color.Red, color.Green, color.Blue);

    private void Click(int x, int y)
    {
        manager.Update(new DesktopInput(x, y, true));
        manager.Update(new DesktopInput(x, y, false));
    }
}
