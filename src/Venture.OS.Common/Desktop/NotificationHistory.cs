namespace Venture.OS.Desktop;

/// <summary>
/// The notification history, after Mountain Lion's Notification Center as modern macOS draws it: a column of floating
/// cards down the right edge, a heading with Clear All, then a card per app, newest app first, its notifications
/// stacked with the newest on top. The bell beside the clock opens it. See
/// <c>docs/design/2026-11-25-toasts-design.md</c>.
/// </summary>
/// <remarks>
/// It has no backing: the wallpaper shows between the cards, and every word sits on a card, so it reads over any
/// wallpaper. A click on a stack expands it, and on an expanded app's card collapses it; nothing calls an app.
/// </remarks>
public sealed class NotificationHistory(Notifications notifications)
{
    /// <summary>The room between the column and the screen's edge, and between its cards.</summary>
    public const int Margin = 8;

    private const int HeadingHeight = 30;

    // How far the cards under a stack peek out below it, and how far in from its sides.
    private const int Peek = 5;
    private const int PeekInset = 10;

    // The apps whose stacks are expanded, by title.
    private readonly HashSet<string> expanded = new(StringComparer.Ordinal);

    // Where things were last drawn, for clicks.
    private readonly List<(UIRect Bounds, string App)> cards = [];
    private UIRect column;
    private UIRect clearAll;
    private int contentHeight;

    /// <summary>Whether it's open.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>How far down it's scrolled, in pixels.</summary>
    public int Scrolled { get; private set; }

    /// <summary>The apps with notifications, newest first, each with its notifications, newest first.</summary>
    public IReadOnlyList<(string App, IReadOnlyList<Notification> Notifications)> Groups
    {
        get
        {
            List<(string App, IReadOnlyList<Notification> Notifications)> groups = [];
            foreach (var notification in notifications.History)
            {
                var index = groups.FindIndex(group => group.App == notification.Title);
                if (index < 0)
                    groups.Add((notification.Title, [notification]));
                else
                    groups[index] = (groups[index].App, [.. groups[index].Notifications, notification]);
            }

            return groups;
        }
    }

    /// <summary>Opens it: what's arrived counts as seen, and toasts waiting to show go straight into it.</summary>
    public void Open()
    {
        IsOpen = true;
        Scrolled = 0;
        expanded.Clear();
        notifications.MarkSeen();
    }

    /// <summary>Closes it.</summary>
    public void Close()
    {
        IsOpen = false;
        cards.Clear();
    }

    /// <summary>Opens it when it's closed, and closes it when it's open.</summary>
    public void Toggle()
    {
        if (IsOpen)
            Close();
        else
            Open();
    }

    /// <summary>Whether a point is on the column's heading or a card.</summary>
    public bool Contains(int x, int y) => IsOpen && column.Contains(x, y);

    /// <summary>A press inside the column: Clear All, or a card, which expands or collapses its app.</summary>
    /// <returns>Whether anything changed.</returns>
    public bool Press(int x, int y)
    {
        if (!Contains(x, y))
            return false;

        if (clearAll.Contains(x, y))
        {
            notifications.Clear();
            expanded.Clear();
            Scrolled = 0;
            return true;
        }

        foreach (var (bounds, app) in cards)
        {
            if (!bounds.Contains(x, y))
                continue;
            if (!expanded.Remove(app))
                expanded.Add(app);
            return true;
        }

        return false;
    }

    /// <summary>Scrolls by the wheel's turn, kept within what there is.</summary>
    /// <returns>Whether it moved.</returns>
    public bool Scroll(int lines)
    {
        var room = Math.Max(0, contentHeight - (int)column.Height);
        var scrolled = Math.Clamp(Scrolled - (lines * NotificationCard.LineHeight * 3), 0, room);
        if (scrolled == Scrolled)
            return false;
        Scrolled = scrolled;
        return true;
    }

