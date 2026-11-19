using Venture.OS.Interface;

namespace Venture.OS.Desktop;

/// <summary>An app in the dock, or a minimized window.</summary>
/// <param name="Name">The name it shows: an app's from its <c>Info.xml</c>, or a window's title.</param>
/// <param name="Target">What opens an app, such as its name in the apps folder; empty for a window.</param>
/// <param name="IsOpen">Whether the app is open, which puts a dot under its icon.</param>
/// <param name="IsUser">Whether it's one of the user's apps, which sit after the system's, past a divider.</param>
/// <param name="Window">The minimized window this tile restores, after the apps, past another divider.</param>
/// <param name="Icon">The app's icon, at <see cref="Dock.IconSize"/>, or <see langword="null"/> for a tile with its
/// initial.</param>
public sealed record DockItem(
    string Name, string Target, bool IsOpen, bool IsUser = false, DesktopWindow? Window = null, DesktopIcon? Icon = null)
{
    /// <summary>Which group it's in, left to right: the system's apps, the user's, then minimized windows.</summary>
    public int Group => Window is not null ? 2 : IsUser ? 1 : 0;
}

/// <summary>
/// The dock: a <see cref="Shelf"/> along the bottom of the screen with an icon for each app, the system's then the
/// user's, then a tile for each minimized window, with a divider between the groups.
/// </summary>
/// <remarks>
/// <para>Clicking an icon reports its item, so the desktop can open the app; a press that leaves the icon before the
/// button comes up does nothing. The icon under the pointer shows the app's name above it, and an open app has a dot
/// below.</para>
/// <para>An app shows the icon its <c>Info.xml</c> names, as a rounded tile or a shape of its own
/// (<see cref="DesktopIcon"/>). One without, or whose icon couldn't be read, is a colored tile with its initial, its
/// color from its name, so an app keeps its color.</para>
/// </remarks>
public sealed class Dock(int screenWidth, int screenHeight)
{
    /// <summary>An icon's width and height.</summary>
    public const int IconSize = Shelf.IconSize;

    private readonly Shelf shelf = new();
    private IReadOnlyList<DockItem> items = [];
    private int pressed = -1;
    private int hovered = -1;

    /// <summary>The apps in the dock, left to right.</summary>
    public IReadOnlyList<DockItem> Items => items;

    /// <summary>The icon under the pointer, or <see langword="null"/>.</summary>
    public DockItem? Hovered => hovered >= 0 && hovered < items.Count ? items[hovered] : null;

    /// <summary>The shelf's height, which windows may sit over.</summary>
    public static int Height => Shelf.Height;

    /// <summary>The shelf, centered along the bottom; empty with no apps.</summary>
    public UIRect Bounds => shelf.Bounds;

    /// <summary>Replaces the apps, such as after one opens or closes.</summary>
    public void SetItems(IReadOnlyList<DockItem> newItems)
    {
        ArgumentNullException.ThrowIfNull(newItems);
        items = newItems;
        var shown = new ShelfItem[newItems.Count];
        for (var index = 0; index < shown.Length; index++)
        {
            var item = newItems[index];
            shown[index] = new ShelfItem(item.Name, item.Icon, item.IsOpen, item.Group, item.Window is not null);
        }

        shelf.SetItems(shown);
        shelf.Place((screenWidth - shelf.Width) / 2, screenHeight - Height);
        if (pressed >= items.Count)
            pressed = -1;
        if (hovered >= items.Count)
            hovered = -1;
    }

    /// <summary>Whether a point is on the shelf.</summary>
    public bool Contains(int x, int y) => items.Count > 0 && Bounds.Contains(x, y);

    /// <summary>Where an icon is.</summary>
    public UIRect IconBounds(int index) => shelf.IconBounds(index);

    /// <summary>Handles the pointer for a frame.</summary>
    /// <param name="owned">Whether the dock has the pointer: the button went down on it, or isn't down at all.</param>
    /// <returns>The app clicked, if one was.</returns>
    public DockItem? Update(int x, int y, bool owned, bool justPressed, bool released)
    {
        hovered = owned ? shelf.IndexAt(x, y) : -1;

        if (justPressed && owned)
            pressed = hovered;

        if (!released)
            return null;

        var clicked = pressed >= 0 && pressed == shelf.IndexAt(x, y) ? items[pressed] : null;
        pressed = -1;
        return clicked;
    }

    /// <summary>Draws the shelf, the icons, the dots under open apps, and the name of the icon under the pointer.</summary>
    public void Draw(IPainter painter) => shelf.Draw(painter, hovered, pressed, screenWidth);
}
