using Venture.OS.Desktop;
using Venture.OS.Interface;

namespace Venture.OS.Tests.Desktop;

/// <summary>
/// The bars lists, text areas, and the sidebar draw for themselves. Rows are 20 pixels at the default size, so the
/// 102-pixel list shows five of its twenty items, and can scroll to item 15. Its classic bar runs down x 84 to 99:
/// an up arrow at y 1 to 16, a down arrow at 86 to 101, and an 18-pixel thumb on the track between.
/// </summary>
public sealed class ScrollBarTests
{
    private const string Document = """
        <UIScript>
          <Window width="300" height="200"/>
          <List name="picks" x="0" y="0" width="100" height="102" selectable="true"/>
          <TextArea name="notes" x="120" y="0" width="100" height="50"/>
          <TextField name="field" x="120" y="100" width="100" height="22"/>
          <List name="short" x="230" y="0" width="60" height="102" selectable="true"/>
        </UIScript>
        """;

    private readonly UIView view = new(UIScript.Parse(Document));

    public ScrollBarTests()
    {
        var picks = view.List("picks")!;
        for (var item = 0; item < 20; item++)
            picks.Items.Add($"Item {item}");
        view.List("short")!.Items.AddRange(["one", "two"]);
    }

    private UIListState Picks => view.List("picks")!;

    [Fact]
    public void TheArrowsScrollARowAndTheTrackAPage()
    {
        Press(90, 95);
        Assert.Equal(1, Picks.FirstVisible);

        Press(90, 5);
        Assert.Equal(0, Picks.FirstVisible);

        // Below the thumb, a page: one fewer than the rows showing, so the last row stays in sight.
        Press(90, 80);
        Assert.Equal(4, Picks.FirstVisible);
    }

    [Fact]
    public void AHeldArrowRepeatsAfterAPause()
    {
        Down(90, 95, pressed: true, seconds: 0);
        Down(90, 95, pressed: false, seconds: 0.3);
        Assert.Equal(1, Picks.FirstVisible);

        Down(90, 95, pressed: false, seconds: 0.36);
        Down(90, 95, pressed: false, seconds: 0.42);
        Assert.Equal(3, Picks.FirstVisible);

        Release(90, 95, seconds: 0.5);
        Down(90, 95, pressed: false, seconds: 1);
        Assert.Equal(3, Picks.FirstVisible);
    }

    [Fact]
    public void DraggingTheThumbScrollsWithIt()
    {
        Down(90, 20, pressed: true);
        Down(90, 200, pressed: false);

        Assert.Equal(15, Picks.FirstVisible);

        Down(90, 0, pressed: false);
        Assert.Equal(0, Picks.FirstVisible);
    }

    [Fact]
    public void APressOnTheBarIsntAPressOnTheRowBesideIt()
    {
        Assert.Empty(Press(90, 30));
        Assert.Equal(-1, Picks.SelectedIndex);

        Assert.Equal([new UIEvent(UIEventKind.Selected, "picks", Index: 1)], Press(40, 30));
    }

    [Fact]
    public void AListThatFitsHasNoBar()
    {
        // Where its bar's up arrow would be, the press picks the row.
        Assert.Equal([new UIEvent(UIEventKind.Selected, "short", Index: 0)], Press(285, 5));
    }

    [Fact]
    public void APressOnABarLeavesTheKeyboardWhereItWas()
    {
        Press(130, 110);
        Assert.Equal("field", view.FocusedControl);

        Press(90, 95);

        Assert.Equal("field", view.FocusedControl);
    }

    [Fact]
    public void AClassicBarNarrowsTheRowsAndDrawsInTheAccent()
    {
        RecordingPainter painter = new(300, 200);
        Picks.SelectedIndex = 0;

        view.Draw(painter, 0, 0);

        // The selection stops at the bar, and the thumb is in the accent's blend, not the list's background.
        Assert.Equal(painter.At(82, 5), painter.At(10, 5));
        Assert.NotEqual(painter.At(85, 5), painter.At(10, 5));
        Assert.NotEqual(painter.At(50, 60), painter.At(92, 25));
    }

    // Five ten-letter words, one to a line, in an area that shows two lines. At 8 pixels a character, the 92 pixels
    // inside the insets hold a word, and the 77 beside a classic bar hold 9 of its letters.
    private const string Words = "aaaaaaaaaa aaaaaaaaaa aaaaaaaaaa aaaaaaaaaa aaaaaaaaaa";

    [Fact]
    public void TextWrapsShortOfAClassicBarOnceItDoesntFit()
    {
        view.SetText("notes", Words);

        var lines = NotesLines();

        Assert.Equal(2, lines.Count);
        Assert.All(lines, line => Assert.True(line.Length <= 9, line));
    }

    [Fact]
    public void AnAutomaticBarTakesNoRoom()
    {
        view.ScrollBars = ScrollBarStyle.Automatic;
        view.SetText("notes", Words);

        Assert.Equal(["aaaaaaaaaa", "aaaaaaaaaa"], NotesLines());
    }

