namespace Venture.OS.Desktop;

/// <summary>
/// How a notification is drawn, as a toast and as a card in the history: its icon, title, how long ago, and up to
/// <see cref="MaximumLines"/> lines of text, on the shelf's body. One drawing, so the two always match.
/// </summary>
public static class NotificationCard
{
    /// <summary>How wide a toast or a card is.</summary>
    public const int Width = 300;

    /// <summary>The icon's size.</summary>
    public const int IconSize = 32;

    /// <summary>The most lines of a notification's text; more end in an ellipsis.</summary>
    public const int MaximumLines = 3;

    /// <summary>The room inside a card's edge.</summary>
    public const int Padding = 10;

    /// <summary>The height of a line of text.</summary>
    public const int LineHeight = 18;

    private const int CornerRadius = 10;

    private const int TextRoom = Width - (2 * Padding) - IconSize - Padding;

    /// <summary>The lines a notification's text shows in, at most <see cref="MaximumLines"/>, the last cut with an ellipsis.</summary>
    public static List<string> Lines(IPainter painter, string text)
    {
        var lines = UIView.Wrap(painter, text, DesktopTheme.TextSize, TextRoom);
        if (lines.Count > MaximumLines)
        {
            var last = UIView.Fit(painter, lines[MaximumLines - 1] + " " + lines[MaximumLines], DesktopTheme.TextSize, TextRoom);
            lines[MaximumLines - 1] = last.EndsWith('…') ? last : last + "…";
            lines.RemoveRange(MaximumLines, lines.Count - MaximumLines);
        }

        return lines;
    }

    /// <summary>How tall a card is for these lines, and an extra one, such as "2 more from Notes", when there is.</summary>
    public static int Height(int lines, bool extra = false) =>
        Math.Max(IconSize, LineHeight * (1 + lines + (extra ? 1 : 0))) + (2 * Padding);

    /// <summary>Draws a notification as a card at <paramref name="x"/>, <paramref name="y"/>.</summary>
    /// <param name="lines">Its text's lines, from <see cref="Lines"/>.</param>
    /// <param name="extra">A line under the text in the secondary color, or <see langword="null"/>.</param>
    /// <returns>The card's height.</returns>
    public static int Draw(
        IPainter painter, int x, int y, Notification notification, List<string> lines, TimeSpan now,
        Func<string?, DesktopImage?> icon, DesktopText text, string? extra = null)
    {
        var height = Height(lines.Count, extra is not null);
        DrawBody(painter, x, y, Width, height);

        if (icon(notification.Icon) is { } picture)
            painter.DrawImage(picture, x + Padding, y + Padding);

        var textX = x + Padding + IconSize + Padding;
        var age = Age(now - notification.Posted, text);
        var ageWidth = painter.MeasureText(age, DesktopTheme.CaptionSize);
        painter.DrawText(UIView.Fit(painter, notification.Title, DesktopTheme.TextSize, TextRoom - ageWidth - Padding),
            textX, y + Padding, DesktopTheme.Text, DesktopTheme.TextSize);
        painter.DrawText(age, x + Width - Padding - ageWidth, y + Padding + 2, DesktopTheme.SecondaryText, DesktopTheme.CaptionSize);
        for (var line = 0; line < lines.Count; line++)
            painter.DrawText(lines[line], textX, y + Padding + (LineHeight * (line + 1)), DesktopTheme.Text, DesktopTheme.TextSize);
        if (extra is not null)
        {
            painter.DrawText(UIView.Fit(painter, extra, DesktopTheme.CaptionSize, TextRoom), textX,
                y + Padding + (LineHeight * (lines.Count + 1)) + 2, DesktopTheme.SecondaryText, DesktopTheme.CaptionSize);
        }

        return height;
    }

    /// <summary>
    /// A card's body: the window's short shadow, the shelf's bright edge, and the notification body over it, so the
    /// body is never fainter than its contrast was checked at.
    /// </summary>
    public static void DrawBody(IPainter painter, int x, int y, int width, int height)
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
}
