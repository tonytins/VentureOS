using Venture.OS.Desktop;
using Venture.OS.Interface;

namespace Venture.OS.Tests.Desktop;

/// <summary>
/// A value slider: dragged, clicked, and moved with keys; snapped to its step; reporting <c>changed</c> as it moves and
/// <c>committed</c> when let go; drawn on the rail it shares with progress bars.
/// </summary>
/// <remarks>
/// "volume" runs across 218 pixels, so its rail runs 200 pixels between the knob's centers, from x 9 to 209: a point
/// at 9 + 2 × value is that value. "taxes" is the same with a step of 1 from 0 to 20; "level" stands upright.
/// </remarks>
public sealed class SliderTests
{
    private const string Document = """
        <UIScript>
          <Window width="300" height="300"/>
          <Slider name="volume" x="0" y="0" width="218" height="20" minValue="0" maxValue="100"/>
          <Slider name="taxes" x="0" y="40" width="218" height="20" minValue="0" maxValue="20" step="1"/>
          <Slider name="level" x="250" y="0" width="20" height="218" minValue="0" maxValue="100"/>
          <ProgressBar name="copying" x="0" y="80" width="200" height="8"/>
        </UIScript>
        """;

    private readonly UIView view = new(UIScript.Parse(Document));

    public SliderTests() => DesktopAppearance.Use(DesktopLook.Standard);

    private IReadOnlyList<UIEvent> Input(int x, int y, bool down, bool pressed = false, bool released = false, params KeyPress[] keys) =>
        view.Update(new UIViewInput(x, y, down, pressed, released, 0, keys, TimeSpan.Zero), 0, 0);

    private IReadOnlyList<UIEvent> Key(params KeyPress[] keys) => Input(299, 299, false, keys: keys);

    [Fact]
    public void ItStartsAtItsLowestValue() => Assert.Equal(0, view.Value("volume"));

    [Fact]
    public void AClickOnTheTrackJumpsThereAndLettingGoCommits()
    {
        Assert.Equal([new UIEvent(UIEventKind.Changed, "volume", Value: 50)], Input(109, 10, true, pressed: true));
        Assert.Equal([new UIEvent(UIEventKind.Committed, "volume", Value: 50)], Input(109, 10, false, released: true));
        Assert.Equal(50, view.Value("volume"));
    }

    [Fact]
    public void ADragReportsEachValueThenCommitsTheLast()
    {
        Input(9, 10, true, pressed: true);
        Assert.Equal([new UIEvent(UIEventKind.Changed, "volume", Value: 25)], Input(59, 10, true));
        Assert.Equal([new UIEvent(UIEventKind.Changed, "volume", Value: 100)], Input(400, 10, true));

        // Off the end it stays at the end; holding still reports nothing.
        Assert.Empty(Input(500, 10, true));
        Assert.Equal([new UIEvent(UIEventKind.Committed, "volume", Value: 100)], Input(500, 10, false, released: true));
    }

    [Fact]
    public void WithAStepItOnlyLandsOnSteps()
    {
        // 7.4 of 20 is nearest 7.
        var events = Input(9 + (int)(200 * 7.4 / 20), 50, true, pressed: true);

        Assert.Equal([new UIEvent(UIEventKind.Changed, "taxes", Value: 7)], events);

        // A little further, still nearer 7: nothing.
        Assert.Empty(Input(9 + (int)(200 * 7.3 / 20), 50, true));
    }

    [Fact]
    public void AnUprightSliderRunsUpFromTheBottom()
    {
        Assert.Equal([new UIEvent(UIEventKind.Changed, "level", Value: 75)], Input(260, 209 - 150, true, pressed: true));
    }

    [Fact]
    public void ItTakesTheKeyboardOnceClickedAndKeysMoveItByAStep()
    {
        Input(9 + 100, 50, true, pressed: true);
        Input(9 + 100, 50, false, released: true);
        Assert.Equal("taxes", view.FocusedControl);

        Assert.Equal(
            [new UIEvent(UIEventKind.Changed, "taxes", Value: 11), new UIEvent(UIEventKind.Committed, "taxes", Value: 11)],
            Key(new KeyPress(DesktopKey.Right)));
        Assert.Equal(10, Key(new KeyPress(DesktopKey.Down))[0].Value);
        Assert.Equal(20, Key(new KeyPress(DesktopKey.End))[0].Value);
        Assert.Empty(Key(new KeyPress(DesktopKey.Up)));
        Assert.Equal(0, Key(new KeyPress(DesktopKey.Home))[0].Value);
    }

