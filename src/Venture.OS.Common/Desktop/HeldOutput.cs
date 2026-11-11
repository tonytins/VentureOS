using Venture.OS.Shell;

namespace Venture.OS.Desktop;

/// <summary>
/// Output for apps the desktop opens: held while the desktop is showing, and passed on once something can show it.
/// </summary>
/// <remarks>
/// An app keeps the output it was opened with for as long as it's open. The text console draws on the same screen as
/// the desktop, so writing there would scribble over it. The desktop takes what's held and shows it in a window;
/// when the desktop closes, the shell sets <see cref="PassTo"/> so the app's later messages reach the console again.
/// At most <see cref="Limit"/> lines are held, dropping the oldest, so an app that writes in a loop can't fill
/// memory.
/// </remarks>
public sealed class HeldOutput : IShellOutput
{
    /// <summary>The most lines held at once.</summary>
    public const int Limit = 200;

    private readonly List<string> held = [];

    /// <summary>Where lines go straight away, or <see langword="null"/> to hold them.</summary>
    public IShellOutput? PassTo { get; set; }

    /// <summary>Whether any lines are waiting.</summary>
    public bool HasLines => held.Count > 0;

    public void WriteLine(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (PassTo is { } output)
        {
            output.WriteLine(text);
            return;
        }

        if (held.Count == Limit)
            held.RemoveAt(0);
        held.Add(text);
    }

    public void Clear()
    {
        if (PassTo is { } output)
            output.Clear();
        else
            held.Clear();
    }

    /// <summary>Takes the waiting lines, leaving none.</summary>
    public IReadOnlyList<string> Take()
    {
        string[] lines = [.. held];
        held.Clear();
        return lines;
    }
}
