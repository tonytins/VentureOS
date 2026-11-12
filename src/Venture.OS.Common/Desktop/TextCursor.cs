namespace Venture.OS.Desktop;

/// <summary>
/// The caret and selection in text being edited, shared by text fields, text areas, and the title field, so they all
/// select, type over, and delete the same way.
/// </summary>
/// <remarks>
/// A selection runs between <see cref="Anchor"/>, where it started, and <see cref="Caret"/>, which moves; when the two
/// are the same, nothing is selected. Moving with Shift held keeps the anchor; moving without it, or setting
/// <see cref="Caret"/>, brings the anchor along and drops the selection. Positions are indexes into the text, which
/// the caller keeps, and are clamped with <see cref="Clamp"/> when the text changes underneath.
/// </remarks>
public sealed class TextCursor
{
    private int caret;

    /// <summary>Where typing goes. Setting it drops any selection.</summary>
    public int Caret
    {
        get => caret;
        set => MoveTo(value, extend: false);
    }

    /// <summary>Where the selection started; the same as <see cref="Caret"/> when nothing is selected.</summary>
    public int Anchor { get; private set; }

    /// <summary>Whether any text is selected.</summary>
    public bool HasSelection => Anchor != caret;

    /// <summary>The selected text's start and length; an empty one at the caret when nothing is selected.</summary>
    public (int Start, int Length) Selection => (Math.Min(Anchor, caret), Math.Abs(caret - Anchor));

    /// <summary>Moves the caret, keeping the anchor to extend a selection, or bringing it along to drop one.</summary>
    public void MoveTo(int position, bool extend)
    {
        caret = Math.Max(0, position);
        if (!extend)
            Anchor = caret;
    }

    /// <summary>Selects all of a text this long.</summary>
    public void SelectAll(int length)
    {
        Anchor = 0;
        caret = Math.Max(0, length);
    }

    /// <summary>Keeps both ends within a text this long, as after it changes.</summary>
    public void Clamp(int length)
    {
        caret = Math.Clamp(caret, 0, length);
        Anchor = Math.Clamp(Anchor, 0, length);
    }

    /// <summary>One character left, or without Shift, to the start of a selection.</summary>
    public void Left(bool extend)
    {
        if (!extend && HasSelection)
            MoveTo(Selection.Start, extend: false);
        else
            MoveTo(caret - 1, extend);
    }

    /// <summary>One character right within a text this long, or without Shift, to the end of a selection.</summary>
    public void Right(int length, bool extend)
    {
        if (!extend && HasSelection)
            MoveTo(Selection.Start + Selection.Length, extend: false);
        else
            MoveTo(Math.Min(length, caret + 1), extend);
    }

    /// <summary>
    /// Puts <paramref name="with"/> in place of the selection, or at the caret, leaving the caret after it, unless the
    /// result would pass <paramref name="maximum"/>, which leaves the text as it was.
    /// </summary>
    /// <returns>The text after the change.</returns>
    public string Replace(string text, string with, int maximum = int.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(with);
        Clamp(text.Length);
        var (start, length) = Selection;
        if (text.Length - length + with.Length > maximum)
            return text;

        MoveTo(start + with.Length, extend: false);
        return text.Remove(start, length).Insert(start, with);
    }

    /// <summary>Backspace: removes the selection, or the character before the caret.</summary>
    public string DeleteBackward(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Clamp(text.Length);
        if (HasSelection)
            return Replace(text, "");
        if (caret == 0)
            return text;

        MoveTo(caret - 1, extend: false);
        return text.Remove(caret, 1);
    }

    /// <summary>Delete: removes the selection, or the character after the caret.</summary>
    public string DeleteForward(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Clamp(text.Length);
        if (HasSelection)
            return Replace(text, "");
        return caret < text.Length ? text.Remove(caret, 1) : text;
    }
}
