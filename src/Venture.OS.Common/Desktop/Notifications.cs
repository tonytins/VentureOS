using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Venture.OS.Desktop;

/// <summary>A heads-up from an app, the system, or vdb: an icon, a title, a text, and when it came.</summary>
/// <param name="App">The app it's from, by name, or <see langword="null"/> for VentureOS itself.</param>
/// <param name="Icon">The icon's path, or <see langword="null"/> for VentureOS's.</param>
/// <param name="Posted">When it came, on <see cref="Notifications"/>' own clock.</param>
public sealed record Notification(string Title, string Text, string? App, string? Icon, TimeSpan Posted);

/// <summary>
/// The desktop's notifications: those waiting to show as toasts, and the history, which keeps the newest
/// <see cref="MaximumKept"/> until VentureOS restarts. It lives with the windows, so it outlasts each visit to the
/// desktop, and the desktop draws it, so a crash notice never depends on an app. See
/// <c>docs/design/2026-11-25-toasts-design.md</c>.
/// </summary>
/// <remarks>
/// Toasts are passive: nothing here calls anyone back. A flood can't bury the screen or the history: past
/// <see cref="MaximumWaiting"/> waiting, the oldest waiting ones only go into the history, and the history drops its
/// oldest past <see cref="MaximumKept"/>.
/// </remarks>
public sealed class Notifications
{
    /// <summary>The most characters a notification's text holds; longer text is cut with an ellipsis.</summary>
    public const int MaximumTextLength = 200;

    /// <summary>How many toasts show at once.</summary>
    public const int MaximumShowing = 3;

    /// <summary>How many toasts wait to show, past which the oldest waiting ones only go into the history.</summary>
    public const int MaximumWaiting = 10;

    /// <summary>How many notifications the history keeps.</summary>
    public const int MaximumKept = 50;

    /// <summary>How long a toast shows.</summary>
    public static readonly TimeSpan ShowFor = TimeSpan.FromSeconds(5);

    private readonly Func<TimeSpan> clock;
    private readonly List<Notification> history = [];
    private readonly List<Notification> waiting = [];

    /// <param name="clock">The time, on a clock that only goes forward; a stopwatch started now, unless a test sets one.</param>
    public Notifications(Func<TimeSpan>? clock = null)
    {
        if (clock is null)
        {
            var started = Stopwatch.GetTimestamp();
            clock = () => Stopwatch.GetElapsedTime(started);
        }

        this.clock = clock;
    }

    /// <summary>The time on this list's clock, which <see cref="Notification.Posted"/> is measured on.</summary>
    public TimeSpan Now => clock();

    /// <summary>The history, newest first.</summary>
    public IReadOnlyList<Notification> History => history;

    /// <summary>The notifications waiting to show as toasts, oldest first.</summary>
    public IReadOnlyList<Notification> Waiting => waiting;

    /// <summary>Whether something came since the history was last seen, for the bell's dot.</summary>
    public bool HasUnseen { get; private set; }

    /// <summary>Rises with every change, so the desktop knows to draw again.</summary>
    public int Version { get; private set; }

    /// <summary>Posts a notification: into the history, and to show as a toast when <paramref name="show"/> is set.</summary>
    /// <param name="show">Whether to show it as a toast; not while the desktop is hidden, where nobody would see it.</param>
    /// <returns>Whether it was posted; when it wasn't, <paramref name="problem"/> says why.</returns>
    public bool TryPost(
        string title, string text, string? app, string? icon, bool show, [NotNullWhen(true)] out Notification? posted,
        [NotNullWhen(false)] out string? problem)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(text);
        posted = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            problem = "a notification needs some text";
            return false;
        }

        problem = null;
        posted = new Notification(title, Cut(text.Trim()), app, icon, clock());
        history.Insert(0, posted);
        if (history.Count > MaximumKept)
            history.RemoveRange(MaximumKept, history.Count - MaximumKept);

        if (show)
        {
            waiting.Add(posted);
            if (waiting.Count > MaximumWaiting)
                waiting.RemoveRange(0, waiting.Count - MaximumWaiting);
        }

        HasUnseen = true;
        Version++;
        return true;
    }

    /// <summary>Takes the oldest notification waiting to show, for the toasts to show it.</summary>
    public Notification? TakeWaiting()
    {
        if (waiting.Count == 0)
            return null;
        var next = waiting[0];
        waiting.RemoveAt(0);
        Version++;
        return next;
    }

    /// <summary>The history was seen, as opening it does: the bell's dot goes, and nothing waits to show.</summary>
    public void MarkSeen()
    {
        if (!HasUnseen && waiting.Count == 0)
            return;
        HasUnseen = false;
        waiting.Clear();
        Version++;
    }

    /// <summary>Empties the history, as Clear All does.</summary>
    public void Clear()
    {
        history.Clear();
        waiting.Clear();
        HasUnseen = false;
        Version++;
    }

    // Text past the limit is cut, with an ellipsis that keeps it within it, never splitting a pair.
    private static string Cut(string text)
    {
        if (text.Length <= MaximumTextLength)
            return text;
        var kept = text[..(MaximumTextLength - 1)];
        if (char.IsHighSurrogate(kept[^1]))
            kept = kept[..^1];
        return kept.TrimEnd() + "…";
    }
}
