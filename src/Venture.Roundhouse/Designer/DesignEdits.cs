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
                var (menu, nested, place) = MenuFor(document, selected);
                List<KeyValuePair<string, string>> entry = nested ? [] : [new("menu", document.Xml.Children[menu]["name"]!)];
                if (element == "MenuItem")
                    entry.AddRange([new("name", UniqueName(document, "item")), new("title", "Item")]);
                return (document.InsertElement(place, element, entry, nested ? menu : UIDocument.Root), place);
            }

            case "Sidebar":
            {
                return Insert(document, end, element, [new("name", UniqueName(document, "sidebar")), new("width", "160")]);
            }

            case "Pane":
            {
                if (document.Script?.Sidebar is null)
                    throw new UIScriptException("A pane is a row of a sidebar; add a sidebar first.");
                // After the last pane and everything in it, or else after the sidebar.
                var xml = document.Xml!;
                var after = -1;
                foreach (var child in xml.ChildrenOf(UIDocument.Root))
                {
                    if (xml.Children[child].Name is "Pane" || (after < 0 && xml.Children[child].Name == "Sidebar"))
                        after = child;
                }

                return Insert(document, after < 0 ? 0 : xml.End(after), element, [new("name", UniqueName(document, "pane")), new("title", "Pane")]);
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

        // In a pane, a control goes inside it, unless its controls name it from outside, the form it then keeps.
        if (pane is not null && PaneElement(document, pane) is { } holder)
        {
            var xml = document.Xml!;
            var named = xml.ChildrenOf(UIDocument.Root).Any(child => xml.Children[child]["pane"] == pane);
            if (xml.ChildrenOf(holder).Length > 0 || !named)
            {
                var inside = xml.End(holder);
                return (document.InsertElement(inside, element, attributes, holder), inside);
            }

            attributes.Add(new("pane", pane));
        }

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

        // The copy goes just after the original and everything inside it, in the same parent, such as a menu.
        var place = document.Xml!.End(element);
        return (document.InsertElement(place, original.Name, attributes, original.Parent), place);
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
        var xml = document.Xml ?? throw new InvalidOperationException("Only a document VentureOS reads can be edited.");
        if (Layer(Child(document, element).Name) is not { } layer)
            return (document, element);

        // Among its siblings: the controls in the same pane, or those directly in the window. A control in a pane is
        // drawn where it's written, so it passes only its neighbours there.
        var siblings = xml.ChildrenOf(xml.Children[element].Parent);
        for (var place = Array.IndexOf(siblings, element) + direction; place >= 0 && place < siblings.Length; place += direction)
        {
            var index = siblings[place];
            if (Layer(xml.Children[index].Name) == layer)
            {
                var moved = document.MoveElement(element, index);
                return (moved, moved.Xml!.ChildrenOf(xml.Children[element].Parent)[place]);
            }
        }

        return (document, element);
    }

    /// <summary>The index of the pane named <paramref name="name"/>, or <see langword="null"/>.</summary>
    private static int? PaneElement(UIDocument document, string name)
    {
        var xml = document.Xml!;
        foreach (var child in xml.ChildrenOf(UIDocument.Root))
        {
            if (xml.Children[child].Name == "Pane" && xml.Children[child]["name"] == name)
                return child;
        }

        return null;
    }

    /// <summary>
    /// The menu a new item joins, by its index, whether it holds its items inside it, and where the item goes: after
    /// its last entry. The menu is the selected one, the menu of the selected item or separator, or else the
    /// document's last menu. The item goes inside the menu, opening up an empty one, unless the menu's items name it
    /// from outside with <c>menu</c>, the flat form, which it then keeps.
    /// </summary>
    private static (int Menu, bool Nested, int Place) MenuFor(UIDocument document, int? selected)
    {
        var xml = document.Xml!;
        var children = xml.Children;
        int? menu = null;
        if (selected is { } index && index >= 0 && index < children.Count)
        {
            var chosen = children[index];
            if (chosen.Name == "Menu")
                menu = index;
            else if (chosen.Name is "MenuItem" or "MenuSeparator")
                menu = chosen.Parent != UIDocument.Root ? chosen.Parent : FindMenu(children, chosen["menu"]);
        }

        if (menu is null)
        {
            for (var at = children.Count - 1; at >= 0 && menu is null; at--)
            {
                if (children[at].Name == "Menu")
                    menu = at;
            }
        }

        if (menu is not { } found)
            throw new ArgumentException("Add a menu first, then its items.", nameof(selected));

        // Flat only where its items already name it from outside, the form it then keeps; otherwise inside it.
        var name = children[found]["name"];
        var last = -1;
        for (var at = 0; at < children.Count; at++)
        {
            if (children[at].Parent == UIDocument.Root && children[at].Name is "MenuItem" or "MenuSeparator" && children[at]["menu"] == name)
                last = at;
        }

        return last < 0 ? (found, true, xml.End(found)) : (found, false, last + 1);
    }

    private static int? FindMenu(IReadOnlyList<FlatXmlElement> children, string? name)
    {
        for (var at = 0; at < children.Count; at++)
        {
            if (children[at].Name == "Menu" && children[at]["name"] == name)
                return at;
        }

        return null;
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
