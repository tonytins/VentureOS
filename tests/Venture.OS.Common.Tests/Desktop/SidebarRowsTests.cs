using Venture.OS.Desktop;
using Venture.OS.Interface;

namespace Venture.OS.Tests.Desktop;

/// <summary>
/// A sidebar drawn as Nautilus draws one: rounded shades, icons as symbols in the text's color, and faint lines
/// between groups, which clicks and scrolling step over. Rows start 7 pixels below the content's top and are 26
/// tall; a separator takes 9. It draws in the palette in use, so it runs with the appearance tests.
/// </summary>
[Collection(nameof(AppearanceCollection))]
public sealed class SidebarRowsTests
{
    private const string Document = """
        <UIScript version="1" requires="sidebar">
          <Window width="300" height="200"/>
          <Image name="homeIcon" path="home.png"/>
          <Sidebar name="places" width="100"/>
        </UIScript>
        """;

    private const int FirstRow = 7;
    private const int Row = 26;
    private const int Gap = 9;
    private const int Black = unchecked((int)0xFF000000);
    private const int Key = unchecked((int)0xFF000000) | DesktopIcon.ClearKey;

    private readonly WindowManager manager = new(800, 600);
    private readonly RecordingPainter painter = new(800, 600);
    private readonly List<UIEvent> events = [];
    private readonly DesktopWindow window;
    private readonly UIListState places;

    public SidebarRowsTests()
    {
        UIView view = new(UIScript.Parse(Document));

        // A black square on the key, as AniCraft exports a symbol.
        var pixels = Enumerable.Repeat(Key, 32 * 32).ToArray();
        for (var y = 8; y < 24; y++)
        {
            for (var x = 8; x < 24; x++)
                pixels[(y * 32) + x] = Black;
        }

        view.SetImage("homeIcon", new DesktopImage(32, 32, pixels));
        places = view.List("places")!;
        places.Items.AddRange(["Home", "User", "System"]);
        places.Icons.AddRange(["homeIcon", null, null]);
        places.GroupStarts.Add(2);

        window = new DesktopWindow("Files", view) { EventHandler = (_, uiEvent) => events.Add(uiEvent) };
        manager.Open(window);
        manager.Draw(painter);
    }

    private int Top => window.ContentOrigin.Y;

    [Fact]
    public void ARowsIconIsASymbolInTheTextColorBeforeItsText()
    {
        var (symbol, x, y) = Assert.Single(painter.Images);

        Assert.Equal(16, symbol.Width);
        Assert.Equal(window.X + 12, x);
        Assert.InRange(y, Top + FirstRow, Top + FirstRow + Row - 16);
        Assert.Equal(Pack(DesktopTheme.Text), painter.At(x + 8, y + 8));
    }

    [Fact]
    public void EveryRowsTextLinesUpAfterTheIconsRoom()
    {
        var home = Assert.Single(painter.Texts, text => text.Text == "Home");
        var user = Assert.Single(painter.Texts, text => text.Text == "User");

        Assert.Equal(window.X + 12 + 16 + 8, home.X);
        Assert.Equal(home.X, user.X);
    }

    [Fact]
    public void AFaintLineSplitsTheGroupsAndPushesTheNextOneDown()
    {
        var user = Assert.Single(painter.Texts, text => text.Text == "User");
        var system = Assert.Single(painter.Texts, text => text.Text == "System");

        Assert.Equal(user.Y + Row + Gap, system.Y);
        Assert.Equal(Pack(DesktopTheme.Separator), painter.At(window.X + 50, Top + FirstRow + (2 * Row) + (Gap / 2)));
    }

    [Fact]
    public void ClicksStepOverTheSeparator()
    {
        // On the line itself, nothing; below it, System, the third row.
        Click(window.X + 50, Top + FirstRow + (2 * Row) + (Gap / 2));
        Assert.Equal(-1, places.SelectedIndex);

        Click(window.X + 50, Top + FirstRow + (2 * Row) + Gap + 5);
        Assert.Equal(2, places.SelectedIndex);
        Assert.Contains(new UIEvent(UIEventKind.Selected, "places", Index: 2), events);
    }

    [Fact]
    public void TheSelectionIsRounded()
    {
        Click(window.X + 50, Top + FirstRow + 5);
        manager.Draw(painter);

        var shade = Pack(DesktopTheme.SidebarSelection);
        var rowTop = Top + FirstRow;
        Assert.Equal(shade, painter.At(window.X + 50, rowTop + 2));
        Assert.NotEqual(shade, painter.At(window.X + 6, rowTop));
        Assert.Equal(shade, painter.At(window.X + 6 + 6, rowTop));
    }

    [Fact]
    public void WithoutIconsTextStartsAtTheEdge()
    {
        places.Icons.Clear();
        painter.Texts.Clear();

        manager.Draw(painter);

        Assert.Equal(window.X + 12, Assert.Single(painter.Texts, text => text.Text == "Home").X);
    }

    [Fact]
    public void ScrollingStopsWhereTheLastRowAndItsSeparatorsFit()
    {
        // 9 rows in 3 groups in a window whose sidebar shows 200 - 12 - 2 = 186 pixels: 7 rows, or 6 and the lines.
        places.Items.Clear();
        places.Icons.Clear();
        places.GroupStarts.Clear();
        places.Items.AddRange(["1", "2", "3", "4", "5", "6", "7", "8", "9"]);
        places.GroupStarts.AddRange([3, 6]);

        manager.Update(new DesktopInput(window.X + 50, Top + 50, false, Scroll: 20));
        manager.Draw(painter);

        // From row 4 down: rows 4 to 9 take 6 * 26 = 156, with one line before 7, 165, which fits; row 3 too would
        // need 26 + 9 more, 200, which doesn't.
        Assert.Equal(3, places.FirstVisible);
        Assert.Contains(painter.Texts, text => text.Text == "9");
    }

    private static int Pack(UIColor color) => DesktopImage.Pack(255, color.Red, color.Green, color.Blue);

    private void Click(int x, int y)
    {
        manager.Update(new DesktopInput(x, y, true));
        manager.Update(new DesktopInput(x, y, false));
    }
}