    [Fact]
    public void AnAutomaticBarShowsWhileItScrollsAndForAMomentAfter()
    {
        view.ScrollBars = ScrollBarStyle.Automatic;
        Assert.False(view.TickScrollBars(TimeSpan.Zero));
        Assert.Equal(painterWithout(), Drawn().At(94, 20));

        view.Update(new UIViewInput(40, 40, false, false, false, 2, [], TimeSpan.FromSeconds(1)), 0, 0);

        Assert.True(view.TickScrollBars(TimeSpan.FromSeconds(1.5)));
        Assert.NotEqual(painterWithout(), Drawn().At(94, 20));

        // The frame after it hides is drawn too, so the bar doesn't stay on the screen.
        Assert.True(view.TickScrollBars(TimeSpan.FromSeconds(2.1)));
        Assert.Equal(painterWithout(), Drawn().At(94, 20));
        Assert.False(view.TickScrollBars(TimeSpan.FromSeconds(2.2)));

        int painterWithout() => Drawn().At(50, 10);
    }

    [Fact]
    public void AnAutomaticBarShowsAsThePointerMovesOverItsList()
    {
        view.ScrollBars = ScrollBarStyle.Automatic;
        view.TickScrollBars(TimeSpan.FromSeconds(5));

        view.SetHover((40, 40));

        Assert.True(view.TickScrollBars(TimeSpan.FromSeconds(5.5)));
        view.SetHover((250, 150));
        Assert.True(view.TickScrollBars(TimeSpan.FromSeconds(6.1)));
        Assert.False(view.TickScrollBars(TimeSpan.FromSeconds(6.2)));
    }

    [Fact]
    public void AnAutomaticBarsThumbDrags()
    {
        view.ScrollBars = ScrollBarStyle.Automatic;

        Down(95, 10, pressed: true);
        Down(95, 200, pressed: false);

        Assert.Equal(15, Picks.FirstVisible);
    }

    [Fact]
    public void AListASliderScrollsKeepsTheSliderAndHasNoBar()
    {
        UIView bound = new(UIScript.Parse("""
            <UIScript>
              <Window width="300" height="200"/>
              <List name="log" x="0" y="0" width="100" height="102"/>
              <Slider name="logScroll" x="100" y="0" width="10" height="102" scrolls="log"/>
            </UIScript>
            """));
        var log = bound.List("log")!;
        for (var item = 0; item < 20; item++)
            log.Items.Add($"Line {item}");
        bound.Update(new UIViewInput(0, 0, false, false, false, 0, []), 0, 0);
        var first = log.FirstVisible;

        bound.Update(new UIViewInput(90, 5, true, true, false, 0, []), 0, 0);

        Assert.Equal(first, log.FirstVisible);
    }

    [Fact]
    public void TheSidebarsRowsScrollWithAnAutomaticBarThatTakesNoRoom()
    {
        UIView sided = new(UIScript.Parse("""
            <UIScript>
              <Window width="300" height="120"/>
              <Sidebar name="places" width="100"/>
            </UIScript>
            """));
        var places = sided.List("places")!;
        for (var item = 0; item < 20; item++)
            places.Items.Add($"Place {item}");
        places.SelectedIndex = 0;

        // Classic everywhere else, but the selection still runs to the sidebar's inset, past where a classic bar would
        // start. The sidebar sits left of the view's corner, so with the corner at 100 it runs from 0 to 100.
        RecordingPainter painter = new(300, 120);
        sided.Draw(painter, 100, 0);
        Assert.Equal(painter.At(20, 15), painter.At(90, 15));

        // Its thumb, at the top of the strip down its right edge, drags.
        sided.Update(new UIViewInput(95, 12, true, true, false, 0, []), 100, 0);
        sided.Update(new UIViewInput(95, 300, true, false, false, 0, []), 100, 0);

        Assert.True(places.FirstVisible > 0);
    }

    private IReadOnlyList<UIEvent> Press(int x, int y)
    {
        var events = Down(x, y, pressed: true);
        Release(x, y);
        return events;
    }

    private IReadOnlyList<UIEvent> Down(int x, int y, bool pressed, double seconds = 0) =>
        view.Update(new UIViewInput(x, y, true, pressed, false, 0, [], TimeSpan.FromSeconds(seconds)), 0, 0);

    private void Release(int x, int y, double seconds = 0) =>
        view.Update(new UIViewInput(x, y, false, false, true, 0, [], TimeSpan.FromSeconds(seconds)), 0, 0);

    /// <summary>The lines the text area showed, trimmed.</summary>
    private List<string> NotesLines() =>
        [.. Drawn().Texts.Where(text => text.X is >= 120 and < 230 && text.Y < 50).Select(text => text.Text.TrimEnd())];

    private RecordingPainter Drawn()
    {
        RecordingPainter painter = new(300, 200);
        view.Draw(painter, 0, 0);
        return painter;
    }
}
