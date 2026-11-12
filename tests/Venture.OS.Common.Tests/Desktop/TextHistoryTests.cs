using Venture.OS.Desktop;

namespace Venture.OS.Tests.Desktop;

/// <summary>
/// Undo and Redo for text: edits kept as values (where, what was there, what replaced it) on a
/// <see cref="TextCursor"/>, typing grouped into steps, and a capped history.
/// </summary>
public sealed class TextHistoryTests
{
    [Fact]
    public void UndoesAndRedoesAnEdit()
    {
        TextCursor cursor = new() { Caret = 6 };
        cursor.MoveTo(11, extend: true);
        var text = cursor.Replace("Hello world", "there");
        Assert.Equal("Hello there", text);

        text = cursor.Undo(text);
        Assert.Equal("Hello world", text);
        Assert.Equal((6, 5), cursor.Selection);

        text = cursor.Redo(text);
        Assert.Equal("Hello there", text);
        Assert.Equal((11, 0), cursor.Selection);
    }

    [Fact]
    public void TypingIsOneStepUntilASpace()
    {
        TextCursor cursor = new();
        var text = Type(cursor, "", "hi there");

        Assert.True(cursor.History.CanUndo);
        text = cursor.Undo(text);
        Assert.Equal("hi ", text);
        text = cursor.Undo(text);
        Assert.Equal("", text);
        Assert.False(cursor.History.CanUndo);
    }

    [Fact]
    public void MovingTheCaretEndsAStep()
    {
        TextCursor cursor = new();
        var text = Type(cursor, "", "ab");
        cursor.Left(extend: false);
        text = Type(cursor, text, "c");
        Assert.Equal("acb", text);

        text = cursor.Undo(text);

        Assert.Equal("ab", text);
    }

    [Fact]
    public void DeletingIsOneStepAndPastingItsOwn()
    {
        TextCursor cursor = new() { Caret = 5 };
        var text = "Hello";
        for (var count = 0; count < 3; count++)
            text = cursor.DeleteBackward(text);
        Assert.Equal("He", text);

        text = cursor.Replace(text, "y there");
        text = cursor.Undo(text);
        Assert.Equal("He", text);
        text = cursor.Undo(text);
        Assert.Equal("Hello", text);
    }

    [Fact]
    public void ANewEditDropsWhatCouldBeRedone()
    {
        TextCursor cursor = new();
        var text = Type(cursor, "", "one");
        text = cursor.Undo(text);
        Assert.True(cursor.History.CanRedo);

        text = Type(cursor, text, "two");

        Assert.False(cursor.History.CanRedo);
        Assert.Equal("two", text);
    }

    [Fact]
    public void KeepsOnlyTheNewestSteps()
    {
        TextCursor cursor = new();
        var text = "";
        for (var step = 0; step < TextHistory.MostSteps + 5; step++)
            text = Type(cursor, text, "a ");

        var undone = 0;
        while (cursor.History.CanUndo)
        {
            text = cursor.Undo(text);
            undone++;
        }

        // Each "a " is one step, ended by its space, so the oldest five are gone.
        Assert.Equal(TextHistory.MostSteps, undone);
        Assert.NotEqual("", text);
    }

    [Fact]
    public void TextChangedUnderneathClearsTheHistoryRatherThanBreakingIt()
    {
        TextCursor cursor = new();
        Type(cursor, "", "abc");

        var text = cursor.Undo("something else entirely");

        Assert.Equal("something else entirely", text);
        Assert.False(cursor.History.CanUndo);
        Assert.False(cursor.History.CanRedo);
    }

    [Fact]
    public void UndoWithNothingToUndoChangesNothing()
    {
        TextCursor cursor = new();

        Assert.Equal("abc", cursor.Undo("abc"));
        Assert.Equal("abc", cursor.Redo("abc"));
    }

    private static string Type(TextCursor cursor, string text, string typed)
    {
        foreach (var character in typed)
            text = cursor.Replace(text, character.ToString(), kind: TextChangeKind.Typing);
        return text;
    }
}
