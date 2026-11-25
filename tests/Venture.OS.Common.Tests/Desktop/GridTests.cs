using Venture.OS.Desktop;
using Venture.OS.Interface;

namespace Venture.OS.Tests.Desktop;

/// <summary>
/// A grid 200 by 100 of 40-by-30 cells, 8 apart, inside a 3-pixel margin for the ring a chosen item gets. Four cells fit
/// across, so cell (column, row) starts at (3 + 48 × column, 3 + 60 × row): each row is a cell, a 22-pixel title under
/// it at the default size, and the spacing. One row shows at a time.
/// </summary>
public sealed class GridTests
{
    private static string Document(string items, string titles = "below") => $"""
        <UIScript>
          <Window width="300" height="200"/>
          <String name="tip" table="1" index="1"/>
          <Image name="sky" path="sky.png"/>
          <Grid name="g" x="0" y="0" width="200" height="100" itemWidth="40" itemHeight="30" titles="{titles}">
            {items}
          </Grid>
        </UIScript>
        """;

    private const string ThreeItems = """
        <Item name="one" title="One" color="#FF0000"/>
        <Item name="two" title="Two" color="#00FF00" tooltip="tip"/>
        <Item name="three" title="Three" image="sky"/>
        """;

    private UIView view = new(UIScript.Parse(Document(ThreeItems)), resource => resource.Name == "tip" ? "Second" : null);

    private UIGridState Grid => view.Grid("g")!;

    [Fact]
    public void StartsWithTheDocumentsItemsAndNoneChosen()
    {
        Assert.Equal(["One", "Two", "Three"], Grid.Items.Select(item => item.Title));
        Assert.Equal(new UIColor(255, 0, 0), Grid.Items[0].Color);
        Assert.Equal("sky", Grid.Items[2].Image);
        Assert.Equal("Second", Grid.Items[1].Tooltip);
        Assert.Equal(-1, Grid.SelectedIndex);
    }

    [Fact]
    public void AClickChoosesAnItemAndReportsItsIndexAndName()
    {
        var events = Click(3 + 48 + 10, 10);

        Assert.Equal([new UIEvent(UIEventKind.Selected, "g", Index: 1, Item: "two")], events);
        Assert.Equal(1, Grid.SelectedIndex);

        // The title under a cell is the item's too; choosing it again reports nothing new.
        Assert.Empty(Click(3 + 48 + 10, 3 + 30 + 10, seconds: 5));
    }

    [Fact]
    public void AClickBetweenCellsOrPastTheLastItemChoosesNothing()
    {
        Assert.Empty(Click(3 + 40 + 4, 10));
        Assert.Empty(Click(3 + (3 * 48) + 10, 10));
        Assert.Equal(-1, Grid.SelectedIndex);
    }

    [Fact]
    public void ASecondClickSoonAfterOpensTheItem()
    {
        Click(10, 10, seconds: 1);
        var events = Click(10, 10, seconds: 1.2);

        Assert.Equal([new UIEvent(UIEventKind.Submitted, "g", "One", Index: 0, Item: "one")], events);
    }

    [Fact]
    public void ItemsWrapIntoRowsThatScrollWithAClassicBarTakingRoomFromThem()
    {
        Fill(10);

        // Ten items take three rows of four; they don't fit, so the bar takes 15 pixels and three fit across.
        Assert.Empty(Click(3 + (3 * 48) + 10, 10));
        view.Update(new UIViewInput(50, 50, false, false, false, 1, []), 0, 0);
        Assert.Equal(1, Grid.FirstRow);
        Assert.Equal([new UIEvent(UIEventKind.Selected, "g", Index: 3, Item: "item4")], Click(10, 10));

        // The bar's down arrow scrolls a row; the grid can scroll to its fourth and last row.
        Click(192, 95);
        Click(192, 95);
        Assert.Equal(3, Grid.FirstRow);
    }

    [Fact]
    public void AnAutomaticBarTakesNoRoomFromTheItems()
    {
        view.ScrollBars = ScrollBarStyle.Automatic;
        Fill(10);

        Assert.Equal([new UIEvent(UIEventKind.Selected, "g", Index: 3, Item: "item4")], Click(3 + (3 * 48) + 10, 10));
    }

    [Fact]
    public void ItemsTheOwnerReplacesKeepTheChoiceInRange()
    {
        Grid.SelectedIndex = 2;
        Grid.FirstRow = 5;

        Grid.Items.RemoveRange(1, 2);
        view.Update(new UIViewInput(0, 0, false, false, false, 0, []), 0, 0);

        Assert.Equal(-1, Grid.SelectedIndex);
        Assert.Equal(0, Grid.FirstRow);
    }

