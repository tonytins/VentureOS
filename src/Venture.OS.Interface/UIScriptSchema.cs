namespace Venture.OS.Interface;

/// <summary>What kind of value an attribute takes, so an editor can offer the right field for it.</summary>
public enum UIAttributeKind
{
    /// <summary>A name other elements refer to.</summary>
    Name,

    /// <summary>A number, which may have a fraction.</summary>
    Number,

    /// <summary>A whole number.</summary>
    Integer,

    /// <summary><c>true</c> or <c>false</c>.</summary>
    Boolean,

    /// <summary>Text shown as written.</summary>
    Text,

    /// <summary>One of <see cref="UIAttributeInfo.Choices"/>.</summary>
    Choice,

    /// <summary><c>#RRGGBB</c>, <c>#RRGGBBAA</c>, or a color role such as <c>secondaryText</c>.</summary>
    Color,

    /// <summary>A font size in points, or a size role such as <c>title</c>.</summary>
    FontSize,

    /// <summary>The name of another element, of the kind <see cref="UIAttributeInfo.RefersTo"/> names.</summary>
    Reference,

    /// <summary>One letter or digit, the key that opens a menu or picks an item with Alt.</summary>
    Hotkey,

    /// <summary>A key with Ctrl, Alt, or both, and maybe Shift, such as <c>Ctrl+Shift+S</c>.</summary>
    Shortcut,

    /// <summary>Feature names separated by spaces, such as <c>menus sidebar</c>.</summary>
    Features,

    /// <summary>A file path.</summary>
    Path,
}

/// <summary>Where an attribute belongs in an inspector.</summary>
public enum UIAttributeGroup
{
    /// <summary>What the element is called and how it's found.</summary>
    Identity,

    /// <summary>Where it is and how big.</summary>
    Frame,

    /// <summary>What it says.</summary>
    Text,

    /// <summary>How it looks.</summary>
    Appearance,

    /// <summary>What it does.</summary>
    Behaviour,
}

/// <summary>One attribute an element takes, as <see cref="UIScript"/> reads it.</summary>
/// <param name="Name">The attribute's name, such as <c>width</c>.</param>
/// <param name="Group">Where it belongs in an inspector.</param>
/// <param name="Kind">What kind of value it takes.</param>
/// <param name="IsRequired">Whether every such element must have it.</param>
/// <param name="Choices">The values a <see cref="UIAttributeKind.Choice"/> takes.</param>
/// <param name="RefersTo">
/// For a <see cref="UIAttributeKind.Reference"/>, the element it names: <c>Style</c>, <c>String</c>, <c>Image</c>,
/// <c>Menu</c>, or <c>List</c> for a list or text area to scroll.
/// </param>
public sealed record UIAttributeInfo(
    string Name,
    UIAttributeGroup Group,
    UIAttributeKind Kind,
    bool IsRequired = false,
    IReadOnlyList<string>? Choices = null,
    string? RefersTo = null);

/// <summary>
/// Which attributes each UIScript element takes, as <see cref="UIScript"/> reads them, so an editor can offer every
/// attribute an element can have and nothing it can't. A control takes only what its kind allows: a placeholder only
/// on a text field or text area, an image only on a button.
/// </summary>
/// <remarks>Tests check this against the reader and every shipped document, so the two can't drift apart.</remarks>
public static class UIScriptSchema
{
    private static readonly string[] Alignments = ["left", "center", "right"];

    private static readonly UIAttributeInfo[] Frame =
    [
        new("x", UIAttributeGroup.Frame, UIAttributeKind.Number, IsRequired: true),
        new("y", UIAttributeGroup.Frame, UIAttributeKind.Number, IsRequired: true),
        new("width", UIAttributeGroup.Frame, UIAttributeKind.Number),
        new("height", UIAttributeGroup.Frame, UIAttributeKind.Number),
        new("edgeOffsetLeft", UIAttributeGroup.Frame, UIAttributeKind.Number),
        new("edgeOffsetRight", UIAttributeGroup.Frame, UIAttributeKind.Number),
        new("edgeOffsetTop", UIAttributeGroup.Frame, UIAttributeKind.Number),
        new("edgeOffsetBottom", UIAttributeGroup.Frame, UIAttributeKind.Number),
    ];

    private static readonly UIAttributeInfo[] Control =
    [
        new("name", UIAttributeGroup.Identity, UIAttributeKind.Name, IsRequired: true),
        new("legacyID", UIAttributeGroup.Identity, UIAttributeKind.Integer),
        .. Frame,
        new("text", UIAttributeGroup.Text, UIAttributeKind.Text),
        new("string", UIAttributeGroup.Text, UIAttributeKind.Reference, RefersTo: "String"),
        new("alignment", UIAttributeGroup.Text, UIAttributeKind.Choice, Choices: Alignments),
        new("style", UIAttributeGroup.Appearance, UIAttributeKind.Reference, RefersTo: "Style"),
        new("tooltip", UIAttributeGroup.Behaviour, UIAttributeKind.Reference, RefersTo: "String"),
        new("pane", UIAttributeGroup.Behaviour, UIAttributeKind.Reference, RefersTo: "Pane"),
    ];

