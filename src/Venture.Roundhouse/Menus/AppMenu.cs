using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Input;

namespace Venture.Roundhouse.Menus;

/// <summary>One entry in Roundhouse's menus: a command, a submenu, or a separator.</summary>
public abstract record MenuEntry;

/// <summary>A line between groups of commands.</summary>
public sealed record MenuSeparator : MenuEntry;

/// <summary>
/// A command: its title, what it does, its shortcut, and, read each time the menus are shown, whether it can run now
/// and whether it's checked.
/// </summary>
/// <param name="Title">What the menu says.</param>
/// <param name="Run">What it does.</param>
/// <param name="Gesture">Its shortcut, shown beside it, or <see langword="null"/>.</param>
/// <param name="IsEnabled">Whether it can run now; always, when <see langword="null"/>.</param>
/// <param name="IsChecked">Whether it's on, for a command that turns something on and off; <see langword="null"/> for one that doesn't.</param>
public sealed record MenuCommand(
    string Title,
    Action Run,
    KeyGesture? Gesture = null,
    Func<bool>? IsEnabled = null,
    Func<bool>? IsChecked = null) : MenuEntry;

/// <summary>A menu: a title and its entries, either on the menu bar or inside another menu.</summary>
public sealed record AppMenuGroup(string Title, IReadOnlyList<MenuEntry> Entries) : MenuEntry;

/// <summary>
/// Turns Roundhouse's menus into the two forms they're shown in, after Passenger: a <see cref="NativeMenu"/>, which
/// Avalonia hands to the desktop's global menu bar (KDE's over D-Bus, macOS's own), and a dropdown behind the window's
/// ☰ button, for desktops without one. Both come from the same list, so they never differ.
/// </summary>
public static class AppMenu
{
    /// <summary>Fills <paramref name="menu"/> with the menus as they are now: titles, shortcuts, and what's enabled and checked.</summary>
    public static void Fill(NativeMenu menu, IReadOnlyList<AppMenuGroup> menus)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(menus);
        menu.Items.Clear();
        foreach (var group in menus)
            menu.Items.Add(Native(group));
    }

    /// <summary>A dropdown of menus or entries as they are now, for the ☰ button or a menu of its own, such as Add's.</summary>
    public static MenuFlyout Flyout(IEnumerable<MenuEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        MenuFlyout flyout = new();
        foreach (var entry in entries)
            flyout.Items.Add(Item(entry));
        return flyout;
    }

    /// <summary>A title as a menu writes it: an underscore marks the access key, so a real one is doubled.</summary>
    public static string Words(string title) => title.Replace("_", "__", StringComparison.Ordinal);

    /// <summary>The command with this title in the menu with this title, or <see langword="null"/>.</summary>
    public static MenuCommand? Find(IReadOnlyList<AppMenuGroup> menus, string menu, string title) =>
        menus.FirstOrDefault(group => group.Title == menu) is { } found ? Find(found, title) : null;

    private static MenuCommand? Find(AppMenuGroup group, string title)
    {
        foreach (var entry in group.Entries)
        {
            if (entry is MenuCommand command && command.Title == title)
                return command;
            if (entry is AppMenuGroup inner && Find(inner, title) is { } nested)
                return nested;
        }

        return null;
    }

    private static NativeMenuItemBase Native(MenuEntry entry)
    {
        switch (entry)
        {
            case AppMenuGroup group:
            {
                NativeMenu submenu = new();
                foreach (var inner in group.Entries)
                    submenu.Items.Add(Native(inner));
                return new NativeMenuItem(Words(group.Title)) { Menu = submenu };
            }

            case MenuCommand command:
            {
                NativeMenuItem item = new(Words(command.Title))
                {
                    Command = new Runner(command.Run),
                    IsEnabled = command.IsEnabled?.Invoke() ?? true,
                };
                if (command.Gesture is { } gesture)
                    item.Gesture = gesture;
                if (command.IsChecked is { } isChecked)
                {
                    item.ToggleType = MenuItemToggleType.CheckBox;
                    item.IsChecked = isChecked();
                }

                return item;
            }

            default:
                return new NativeMenuItemSeparator();
        }
    }

    private static Control Item(MenuEntry entry)
    {
        switch (entry)
        {
            case AppMenuGroup group:
            {
                MenuItem item = new() { Header = Words(group.Title) };
                foreach (var inner in group.Entries)
                    item.Items.Add(Item(inner));
                return item;
            }

            case MenuCommand command:
            {
                MenuItem item = new()
                {
                    Header = Words(command.Title),
                    Command = new Runner(command.Run),
                    InputGesture = command.Gesture,
                    IsEnabled = command.IsEnabled?.Invoke() ?? true,
                };
                if (command.IsChecked is { } isChecked)
                {
                    item.ToggleType = MenuItemToggleType.CheckBox;
                    item.IsChecked = isChecked();
                }

                return item;
            }

            default:
                return new Separator();
        }
    }

    /// <summary>Runs a menu command; the menus are rebuilt when what's enabled changes, so it can always run.</summary>
    private sealed class Runner(Action run) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => run();
    }
}
