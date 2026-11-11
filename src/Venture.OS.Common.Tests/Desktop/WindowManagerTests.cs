using Venture.OS.Desktop;
using Venture.OS.Interface;

namespace Venture.OS.Tests.Desktop;

public sealed class WindowManagerTests
{
    private const string Small = """
        <UIScript><Window width="200" height="100"/>
        <Button name="ok" x="10" y="10" width="60" height="20" text="OK"/>
        <TextField name="field" x="10" y="40" width="100" height="20"/></UIScript>
        """;

    private readonly WindowManager manager = new(800, 600);

    [Fact]
    public void OpensWindowsCenteredAndCascaded()
    {
        var first = Window("One");
        var second = Window("Two");
        manager.Open(first);
        manager.Open(second);

        Assert.Equal(((800 - 200) / 2, ((600 - 128) / 2) - 24), (first.X, first.Y));
        Assert.Equal((first.X + 24, first.Y + 24), (second.X, second.Y));
        Assert.Same(second, manager.Focused);
    }

    [Fact]
    public void AClickBringsAWindowToTheFrontAndTakesItsFocus()
    {
        var back = Window("Back");
        var front = Window("Front");
        manager.Open(back);
        manager.Open(front);
        front.View.Focus("field");

        Press(back.X + 5, back.Y + 40);

        Assert.Same(back, manager.Focused);
        Assert.Null(front.View.FocusedControl);
    }

    [Fact]
    public void DragsAWindowByItsTitleBar()
    {
        var window = Window("Drag me");
        manager.Open(window);
        var (startX, startY) = (window.X, window.Y);

        Press(window.X + 100, window.Y + 10);
        manager.Update(new DesktopInput(startX + 100 + 30, startY + 10 + 20, true));
        manager.Update(new DesktopInput(startX + 130, startY + 30, false));

        Assert.Equal((startX + 30, startY + 20), (window.X, window.Y));
    }

    [Fact]
    public void KeepsTheTitleBarOnTheScreen()
    {
        manager.TopInset = 30;
        var window = Window("Away");
        manager.Open(window);

        Press(window.X + 100, window.Y + 10);
        manager.Update(new DesktopInput(-5000, -5000, true));

        Assert.Equal(30, window.Y);
        Assert.Equal(40 - window.Width, window.X);
    }

    [Fact]
    public void ClosesAWindowFromItsCloseButtonOnlyWhenReleasedOnIt()
    {
        var window = Window("Closable");
        manager.Open(window);
        var (x, y) = ((int)window.CloseButton.X + 6, (int)window.CloseButton.Y + 6);

        Press(x, y);
        Assert.Empty(manager.Update(new DesktopInput(x + 200, y + 50, false)));
        Assert.Same(window, manager.Focused);

        Press(x, y);
        var closed = manager.Update(new DesktopInput(x, y, false));

        Assert.Equal([new WindowEvent(window, null, Closed: true)], closed);
        Assert.Empty(manager.Windows);
    }

    [Fact]
    public void PassesAViewsEventsOnAndToItsHandler()
    {
        var window = Window("Events");
        List<UIEvent> handled = [];
        window.EventHandler = (_, uiEvent) => handled.Add(uiEvent);
        manager.Open(window);
        var (originX, originY) = window.ContentOrigin;

        Press(originX + 20, originY + 15);
        var happened = manager.Update(new DesktopInput(originX + 20, originY + 15, false));

        Assert.Equal([new WindowEvent(window, new UIEvent(UIEventKind.Pressed, "ok"))], happened);
        Assert.Equal([new UIEvent(UIEventKind.Pressed, "ok")], handled);
    }

    [Fact]
    public void SendsKeysToTheWindowInFront()
    {
        var back = Window("Back");
        var front = Window("Front");
        manager.Open(back);
        manager.Open(front);
        back.View.Focus("field");
        front.View.Focus("field");

        manager.Update(new DesktopInput(0, 0, false, Keys: [KeyPress.Typed('a')]));

        Assert.Equal("", back.View.Text("field"));
        Assert.Equal("a", front.View.Text("field"));
    }

    [Fact]
    public void FadesWindowsThatArentInFront()
    {
        var back = Window("Back");
        var front = Window("Front");
        manager.Open(back);
        manager.Open(front);
        RecordingPainter painter = new(800, 600);

        manager.Draw(painter);

        var (frontX, frontY) = front.ContentOrigin;
        Assert.Equal(DesktopBackground.Pack(240, 243, 247), painter.At(frontX + 150, frontY + 80));
        // The back window's corner sticks out from under the front one, faded toward white.
        var backPanel = painter.At(back.X + 2, back.Y + 40);
        Assert.True((backPanel & 0xFF) > 247, "The back window should draw faded.");
    }

    [Fact]
    public void DrawsATitleAndARedCloseButton()
    {
        var window = Window("About VentureOS");
        manager.Open(window);
        RecordingPainter painter = new(800, 600);

        manager.Draw(painter);

        Assert.Contains(painter.Texts, text => text.Text == "About VentureOS");
        var center = (X: (int)(window.CloseButton.X + 6), Y: (int)(window.CloseButton.Y + 6));
        Assert.Equal(DesktopBackground.Pack(236, 95, 86), painter.At(center.X, center.Y));
    }

    private DesktopWindow Window(string title) => new(title, new UIView(UIScript.Parse(Small)));

    private void Press(int x, int y)
    {
        manager.Update(new DesktopInput(x, y, false));
        manager.Update(new DesktopInput(x, y, true));
    }
}
