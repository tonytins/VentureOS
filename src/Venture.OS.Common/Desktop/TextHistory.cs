namespace Venture.OS.Desktop;

/// <summary>What kind of edit a change was, which decides whether it joins the step before it.</summary>
public enum TextChangeKind
{
    /// <summary>Its own step, such as a paste or a cut.</summary>
    Other,

    /// <summary>Typing, which joins the typing before it until a space or line break ends the step.</summary>
    Typing,

    /// <summary>Backspace or Delete, which joins the deleting before it.</summary>
    Deleting,
}

/// <summary>
/// One edit as a value: at <see cref="Start"/>, <see cref="Removed"/> was replaced by <see cref="Inserted"/>, with
/// the selection as it was before and after. Undo reverts it and Redo applies it again, so a history keeps only
/// what changed, not the whole text.
/// </summary>
public readonly record struct TextChange(
    int Start, string Removed, string Inserted, (int Anchor, int Caret) Before, (int Anchor, int Caret) After)
{
    /// <summary>Whether <paramref name="text"/> still holds what this change put there, so reverting it is safe.</summary>
    public bool CanRevert(string text) => Holds(text, Inserted);

    /// <summary>Whether <paramref name="text"/> still holds what this change replaced, so applying it is safe.</summary>
    public bool CanApply(string text) => Holds(text, Removed);

    /// <summary>The text with this change applied.</summary>
    public string Apply(string text) => text.Remove(Start, Removed.Length).Insert(Start, Inserted);

    /// <summary>The text with this change taken back.</summary>
    public string Revert(string text) => text.Remove(Start, Inserted.Length).Insert(Start, Removed);

    private bool Holds(string text, string part) =>
        Start >= 0 && Start + part.Length <= text.Length && string.CompareOrdinal(text, Start, part, 0, part.Length) == 0;
}

/// <summary>
/// The changes Undo and Redo step through for one piece of text, newest last, at most <see cref="MostSteps"/> of
/// them. Typing joins one step until a space or line break; deleting with Backspace or Delete joins one step; and
/// anything else, or moving the caret, starts a new one.
/// </summary>
public sealed class TextHistory
{
    /// <summary>How many steps Undo can go back; older ones are dropped, so memory stays bounded.</summary>
    public const int MostSteps = 100;

    private readonly List<TextChange> undo = [];
    private readonly List<TextChange> redo = [];

    // Whether the newest step may still grow, and with what kind of change.
    private bool open;
    private TextChangeKind openKind;

    /// <summary>Whether there's a step to undo.</summary>
    public bool CanUndo => undo.Count > 0;

    /// <summary>Whether there's a step to redo.</summary>
    public bool CanRedo => redo.Count > 0;

    /// <summary>Forgets every step, as when an app replaces the text itself.</summary>
    public void Clear()
    {
        undo.Clear();
        redo.Clear();
        open = false;
    }

    /// <summary>Ends the step being grown, so the next change starts a new one.</summary>
    public void Break() => open = false;

    /// <summary>Records a change, joining the newest step when it continues it, and drops what could be redone.</summary>
    public void Record(TextChange change, TextChangeKind kind)
    {
        redo.Clear();
        if (open && kind == openKind && kind != TextChangeKind.Other && TryJoin(undo[^1], change, kind) is { } joined)
        {
            undo[^1] = joined;
        }
        else
        {
            undo.Add(change);
            if (undo.Count > MostSteps)
                undo.RemoveAt(0);
        }

        open = kind != TextChangeKind.Other;
        openKind = kind;

        // A space or a line break ends a step of typing, so Undo takes back a word at a time.
        if (kind == TextChangeKind.Typing && change.Inserted.Length > 0 && char.IsWhiteSpace(change.Inserted[^1]))
            open = false;
    }

    /// <summary>Takes the newest step to undo, moving it to the redo side.</summary>
    public bool TryUndo(out TextChange change) => Move(undo, redo, out change);

    /// <summary>Takes the newest step to redo, moving it back to the undo side.</summary>
    public bool TryRedo(out TextChange change) => Move(redo, undo, out change);

    private bool Move(List<TextChange> from, List<TextChange> to, out TextChange change)
    {
        open = false;
        if (from.Count == 0)
        {
            change = default;
            return false;
        }

        change = from[^1];
        from.RemoveAt(from.Count - 1);
        to.Add(change);
        return true;
    }

    // One change continuing another: typing right after the last typed text, or deleting next to the last deletion.
    private static TextChange? TryJoin(TextChange last, TextChange next, TextChangeKind kind)
    {
        if (kind == TextChangeKind.Typing && next.Removed.Length == 0 && next.Start == last.Start + last.Inserted.Length)
            return last with { Inserted = last.Inserted + next.Inserted, After = next.After };

        if (kind == TextChangeKind.Deleting && next.Inserted.Length == 0 && last.Inserted.Length == 0)
        {
            if (next.Start + next.Removed.Length == last.Start)
                return last with { Start = next.Start, Removed = next.Removed + last.Removed, After = next.After };
            if (next.Start == last.Start)
                return last with { Removed = last.Removed + next.Removed, After = next.After };
        }

        return null;
    }
}
