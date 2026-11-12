using Venture.OS.Desktop;

namespace Venture.OS.Tests.Desktop;

/// <summary>
/// The caret and selection every text the desktop edits shares: fields, areas, and the title field.
/// </summary>
public sealed class TextCursorTests
{
    [Fact]
    public void ShiftExtendsASelectionFromWhereItStarted()
    {
        TextCursor cursor = new() { Caret = 2 };

        cursor.Right(5, extend: true);
        cursor.Right(5, extend: true);

        Assert.Equal((2, 2), cursor.Selection);
        Assert.True(cursor.HasSelection);
    }

    [Fact]
    public void AnArrowWithoutShiftLeavesASelectionAtItsEdge()
    {
        TextCursor cursor = new() { Caret = 1 };
        cursor.MoveTo(4, extend: true);

        cursor.Left(extend: false);
        Assert.Equal((1, 0), cursor.Selection);

        cursor.MoveTo(4, extend: true);
        cursor.Right(5, extend: false);
        Assert.Equal((4, 0), cursor.Selection);
    }

    [Fact]
    public void TypingReplacesTheSelection()
    {
        TextCursor cursor = new() { Caret = 0 };
        cursor.MoveTo(5, extend: true);

        var text = cursor.Replace("Hello world", "Howdy");

        Assert.Equal("Howdy world", text);
        Assert.Equal((5, 0), cursor.Selection);
    }

    [Fact]
    public void DeletingRemovesTheSelectionOrOneCharacter()
    {
        TextCursor cursor = new() { Caret = 6 };
        cursor.MoveTo(11, extend: true);
        Assert.Equal("Hello ", cursor.DeleteBackward("Hello world"));

        TextCursor plain = new() { Caret = 5 };
        Assert.Equal("Hell world", plain.DeleteBackward("Hello world"));
        Assert.Equal("Hellworld", plain.DeleteForward("Hell world"));
        Assert.Equal(4, plain.Caret);
    }

    [Fact]
    public void SelectAllAndClamp()
    {
        TextCursor cursor = new();

        cursor.SelectAll(11);
        Assert.Equal((0, 11), cursor.Selection);

        cursor.Clamp(4);
        Assert.Equal((0, 4), cursor.Selection);
    }

    [Fact]
    public void SettingTheCaretDropsTheSelection()
    {
        TextCursor cursor = new();
        cursor.SelectAll(5);

        cursor.Caret = 3;

        Assert.False(cursor.HasSelection);
        Assert.Equal((3, 0), cursor.Selection);
    }

    [Fact]
    public void ReplacingCanBeLimitedToALength()
    {
        TextCursor cursor = new() { Caret = 3 };

        Assert.Equal("abc", cursor.Replace("abc", "d", maximum: 3));
        cursor.MoveTo(0, extend: true);
        Assert.Equal("dd", cursor.Replace("abc", "dd", maximum: 3));
    }
}