    private static readonly UIAttributeInfo[] TextEntry =
    [
        new("placeholder", UIAttributeGroup.Text, UIAttributeKind.Text),
        new("placeholderString", UIAttributeGroup.Text, UIAttributeKind.Reference, RefersTo: "String"),
        new("maximumLength", UIAttributeGroup.Behaviour, UIAttributeKind.Integer),
        new("readOnly", UIAttributeGroup.Behaviour, UIAttributeKind.Boolean),
    ];

    private static readonly UIAttributeInfo[] Range =
    [
        new("minValue", UIAttributeGroup.Behaviour, UIAttributeKind.Number),
        new("maxValue", UIAttributeGroup.Behaviour, UIAttributeKind.Number),
    ];

    private static readonly UIAttributeInfo Scrolls =
        new("scrolls", UIAttributeGroup.Behaviour, UIAttributeKind.Reference, RefersTo: "List");

    private static readonly UIAttributeInfo[] Colors = [.. StyleColors("", UIAttributeGroup.Appearance), .. StyleColors("dark", UIAttributeGroup.Appearance)];

    private static readonly Dictionary<string, UIAttributeInfo[]> Attributes = new(StringComparer.Ordinal)
    {
        ["UIScript"] =
        [
            new("version", UIAttributeGroup.Identity, UIAttributeKind.Integer),
            new("requires", UIAttributeGroup.Identity, UIAttributeKind.Features),
        ],
        ["Window"] =
        [
            new("width", UIAttributeGroup.Frame, UIAttributeKind.Number, IsRequired: true),
            new("height", UIAttributeGroup.Frame, UIAttributeKind.Number, IsRequired: true),
            new("minWidth", UIAttributeGroup.Frame, UIAttributeKind.Number),
            new("minHeight", UIAttributeGroup.Frame, UIAttributeKind.Number),
            new("resizable", UIAttributeGroup.Behaviour, UIAttributeKind.Boolean),
            new("subtitle", UIAttributeGroup.Text, UIAttributeKind.Text),
        ],
        ["Panel"] =
        [
            new("width", UIAttributeGroup.Frame, UIAttributeKind.Number, IsRequired: true),
            new("height", UIAttributeGroup.Frame, UIAttributeKind.Number, IsRequired: true),
            new("minWidth", UIAttributeGroup.Frame, UIAttributeKind.Number),
            new("minHeight", UIAttributeGroup.Frame, UIAttributeKind.Number),
            new("edge", UIAttributeGroup.Frame, UIAttributeKind.Choice, IsRequired: true, Choices: ["top", "bottom"]),
        ],
        ["Image"] =
        [
            new("name", UIAttributeGroup.Identity, UIAttributeKind.Name, IsRequired: true),
            new("path", UIAttributeGroup.Appearance, UIAttributeKind.Path),
            new("assetID", UIAttributeGroup.Identity, UIAttributeKind.Text),
            new("stateCount", UIAttributeGroup.Appearance, UIAttributeKind.Choice, Choices: ["1", "4", "6"]),
        ],
        ["String"] =
        [
            new("name", UIAttributeGroup.Identity, UIAttributeKind.Name, IsRequired: true),
            new("table", UIAttributeGroup.Text, UIAttributeKind.Text, IsRequired: true),
            new("index", UIAttributeGroup.Text, UIAttributeKind.Integer, IsRequired: true),
        ],
        ["Style"] =
        [
            new("name", UIAttributeGroup.Identity, UIAttributeKind.Name, IsRequired: true),
            new("fontSize", UIAttributeGroup.Text, UIAttributeKind.FontSize),
            new("font", UIAttributeGroup.Text, UIAttributeKind.Choice, Choices: ["interface", "monospace"]),
            .. Colors,
        ],
        ["Visual"] =
        [
            new("name", UIAttributeGroup.Identity, UIAttributeKind.Name, IsRequired: true),
            new("image", UIAttributeGroup.Appearance, UIAttributeKind.Reference, IsRequired: true, RefersTo: "Image"),
            .. Frame,
        ],
        ["Menu"] =
        [
            new("name", UIAttributeGroup.Identity, UIAttributeKind.Name, IsRequired: true),
            new("title", UIAttributeGroup.Text, UIAttributeKind.Text, IsRequired: true),
            new("hotkey", UIAttributeGroup.Behaviour, UIAttributeKind.Hotkey),
            new("role", UIAttributeGroup.Behaviour, UIAttributeKind.Choice, Choices: ["file", "edit", "view", "help"]),
        ],
        ["MenuItem"] =
        [
            new("menu", UIAttributeGroup.Identity, UIAttributeKind.Reference, IsRequired: true, RefersTo: "Menu"),
            new("name", UIAttributeGroup.Identity, UIAttributeKind.Name, IsRequired: true),
            new("title", UIAttributeGroup.Text, UIAttributeKind.Text, IsRequired: true),
            new("hotkey", UIAttributeGroup.Behaviour, UIAttributeKind.Hotkey),
            new("shortcut", UIAttributeGroup.Behaviour, UIAttributeKind.Shortcut),
        ],
        ["MenuSeparator"] =
        [
            new("menu", UIAttributeGroup.Identity, UIAttributeKind.Reference, IsRequired: true, RefersTo: "Menu"),
        ],
        ["ToolbarItem"] =
        [
            new("name", UIAttributeGroup.Identity, UIAttributeKind.Name, IsRequired: true),
            new("title", UIAttributeGroup.Text, UIAttributeKind.Text, IsRequired: true),
            new("image", UIAttributeGroup.Appearance, UIAttributeKind.Reference, RefersTo: "Image"),
            new("placement", UIAttributeGroup.Frame, UIAttributeKind.Choice, Choices: ["end", "start"]),
        ],
        ["TitleField"] =
        [
            new("name", UIAttributeGroup.Identity, UIAttributeKind.Name, IsRequired: true),
            new("placeholder", UIAttributeGroup.Text, UIAttributeKind.Text),
        ],
        ["Sidebar"] =
        [
            new("name", UIAttributeGroup.Identity, UIAttributeKind.Name, IsRequired: true),
            new("width", UIAttributeGroup.Frame, UIAttributeKind.Number, IsRequired: true),
        ],
        ["Pane"] =
        [
            new("name", UIAttributeGroup.Identity, UIAttributeKind.Name, IsRequired: true),
            new("title", UIAttributeGroup.Text, UIAttributeKind.Text, IsRequired: true),
        ],
        ["Text"] = Control,
        ["FormattedText"] = Control,
        ["Region"] = Control,
        ["Shelf"] = Control,
        ["Button"] =
        [
            .. Control,
            new("image", UIAttributeGroup.Appearance, UIAttributeKind.Reference, RefersTo: "Image"),
            Scrolls,
            new("direction", UIAttributeGroup.Behaviour, UIAttributeKind.Choice, Choices: ["up", "down"]),
        ],
        ["TextField"] = [.. Control, .. TextEntry, new("secure", UIAttributeGroup.Behaviour, UIAttributeKind.Boolean)],
        ["TextArea"] = [.. Control, .. TextEntry, new("follows", UIAttributeGroup.Behaviour, UIAttributeKind.Boolean)],
        ["List"] = [.. Control, new("selectable", UIAttributeGroup.Behaviour, UIAttributeKind.Boolean)],
        ["Slider"] =
        [
            .. Control,
            .. Range,
            new("orientation", UIAttributeGroup.Appearance, UIAttributeKind.Choice, Choices: ["horizontal", "vertical"]),
            Scrolls,
        ],
        ["ProgressBar"] = [.. Control, .. Range],
    };