    [Theory]
    [InlineData("below", new[] { "One", "Two", "Three" })]
    [InlineData("selected", new[] { "Two" })]
    [InlineData("none", new string[0])]
    public void TitlesShowAsTheGridSays(string titles, string[] shown)
    {
        view = new UIView(UIScript.Parse(Document(ThreeItems, titles)));
        Grid.SelectedIndex = 1;

        Assert.Equal(shown, Drawn().Texts.Select(text => text.Text));
    }

    [Fact]
    public void TheChosenTitleIsInTheTextColorAndTheRestQuieter()
    {
        Grid.SelectedIndex = 1;

        var texts = Drawn().Texts;

        Assert.Equal(DesktopTheme.Text, texts.Single(text => text.Text == "Two").Color);
        Assert.Equal(DesktopTheme.SecondaryText, texts.Single(text => text.Text == "One").Color);
    }

    [Fact]
    public void AnItemsTooltipShowsOverIt_AndWithoutTitlesItsTitleDoes()
    {
        Assert.Equal("Second", view.TooltipAt(3 + 48 + 10, 10));
        Assert.Null(view.TooltipAt(10, 10));

        view = new UIView(UIScript.Parse(Document(ThreeItems, "none")));
        Assert.Equal("One", view.TooltipAt(10, 10));
    }

    [Fact]
    public void ColorsAreCirclesAndTheChosenOneIsRingedInTheAccent()
    {
        Grid.SelectedIndex = 0;
        var painter = Drawn();

        // The first cell's circle is 30 across, centred in the 40-by-30 cell from (3, 3): its middle is (23, 18).
        Assert.Equal(Pixel(new UIColor(255, 0, 0)), painter.At(23, 18));
        Assert.Equal(painter.At(250, 150), painter.At(3, 3));
        Assert.Equal(Pixel(DesktopTheme.Accent), painter.At(23, 3 - 2));
        Assert.NotEqual(Pixel(DesktopTheme.Accent), painter.At(3 + 48 + 20, 3 - 2));
    }

    [Fact]
    public void PicturesAreScaledToTheirCells_AndOneNotSuppliedShowsTheMissingOutline()
    {
        Assert.Equal(Pixel(DesktopTheme.MissingImage), Drawn().At(3 + 96, 3));

        view.SetImage("sky", new DesktopImage(2, 2, [.. Enumerable.Repeat(unchecked((int)0xFF0000FF), 4)]));
        var painter = Drawn();

        var (image, x, y) = Assert.Single(painter.Images);
        Assert.Equal((40, 30, 3 + 96, 3), (image.Width, image.Height, x, y));
    }

    [Fact]
    public void AnOwnersPictureWinsOverTheDocumentsImage()
    {
        DesktopImage picture = new(4, 4, [.. Enumerable.Repeat(unchecked((int)0xFF00FF00), 16)]);
        Grid.Items[2] = Grid.Items[2] with { Picture = picture };

        Assert.Equal(Pixel(new UIColor(0, 255, 0)), Drawn().At(3 + 96 + 20, 15));
    }

    [Fact]
    public void APlainTileShowsItsTitleInside()
    {
        view = new UIView(UIScript.Parse(Document("""<Item title="Auto"/>""")));

        var text = Assert.Single(Drawn().Texts);
        Assert.Equal("Auto", text.Text);
        Assert.InRange(text.Y, 3, 3 + 30);
    }

    private void Fill(int count)
    {
        Grid.Items.Clear();
        for (var item = 1; item <= count; item++)
            Grid.Items.Add(new UIGridEntry($"Item {item}", $"item{item}", Color: new UIColor(0, 0, 0)));
    }

    private IReadOnlyList<UIEvent> Click(int x, int y, double seconds = 0)
    {
        var events = view.Update(new UIViewInput(x, y, true, true, false, 0, [], TimeSpan.FromSeconds(seconds)), 0, 0);
        view.Update(new UIViewInput(x, y, false, false, true, 0, [], TimeSpan.FromSeconds(seconds)), 0, 0);
        return events;
    }

    private RecordingPainter Drawn()
    {
        RecordingPainter painter = new(300, 200);
        view.Draw(painter, 0, 0);
        return painter;
    }

    private static int Pixel(UIColor color) => DesktopBackground.Pack(color.Red, color.Green, color.Blue);
}
