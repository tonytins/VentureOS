using Venture.OS.Desktop;
using Venture.OS.Interface;

namespace Venture.OS.Tests.Desktop;

/// <summary>
/// A <c>&lt;TextArea&gt;</c>'s <c>alignment</c>, line by line, as About VentureOS's credits are centred. The recording
/// painter measures 8 pixels a character; each area's text runs 92 pixels wide (100 less 4 each side) from x 14.
/// </summary>
public sealed class TextAreaAlignmentTests
{
    private const string Document = """
        <UIScript version="1">
          <Window width="300" height="200"/>
          <TextArea name="centred" x="10" y="10" width="100" height="80" alignment="center"/>
          <TextArea name="right" x="130" y="10" width="100" height="80" alignment="right"/>
          <TextArea name="left" x="10" y="100" width="100" height="80"/>
        </UIScript>
        """;

    private readonly UIView view = new(UIScript.Parse(Document));
    private readonly RecordingPainter painter = new(400, 300);

    [Fact]
    public void EachLineSitsAsTheAreaIsAligned()
    {
        view.SetText("centred", "VentureOS\nby Tony");
        view.SetText("right", "Fonts\nOpen Sans");
        view.SetText("left", "Plain");

        view.Draw(painter, 0, 0);

        // 92 of room: "VentureOS" is 72 wide, so 10 either side; "by Tony" is 56, so 18.
        Assert.Contains(painter.Texts, text => text.Text == "VentureOS" && text.X == 14 + 10);
        Assert.Contains(painter.Texts, text => text.Text == "by Tony" && text.X == 14 + 18);
        Assert.Contains(painter.Texts, text => text.Text == "Fonts" && text.X == 134 + 92 - 40);
        Assert.Contains(painter.Texts, text => text.Text == "Open Sans" && text.X == 134 + 92 - 72);
        Assert.Contains(painter.Texts, text => text.Text == "Plain" && text.X == 14);
    }

    [Fact]
    public void AClickOnACentredLinePutsTheCaretWhereTheTextIsDrawn()
    {
        view.SetText("centred", "abcd");
        view.Draw(painter, 0, 0);

        // "abcd" is 32 wide, drawn from 14 + 30 = 44; a click at 61 is just past "ab".
        view.Update(new UIViewInput(61, 20, true, true, false, 0, []), 0, 0);
        view.Update(new UIViewInput(61, 20, false, false, true, 0, []), 0, 0);
        view.Update(new UIViewInput(61, 20, false, false, false, 0, [KeyPress.Typed('X')]), 0, 0);

        Assert.Equal("abXcd", view.Text("centred"));
    }

    [Fact]
    public void TheCaretIsDrawnAfterTheCentredText()
    {
        view.Focus("centred");
        view.Update(new UIViewInput(0, 0, false, false, false, 0, [KeyPress.Typed('a'), KeyPress.Typed('b')]), 0, 0);

        view.Draw(painter, 0, 0);

        // "ab" is 16 wide, drawn from 14 + 38 = 52, so the caret is at 52 + 16 + 1.
        Assert.Contains(painter.Texts, text => text.Text == "ab" && text.X == 52);
        Assert.Equal(Pack(DesktopTheme.Text), painter.At(69, 20));
    }

    private static int Pack(UIColor color) => DesktopBackground.Pack(color.Red, color.Green, color.Blue);
}