    /// <summary>The elements UIScript reads, the root <c>UIScript</c> among them.</summary>
    public static IReadOnlyCollection<string> Elements => Attributes.Keys;

    /// <summary>The controls: the elements drawn in the window, which take a frame, text, and a style.</summary>
    public static IReadOnlyList<string> Controls { get; } =
        ["Text", "FormattedText", "Button", "TextField", "TextArea", "List", "Slider", "ProgressBar", "Region", "Shelf"];

    /// <summary>The attributes <paramref name="element"/> takes, in the order an inspector shows them; none for an element UIScript doesn't read.</summary>
    public static IReadOnlyList<UIAttributeInfo> For(string element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return Attributes.TryGetValue(element, out var attributes) ? attributes : [];
    }

    /// <summary>One attribute of <paramref name="element"/>, or <see langword="null"/> when it doesn't take it.</summary>
    public static UIAttributeInfo? Find(string element, string attribute)
    {
        ArgumentNullException.ThrowIfNull(attribute);
        foreach (var info in For(element))
        {
            if (info.Name == attribute)
                return info;
        }

        return null;
    }

    private static IEnumerable<UIAttributeInfo> StyleColors(string prefix, UIAttributeGroup group)
    {
        foreach (var color in (string[])["textColor", "selectedTextColor", "highlightedTextColor", "disabledTextColor", "backgroundColor", "borderColor", "fillColor"])
        {
            var name = prefix.Length == 0 ? color : prefix + char.ToUpperInvariant(color[0]) + color[1..];
            yield return new UIAttributeInfo(name, group, UIAttributeKind.Color);
        }
    }
}
