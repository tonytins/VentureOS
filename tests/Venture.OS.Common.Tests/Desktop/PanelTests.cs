using Venture.OS.Desktop;
using Venture.OS.Interface;

namespace Venture.OS.Tests.Desktop;

/// <summary>Panels in the window manager: pinned, unfocused, above, and the work area they leave.</summary>
public sealed class PanelTests
{
    private readonly WindowManager windows = new(1000, 800) { TopInset = 28 };

    [Fact]
    public void ABottomPanelIsAsWideAsItsShelfAndCentredAlongTheBottom()
    {
        var dock = Dock();
        dock.View.Shelf("apps")!.SetItems([new("Files"), new("Notes")]);

        windows.Open(dock);

        Assert.Equal(dock.View.Shelf("apps")!.Width, dock.Width);
        Assert.Equal(((1000 - dock.Width) / 2, 800 - 72), (dock.X, dock.Y));
        Assert.Equal(0, dock.BarHeight);
        Assert.False(dock.IsClosable);
    }

    [Fact]
    public void ABottomPanelFollowsItsShelfAsItFills()
    {
        var dock = Dock();
        windows.Open(dock);
        var empty = dock.Width;

        dock.View.Shelf("apps")!.SetItems([new("Files"), new("Notes"), new("Terminal")]);
        windows.Update(new DesktopInput(0, 0, false));

        Assert.Equal(0, empty);
        Assert.Equal(dock.View.Shelf("apps")!.Width, dock.Width);
        Assert.Equal((1000 - dock.Width) / 2, dock.X);
    }

    [Fact]
    public void ATopPanelSpansTheScreen()
    {
        var bar = Bar();

        windows.Open(bar);

        Assert.Equal(new UIRect(0, 0, 1000, 24), bar.Bounds);
    }

    [Fact]
    public void APanelNeverTakesFocusOrTheFront()
    {
        var notes = Window("Notes");
        windows.Open(notes);
        var dock = Dock();
        dock.View.Shelf("apps")!.SetItems([new("Files")]);

        windows.Open(dock);
        var icon = dock.View.Shelf("apps")!.IconBounds(0);
        windows.Draw(new RecordingPainter(1000, 800));
        windows.Update(new DesktopInput((int)icon.X + 5, (int)icon.Y + 5, true));
        windows.Update(new DesktopInput((int)icon.X + 5, (int)icon.Y + 5, false));

        Assert.Same(notes, windows.Focused);
        Assert.False(windows.Minimize(dock));
        Assert.False(windows.Zoom(dock));
    }

    [Fact]
    public void APressOnAPanelReachesItsView()
    {
        List<UIEvent> pressed = [];
        var dock = Dock();
        dock.EventHandler = (_, uiEvent) => pressed.Add(uiEvent);
        dock.View.Shelf("apps")!.SetItems([new("Files"), new("Notes")]);
        windows.Open(dock);
        windows.Draw(new RecordingPainter(1000, 800));
        var icon = dock.View.Shelf("apps")!.IconBounds(1);

        windows.Update(new DesktopInput((int)icon.X + 5, (int)icon.Y + 5, true));
        windows.Update(new DesktopInput((int)icon.X + 5, (int)icon.Y + 5, false));

        Assert.Equal([new UIEvent(UIEventKind.Pressed, "apps", "Notes", Index: 1)], pressed);
    }

    [Fact]
    public void APanelIsFoundAndDrawnAboveWindows()
    {
        var notes = Window("Notes", height: 700);
        windows.Open(notes);
        var dock = Dock();
        dock.View.Shelf("apps")!.SetItems([new("Files")]);
        windows.Open(dock);
        notes.Y = 400;
        RecordingPainter painter = new(1000, 800);

        windows.Draw(painter);

        Assert.Same(dock, windows.WindowAt(dock.X + 10, dock.Y + 10));
        var titleAt = painter.Texts.FindIndex(text => text.Text == "Notes");
        var initialAt = painter.Texts.FindIndex(text => text.Text == "F");
        Assert.True(titleAt < initialAt, "The panel was drawn before the window under it.");
    }

    [Fact]
    public void TheWorkAreaRunsBetweenThePanels()
    {
        Assert.Equal(new UIRect(0, 28, 1000, 772), windows.WorkArea);

        windows.Open(Bar());
        var dock = Dock();
        dock.View.Shelf("apps")!.SetItems([new("Files")]);
        windows.Open(dock);

        Assert.Equal(new UIRect(0, 28, 1000, 800 - 72 - 28), windows.WorkArea);
        windows.TopInset = 0;
        Assert.Equal(new UIRect(0, 24, 1000, 800 - 72 - 24), windows.WorkArea);
    }

    [Fact]
    public void ZoomFillsTheWorkAreaAndNewWindowsOpenInIt()
    {
        var dock = Dock();
        dock.View.Shelf("apps")!.SetItems([new("Files")]);
        windows.Open(dock);
        var big = Window("Big", resizable: true, height: 900);

        windows.Open(big);
        Assert.True(big.Y >= 28);
        windows.Zoom(big);

        Assert.Equal(new UIRect(0, 28, 1000, 800 - 72 - 28), big.Bounds);
    }

    [Fact]
    public void AWindowCanBeDraggedOverTheDockButNotAboveTheTop()
    {
        var dock = Dock();
        dock.View.Shelf("apps")!.SetItems([new("Files")]);
        windows.Open(dock);
        var notes = Window("Notes");
        windows.Open(notes);
        var (grabX, grabY) = (notes.X + 100, notes.Y + 10);

        windows.Update(new DesktopInput(grabX, grabY, true));
        windows.Update(new DesktopInput(grabX, 780, true));
        var low = notes.Y;
        windows.Update(new DesktopInput(grabX, 0, true));
        windows.Update(new DesktopInput(grabX, 0, false));

        Assert.True(low > 800 - 72, $"The window stopped at {low}.");
        Assert.Equal(28, notes.Y);
    }

    private static DesktopWindow Dock() => new("Dock", new UIView(UIScript.Parse("""
        <UIScript requires="shelf">
          <Panel width="400" height="72" edge="bottom"/>
          <Shelf name="apps" x="0" y="0" width="400" height="72"/>
        </UIScript>
        """)));

    private static DesktopWindow Bar() =>
        new("Bar", new UIView(UIScript.Parse("""<UIScript><Panel width="300" height="24" edge="top"/></UIScript>""")));

    private static DesktopWindow Window(string title, bool resizable = false, int height = 200) =>
        new(title, new UIView(UIScript.Parse($"""<UIScript><Window width="300" height="{height}" minWidth="100" minHeight="100" resizable="{(resizable ? "true" : "false")}"/></UIScript>""")));
}
