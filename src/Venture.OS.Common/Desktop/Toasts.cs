namespace Venture.OS.Desktop;

/// <summary>
/// The toasts on the screen: up to <see cref="Notifications.MaximumShowing"/> notifications in the top-right corner,
/// below the menu bar, newest on top. Each slides in from the right edge, stays <see cref="Notifications.ShowFor"/>,
/// and slides out; a click dismisses one at once. They're passive: nothing is called back. See
/// <c>docs/design/2026-11-25-toasts-design.md</c>.
/// </summary>
/// <remarks>
/// The desktop draws them over every window and panel and under open menus, and they never take the keyboard. They
/// change only as one arrives, moves, or leaves, so the frame loop redraws for them then, and not while they sit.
/// </remarks>
public sealed class Toasts(Notifications notifications)
{
    /// <summary>How wide a toast is.</summary>
    public const int Width = NotificationCard.Width;

    /// <summary>The room between a toast and the screen's edge, the menu bar, and the next toast.</summary>
    public const int Margin = 8;

    /// <summary>How long a toast takes to slide in or out, as long as the dock's.</summary>
    public static readonly TimeSpan SlideTime = WindowManager.SlideTime;

    private readonly List<Shown> shown = [];

    // Where each was last drawn, for clicks: the frame's toasts, newest first.
    private readonly List<(Shown Toast, UIRect Bounds)> drawn = [];

    /// <summary>The notifications showing now, newest first, leaving ones included.</summary>
    public IReadOnlyList<Notification> Showing => [.. shown.Select(toast => toast.Notification)];

    /// <summary>Whether one is sliding, so the frame loop should draw every frame.</summary>
    public bool IsMoving { get; private set; }

    /// <summary>
    /// Brings the toasts up to <paramref name="now"/>: waiting ones come in while there's room, ones shown long enough
    /// start to leave, and ones that have left go. While <paramref name="hidden"/>, as when the history is open,
    /// none come in, and the ones showing leave.
    /// </summary>
    /// <returns>Whether the toasts look different, so the frame should be drawn.</returns>
    public bool Update(TimeSpan now, bool hidden = false)
    {
        // The frame loop calls this every frame, so it allocates nothing unless a toast comes or goes.
        if (shown.Count == 0 && (hidden || notifications.Waiting.Count == 0))
        {
            IsMoving = false;
            return false;
        }

        var changed = false;
        var staying = 0;
        for (var index = shown.Count - 1; index >= 0; index--)
        {
            var toast = shown[index];
            if (toast.Leaving is null && (hidden || now - toast.Since >= Notifications.ShowFor))
            {
                toast.Leaving = now;
                changed = true;
            }

            if (toast.Leaving is { } leaving && now - leaving >= SlideTime)
            {
                shown.RemoveAt(index);
                changed = true;
            }
            else if (toast.Leaving is null)
            {
                staying++;
            }
        }

        while (!hidden && staying < Notifications.MaximumShowing && notifications.TakeWaiting() is { } next)
        {
            shown.Insert(0, new Shown(next, now));
            staying++;
            changed = true;
        }

        IsMoving = false;
        foreach (var toast in shown)
            IsMoving |= now - toast.Since < SlideTime || toast.Leaving is not null;
        return changed || IsMoving;
    }

    /// <summary>A press at a point: a toast there leaves at once, the press going no further.</summary>
    /// <returns>Whether a toast took it.</returns>
    public bool Press(int x, int y, TimeSpan now)
    {
        foreach (var (toast, bounds) in drawn)
        {
            if (!bounds.Contains(x, y))
                continue;
            toast.Leaving ??= now;
            IsMoving = true;
            return true;
        }

        return false;
    }

    /// <summary>Draws the toasts at the top-right, below <paramref name="top"/>.</summary>
    /// <param name="icon">An icon by path, at <see cref="NotificationCard.IconSize"/>, or VentureOS's for <see langword="null"/>.</param>
    public void Draw(IPainter painter, int top, TimeSpan now, Func<string?, DesktopImage?> icon, DesktopText text)
    {
        ArgumentNullException.ThrowIfNull(painter);
        ArgumentNullException.ThrowIfNull(icon);
        ArgumentNullException.ThrowIfNull(text);
        drawn.Clear();

        var y = top + Margin;
        foreach (var toast in shown)
        {
            var lines = NotificationCard.Lines(painter, toast.Notification.Text);
            var x = painter.Width - Margin - Width + Offset(toast, now);
            var height = NotificationCard.Draw(painter, x, y, toast.Notification, lines, now, icon, text);
            drawn.Add((toast, new UIRect(x, y, Width, height)));
            y += height + Margin;
        }
    }

    // How far right of its place a toast is: off the screen as it starts and as it finishes leaving.
    private static int Offset(Shown toast, TimeSpan now)
    {
        var slid = toast.Leaving is { } leaving ? 1 - Progress(now - leaving) : Progress(now - toast.Since);
        return (int)((Width + Margin) * (1 - slid));
    }

    // How far through a slide, from 0 to 1, easing out.
    private static double Progress(TimeSpan elapsed)
    {
        var linear = Math.Clamp(elapsed / SlideTime, 0, 1);
        return 1 - ((1 - linear) * (1 - linear));
    }

    private sealed class Shown(Notification notification, TimeSpan since)
    {
        public Notification Notification { get; } = notification;

        public TimeSpan Since { get; } = since;

        public TimeSpan? Leaving { get; set; }
    }
}
