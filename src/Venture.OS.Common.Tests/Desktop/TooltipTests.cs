using Venture.OS.Desktop;
using Venture.OS.Interface;

namespace Venture.OS.Tests.Desktop;

/// <summary>
/// Hover text: a control's <c>tooltip</c> shows after the pointer rests on it for half a second, above every window,
/// and hides when the pointer moves or presses.
/// </summary>
public sealed class TooltipTests
{
    private const string Document = """
        <UIScript>
          <Window width="300" height="100"/>
          <String name="saveTip" table="1" index="1"/>
          <String name="lostTip" table="1" index="9"/>
          <Button name="save" x="10" y="10" width="80" height="24" text="Save" tooltip="saveTip"/>
          <Button name="lost" x="100" y="10" width="80" height="24" text="Lost" tooltip="lostTip"/>
          <Button name="plain" x="190" y="10" width="80" height="24" text="Plain"/>
        </UIScript>
        """;

    private readonly WindowManager manager = new(800, 600);
    private readonly DesktopWindow window;
    private readonly int top;
    private readonly int left;

    public TooltipTests()
    {
        UIView view = new(UIScript.Parse(Document), resource => resource.Index == 1 ? "Save the file" : null);
        window = new DesktopWindow("Window", view);
        manager.Open(window);
        (left, top) = window.ContentOrigin;
    }

    [Fact]
    public void ShowsAfterThePointerRests()
    {
        Rest(left + 20, top + 20, 0);
        Rest(left + 20, top + 20, 0.4);
        Assert.Null(manager.Tooltip);

        Rest(left + 20, top + 20, 0.5);

        Assert.Equal("Save the file", manager.Tooltip);
    }

    [Fact]
    public void HidesWhenThePointerMovesOrPresses()
    {
        Rest(left + 20, top + 20, 0);
        Rest(left + 20, top + 20, 1);
        Rest(left + 21, top + 20, 1.1);
        Assert.Null(manager.Tooltip);

        Rest(left + 21, top + 20, 2);
        Assert.NotNull(manager.Tooltip);
        manager.Update(new DesktopInput(left + 21, top + 20, true, Time: TimeSpan.FromSeconds(2.1)));
        Assert.Null(manager.Tooltip);
    }

    [Fact]
    public void MissingTextShowsItsTableAndIndex()
    {
        Rest(left + 120, top + 20, 0);
        Rest(left + 120, top + 20, 1);

        Assert.Equal("1:9", manager.Tooltip);
        Assert.Contains("lostTip", window.View.MissingStrings);
    }

    [Fact]
    public void NothingShowsWithoutATooltipOrOverTheDesktopsOwnBars()
    {
        Rest(left + 200, top + 20, 0);
        Rest(left + 200, top + 20, 1);
        Assert.Null(manager.Tooltip);

        manager.ShowsHover = false;
        Rest(left + 20, top + 20, 2);
        Rest(left + 20, top + 20, 3);
        Assert.Null(manager.Tooltip);
    }

    [Fact]
    public void IsDrawnBelowThePointerAndKeptOnTheScreen()
    {
        RecordingPainter painter = new(800, 600);
        Rest(left + 20, top + 20, 0);
        Rest(left + 20, top + 20, 1);

        manager.DrawTooltip(painter);

        var text = Assert.Single(painter.Texts);
        Assert.Equal("Save the file", text.Text);
        Assert.True(text.X > left + 20 && text.Y > top + 20);
        Assert.Equal(DesktopTheme.Text, text.Color);
    }

    [Fact]
    public void TheDesktopRedrawsWhenItShows()
    {
        DesktopSession session = new(800, 600);
        var shown = new DesktopWindow("Window", new UIView(UIScript.Parse(Document), _ => "Tip"));
        session.Windows.Open(shown);
        var (x, y) = (shown.ContentOrigin.X + 20, shown.ContentOrigin.Y + 20);
        session.Update(new DesktopInput(x, y, false, Time: TimeSpan.Zero));
        session.Draw(new RecordingPainter(800, 600));
        Assert.False(session.Update(new DesktopInput(x, y, false, Time: TimeSpan.FromSeconds(0.2))));

        Assert.True(session.Update(new DesktopInput(x, y, false, Time: TimeSpan.FromSeconds(0.6))));
        session.Draw(new RecordingPainter(800, 600));
        Assert.False(session.Update(new DesktopInput(x, y, false, Time: TimeSpan.FromSeconds(0.7))));
    }

    private void Rest(int x, int y, double seconds) =>
        manager.Update(new DesktopInput(x, y, false, Time: TimeSpan.FromSeconds(seconds)));
}
