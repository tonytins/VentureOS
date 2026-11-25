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
        new("ProgressBar", 140, 12, null),
        new("Slider", 140, 20, null),
        new("Region", 100, 60, null),
        new("Shelf", 200, 72, null),
        new("Grid", 200, 120, null),
    ];

    /// <summary>A new grid's cells, which it can't be read without.</summary>
    private const string GridCell = "48";

    /// <summary>
    /// Adds a control in front of everything else, centred in the window, or set off down and right from one already
    /// there, named after its kind (<c>button</c>, <c>button2</c>, and so on).
    /// </summary>
    /// <exception cref="ArgumentException">The library has no such control.</exception>
    /// <exception cref="UIScriptException">VentureOS wouldn't read the result.</exception>
    public static (UIDocument Document, int Element) Add(UIDocument document, string element) => Add(document, element, null);

    /// <summary>
    /// The parts of a window that aren't controls, which the library adds too: a picture, the title bar's items and
    /// title field, the menus, the sidebar, and its panes.
    /// </summary>
    public static IReadOnlyList<(string Element, string Title)> Parts { get; } =
    [
        ("Visual", "Picture"),
        ("ToolbarItem", "Toolbar Item"),
        ("TitleField", "Title Field"),
        ("Menu", "Menu"),
        ("MenuItem", "Menu Item"),
        ("MenuSeparator", "Menu Separator"),
        ("Sidebar", "Sidebar"),
        ("Pane", "Pane"),
        ("Item", "Grid Item"),
    ];

    /// <summary>
    /// Adds a control (see <see cref="Add(UIDocument, string)"/>) or one of the window's other <see cref="Parts"/>. A
    /// menu item or separator joins the <paramref name="selected"/> menu, or the menu of the selected item, or else
    /// the last menu, after its last entry. A pane needs a sidebar, and goes after the last pane, so it's the
    /// sidebar's last row. A control goes in the sidebar, centred there, when the sidebar or something in it is
    /// <paramref name="selected"/> and it has no panes; otherwise in <paramref name="pane"/>, the pane on show, when
    /// there is one. A picture goes where a control would: a <c>&lt;Visual&gt;</c> there, with the
    /// <c>&lt;Image&gt;</c> it shows among the resources. A grid item joins the <paramref name="selected"/> grid, or the
    /// grid of the selected item, or else the last grid, after its last item.
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

            case "Visual":
                return AddPicture(document, pane, SidebarFor(document, selected));

            case "Item":
            {
                var grid = GridFor(document, selected)
                    ?? throw new UIScriptException("An item goes in a grid; add a grid first.");
                var inside = document.Xml.End(grid);
                List<KeyValuePair<string, string>> item = [new("name", UniqueName(document, "item")), new("title", "Item")];
                return (document.InsertElement(inside, element, item, grid), inside);
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

        var sidebar = SidebarFor(document, selected);
        var (x, y) = Centred(script, sidebar is not null, control.Width, control.Height);
        (x, y) = FreePlace(document, x, y);

        List<KeyValuePair<string, string>> attributes =
        [
            new("name", UniqueName(document, char.ToLowerInvariant(element[0]) + element[1..])),
            new("x", Number(x)), new("y", Number(y)),
            new("width", Number(control.Width)), new("height", Number(control.Height)),
        ];
        if (control.Text is { } text)
            attributes.Add(new("text", text));
        if (element == "Grid")
            attributes.AddRange([new("itemWidth", GridCell), new("itemHeight", GridCell)]);

        if (sidebar is { } side)
        {
            var inside = document.Xml!.End(side);
            return (document.InsertElement(inside, element, attributes, side), inside);
        }

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

    /// <summary>
    /// The menu, pane, or sidebar an element would move into: the one just before it among its siblings, or
    /// <see langword="null"/> when what's before it isn't one.
    /// </summary>
    public static int? MoveIntoTarget(UIDocument document, int element)
    {
        ArgumentNullException.ThrowIfNull(document);
        var xml = document.Xml;
        if (xml is null || element < 0 || element >= xml.Children.Count)
            return null;

        // An item goes only into a grid, and a grid holds only items.
        var siblings = xml.ChildrenOf(xml.Children[element].Parent);
        var place = Array.IndexOf(siblings, element);
        if (place <= 0)
            return null;
        var before = xml.Children[siblings[place - 1]].Name;
        var fits = xml.Children[element].Name == "Item" ? before == "Grid" : before is "Menu" or "Pane" or "Sidebar";
        return fits ? siblings[place - 1] : null;
    }

    /// <summary>
    /// Whether an element can move out of what holds it: anything inside a menu, pane, or sidebar, but not a grid's
    /// item, which belongs only in a grid.
    /// </summary>
    public static bool CanMoveOut(UIDocument document, int element)
    {
        ArgumentNullException.ThrowIfNull(document);
        var xml = document.Xml;
        if (xml is null || element < 0 || element >= xml.Children.Count || xml.Children[element].Parent < 0)
            return false;
        return xml.Children[xml.Children[element].Parent].Name != "Grid";
    }

    /// <summary>
    /// Moves an element into the menu, pane, or sidebar just before it, after what's there: an item into a menu, a
    /// menu into another as a submenu, or a control into a pane or the sidebar.
    /// </summary>
    /// <exception cref="ArgumentException">There's no menu or pane just before it.</exception>
    /// <exception cref="UIScriptException">VentureOS wouldn't read the result, such as a pane in a pane.</exception>
    public static (UIDocument Document, int Element) MoveInto(UIDocument document, int element)
    {
        ArgumentNullException.ThrowIfNull(document);
        Child(document, element);
        var target = MoveIntoTarget(document, element)
            ?? throw new ArgumentException("Move Into needs a menu, pane, or sidebar just before the element.", nameof(element));
        return document.MoveInto(element, target);
    }

    /// <summary>Moves an element out of its menu, pane, or sidebar, to just after it.</summary>
    /// <exception cref="ArgumentException">The element isn't inside anything.</exception>
    /// <exception cref="UIScriptException">VentureOS wouldn't read the result, such as a menu item outside any menu.</exception>
    public static (UIDocument Document, int Element) MoveOut(UIDocument document, int element)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (Child(document, element).Parent == UIDocument.Root)
            throw new ArgumentException("The element isn't inside a menu, pane, or sidebar.", nameof(element));
        return document.MoveOut(element);
    }

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

    /// <summary>The size a new picture is drawn at, an app icon's in About.</summary>
    private const int PictureSize = 64;

    /// <summary>
    /// Adds an <c>&lt;Image&gt;</c> after the other resources, since images stay at the root, and a
    /// <c>&lt;Visual&gt;</c> showing it, of the same name, in the pane on show or else the window. Its path is a
    /// placeholder the person changes in the inspector; until then the visual is outlined.
    /// </summary>
    private static (UIDocument, int) AddPicture(UIDocument document, string? pane, int? sidebar)
    {
        var script = document.Script ?? throw new InvalidOperationException("Only a document VentureOS reads can be edited.");
        var xml = document.Xml!;

        // A pane whose controls name it from outside can't also hold a picture, and a picture outside it would show
        // on every pane, so it's refused rather than put somewhere else.
        var holder = sidebar ?? (pane is not null ? PaneElement(document, pane) : null);
        if (sidebar is null && holder is { } paneIndex && xml.ChildrenOf(paneIndex).Length == 0
            && xml.ChildrenOf(UIDocument.Root).Any(child => xml.Children[child]["pane"] == pane))
        {
            throw new UIScriptException($"The pane \"{pane}\" has controls that name it from outside, so a picture can't go in it. Move its controls into it first.");
        }

        // After the last resource, or else after the window.
        var after = -1;
        foreach (var child in xml.ChildrenOf(UIDocument.Root))
        {
            if (xml.Children[child].Name is "Image" or "String" or "Style" || (after < 0 && xml.Children[child].Name is "Window" or "Panel"))
                after = child;
        }

        var name = UniqueName(document, "picture");
        var imageAt = after < 0 ? 0 : xml.End(after);
        var withImage = document.InsertElement(imageAt, "Image", [new("name", name), new("path", name + ".png")]);

        var (x, y) = Centred(script, sidebar is not null, PictureSize, PictureSize);
        (x, y) = FreePlace(withImage, x, y);
        List<KeyValuePair<string, string>> attributes =
        [
            new("name", name), new("image", name),
            new("x", Number(x)), new("y", Number(y)), new("width", Number(PictureSize)), new("height", Number(PictureSize)),
        ];

        // Indexes after the image moved up by one when it went in before the pane.
        if (holder is { } held)
        {
            var moved = held >= imageAt ? held + 1 : held;
            var inside = withImage.Xml!.End(moved);
            return (withImage.InsertElement(inside, "Visual", attributes, moved), inside);
        }

        var index = withImage.Xml!.Children.Count;
        return (withImage.InsertElement(index, "Visual", attributes), index);
    }

    /// <summary>
    /// The sidebar a new control goes in: the selected one, or the one holding the selected element, when it has no
    /// panes, whose rows a sidebar of controls can't have. <see langword="null"/> otherwise.
    /// </summary>
    /// <summary>
    /// The grid a new item joins: the selected one, or the one the selected item is in, or else the last grid; or
    /// <see langword="null"/> when there's none.
    /// </summary>
    private static int? GridFor(UIDocument document, int? selected)
    {
        var xml = document.Xml!;
        if (selected is { } index && index >= 0 && index < xml.Children.Count)
        {
            var chosen = xml.Children[index];
            if (chosen.Name == "Grid")
                return index;
            if (chosen.Name == "Item" && chosen.Parent >= 0)
                return chosen.Parent;
        }

        int? last = null;
        for (var at = 0; at < xml.Children.Count; at++)
        {
            if (xml.Children[at].Name == "Grid")
                last = at;
        }

        return last;
    }

    private static int? SidebarFor(UIDocument document, int? selected)
    {
        var xml = document.Xml!;
        if (selected is not { } index || index < 0 || index >= xml.Children.Count || document.Script?.Panes.Count > 0)
            return null;

        var element = xml.Children[index];
        if (element.Name == "Sidebar")
            return index;
        return element.Parent >= 0 && xml.Children[element.Parent].Name == "Sidebar" ? element.Parent : null;
    }

    /// <summary>Where something of a size is centred: in the window's content, or in the sidebar, from its corner.</summary>
    private static (int X, int Y) Centred(UIScript script, bool inSidebar, int width, int height)
    {
        var across = inSidebar && script.Sidebar is { } side ? side.Width : script.Window.Size.Width;
        return ((int)MathF.Max(0, MathF.Round((across - width) / 2)),
                (int)MathF.Max(0, MathF.Round((script.Window.Size.Height - height) / 2)));
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
