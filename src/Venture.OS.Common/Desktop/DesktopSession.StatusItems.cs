using Venture.OS.Interface;

namespace Venture.OS.Desktop;

/// <summary>
/// Apps' status items in the menu bar, left of the bell, after the sidebar's logic: one whose document holds menus
/// drops them down; one that holds controls opens a panel of them, made of the shelf as toasts are; one that holds
/// neither is a button. Each is a window of its app's, so it ends with the app, which the bar draws and the window
/// manager never shows. See <c>docs/design/2026-11-25-status-items-design.md</c>.
/// </summary>
public sealed partial class DesktopSession
{
    /// <summary>How tall a status item's symbol is in the bar.</summary>
    public const int StatusIconSize = 16;

    // The status item whose panel is open, if one is.
    private DesktopWindow? statusPanel;

    // Each status item's symbol at the bar's size, made once from its document's image.
    private readonly Dictionary<DesktopWindow, (DesktopImage Source, DesktopImage Scaled)> statusIcons = [];

    /// <summary>The status item whose panel is open, or <see langword="null"/>.</summary>
    public DesktopWindow? OpenStatusPanel => statusPanel;

    /// <summary>
    /// A number that changes as the status items do, opening, closing, or changing title, so the bar's menus are built
    /// again then and not every frame. It allocates nothing.
    /// </summary>
    private int StatusKey()
    {
        // By index: a foreach over the list as an interface would allocate its enumerator every frame.
        var key = 17;
        var windows = Windows.Windows;
        for (var index = 0; index < windows.Count; index++)
        {
            if (windows[index].IsStatusItem)
                key = unchecked((key * 31) + windows[index].Id + (windows[index].Title.GetHashCode() * 7));
        }

        return key;
    }

    /// <summary>Adds each status item to the bar's menus, in the order they opened, after the rest.</summary>
    private void AddStatusMenus(List<BarMenu> menus)
    {
        foreach (var window in Windows.Windows)
        {
            if (!window.IsStatusItem || window.View.Script.StatusItem is not { } item)
                continue;

            var script = window.View.Script;
            var icon = StatusIcon(window, item);
            if (script.Menus.Count > 0)
            {
                // One menu drops down as it is; several drop down as submenus of the item, each under its title.
                IReadOnlyList<UIMenuEntry> entries = script.Menus.Count == 1
                    ? script.Menus[0].Entries
                    : [.. script.Menus.Select(menu => new UIMenuEntry(menu.Name, menu.Title, null, null, menu.Line, menu))];
                menus.Add(new BarMenu(new UIMenu(item.Name, window.Title, null, entries, item.Line), MenuGroup.Status, window.View, Icon: icon));
            }
            else
            {
                var panel = script.Controls.Count > 0 || script.Visuals.Count > 0;
                Action press = panel ? () => ToggleStatusPanel(window) : () => PressStatusItem(window);
                menus.Add(new BarMenu(new UIMenu(item.Name, window.Title, null, [], item.Line), MenuGroup.Status, window.View, Icon: icon, Pressed: press));
            }
        }

        // A panel whose item has gone, as when its app ended, closes.
        if (statusPanel is not null && !Windows.Contains(statusPanel))
            statusPanel = null;
    }

    private DesktopImage? StatusIcon(DesktopWindow window, UIStatusItem item)
    {
        if (item.Image is not { } name || window.View.Image(name) is not { } source || source.Height <= 0)
            return null;
        if (statusIcons.TryGetValue(window, out var known) && ReferenceEquals(known.Source, source))
            return known.Scaled;

        var width = Math.Max(1, source.Width * StatusIconSize / source.Height);
        var scaled = source.Height == StatusIconSize ? source : source.Scaled(width, StatusIconSize);
        statusIcons[window] = (source, scaled);
        return scaled;
    }

    // A status item that's a button: pressed, as a toolbar item is.
    private void PressStatusItem(DesktopWindow window)
    {
        UIEvent pressed = new(UIEventKind.Pressed, window.View.Script.StatusItem!.Name);
        happened.Add(new WindowEvent(window, pressed));
        window.EventHandler?.Invoke(window, pressed);
    }

    /// <summary>Opens a status item's panel, or closes it when it's the one open; the history and menus close for it.</summary>
    private void ToggleStatusPanel(DesktopWindow window)
    {
        if (statusPanel == window)
        {
            CloseStatusPanel();
            return;
        }

        History.Close();
        statusPanel = window;
        historyChanged = true;
    }

    private void CloseStatusPanel()
    {
        if (statusPanel is null)
            return;

        statusPanel.View.ClearFocus();
        statusPanel = null;
        historyChanged = true;
    }

    /// <summary>
    /// Where the open panel is: below the bar, its right edge under its item's, kept on the screen; empty without one.
    /// </summary>
    private UIRect StatusPanelBounds()
    {
        if (statusPanel is null)
            return default;

        var size = statusPanel.View.WindowSize;
        var right = (float)Width - Toasts.Margin;
        for (var index = 0; index < MenuBar.Menus.Count; index++)
        {
            if (MenuBar.Menus[index].View == statusPanel.View && MenuBar.TitleBounds(index) is { Width: > 0 } title)
                right = title.X + title.Width;
        }

        var x = Math.Clamp(right - size.Width, Toasts.Margin, Math.Max(Toasts.Margin, Width - Toasts.Margin - size.Width));
        return new UIRect(x, MenuBar.Height + 4, size.Width, size.Height);
    }

    private bool OverStatusPanel(int x, int y) => statusPanel is not null && StatusPanelBounds().Contains(x, y);

    /// <summary>Hands the open panel its pointer and keys for the frame, and its owner what they made happen.</summary>
    private void UpdateStatusPanel(int x, int y, DesktopInput input, bool pressed, bool released, List<WindowEvent> events)
    {
        if (statusPanel is not { } window)
            return;

        var bounds = StatusPanelBounds();
        var mine = owner == PointerOwner.Desktop;
        UIViewInput viewInput = new(
            x, y, input.PointerDown && mine, pressed && mine && bounds.Contains(x, y), released && mine,
            bounds.Contains(x, y) ? input.Scroll : 0, forPanel.Count == 0 ? [] : [.. forPanel], input.Time);
        foreach (var uiEvent in window.View.Update(viewInput, (int)bounds.X, (int)bounds.Y))
        {
            events.Add(new WindowEvent(window, uiEvent));
            window.EventHandler?.Invoke(window, uiEvent);
        }

        if (input.HasEvents || pressed || released || input.PointerDown)
            historyChanged = true;
    }

    /// <summary>Draws the open panel: the shelf's card, and the item's controls on it.</summary>
    private void DrawStatusPanel(IPainter painter)
    {
        if (statusPanel is not { } window)
            return;

        var bounds = StatusPanelBounds();
        NotificationCard.DrawBody(painter, (int)bounds.X, (int)bounds.Y, (int)bounds.Width, (int)bounds.Height);
        window.View.Draw(painter, (int)bounds.X, (int)bounds.Y);
    }
}
