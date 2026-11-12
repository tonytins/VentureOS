using Venture.OS.Desktop;
using Venture.OS.Interface;

namespace Venture.OS.Tests.Desktop;

/// <summary>
/// Selecting text in fields and areas: with Shift and the arrows, Ctrl+A, and dragging, and typing or deleting over
/// it. The recording painter measures 8 pixels a character, and text starts 4 pixels in from a control's edge.
/// </summary>
public sealed class TextSelectionTests
{
    private const int Inset = 4;
    private const int Character = 8;

    private const string Document = """
        <UIScript>
          <Window width="400" height="200"/>
          <TextField name="field" x="10" y="10" width="300" height="24"/>
          <TextArea name="area" x="10" y="50" width="300" height="100"/>
        </UIScript>
        """;

    private readonly UIView view = new(UIScript.Parse(Document));
    private readonly RecordingPainter painter = new(400, 200);

    [Fact]
    public void ShiftSelectsAndTypingReplacesIt()
    {
        Focus("field", "Hello world");

        Keys(new KeyPress(DesktopKey.Home), Shift(DesktopKey.Right), Shift(DesktopKey.Right), Shift(DesktopKey.Right),
            Shift(DesktopKey.Right), Shift(DesktopKey.Right));
        Assert.Equal((0, 5), view.Selection("field"));

        Keys([.. "Howdy".Select(KeyPress.Typed)]);
        Assert.Equal("Howdy world", view.Text("field"));
        Assert.Equal((5, 0), view.Selection("field"));
    }

    [Fact]
    public void CtrlASelectsEverythingAndBackspaceRemovesIt()
    {
        Focus("field", "Hello world");

        Keys(new KeyPress(DesktopKey.Character, 'a', Control: true));
        Assert.Equal((0, 11), view.Selection("field"));

        Keys(new KeyPress(DesktopKey.Backspace));
        Assert.Equal("", view.Text("field"));
    }

    [Fact]
    public void AClickPlacesTheCaretAndADragSelects()
    {
        view.SetText("field", "Hello world");
        view.Draw(painter, 0, 0);
        var (left, middle) = (10 + Inset, 10 + 12);

        Pointer(left + (3 * Character) + 2, middle, down: true, pressed: true);
        Assert.Equal((3, 0), view.Selection("field"));

        Pointer(left + (8 * Character) + 2, middle, down: true);
        Pointer(left + (8 * Character) + 2, middle, released: true);
        Assert.Equal((3, 5), view.Selection("field"));
        Assert.Equal("field", view.FocusedControl);
    }

    [Fact]
    public void TheSelectionIsDrawnBehindItsText()
    {
        Focus("field", "Hello world");
        Keys(new KeyPress(DesktopKey.Home), Shift(DesktopKey.Right), Shift(DesktopKey.Right));

        view.Draw(painter, 0, 0);

        var selection = DesktopTheme.Selection;
        var highlight = DesktopBackground.Pack(selection.Red, selection.Green, selection.Blue);
        Assert.Equal(highlight, painter.At(10 + Inset + Character, 20));
        Assert.NotEqual(highlight, painter.At(10 + Inset + (3 * Character), 20));
    }

    [Fact]
    public void AnAreaSelectsAcrossLinesAndReportsTheChange()
    {
        Focus("area", "one\ntwo\nthree");
        view.Draw(painter, 0, 0);
        Keys(new KeyPress(DesktopKey.Character, 'a', Control: true));
        Keys(new KeyPress(DesktopKey.Left));
        Assert.Equal((0, 0), view.Selection("area"));

        Keys(Shift(DesktopKey.Down));
        Assert.Equal((0, 4), view.Selection("area"));

        var events = Keys(KeyPress.Typed('X'));
        Assert.Equal("Xtwo\nthree", view.Text("area"));
        Assert.Equal([new UIEvent(UIEventKind.Changed, "area", "Xtwo\nthree")], events);
    }

    [Fact]
    public void OnlyTextControlsHaveASelection()
    {
        Assert.Null(view.Selection("nothing"));
    }

    private static KeyPress Shift(DesktopKey key) => new(key, Shift: true);

    private void Focus(string name, string text)
    {
        view.SetText(name, text);
        view.Focus(name);
    }

    private IReadOnlyList<UIEvent> Keys(params KeyPress[] keys) =>
        view.Update(new UIViewInput(0, 0, false, false, false, 0, keys), 0, 0);

    private void Pointer(int x, int y, bool down = false, bool pressed = false, bool released = false) =>
        view.Update(new UIViewInput(x, y, down, pressed, released, 0, []), 0, 0);
}
