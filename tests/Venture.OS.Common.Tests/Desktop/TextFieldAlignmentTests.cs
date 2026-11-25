using Venture.OS.Desktop;
using Venture.OS.Interface;

namespace Venture.OS.Tests.Desktop;

/// <summary>
/// A <c>&lt;TextField&gt;</c>'s <c>alignment</c>, as a text area's: text that fits sits as the field is aligned, and
/// text wider than the field fills it and scrolls, as before. The recording painter measures 8 pixels a character; each
/// field's text runs 92 pixels wide (100 less 4 each side), from x 14 or 134.
/// </summary>
public sealed class TextFieldAlignmentTests
{
    private const string Document = """
        <UIScript version="1">
          <Window width="300" height="200"/>
          <TextField name="centred" x="10" y="10" width="100" height="26" alignment="center" placeholder="Name"/>
          <TextField name="right" x="130" y="10" width="100" height="26" alignment="right"/>
        </UIScript>
        """;

    private readonly UIView view = new(UIScript.Parse(Document));
    private readonly RecordingPainter painter = new(400, 300);

    [Fact]
    public void TextThatFitsSitsAsTheFieldIsAligned()
    {
        view.SetText("centred", "abcd");
        view.SetText("right", "12:00");

        view.Draw(painter, 0, 0);

        // 92 of room: "abcd" is 32, so 30 either side; "12:00" is 40, so 52 before it.
        Assert.Contains(painter.Texts, text => text.Text == "abcd" && text.X == 14 + 30);
        Assert.Contains(painter.Texts, text => text.Text == "12:00" && text.X == 134 + 52);
    }

    [Fact]
    public void ThePlaceholderIsAlignedToo()
    {
        view.Draw(painter, 0, 0);

        // "Name" is 32 wide.
        Assert.Contains(painter.Texts, text => text.Text == "Name" && text.X == 14 + 30);
    }

    [Fact]
    public void TextWiderThanTheFieldFillsItFromTheInset()
    {
        view.SetText("right", "abcdefghijklmnop");

        view.Draw(painter, 0, 0);

        // The 11 characters that fit, from the caret's end at the start, sit at the inset, not nudged by the 4 pixels left
        // over, though the field is aligned right.
        Assert.Contains(painter.Texts, text => text.Text == "abcdefghijk" && text.X == 134);
    }

    [Fact]
    public void AClickOnCentredTextPutsTheCaretWhereItsDrawn()
    {
        view.SetText("centred", "abcd");
        view.Draw(painter, 0, 0);

        // "abcd" is drawn from 44; a click at 61 is just past "ab".
        view.Update(new UIViewInput(61, 20, true, true, false, 0, []), 0, 0);
        view.Update(new UIViewInput(61, 20, false, false, true, 0, []), 0, 0);
        view.Update(new UIViewInput(61, 20, false, false, false, 0, [KeyPress.Typed('X')]), 0, 0);

        Assert.Equal("abXcd", view.Text("centred"));
    }
}
