using Venture.OS.Desktop;
using Venture.OS.Interface;

namespace Venture.OS.Tests.Desktop;

/// <summary>
/// <c>&lt;TextArea&gt;</c>. The recording painter measures 8 pixels a character, and the area's text runs 92 pixels
/// wide (100 less 4 each side), so 11 characters fit a line. Lines are 18 pixels, so 4 fit its 80-pixel height.
/// </summary>
public sealed class TextAreaTests
{
    private const string Document = """
        <UIScript version="1">
          <Window width="300" height="200"/>
          <TextArea name="notes" x="10" y="10" width="100" height="80" placeholder="Write here"/>
          <Slider name="notesScroll" x="112" y="10" width="10" height="80" scrolls="notes"/>
          <TextArea name="log" x="130" y="10" width="100" height="80" readOnly="true" text="locked"/>
          <TextArea name="short" x="10" y="100" width="100" height="40" maximumLength="3"/>
        </UIScript>
        """;

    private readonly UIView view = new(UIScript.Parse(Document));
    private readonly RecordingPainter painter = new(400, 300);

    [Fact]
    public void TypesAcrossLinesAndReportsChanges()
    {
        view.Focus("notes");

        var events = Type(KeyPress.Typed('h'), KeyPress.Typed('i'), new KeyPress(DesktopKey.Enter), KeyPress.Typed('!'));

        Assert.Equal("hi\n!", view.Text("notes"));
        Assert.Equal(new UIEvent(UIEventKind.Changed, "notes", "hi\n!"), events[^1]);
        Assert.Equal(4, events.Count);
    }

    [Fact]
    public void WrapsAtSpacesAndCutsWordsTooLongForALine()
    {
        view.SetText("notes", "one two three four\nabcdefghijklmnopq");

        Draw();

        Assert.Equal(["one two ", "three four", "abcdefghijk", "lmnopq"], Lines());
    }

    [Fact]
    public void ScrollsWithTheWheelAndItsSliderAndKeepsTheCaretInView()
    {
        view.SetText("notes", "1\n2\n3\n4\n5\n6\n7");
        Draw();
        Assert.Equal("1", Lines()[0]);

        view.Update(new UIViewInput(20, 20, false, false, false, 2, []), 0, 0);
        Draw();
        Assert.Equal("3", Lines()[0]);

        // The slider's bottom is the last line in view.
        view.Update(new UIViewInput(116, 89, true, true, false, 0, []), 0, 0);
        Draw();
        Assert.Equal("4", Lines()[0]);

        view.Focus("notes");
        Type(new KeyPress(DesktopKey.Up), new KeyPress(DesktopKey.Up), new KeyPress(DesktopKey.Up), new KeyPress(DesktopKey.Up),
            new KeyPress(DesktopKey.Up), new KeyPress(DesktopKey.Up));
        Draw();
        Assert.Equal("1", Lines()[0]);
    }

    [Fact]
    public void HomeEndUpAndDownFollowTheLines()
    {
        view.SetText("notes", "abc\ndefgh\nij");
        view.Focus("notes");
        Draw();

        Type(new KeyPress(DesktopKey.Up), new KeyPress(DesktopKey.Home), KeyPress.Typed('<'));
        Assert.Equal("abc\n<defgh\nij", view.Text("notes"));

        Type(new KeyPress(DesktopKey.End), KeyPress.Typed('>'));
        Assert.Equal("abc\n<defgh>\nij", view.Text("notes"));

        Type(new KeyPress(DesktopKey.Up), KeyPress.Typed('^'));
        Assert.Equal("abc^\n<defgh>\nij", view.Text("notes"));

        Type(new KeyPress(DesktopKey.Down), new KeyPress(DesktopKey.Down), KeyPress.Typed('v'));
        Assert.Equal("abc^\n<defgh>\nijv", view.Text("notes"));
    }

    [Fact]
    public void AClickPutsTheCaretWhereItLands()
    {
        view.SetText("notes", "abcdef\nghijkl");
        Draw();

        // The second line, two and a bit characters in: between "gh" and "ij".
        view.Update(new UIViewInput(10 + 4 + 17, 10 + 4 + 18 + 5, true, true, false, 0, []), 0, 0);
        Type(KeyPress.Typed('_'));

        Assert.Equal("abcdef\ngh_ijkl", view.Text("notes"));
        Assert.Equal("notes", view.FocusedControl);
    }

    [Fact]
    public void AReadOnlyAreaTakesNoFocusAndALimitedOneStopsAtItsLength()
    {
        view.Update(new UIViewInput(140, 20, true, true, false, 0, []), 0, 0);
        view.Focus("log");
        Assert.Null(view.FocusedControl);

        view.Focus("short");
        Type(KeyPress.Typed('a'), KeyPress.Typed('b'), KeyPress.Typed('c'), KeyPress.Typed('d'), new KeyPress(DesktopKey.Enter));

        Assert.Equal("abc", view.Text("short"));
    }

    [Fact]
    public void ShowsItsPlaceholderWhileEmptyAndUnfocused()
    {
        Draw();

        Assert.Contains(painter.Texts, text => text.Text == "Write here" && text.Color == DesktopTheme.SecondaryText);
        Assert.Contains(painter.Texts, text => text.Text == "locked");
    }

    [Theory]
    [InlineData("""<TextArea name="a" x="0" y="0" width="10" height="10" secure="true"/>""", "secure")]
    [InlineData("""<TextArea name="a" x="0" y="0" width="10" height="10" maximumLength="0"/>""", "maximumLength")]
    [InlineData("""<TextArea name="a" x="0" y="0" width="10" height="10"/><Button name="b" x="0" y="0" width="5" height="5" scrolls="a"/>""", "scrolls")]
    public void RefusesMistakes(string elements, string message)
    {
        var text = $"""<UIScript><Window width="10" height="10"/>{elements}</UIScript>""";

        Assert.Contains(message, Assert.Throws<UIScriptException>(() => UIScript.Parse(text)).Message);
    }

    private IReadOnlyList<UIEvent> Type(params KeyPress[] keys)
    {
        var events = view.Update(new UIViewInput(0, 0, false, false, false, 0, keys), 0, 0);
        Draw();
        return events;
    }

    private void Draw()
    {
        painter.Texts.Clear();
        view.Draw(painter, 0, 0);
    }

    /// <summary>The notes area's lines as drawn, top to bottom.</summary>
    private List<string> Lines()
    {
        List<string> lines = [];
        foreach (var text in painter.Texts)
        {
            if (text.X == 14 && text.Y >= 14 && text.Y < 90)
                lines.Add(text.Text);
        }

        return lines;
    }
}
