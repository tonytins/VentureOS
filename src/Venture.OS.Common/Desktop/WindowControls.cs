using System.Diagnostics.CodeAnalysis;
using Venture.OS.Interface;

namespace Venture.OS.Desktop;

/// <summary>
/// Using a window's controls by name, as a person would, for whatever drives an app without a pointer: airo's app
/// tests and vdb's <c>ui</c> commands. Both say the same words for the same things, so these are the one place
/// those words mean something.
/// </summary>
/// <remarks>
/// <para>An action does what the desktop does after a click or a key: it changes the control, then hands the app
/// its event, whose handler runs before the action returns. A control a person couldn't use, because it's missing,
/// hidden, or dimmed, isn't used, and the reason says which; callers add their own prefix, such as
/// <c>app:press: </c>.</para>
/// <para>The menu bar's items, toolbar items, and the title field count as the window's, since they're there
/// whenever it is; only dimming stops them.</para>
/// </remarks>
public static class WindowControls
{
    /// <summary>Whether the window has a control, list, menu item, toolbar item, or title field of that name.</summary>
    public static bool Has(DesktopWindow window, string name)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(name);
        return window.View.Bounds(name) is not null || window.View.List(name) is not null || InBars(window, name);
    }

    /// <summary>Checks that the window has <paramref name="name"/>, before reading it.</summary>
    public static bool TryFind(DesktopWindow window, string name, [NotNullWhen(false)] out string? problem)
    {
        problem = Has(window, name) ? null : $"there's no \"{name}\" in {window.Title}";
        return problem is null;
    }

    /// <summary>Presses a button, toolbar item, or menu item.</summary>
    public static bool TryPress(DesktopWindow window, string name, [NotNullWhen(false)] out string? problem)
    {
        if (!TryUse(window, name, out problem))
            return false;
        Send(window, new UIEvent(UIEventKind.Pressed, name));
        return true;
    }

    /// <summary>Puts <paramref name="text"/> in a field, or the title bar's field, and presses Enter.</summary>
    public static bool TrySubmit(DesktopWindow window, string name, string text, [NotNullWhen(false)] out string? problem)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!TryUse(window, name, out problem))
            return false;
        window.View.SetText(name, text);
        Send(window, new UIEvent(UIEventKind.Submitted, name, text));
        return true;
    }

    /// <summary>Puts <paramref name="text"/> in a field or text area, without Enter.</summary>
    public static bool TryType(DesktopWindow window, string name, string text, [NotNullWhen(false)] out string? problem)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!TryUse(window, name, out problem))
            return false;
        window.View.SetText(name, text);
        Send(window, new UIEvent(UIEventKind.Changed, name, text));
        return true;
    }

    /// <summary>Chooses a list's or sidebar's row, or a grid's item, by its number.</summary>
    /// <param name="row">Counting from 1, as tests and agents count rows.</param>
    public static bool TryChoose(DesktopWindow window, string name, int row, [NotNullWhen(false)] out string? problem)
    {
        if (!TryUseChoices(window, name, out var choices, out problem))
            return false;
        if (row < 1 || row > choices.Rows.Count)
        {
            problem = $"\"{name}\" has {choices.Rows.Count} rows, so there's no row {row}";
            return false;
        }

        Choose(window, name, choices, row - 1);
        return true;
    }

    /// <summary>Chooses a list's or sidebar's row by its text, as a person reads it, or a grid's item by its title or name.</summary>
    public static bool TryChoose(DesktopWindow window, string name, string text, [NotNullWhen(false)] out string? problem)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!TryUseChoices(window, name, out var choices, out problem))
            return false;
        var index = choices.IndexOf(text);
        if (index < 0)
        {
            problem = $"\"{name}\" has no row \"{text}\"; its rows are {string.Join(", ", choices.Rows)}";
            return false;
        }

        Choose(window, name, choices, index);
        return true;
    }

    /// <summary>A list's, sidebar's, or grid's rows and the one chosen, or <see langword="null"/> when it isn't one.</summary>
    public static WindowChoices? Choices(DesktopWindow window, string name)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(name);
        return ChoicesOf(window.View, name) is { } choices
            ? new WindowChoices(choices.Rows, choices.Selected >= 0 ? choices.Selected + 1 : null)
            : null;
    }

    /// <summary>
    /// Everything in the window a person could read or use, in the document's order: its controls, the sidebar, the
    /// title field, toolbar items, then menu items, each with what the reads above would say of it.
    /// </summary>
    public static WindowSnapshot Snapshot(DesktopWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);
        var view = window.View;
        var script = view.Script;
        var focused = view.FocusedControl;
        var controls = new List<ControlSnapshot>();

        foreach (var control in script.Controls)
        {
            var choices = Choices(window, control.Name);
            controls.Add(new ControlSnapshot(
                control.Name,
                KindOf(control.Type),
                view.Text(control.Name),
                view.Value(control.Name),
                view.IsShowing(control.Name),
                view.IsDisabled(control.Name),
                view.IsSelected(control.Name),
                focused == control.Name,
                choices?.Rows,
                choices?.Chosen));
        }

        if (script.Sidebar is { } sidebar && Choices(window, sidebar.Name) is { } rows)
            controls.Add(new ControlSnapshot(sidebar.Name, "sidebar", null, null, true, view.IsDisabled(sidebar.Name), false, false, rows.Rows, rows.Chosen));

        if (script.TitleField is { } field)
            controls.Add(InBar(window, field.Name, "titleField", view.Text(field.Name), focused == field.Name));

        foreach (var item in script.ToolbarItems)
            controls.Add(InBar(window, item.Name, "toolbarItem", item.Title, false));

        foreach (var menu in script.Menus)
        {
            foreach (var entry in menu.AllEntries())
            {
                if (entry.Name is { } entryName)
                    controls.Add(InBar(window, entryName, "menuItem", entry.Title, false));
            }
        }

        return new WindowSnapshot(
            window.ShowsTitle ? window.Title : null,
            view.Subtitle,
            view.SelectedPane?.Name,
            focused,
            controls);
    }

    private static ControlSnapshot InBar(DesktopWindow window, string name, string kind, string? text, bool focused) =>
        new(name, kind, text, null, true, window.View.IsDisabled(name), false, focused, null, null);

    // The names a snapshot gives each kind of control, as written in a document, starting lowercase.
    private static string KindOf(UIControlType type) => type switch
    {
        UIControlType.Text => "text",
        UIControlType.FormattedText => "formattedText",
        UIControlType.Button => "button",
        UIControlType.TextField => "textField",
        UIControlType.TextArea => "textArea",
        UIControlType.List => "list",
        UIControlType.ProgressBar => "progressBar",
        UIControlType.Region => "region",
        UIControlType.Shelf => "shelf",
        UIControlType.Grid => "grid",
        _ => "control",
    };

    private static void Send(DesktopWindow window, UIEvent uiEvent) => window.EventHandler?.Invoke(window, uiEvent);

    // As a click does: the list or grid selects it, then reports it.
    private static void Choose(DesktopWindow window, string name, Choosable choices, int index)
    {
        choices.Select(index);
        Send(window, new UIEvent(UIEventKind.Selected, name, Index: index, Item: choices.ItemName(index)));
    }

    // The control a person is about to use, which has to be there, showing, and not dimmed.
    private static bool TryUse(DesktopWindow window, string name, [NotNullWhen(false)] out string? problem)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!TryFind(window, name, out problem))
            return false;

        // The menu bar and title bar are always there, so only dimming counts for what's in them.
        if (!InBars(window, name) && !window.View.IsShowing(name))
            problem = $"\"{name}\" isn't showing, so a person couldn't use it";
        else if (window.View.IsDisabled(name))
            problem = $"\"{name}\" is dimmed, so a person couldn't use it";
        return problem is null;
    }

    private static bool TryUseChoices(
        DesktopWindow window, string name, [NotNullWhen(true)] out Choosable? choices, [NotNullWhen(false)] out string? problem)
    {
        choices = null;
        if (!TryUse(window, name, out problem))
            return false;
        choices = ChoicesOf(window.View, name);
        if (choices is null)
        {
            problem = $"\"{name}\" isn't a list or a grid";
            return false;
        }

        return true;
    }

    // Whether it's in the menu bar or the title bar, rather than among the window's controls.
    private static bool InBars(DesktopWindow window, string name)
    {
        var script = window.View.Script;
        if (script.TitleField?.Name == name)
            return true;
        foreach (var item in script.ToolbarItems)
        {
            if (item.Name == name)
                return true;
        }

        foreach (var menu in script.Menus)
        {
            foreach (var entry in menu.AllEntries())
            {
                if (entry.Name == name)
                    return true;
            }
        }

        return false;
    }

    private static Choosable? ChoicesOf(UIView view, string name) => (view.List(name), view.Grid(name)) switch
    {
        ({ } list, _) => new Choosable([.. list.Items], list.SelectedIndex, index => list.SelectedIndex = index, _ => null),
        (_, { } grid) => new Choosable(
            [.. grid.Items.Select(item => item.Title)], grid.SelectedIndex, index => grid.SelectedIndex = index,
            index => grid.Items[index].Name, [.. grid.Items.Select(item => item.Name)]),
        _ => null,
    };

    private sealed record Choosable(
        IReadOnlyList<string> Rows, int Selected, Action<int> Select, Func<int, string?> ItemName, IReadOnlyList<string?>? Names = null)
    {
        public int IndexOf(string text)
        {
            for (var index = 0; index < Rows.Count; index++)
            {
                if (Rows[index] == text)
                    return index;
            }

            for (var index = 0; index < (Names?.Count ?? 0); index++)
            {
                if (Names![index] == text)
                    return index;
            }

            return -1;
        }
    }
}

