using Venture.OS.Desktop;
using Venture.OS.Interface;

namespace Venture.OS.Tests.Desktop;

/// <summary>
/// Header bar toolbar items: flat until the pointer's over them, packed at the start or the end, and drawn as symbols
/// in the text's color when they have images. They draw in the palette in use, so they run with the appearance tests.
/// </summary>
[Collection(nameof(AppearanceCollection))]
public sealed class ToolbarTests : IDisposable
{
    private const int Black = unchecked((int)0xFF000000);
    private const int Key = unchecked((int)0xFF000000) | DesktopIcon.ClearKey;

    private const string Document = """
        <UIScript version="1">
          <Window width="400" height="100"/>
          <Image name="addIcon" path="add.png"/>
          <Image name="shareIcon" path="share.png"/>
          <ToolbarItem name="back" title="Back" placement="start"/>
          <ToolbarItem name="add" title="Add" image="addIcon"/>
          <ToolbarItem name="share" title="Share" image="shareIcon"/>
          <ToolbarItem name="remove" title="Remove"/>
        </UIScript>
        """;

    private readonly WindowManager manager = new(800, 600);
    private readonly RecordingPainter painter = new(800, 600);
    private readonly List<UIEvent> events = [];
    private readonly DesktopWindow window;

    public ToolbarTests()
    {
        UIView view = new(UIScript.Parse(Document));

        // A black square on the key, as AniCraft exports a symbol; Share's image is never supplied.
        var pixels = Enumerable.Repeat(Key, 32 * 32).ToArray();
        for (var y = 8; y < 24; y++)
        {
            for (var x = 8; x < 24; x++)
                pixels[(y * 32) + x] = Black;
        }

        view.SetImage("addIcon", new DesktopImage(32, 32, pixels));
        window = new DesktopWindow("Files", view) { EventHandler = (_, uiEvent) => events.Add(uiEvent) };
        manager.Open(window);
        manager.Draw(painter);
    }

    public void Dispose() => DesktopAppearance.Use(DesktopLook.Standard);

    [Fact]
    public void PacksItemsAtTheStartAndTheEndInDocumentOrder()
    {
        var (back, add, share, remove) = (Item(0), Item(1), Item(2), Item(3));

        // Back sits after the buttons; Add, Share, and Remove run to the right end, Remove last.
        Assert.Equal(window.X + 53, (int)back.X);
        Assert.Equal(window.X + window.Width - 8, (int)(remove.X + remove.Width));
        Assert.True(add.X < share.X && share.X < remove.X);
        Assert.Equal((FramePart.ToolbarItem, 0), window.Hit((int)back.X + 3, (int)back.Y + 3));
    }

    [Fact]
    public void TheTitleSitsBetweenTheEnds()
    {
        var title = Assert.Single(painter.Texts, text => text.Text == "Files");

        Assert.True(title.X > Item(0).X + Item(0).Width);
        Assert.True(title.X + 40 < Item(1).X);
    }

    [Fact]
    public void AnImageItemIsASymbolInTheTextColorWithItsTitleAsHoverText()
    {
        var add = Item(1);

        // Square, as tall as the item, with a 16-pixel symbol in its middle, in the text's color, and no title text.
        Assert.Equal(add.Width, add.Height);
        var (symbol, x, y) = Assert.Single(painter.Images);
        Assert.Equal(16, symbol.Width);
        Assert.Equal(((int)add.X + (((int)add.Width - 16) / 2), (int)add.Y + (((int)add.Height - 16) / 2)), (x, y));
        Assert.Equal(Pack(DesktopTheme.Text), painter.At(x + 8, y + 8));
        Assert.DoesNotContain(painter.Texts, text => text.Text == "Add");
        Assert.Equal("Add", window.TooltipAt((int)add.X + 3, (int)add.Y + 3));
    }

    [Fact]
    public void AnItemWhoseImageIsntSuppliedShowsItsTitle()
    {
        Assert.Contains(painter.Texts, text => text.Text == "Share");
        Assert.Null(window.TooltipAt((int)Item(2).X + 3, (int)Item(2).Y + 3));
    }

    [Fact]
    public void ItemsAreFlatUntilThePointerIsOverThem()
    {
        var remove = Item(3);
        var (x, y) = ((int)remove.X + 3, (int)remove.Y + ((int)remove.Height / 2));
        var flat = painter.At(x, y);

        manager.Update(new DesktopInput(x + 10, y, false));
        manager.Draw(painter);

        Assert.NotEqual(Pack(DesktopTheme.ButtonHover), flat);
        Assert.Equal(Pack(DesktopTheme.ButtonHover), painter.At(x, y));
    }

    [Fact]
    public void ADisabledItemsSymbolIsDimmed()
    {
        window.View.SetDisabled(true, "add");
        painter.Images.Clear();

        manager.Draw(painter);

        var (_, x, y) = Assert.Single(painter.Images);
        Assert.Equal(Pack(DesktopTheme.DisabledText), painter.At(x + 8, y + 8));
    }

    [Fact]
    public void TheSymbolFollowsDarkMode()
    {
        DesktopTheme.Use(DesktopPalette.Create(DesktopPalette.StandardAccent, isDark: true));
        painter.Images.Clear();

        manager.Draw(painter);

        var (_, x, y) = Assert.Single(painter.Images);
        Assert.Equal(Pack(DesktopTheme.Text), painter.At(x + 8, y + 8));
    }

    [Fact]
    public void AnImageItemStillReportsPresses()
    {
        var add = Item(1);
        var (x, y) = ((int)add.X + 3, (int)add.Y + 3);

        manager.Update(new DesktopInput(x, y, true));
        manager.Update(new DesktopInput(x, y, false));

        Assert.Equal([new UIEvent(UIEventKind.Pressed, "add")], events);
    }

    private UIRect Item(int index) => window.ToolbarItemBounds(index);

    private static int Pack(UIColor color) => DesktopImage.Pack(255, color.Red, color.Green, color.Blue);
}
