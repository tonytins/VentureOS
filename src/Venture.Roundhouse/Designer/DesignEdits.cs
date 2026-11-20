using System.Globalization;
using Venture.OS.Interface;
using Venture.OS.Interface.Xml;

namespace Venture.Roundhouse.Designer;

/// <summary>A control the library adds: its element, and the size and text it starts with.</summary>
/// <param name="Element">The element, such as <c>Button</c>.</param>
/// <param name="Width">How wide it starts.</param>
/// <param name="Height">How tall it starts.</param>
/// <param name="Text">The text it starts with, or <see langword="null"/> for a control that shows none.</param>
public sealed record LibraryControl(string Element, int Width, int Height, string? Text);

/// <summary>
/// The designer's edits beyond a frame: adding a control from the library, duplicating, and moving an element in
/// front of or behind the others. Deleting is <see cref="UIDocument.RemoveElement"/> itself. Each gives the edited document and the element to select after it, and
/// each is refused, with VentureOS's reason, when VentureOS wouldn't read the result.
/// </summary>
public static class DesignEdits
{
    /// <summary>How far a new or duplicated control is set off from one already in its place.</summary>
    public const int Step = 8;

    /// <summary>The controls the library offers, in the order it lists them.</summary>
    public static IReadOnlyList<LibraryControl> Library { get; } =
    [
        new("Text", 100, 20, "Text"),
        new("FormattedText", 160, 40, "Formatted text"),
        new("Button", 80, 24, "Button"),
        new("TextField", 140, 24, null),
        new("TextArea", 180, 100, null),
        new("List", 140, 120, null),
        new("Slider", 140, 20, null),
        new("ProgressBar", 140, 12, null),
        new("Region", 100, 60, null),
        new("Shelf", 200, 72, null),
    ];

    /// <summary>
    /// Adds a control in front of everything else, centred in the window, or set off down and right from one already
    /// there, named after its kind (<c>button</c>, <c>button2</c>, and so on).
    /// </summary>
    /// <exception cref="ArgumentException">The library has no such control.</exception>
    /// <exception cref="UIScriptException">VentureOS wouldn't read the result.</exception>
    public static (UIDocument Document, int Element) Add(UIDocument document, string element) => Add(document, element, null);

    /// <summary>
    /// The parts of a window that aren't controls, which the library adds too: the title bar's items and title field,
    /// the menus, the sidebar, and its panes.
    /// </summary>
    public static IReadOnlyList<(string Element, string Title)> Parts { get; } =
    [
        ("ToolbarItem", "Toolbar Item"),
        ("TitleField", "Title Field"),
        ("Menu", "Menu"),
        ("MenuItem", "Menu Item"),
        ("MenuSeparator", "Menu Separator"),
        ("Sidebar", "Sidebar"),
        ("Pane", "Pane"),
    ];