    /// <summary>Draws the column in <paramref name="area"/>, the screen between the menu bar and the dock.</summary>
    public void Draw(IPainter painter, UIRect area, TimeSpan now, Func<string?, DesktopImage?> icon, DesktopText text)
    {
        ArgumentNullException.ThrowIfNull(painter);
        ArgumentNullException.ThrowIfNull(icon);
        ArgumentNullException.ThrowIfNull(text);
        cards.Clear();
        if (!IsOpen)
            return;

        var x = painter.Width - Margin - NotificationCard.Width;
        var top = (int)area.Y + Margin;
        column = new UIRect(x, top, NotificationCard.Width, Math.Max(0, (int)(area.Y + area.Height) - Margin - top));

        // Everything is laid out from the top, then moved up by how far it's scrolled; what falls outside isn't drawn.
        var y = top - Scrolled;
        DrawHeading(painter, x, y, text);
        y += HeadingHeight + Margin;

        var groups = Groups;
        if (groups.Count == 0)
        {
            NotificationCard.DrawBody(painter, x, y, NotificationCard.Width, HeadingHeight + 10);
            var empty = text.Get("noNotifications");
            painter.DrawText(empty, x + ((NotificationCard.Width - painter.MeasureText(empty, DesktopTheme.TextSize)) / 2),
                y + 12, DesktopTheme.SecondaryText, DesktopTheme.TextSize);
            y += HeadingHeight + 10 + Margin;
        }

        foreach (var (app, shown) in groups)
        {
            var isExpanded = shown.Count == 1 || expanded.Contains(app);
            var count = isExpanded ? shown.Count : 1;
            for (var index = 0; index < count; index++)
            {
                var notification = shown[index];
                var lines = NotificationCard.Lines(painter, notification.Text);
                var more = isExpanded ? null : text.Get("moreFrom", (shown.Count - 1).ToString(System.Globalization.CultureInfo.InvariantCulture), app);
                var height = NotificationCard.Height(lines.Count, more is not null);
                if (!isExpanded)
                {
                    // The cards beneath peek out under the stack, the nearer one wider.
                    var peeks = Math.Min(2, shown.Count - 1);
                    for (var peek = peeks; peek >= 1; peek--)
                    {
                        NotificationCard.DrawBody(painter, x + (PeekInset * peek), y + height - 10 + (Peek * peek),
                            NotificationCard.Width - (2 * PeekInset * peek), 10);
                    }
                }

                if (y + height > top && y < column.Y + column.Height)
                    NotificationCard.Draw(painter, x, y, notification, lines, now, icon, text, more);
                cards.Add((new UIRect(x, y, NotificationCard.Width, height), app));
                y += height + Margin + (isExpanded ? 0 : Peek * Math.Min(2, shown.Count - 1));
            }
        }

        contentHeight = y + Scrolled - top;

        // The automatic scroll bar, in the margin beside the cards, while there's more than fits.
        if (contentHeight > column.Height && column.Height > 0)
        {
            var thumb = Math.Max(20, (int)(column.Height * column.Height / contentHeight));
            var room = contentHeight - (int)column.Height;
            var thumbY = top + (int)((column.Height - thumb) * Scrolled / Math.Max(1, room));
            DesktopTheme.FillRoundedGradient(painter, x + NotificationCard.Width + 2, thumbY, 4, thumb, 2,
                DesktopTheme.SecondaryText, DesktopTheme.SecondaryText);
        }
    }

    private void DrawHeading(IPainter painter, int x, int y, DesktopText text)
    {
        // On a card of its own, so the heading reads over any wallpaper too.
        NotificationCard.DrawBody(painter, x, y, NotificationCard.Width, HeadingHeight);
        var textY = y + ((HeadingHeight - DesktopTheme.TextSize) / 2) - 2;
        painter.DrawText(text.Get("notifications"), x + NotificationCard.Padding, textY, DesktopTheme.Text, DesktopTheme.TextSize);

        var clear = text.Get("clearAll");
        var clearWidth = painter.MeasureText(clear, DesktopTheme.TextSize);
        var clearX = x + NotificationCard.Width - NotificationCard.Padding - clearWidth;
        painter.DrawText(clear, clearX, textY, DesktopTheme.Accent, DesktopTheme.TextSize);
        clearAll = new UIRect(clearX - 4, y, clearWidth + 8, HeadingHeight);
    }
}
