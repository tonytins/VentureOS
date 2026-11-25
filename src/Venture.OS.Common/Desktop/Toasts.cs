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
    public const int Width = 300;

    /// <summary>The room between a toast and the screen's edge, the menu bar, and the next toast.</summary>
    public const int Margin = 8;

    /// <summary>The icon's size.</summary>
    public const int IconSize = 32;

    /// <summary>The most lines of a toast's text; more end in an ellipsis.</summary>
    public const int MaximumLines = 3;

    /// <summary>How long a toast takes to slide in or out, as long as the dock's.</summary>
    public static readonly TimeSpan SlideTime = WindowManager.SlideTime;

    private const int Padding = 10;
    private const int CornerRadius = 10;
    private const int LineHeight = 18;

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
    /// <param name="icon">An icon by path, at <see cref="IconSize"/>, or VentureOS's for <see langword="null"/>.</param>
    public void Draw(IPainter painter, int top, TimeSpan now, Func<string?, DesktopImage?> icon, DesktopText text)
    {
        ArgumentNullException.ThrowIfNull(painter);
        ArgumentNullException.ThrowIfNull(icon);
        ArgumentNullException.ThrowIfNull(text);
        drawn.Clear();

        var y = top + Margin;
        foreach (var toast in shown)
        {
            var notification = toast.Notification;
            var textRoom = Width - (2 * Padding) - IconSize - Padding;
            var lines = UIView.Wrap(painter, notification.Text, DesktopTheme.TextSize, textRoom);
            if (lines.Count > MaximumLines)
            {
                lines[MaximumLines - 1] = UIView.Fit(painter, lines[MaximumLines - 1] + " " + lines[MaximumLines], DesktopTheme.TextSize, textRoom);
                if (!lines[MaximumLines - 1].EndsWith('…'))
                    lines[MaximumLines - 1] += "…";
                lines.RemoveRange(MaximumLines, lines.Count - MaximumLines);
            }

            var height = Math.Max(IconSize, LineHeight * (1 + lines.Count)) + (2 * Padding);
            var x = painter.Width - Margin - Width + Offset(toast, now);
            drawn.Add((toast, new UIRect(x, y, Width, height)));
            DrawCard(painter, x, y, Width, height);

            if (icon(notification.Icon) is { } picture)
                painter.DrawImage(picture, x + Padding, y + Padding);

            var textX = x + Padding + IconSize + Padding;
            var age = Age(now - notification.Posted, text);
            var ageWidth = painter.MeasureText(age, DesktopTheme.CaptionSize);
            painter.DrawText(UIView.Fit(painter, notification.Title, DesktopTheme.TextSize, textRoom - ageWidth - Padding),
                textX, y + Padding, DesktopTheme.Text, DesktopTheme.TextSize);
            painter.DrawText(age, x + Width - Padding - ageWidth, y + Padding + 2, DesktopTheme.SecondaryText, DesktopTheme.CaptionSize);
            for (var line = 0; line < lines.Count; line++)
                painter.DrawText(lines[line], textX, y + Padding + (LineHeight * (line + 1)), DesktopTheme.Text, DesktopTheme.TextSize);

            y += height + Margin;
        }
    }

    /// <summary>
    /// A toast's or a card's body: the window's short shadow, the shelf's bright edge, and the body over it, so the
    /// body is never fainter than its contrast was checked at.
    /// </summary>
    public static void DrawCard(IPainter painter, int x, int y, int width, int height)
    {
        DesktopTheme.FillRoundedGradient(painter, x, y + 2, width, height, CornerRadius, DesktopTheme.Shadow, DesktopTheme.Shadow);
        DesktopTheme.FillRoundedGradient(painter, x, y, width, height, CornerRadius, DesktopTheme.NotificationEdge, DesktopTheme.NotificationEdge);
        DesktopTheme.FillRoundedGradient(painter, x + 1, y + 1, width - 2, height - 2, CornerRadius - 1,
            DesktopTheme.NotificationBackground, DesktopTheme.NotificationBackground);
    }

    /// <summary>How long ago, in the desktop's words: now, then minutes, then hours.</summary>
    public static string Age(TimeSpan age, DesktopText text) =>
        age < TimeSpan.FromMinutes(1) ? text.Get("justNow")
        : age < TimeSpan.FromHours(1) ? text.Get("minutesAgo", ((int)age.TotalMinutes).ToString(System.Globalization.CultureInfo.InvariantCulture))
        : text.Get("hoursAgo", ((int)age.TotalHours).ToString(System.Globalization.CultureInfo.InvariantCulture));

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