    /// <summary>
    /// Adds a control (see <see cref="Add(UIDocument, string)"/>) or one of the window's other <see cref="Parts"/>. A
    /// menu item or separator joins the <paramref name="selected"/> menu, or the menu of the selected item, or else
    /// the last menu, after its last entry. A pane needs a sidebar, and goes after the last pane, so it's the
    /// sidebar's last row. A control goes in <paramref name="pane"/>, the pane on show, when there is one.
    /// </summary>
    /// <exception cref="ArgumentException">The library has no such element, or a menu item has no menu to join.</exception>
    /// <exception cref="UIScriptException">VentureOS wouldn't read the result, such as a second title field.</exception>
    public static (UIDocument Document, int Element) Add(UIDocument document, string element, int? selected, string? pane = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(element);
        var end = document.Xml?.Children.Count ?? throw new InvalidOperationException("Only a document VentureOS reads can be edited.");
        switch (element)
        {
            case "ToolbarItem":
                return Insert(document, end, element, [new("name", UniqueName(document, "item")), new("title", "Item")]);
            case "TitleField":
                return Insert(document, end, element, [new("name", UniqueName(document, "title")), new("placeholder", "Title")]);
            case "Menu":
            {
                return Insert(document, end, element, [new("name", UniqueName(document, "menu")), new("title", "Menu")]);
            }

            case "MenuItem" or "MenuSeparator":
            {
                var (menu, after) = MenuFor(document, selected);
                List<KeyValuePair<string, string>> entry = [new("menu", menu)];
                if (element == "MenuItem")
                    entry.AddRange([new("name", UniqueName(document, "item")), new("title", "Item")]);
                return Insert(document, after + 1, element, entry);
            }

            case "Sidebar":
            {
                return Insert(document, end, element, [new("name", UniqueName(document, "sidebar")), new("width", "160")]);
            }

            case "Pane":
            {
                if (document.Script?.Sidebar is null)
                    throw new UIScriptException("A pane is a row of a sidebar; add a sidebar first.");
                var children = document.Xml!.Children;
                var after = -1;
                for (var child = 0; child < children.Count; child++)
                {
                    if (children[child].Name is "Pane" || (after < 0 && children[child].Name == "Sidebar"))
                        after = child;
                }

                return Insert(document, after + 1, element, [new("name", UniqueName(document, "pane")), new("title", "Pane")]);
            }
        }

        var control = Library.FirstOrDefault(item => item.Element == element)
            ?? throw new ArgumentException($"The library has no <{element}>.", nameof(element));

        var script = document.Script ?? throw new InvalidOperationException("Only a document VentureOS reads can be edited.");

        var x = (int)MathF.Max(0, MathF.Round((script.Window.Size.Width - control.Width) / 2));
        var y = (int)MathF.Max(0, MathF.Round((script.Window.Size.Height - control.Height) / 2));
        (x, y) = FreePlace(document, x, y);

        List<KeyValuePair<string, string>> attributes =
        [
            new("name", UniqueName(document, char.ToLowerInvariant(element[0]) + element[1..])),
            new("x", Number(x)), new("y", Number(y)),
            new("width", Number(control.Width)), new("height", Number(control.Height)),
        ];
        if (control.Text is { } text)
            attributes.Add(new("text", text));
        if (pane is not null)
            attributes.Add(new("pane", pane));

        var index = document.Xml!.Children.Count;
        return (document.InsertElement(index, element, attributes), index);
    }

    /// <summary>
    /// Copies an element just in front of it, with a new name and, for one with a frame, set off down and right.
    /// Comments aren't copied, and the copy is written on one line.
    /// </summary>
    /// <exception cref="ArgumentException">The element has no name, so a copy couldn't be told apart, such as the window.</exception>
    /// <exception cref="UIScriptException">VentureOS wouldn't read the result, such as a second title field.</exception>
    public static (UIDocument Document, int Element) Duplicate(UIDocument document, int element)
    {
        ArgumentNullException.ThrowIfNull(document);
        var original = Child(document, element);
        if (original["name"] is not { } name)
            throw new ArgumentException($"<{original.Name}> has no name, so it can't be copied.", nameof(element));

        var copyName = UniqueName(document, TrimNumber(name));
        var framed = FrameEdit.IsFramed(original.Name) && Parse(original["x"]) is { } x && Parse(original["y"]) is { } y;
        List<KeyValuePair<string, string>> attributes = [];
        foreach (var (attribute, value) in original.Attributes)
        {
            attributes.Add(attribute switch
            {
                "name" => new(attribute, copyName),
                "x" when framed => new(attribute, FrameEdit.Write(Parse(value)!.Value + Step)),
                "y" when framed => new(attribute, FrameEdit.Write(Parse(value)!.Value + Step)),
                _ => new(attribute, value),
            });
        }

        return (document.InsertElement(element + 1, original.Name, attributes), element + 1);
    }

    /// <summary>
    /// Moves a control or visual in front of the next one drawn after it, so it covers it; one already in front stays.
    /// Elements that aren't drawn, such as styles, are passed over.
    /// </summary>
    /// <returns>The edited document and where the element went, or the same document and place when it can't go further.</returns>
    /// <exception cref="UIScriptException">VentureOS wouldn't read the result.</exception>
    public static (UIDocument Document, int Element) BringForward(UIDocument document, int element) => Restack(document, element, 1);

    /// <summary>Moves a control or visual behind the one drawn before it; one already at the back stays.</summary>
    /// <returns>The edited document and where the element went, or the same document and place when it can't go further.</returns>
    /// <exception cref="UIScriptException">VentureOS wouldn't read the result.</exception>
    public static (UIDocument Document, int Element) SendBackward(UIDocument document, int element) => Restack(document, element, -1);

