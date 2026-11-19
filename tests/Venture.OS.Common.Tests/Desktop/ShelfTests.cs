using Venture.OS.Desktop;
using Venture.OS.Interface;

namespace Venture.OS.Tests.Desktop;

/// <summary>The shelf on its own, and as a <c>&lt;Shelf&gt;</c> control in a view.</summary>
public sealed class ShelfTests
{
    private const string Document = """
        <UIScript requires="shelf">
          <Window width="400" height="72"/>
          <Shelf name="apps" x="0" y="0" width="400" height="72"/>
        </UIScript>
        """;

    [Fact]
    public void IsAsWideAsItsIconsWithRoomForDividers()
    {
        Shelf shelf = new();
        Assert.Equal(0, shelf.Width);

        shelf.SetItems([new("Files"), new("Terminal")]);
        var twoInOneGroup = shelf.Width;
        shelf.SetItems([new("Files"), new("Terminal", Group: 1)]);

        Assert.Equal((2 * Shelf.IconSize) + 10 + 16, twoInOneGroup);
        Assert.Equal(twoInOneGroup + 12, shelf.Width);
    }

    [Fact]
    public void FindsTheIconAtAPointWhereverItIsPlaced()
    {
        Shelf shelf = new();
        shelf.SetItems([new("Files"), new("Notes", Group: 1)]);
        shelf.Place(100, 500);

        var second = shelf.IconBounds(1);

        Assert.Equal(0, shelf.IndexAt(100 + 8 + 1, 500 + 8 + 1));
        Assert.Equal(1, shelf.IndexAt((int)second.X + 1, (int)second.Y + 1));
        Assert.Equal(-1, shelf.IndexAt(99, 510));
        Assert.Equal(-1, shelf.IndexAt((int)second.X - 3, (int)second.Y + 1));
    }

    [Fact]
    public void NamesTheHoveredIconAboveIt()
    {
        Shelf shelf = new();
        shelf.SetItems([new("Files"), new("Notes", IsOpen: true)]);
        shelf.Place(0, 500);
        RecordingPainter painter = new(800, 600);

        shelf.Draw(painter, hovered: 1, pressed: -1, 800);

        var label = Assert.Single(painter.Texts, text => text.Text == "Notes");
        Assert.True(label.Y < 500);
    }

    [Fact]
    public void AViewDrawsItsShelfFromItsFrame()
    {
        UIView view = new(UIScript.Parse(Document));
        view.Shelf("apps")!.SetItems([new("Files"), new("Notes")]);
        RecordingPainter painter = new(800, 600);

        view.Draw(painter, 200, 300);

        Assert.Equal(new UIRect(200, 300, view.Shelf("apps")!.Width, Shelf.Height), view.Shelf("apps")!.Bounds);
        Assert.Equal(view.Shelf("apps")!.Width, view.ShelfWidth);
        Assert.Contains(painter.Texts, text => text.Text == "F");
        Assert.Null(view.Shelf("missing"));
    }

    [Fact]
    public void AClickOnAnIconIsPressedWithItsIndex()
    {
        UIView view = new(UIScript.Parse(Document));
        view.Shelf("apps")!.SetItems([new("Files"), new("Notes")]);
        view.Draw(new RecordingPainter(800, 600), 0, 0);
        var icon = view.Shelf("apps")!.IconBounds(1);
        var (x, y) = ((int)icon.X + 5, (int)icon.Y + 5);

        view.Update(new UIViewInput(x, y, true, true, false, 0, []), 0, 0);
        var events = view.Update(new UIViewInput(x, y, false, false, true, 0, []), 0, 0);

        var pressed = Assert.Single(events);
        Assert.Equal(new UIEvent(UIEventKind.Pressed, "apps", "Notes", Index: 1), pressed);
    }

    [Fact]
    public void AClickThatLeavesTheIconPressesNothing()
    {
        UIView view = new(UIScript.Parse(Document));
        view.Shelf("apps")!.SetItems([new("Files"), new("Notes")]);
        view.Draw(new RecordingPainter(800, 600), 0, 0);
        var (first, second) = (view.Shelf("apps")!.IconBounds(0), view.Shelf("apps")!.IconBounds(1));

        view.Update(new UIViewInput((int)first.X + 5, (int)first.Y + 5, true, true, false, 0, []), 0, 0);
        var events = view.Update(new UIViewInput((int)second.X + 5, (int)second.Y + 5, false, false, true, 0, []), 0, 0);

        Assert.Empty(events);
    }
}