    [Fact]
    public void WithoutAStepAKeyMovesATenthOfTheRange()
    {
        view.Focus("volume");

        Assert.Equal(10, Key(new KeyPress(DesktopKey.Right))[0].Value);
    }

    [Theory]
    [InlineData(7.6f, 8f)]
    [InlineData(-5f, 0f)]
    [InlineData(99f, 20f)]
    public void SettingItKeepsItInTheRangeAndOnAStepAndReportsNothing(float set, float value)
    {
        view.SetValue("taxes", set);

        Assert.Equal(value, view.Value("taxes"));
        Assert.Empty(Key());
    }

    [Fact]
    public void SettingItToWhatIsntANumberChangesNothing()
    {
        view.SetValue("volume", 40);
        view.SetValue("volume", float.NaN);

        Assert.Equal(40, view.Value("volume"));
    }

    [Fact]
    public void ADimmedSliderTakesNoInput()
    {
        view.SetDisabled(true, "volume");

        Assert.Empty(Input(109, 10, true, pressed: true));
        Assert.Equal(0, view.Value("volume"));
    }

    [Fact]
    public void ItsKnobSitsOnItsValueOverAGlossedAccentRail()
    {
        view.SetValue("volume", 50);
        RecordingPainter painter = new(300, 300);

        view.Draw(painter, 0, 0);

        // The knob's white body at the value; the rail's fill, under the gloss, left of it, the empty rail right of it.
        Assert.Equal(unchecked((int)0xFFFFFFFF), painter.At(109, 10));
        Assert.NotEqual(painter.At(60, 10), painter.At(160, 10));
        Assert.Equal(DesktopTheme.RailBottom, Color(painter.At(160, 12)));
    }

    [Fact]
    public void AProgressBarIsTheSameRailWithoutAKnob()
    {
        view.SetValue("copying", 0.5f);
        RecordingPainter painter = new(300, 300);

        view.Draw(painter, 0, 0);

        Assert.NotEqual(Color(painter.At(50, 84)), Color(painter.At(150, 84)));
        Assert.NotEqual(unchecked((int)0xFFFFFFFF), painter.At(100, 84));
    }

    public static TheoryData<bool, string> Palettes()
    {
        TheoryData<bool, string> palettes = [];
        foreach (var dark in new[] { false, true })
        {
            foreach (var accent in DesktopPalette.Swatches)
                palettes.Add(dark, accent.ToString());
        }

        return palettes;
    }

    /// <summary>
    /// The knob stands out from the window, by its edge on a light one and its white on a dark one, at the 3 to 1 the
    /// WCAG asks of interface parts.
    /// </summary>
    [Theory]
    [MemberData(nameof(Palettes))]
    public void TheKnobStandsOutFromTheWindow(bool dark, string accent)
    {
        var palette = DesktopPalette.Create(DesktopPalette.Swatches.First(swatch => swatch.ToString() == accent), dark);
        var knob = dark ? new UIColor(255, 255, 255) : Over(palette.KnobEdge, palette.Panel);

        Assert.True(Contrast(knob, palette.Panel) >= 3, $"the knob on {palette.Panel}");
    }

    private static UIColor Color(int pixel) => new((byte)(pixel >> 16), (byte)(pixel >> 8), (byte)pixel);

    private static UIColor Over(UIColor top, UIColor under)
    {
        int Mix(int a, int b) => ((a * top.Alpha) + (b * (255 - top.Alpha))) / 255;
        return new UIColor((byte)Mix(top.Red, under.Red), (byte)Mix(top.Green, under.Green), (byte)Mix(top.Blue, under.Blue));
    }

    private static double Contrast(UIColor a, UIColor b)
    {
        double Luminance(UIColor color) => (0.2126 * Linear(color.Red)) + (0.7152 * Linear(color.Green)) + (0.0722 * Linear(color.Blue));
        double Linear(byte channel) => channel / 255.0 is var value && value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        var (x, y) = (Luminance(a), Luminance(b));
        return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
    }
}