    /// <summary>A name no element in the document has: <paramref name="stem"/>, or it with the lowest number from 2 that's free.</summary>
    public static string UniqueName(UIDocument document, string stem)
    {
        ArgumentNullException.ThrowIfNull(document);
        HashSet<string> taken = [.. document.Xml?.Children.Select(child => child["name"]).OfType<string>() ?? []];
        if (!taken.Contains(stem))
            return stem;

        for (var number = 2; ; number++)
        {
            var candidate = stem + number.ToString(CultureInfo.InvariantCulture);
            if (!taken.Contains(candidate))
                return candidate;
        }
    }

    /// <summary>
    /// What an element is stacked among, so moving it forward or back passes over everything else: visuals, all
    /// drawn behind the controls; controls; and toolbar items, whose order is left to right along the title bar.
    /// </summary>
    private static string? Layer(string element) => element switch
    {
        "Visual" or "ToolbarItem" => element,
        _ when FrameEdit.IsFramed(element) => "control",
        _ => null,
    };

    private static (UIDocument, int) Restack(UIDocument document, int element, int direction)
    {
        ArgumentNullException.ThrowIfNull(document);
        var children = document.Xml?.Children ?? throw new InvalidOperationException("Only a document VentureOS reads can be edited.");
        if (Layer(Child(document, element).Name) is not { } layer)
            return (document, element);

        for (var index = element + direction; index >= 0 && index < children.Count; index += direction)
        {
            if (Layer(children[index].Name) == layer)
                return (document.MoveElement(element, index), index);
        }

        return (document, element);
    }

    /// <summary>
    /// The menu a new item joins, and the last element of that menu, which the item goes after: the selected menu,
    /// the menu of the selected item or separator, or else the document's last menu.
    /// </summary>
    private static (string Menu, int Last) MenuFor(UIDocument document, int? selected)
    {
        var children = document.Xml!.Children;
        string? menu = null;
        if (selected is { } index && index >= 0 && index < children.Count)
        {
            var chosen = children[index];
            menu = chosen.Name == "Menu" ? chosen["name"] : chosen.Name is "MenuItem" or "MenuSeparator" ? chosen["menu"] : null;
        }

        menu ??= children.LastOrDefault(child => child.Name == "Menu")?["name"]
            ?? throw new ArgumentException("Add a menu first, then its items.", nameof(selected));

        var last = -1;
        for (var at = 0; at < children.Count; at++)
        {
            var child = children[at];
            if ((child.Name == "Menu" && child["name"] == menu) || (child.Name is "MenuItem" or "MenuSeparator" && child["menu"] == menu))
                last = at;
        }

        return last < 0 ? throw new ArgumentException($"There's no menu named {menu}.", nameof(selected)) : (menu, last);
    }

    private static (UIDocument, int) Insert(UIDocument document, int index, string element, List<KeyValuePair<string, string>> attributes) =>
        (document.InsertElement(index, element, attributes), index);

    private static FlatXmlElement Child(UIDocument document, int element)
    {
        var children = document.Xml?.Children ?? throw new InvalidOperationException("Only a document VentureOS reads can be edited.");
        if (element < 0 || element >= children.Count)
            throw new ArgumentOutOfRangeException(nameof(element), element, "The document has no such element.");
        return children[element];
    }

    /// <summary>The first place from (<paramref name="x"/>, <paramref name="y"/>), stepping down and right, that no framed element starts at.</summary>
    private static (int X, int Y) FreePlace(UIDocument document, int x, int y)
    {
        HashSet<(float, float)> starts = [];
        foreach (var child in document.Xml!.Children)
        {
            if (FrameEdit.IsFramed(child.Name) && Parse(child["x"]) is { } left && Parse(child["y"]) is { } top)
                starts.Add((left, top));
        }

        // Bounded, since a document has only so many elements.
        for (var tries = 0; tries <= starts.Count && starts.Contains((x, y)); tries++)
            (x, y) = (x + Step, y + Step);
        return (x, y);
    }

    private static string TrimNumber(string name)
    {
        var end = name.Length;
        while (end > 1 && char.IsAsciiDigit(name[end - 1]))
            end--;
        return name[..end];
    }

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static float? Parse(string? value) =>
        float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : null;
}