/// <summary>A list's, sidebar's, or grid's rows, as a person reads them, and the one chosen.</summary>
/// <param name="Chosen">The chosen row, counting from 1, or <see langword="null"/> when none is.</param>
public sealed record WindowChoices(IReadOnlyList<string> Rows, int? Chosen);

/// <summary>What a window shows a person, from <see cref="WindowControls.Snapshot"/>.</summary>
/// <param name="Title">Its title, or <see langword="null"/> when its title bar is blank.</param>
/// <param name="Pane">The sidebar pane shown, if it has panes.</param>
/// <param name="Focused">The control with the keyboard.</param>
public sealed record WindowSnapshot(string? Title, string? Subtitle, string? Pane, string? Focused, IReadOnlyList<ControlSnapshot> Controls);

/// <summary>One thing in a window a person could read or use.</summary>
/// <param name="Kind">
/// The element it is, as a document writes it, starting lowercase (<c>button</c>, <c>textField</c>, <c>grid</c>,
/// ...), or <c>sidebar</c>, <c>titleField</c>, <c>toolbarItem</c>, or <c>menuItem</c>.
/// </param>
/// <param name="Text">A label's, button's, or field's text, or an item's title.</param>
/// <param name="Value">A progress bar's value.</param>
/// <param name="Checked">Whether a checkbox, radio button, or toggle is on; <see langword="false"/> for the rest.</param>
/// <param name="Rows">A list's, sidebar's, or grid's rows; <see langword="null"/> for the rest.</param>
/// <param name="Chosen">The chosen row, counting from 1.</param>
public sealed record ControlSnapshot(
    string Name,
    string Kind,
    string? Text,
    float? Value,
    bool Showing,
    bool Dimmed,
    bool Checked,
    bool Focused,
    IReadOnlyList<string>? Rows,
    int? Chosen);
