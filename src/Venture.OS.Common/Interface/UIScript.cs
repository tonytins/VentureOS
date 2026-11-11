using System.Globalization;
using Venture.OS.Xml;

namespace Venture.OS.Interface;

/// <summary>The kinds of control a UIScript document can hold.</summary>
public enum UIControlType
{
    Text,
    FormattedText,
    Button,
    TextField,

    /// <summary>A multi-line text entry: Enter adds a line, and its text wraps and scrolls.</summary>
    TextArea,
    List,
    Slider,
    ProgressBar,
    Region,
}

/// <summary>How text sits in its control.</summary>
public enum UITextAlignment
{
    Left,
    Center,
    Right,
}

/// <summary>
/// The kind of typeface a style asks for. The host picks the font itself; VentureOS uses Open Sans and Fira Code.
/// </summary>
public enum UIFont
{
    /// <summary>The host's interface font, which text uses without a style saying otherwise.</summary>
    Interface,

    /// <summary>A fixed-width font, for code and a terminal.</summary>
    Monospace,
}

/// <summary>Which way a slider runs. Without one, it runs along its longer side.</summary>
public enum UIOrientation
{
    Horizontal,
    Vertical,
}

/// <summary>Which way a scroll button moves its list.</summary>
public enum UIScrollDirection
{
    Up,
    Down,
}

/// <summary>A size in pixels.</summary>
public readonly record struct UISize(float Width, float Height);

/// <summary>A color, as red, green, blue, and alpha from 0 to 255.</summary>
public readonly record struct UIColor(byte Red, byte Green, byte Blue, byte Alpha = 255);

/// <summary>Distances from the window's edges, which replace a position on their axis.</summary>
public sealed record UIEdgeOffsets(float? Left, float? Right, float? Top, float? Bottom);

/// <summary>Where something sits in the window. A size can be left out where an image provides it.</summary>
public sealed record UIFrame(float X, float Y, float? Width, float? Height, UIEdgeOffsets? EdgeOffsets);

/// <summary>
/// The interface's size, the smallest size it lays out at, and whether the desktop lets people resize it, which
/// VentureOS adds. A reader that skips <c>resizable</c> shows a fixed window that still lays out correctly, so it
/// isn't a required feature.
/// </summary>
public sealed record UIWindow(UISize Size, UISize? MinimumSize, bool IsResizable = false);

/// <summary>An image the host loads, by path, asset ID, or both, with 1, 4, or 6 equal-width states.</summary>
public sealed record UIImageResource(string Name, string? Path, string? AssetId, int StateCount);

/// <summary>Localized text the host supplies, by table and index.</summary>
public sealed record UIStringResource(string Name, string Table, int Index);

/// <summary>A named set of visual properties that controls refer to.</summary>
public sealed record UIStyle(
    string Name,
    UIColor? TextColor,
    UIColor? SelectedTextColor,
    UIColor? HighlightedTextColor,
    UIColor? DisabledTextColor,
    UIColor? BackgroundColor,
    UIColor? BorderColor,
    UIColor? FillColor,
    int? FontSize,
    UIFont? Font = null);

/// <summary>Text that's either written in the document or names a <c>&lt;String&gt;</c>.</summary>
public sealed record UIText(string Value, bool IsReference)
{
    /// <summary>Text written in the document.</summary>
    public static UIText Literal(string value) => new(value, false);

    /// <summary>The name of a <c>&lt;String&gt;</c> resource.</summary>
    public static UIText Reference(string name) => new(name, true);
}

/// <summary>A decorative image.</summary>
public sealed record UIVisual(string Name, string Image, UIFrame Frame, int Line);

/// <summary>A list a slider or scroll button moves, and for a button, which way.</summary>
public sealed record UIScrollBinding(string List, UIScrollDirection? Direction);

/// <summary>A control, with every attribute any control can take; those that don't apply are unset.</summary>
public sealed record UIControl(
    string Name,
    UIControlType Type,
    UIFrame Frame,
    int Line,
    int? LegacyId = null,
    string? Style = null,
    UIText? Text = null,
    UIText? Placeholder = null,
    string? Image = null,
    string? Tooltip = null,
    int? MaximumLength = null,
    UIScrollBinding? Scrolls = null,
    bool IsReadOnly = false,
    bool IsSecure = false,
    bool IsSelectable = false,
    UITextAlignment Alignment = UITextAlignment.Left,
    (float Minimum, float Maximum)? ValueRange = null,
    UIOrientation? Orientation = null,
    bool Follows = false);

/// <summary>A drop-down menu in the global menu bar (the <c>menus</c> feature).</summary>
public sealed record UIMenu(string Name, string Title, char? Hotkey, IReadOnlyList<UIMenuEntry> Entries, int Line);

/// <summary>An item or a separator in a <see cref="UIMenu"/>. A separator has no name.</summary>
public sealed record UIMenuEntry(string? Name, string? Title, char? Hotkey, UIShortcut? Shortcut, int Line)
{
    /// <summary>Whether it's a separator line rather than an item.</summary>
    public bool IsSeparator => Name is null;
}

/// <summary>A key combination that chooses a menu item from anywhere, such as Ctrl+N.</summary>
public sealed record UIShortcut(bool Control, bool Shift, bool Alt, char Key)
{
    public override string ToString() =>
        $"{(Control ? "Ctrl+" : "")}{(Alt ? "Alt+" : "")}{(Shift ? "Shift+" : "")}{Key}";
}

/// <summary>
/// A button in the window's unified title bar, such as Add, or a tab when one is shown as selected. The host draws
/// the bar; items run along its right end in document order.
/// </summary>
public sealed record UIToolbarItem(string Name, string Title, int Line);

/// <summary>
/// Makes the window's title editable in place, as an address bar is: the host draws it as a field in the title bar,
/// and Enter submits it under this name.
/// </summary>
public sealed record UITitleField(string Name, string? Placeholder, int Line);

/// <summary>
/// A list of places down the window's left, as Nautilus and Finder have, with the <c>sidebar</c> feature. The host
/// draws it the window's full height, beside the content rather than in it, so it has a width and no position, and
/// the window's own size is the content's. Code treats it as a selectable <c>&lt;List&gt;</c> of this name.
/// </summary>
public sealed record UISidebar(string Name, float Width, int Line);

/// <summary>An attribute the reader didn't know and skipped, which may be a typo.</summary>
public sealed record UIIgnoredAttribute(string Element, string Attribute, int Line);

/// <summary>Why a UIScript document couldn't be read, with its line when it concerns one element.</summary>
public sealed class UIScriptException(string message, int? line = null)
    : FormatException(line is { } number ? $"Line {number}: {message}" : message)
{
    /// <summary>The line of the problem, or <see langword="null"/> when it isn't about one element.</summary>
    public int? Line { get; } = line;
}

/// <summary>
/// A UIScript document (<c>.uix</c>): one interface's window size, resources, styles, decorative images, and
/// controls, plus its menus with the <c>menus</c> feature and its sidebar with the <c>sidebar</c> feature.
/// </summary>
/// <remarks>
/// <para>This reads the format Hudini defines (Draft, version 1; see docs/uiscript-menus.md for VentureOS's
/// <c>menus</c> feature), with the same rules as Hudini's reader, so a document reads the same in both. A document
/// reads completely and validly, or throws <see cref="UIScriptException"/>.</para>
/// <list type="bullet">
/// <item><description>A newer <c>version</c> or an unknown <c>requires</c> feature refuses the document, before
/// anything else is checked, so the message says a newer reader is needed.</description></item>
/// <item><description>An unknown element refuses it; an unknown attribute is skipped and listed in
/// <see cref="IgnoredAttributes"/>.</description></item>
/// </list>
/// </remarks>
public sealed class UIScript
{
    /// <summary>The newest format version this reader understands.</summary>
    public const int SupportedVersion = 1;

    /// <summary>The <c>requires</c> features this reader understands.</summary>
    public static readonly IReadOnlyList<string> SupportedFeatures = [MenusFeature, SidebarFeature];

    /// <summary>The feature that adds <c>&lt;Menu&gt;</c>, <c>&lt;MenuItem&gt;</c>, and <c>&lt;MenuSeparator&gt;</c>.</summary>
    public const string MenusFeature = "menus";

    /// <summary>
    /// The feature that adds <c>&lt;Sidebar&gt;</c>. A reader that skipped a sidebar would lose the window's way
    /// around, so it's a feature older readers refuse rather than an element they could ignore.
    /// </summary>
    public const string SidebarFeature = "sidebar";

    private UIScript()
    {
    }

    public int Version { get; private init; }

    public IReadOnlyList<string> RequiredFeatures { get; private init; } = [];

    public required UIWindow Window { get; init; }

    public IReadOnlyDictionary<string, UIImageResource> Images { get; private init; } =
        new Dictionary<string, UIImageResource>();

    public IReadOnlyDictionary<string, UIStringResource> Strings { get; private init; } =
        new Dictionary<string, UIStringResource>();

    public IReadOnlyDictionary<string, UIStyle> Styles { get; private init; } = new Dictionary<string, UIStyle>();

    /// <summary>Decorative images, drawn before every control, in document order.</summary>
    public IReadOnlyList<UIVisual> Visuals { get; private init; } = [];

    /// <summary>Controls in document order, which is drawing order: later ones on top.</summary>
    public IReadOnlyList<UIControl> Controls { get; private init; } = [];

    /// <summary>Menus in document order, with the <c>menus</c> feature.</summary>
    public IReadOnlyList<UIMenu> Menus { get; private init; } = [];

    /// <summary>The title bar's buttons, in document order, which is left to right.</summary>
    public IReadOnlyList<UIToolbarItem> ToolbarItems { get; private init; } = [];

    /// <summary>The editable title, or <see langword="null"/> for a plain one.</summary>
    public UITitleField? TitleField { get; private init; }

    /// <summary>The sidebar, with the <c>sidebar</c> feature, or <see langword="null"/>.</summary>
    public UISidebar? Sidebar { get; private init; }

    /// <summary>Attributes the reader didn't know and skipped. Log them while developing, to catch typos.</summary>
    public IReadOnlyList<UIIgnoredAttribute> IgnoredAttributes { get; private init; } = [];

    /// <summary>The control with this name, or <see langword="null"/>.</summary>
    public UIControl? Control(string name)
    {
        foreach (var control in Controls)
        {
            if (control.Name == name)
                return control;
        }

        return null;
    }

    /// <summary>Reads a document from its text.</summary>
    /// <exception cref="UIScriptException">The document isn't valid UIScript; the message says why.</exception>
    public static UIScript Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        FlatXmlDocument document;
        try
        {
            document = FlatXml.Parse(text);
        }
        catch (FlatXmlException exception)
        {
            throw new UIScriptException(exception.Reason, exception.Line);
        }

        return Build(document);
    }

    /// <summary>Reads a UTF-8 file.</summary>
    /// <exception cref="UIScriptException">The document isn't valid UIScript.</exception>
    /// <exception cref="IOException">The file couldn't be read.</exception>
    public static UIScript Load(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        return Parse(File.ReadAllText(path, System.Text.Encoding.UTF8));
    }

    private static UIScript Build(FlatXmlDocument document)
    {
        var root = document.Root;
        if (root.Name != "UIScript")
            throw new UIScriptException($"The root element must be <UIScript>, not <{root.Name}>.", root.Line);

        // Version and features come first: a newer document may hold elements this reader would otherwise refuse.
        Attributes rootReader = new(root);
        var version = rootReader.Integer("version") ?? 1;
        if (version < 1)
            throw rootReader.Invalid("version", root["version"]!);
        if (version > SupportedVersion)
            throw new UIScriptException(
                $"This is UIScript version {version}, and this reader reads up to {SupportedVersion}; it needs a newer VentureOS.");

        var features = (rootReader.String("requires") ?? "").Split([' ', '\t', '\n', '\r'],
            StringSplitOptions.RemoveEmptyEntries);
        var hasMenus = false;
        var hasSidebar = false;
        foreach (var feature in features)
        {
            if (feature == MenusFeature)
                hasMenus = true;
            else if (feature == SidebarFeature)
                hasSidebar = true;
            else
                throw new UIScriptException(
                    $"This document requires \"{feature}\", which this reader doesn't support; it needs a newer VentureOS.");
        }

        List<UIIgnoredAttribute> ignored = [.. rootReader.Unused()];

        UIWindow? window = null;
        Dictionary<string, UIImageResource> images = [];
        Dictionary<string, UIStringResource> strings = [];
        Dictionary<string, UIStyle> styles = [];
        List<UIVisual> visuals = [];
        List<UIControl> controls = [];
        List<(string Name, string Title, char? Hotkey, int Line)> menus = [];
        List<(string Menu, UIMenuEntry Entry)> entries = [];
        List<UIToolbarItem> toolbar = [];
        UITitleField? titleField = null;
        UISidebar? sidebar = null;

        foreach (var element in document.Children)
        {
            Attributes reader = new(element);
            switch (element.Name)
            {
                case "Window":
                    if (window is not null)
                        throw new UIScriptException("There's more than one <Window>.", element.Line);
                    window = ReadWindow(reader);
                    break;

                case "Image":
                {
                    var name = reader.RequiredString("name");
                    if (images.ContainsKey(name))
                        throw Duplicate(name, element.Line);
                    images[name] = new UIImageResource(
                        name, reader.String("path"), reader.String("assetID"), reader.Integer("stateCount") ?? 1);
                    break;
                }

                case "String":
                {
                    var name = reader.RequiredString("name");
                    if (strings.ContainsKey(name))
                        throw Duplicate(name, element.Line);
                    strings[name] = new UIStringResource(
                        name, reader.RequiredString("table"), reader.RequiredInteger("index"));
                    break;
                }

                case "Style":
                {
                    var name = reader.RequiredString("name");
                    if (styles.ContainsKey(name))
                        throw Duplicate(name, element.Line);
                    styles[name] = new UIStyle(
                        name,
                        reader.Color("textColor"),
                        reader.Color("selectedTextColor"),
                        reader.Color("highlightedTextColor"),
                        reader.Color("disabledTextColor"),
                        reader.Color("backgroundColor"),
                        reader.Color("borderColor"),
                        reader.Color("fillColor"),
                        reader.Integer("fontSize"),
                        reader.Enum("font", ParseFont));
                    break;
                }

                case "Visual":
                    visuals.Add(new UIVisual(
                        reader.RequiredString("name"), reader.RequiredString("image"), ReadFrame(reader), element.Line));
                    break;

                case "Menu" when hasMenus:
                    menus.Add((reader.RequiredString("name"), reader.RequiredString("title"), reader.Hotkey("hotkey"),
                        element.Line));
                    break;

                case "MenuItem" when hasMenus:
                    entries.Add((reader.RequiredString("menu"), new UIMenuEntry(
                        reader.RequiredString("name"),
                        reader.RequiredString("title"),
                        reader.Hotkey("hotkey"),
                        reader.Shortcut("shortcut"),
                        element.Line)));
                    break;

                case "ToolbarItem":
                    toolbar.Add(new UIToolbarItem(reader.RequiredString("name"), reader.RequiredString("title"), element.Line));
                    break;

                case "TitleField":
                    if (titleField is not null)
                        throw new UIScriptException("There's more than one <TitleField>; a window has one title.", element.Line);
                    titleField = new UITitleField(reader.RequiredString("name"), reader.String("placeholder"), element.Line);
                    break;

                case "Sidebar" when hasSidebar:
                    if (sidebar is not null)
                        throw new UIScriptException("There's more than one <Sidebar>; a window has one.", element.Line);
                    sidebar = new UISidebar(reader.RequiredString("name"), reader.RequiredNumber("width"), element.Line);
                    break;

                case "MenuSeparator" when hasMenus:
                    entries.Add((reader.RequiredString("menu"), new UIMenuEntry(null, null, null, null, element.Line)));
                    break;

                default:
                    if (ControlType(element.Name) is not { } type)
                    {
                        var hint = element.Name switch
                        {
                            "Menu" or "MenuItem" or "MenuSeparator" => $" Add requires=\"{MenusFeature}\" to <UIScript> to use menus.",
                            "Sidebar" => $" Add requires=\"{SidebarFeature}\" to <UIScript> to use a sidebar.",
                            _ => "",
                        };
                        throw new UIScriptException($"<{element.Name}> isn't a UIScript element.{hint}", element.Line);
                    }

                    controls.Add(ReadControl(type, reader, element.Line));
                    break;
            }

            ignored.AddRange(reader.Unused());
        }

        if (window is null)
            throw new UIScriptException("A UIScript document needs a <Window>.");

        UIScript script = new()
        {
            Version = version,
            RequiredFeatures = features,
            Window = window,
            Images = images,
            Strings = strings,
            Styles = styles,
            Visuals = visuals,
            Controls = controls,
            Menus = BuildMenus(menus, entries),
            ToolbarItems = toolbar,
            TitleField = titleField,
            Sidebar = sidebar,
            IgnoredAttributes = ignored,
        };
        script.Validate();
        return script;
    }

    private static UIControlType? ControlType(string element) => element switch
    {
        "Text" => UIControlType.Text,
        "FormattedText" => UIControlType.FormattedText,
        "Button" => UIControlType.Button,
        "TextField" => UIControlType.TextField,
        "TextArea" => UIControlType.TextArea,
        "List" => UIControlType.List,
        "Slider" => UIControlType.Slider,
        "ProgressBar" => UIControlType.ProgressBar,
        "Region" => UIControlType.Region,
        _ => null,
    };

    private static UIWindow ReadWindow(Attributes reader)
    {
        UISize size = new(reader.RequiredNumber("width"), reader.RequiredNumber("height"));
        var minimumWidth = reader.Number("minWidth");
        var minimumHeight = reader.Number("minHeight");
        var resizable = reader.Boolean("resizable") ?? false;
        return (minimumWidth, minimumHeight) switch
        {
            (null, null) => new UIWindow(size, null, resizable),
            ({ } width, { } height) => new UIWindow(size, new UISize(width, height), resizable),
            (null, _) => throw reader.Missing("minWidth"),
            _ => throw reader.Missing("minHeight"),
        };
    }

    private static UIFrame ReadFrame(Attributes reader)
    {
        var left = reader.Number("edgeOffsetLeft");
        var right = reader.Number("edgeOffsetRight");
        var top = reader.Number("edgeOffsetTop");
        var bottom = reader.Number("edgeOffsetBottom");
        var offsets = left is not null || right is not null || top is not null || bottom is not null
            ? new UIEdgeOffsets(left, right, top, bottom)
            : null;

        return new UIFrame(
            reader.RequiredNumber("x"), reader.RequiredNumber("y"), reader.Number("width"), reader.Number("height"),
            offsets);
    }

    private static UIControl ReadControl(UIControlType type, Attributes reader, int line)
    {
        var name = reader.RequiredString("name");

        var direction = reader.Enum("direction", ParseDirection);
        var list = reader.String("scrolls");
        if (list is null && direction is not null)
            throw ScrollProblem(name, line);

        var minimum = reader.Number("minValue");
        var maximum = reader.Number("maxValue");
        (float, float)? range = null;
        if (minimum is not null || maximum is not null)
        {
            var (lower, upper) = (minimum ?? 0, maximum ?? 1);
            if (lower >= upper)
                throw RangeProblem(name, line);
            range = (lower, upper);
        }

        return new UIControl(
            name,
            type,
            ReadFrame(reader),
            line,
            LegacyId: reader.Integer("legacyID"),
            Style: reader.String("style"),
            Text: reader.Text("text", "string"),
            Placeholder: reader.Text("placeholder", "placeholderString"),
            Image: reader.String("image"),
            Tooltip: reader.String("tooltip"),
            MaximumLength: reader.Integer("maximumLength"),
            Scrolls: list is null ? null : new UIScrollBinding(list, direction),
            IsReadOnly: reader.Boolean("readOnly") ?? false,
            IsSecure: reader.Boolean("secure") ?? false,
            IsSelectable: reader.Boolean("selectable") ?? false,
            Alignment: reader.Enum("alignment", ParseAlignment) ?? UITextAlignment.Left,
            ValueRange: range,
            Orientation: reader.Enum("orientation", ParseOrientation),
            Follows: reader.Boolean("follows") ?? false);
    }

    private static IReadOnlyList<UIMenu> BuildMenus(
        List<(string Name, string Title, char? Hotkey, int Line)> menus,
        List<(string Menu, UIMenuEntry Entry)> entries)
    {
        List<UIMenu> built = [];
        HashSet<char> menuHotkeys = [];
        foreach (var (name, title, hotkey, line) in menus)
        {
            if (built.Exists(menu => menu.Name == name))
                throw Duplicate(name, line);
            if (hotkey is { } key && !menuHotkeys.Add(char.ToUpperInvariant(key)))
                throw new UIScriptException($"Two menus use the hotkey {key}.", line);

            List<UIMenuEntry> items = [];
            HashSet<char> itemHotkeys = [];
            foreach (var (menuName, entry) in entries)
            {
                if (menuName != name)
                    continue;
                if (entry.Hotkey is { } itemKey && !itemHotkeys.Add(char.ToUpperInvariant(itemKey)))
                    throw new UIScriptException($"Two items in the {title} menu use the hotkey {itemKey}.", entry.Line);
                items.Add(entry);
            }

            built.Add(new UIMenu(name, title, hotkey, items, line));
        }

        foreach (var (menuName, entry) in entries)
        {
            if (!built.Exists(menu => menu.Name == menuName))
                throw new UIScriptException(
                    $"{(entry.IsSeparator ? "A separator" : $"\"{entry.Name}\"")} is in the menu \"{menuName}\", which isn't declared.",
                    entry.Line);
        }

        return built;
    }

    private void Validate()
    {
        if (Window.Size.Width <= 0 || Window.Size.Height <= 0)
            throw new UIScriptException("The window's width and height must be positive.");
        if (Window.MinimumSize is { } minimum
            && (minimum.Width <= 0 || minimum.Height <= 0
                || minimum.Width > Window.Size.Width || minimum.Height > Window.Size.Height))
            throw new UIScriptException(
                "The window's minWidth and minHeight must be positive and no larger than its width and height.");

        foreach (var style in Styles.Values)
        {
            if (style.FontSize is <= 0)
                throw new UIScriptException($"The style \"{style.Name}\" needs a positive fontSize.");
        }

        foreach (var image in Images.Values)
        {
            if (image.Path is null && image.AssetId is null)
                throw new UIScriptException($"The image \"{image.Name}\" needs a path, an assetID, or both.");
            if (image.StateCount is not (1 or 4 or 6))
                throw new UIScriptException($"The image \"{image.Name}\" has stateCount {image.StateCount}; use 1, 4, or 6.");
        }

        if (Sidebar is { Width: <= 0 } narrow)
            throw new UIScriptException($"The sidebar \"{narrow.Name}\" needs a positive width.", narrow.Line);

        // Controls, visuals, menu items, toolbar items, the title field, and the sidebar share names, since events
        // and the host refer to them by name.
        HashSet<string> names = [];
        if (Sidebar is { } side)
            Unique(names, side.Name, side.Line);
        foreach (var item in ToolbarItems)
            Unique(names, item.Name, item.Line);
        if (TitleField is { } title)
            Unique(names, title.Name, title.Line);
        foreach (var visual in Visuals)
            Unique(names, visual.Name, visual.Line);
        foreach (var control in Controls)
            Unique(names, control.Name, control.Line);
        foreach (var menu in Menus)
        {
            Unique(names, menu.Name, menu.Line);
            foreach (var entry in menu.Entries)
            {
                if (entry.Name is { } entryName)
                    Unique(names, entryName, entry.Line);
            }
        }

        HashSet<string> shortcuts = [];
        foreach (var menu in Menus)
        {
            foreach (var entry in menu.Entries)
            {
                if (entry.Shortcut is { } shortcut && !shortcuts.Add(shortcut.ToString()))
                    throw new UIScriptException($"Two menu items use the shortcut {shortcut}.", entry.Line);
            }
        }

        foreach (var visual in Visuals)
        {
            if (!Images.ContainsKey(visual.Image))
                throw Unknown(visual.Name, "image", visual.Image, visual.Line);
            ValidateFrame(visual.Frame, visual.Name, visual.Line);
        }

        foreach (var control in Controls)
            ValidateControl(control);
    }

    private void ValidateControl(UIControl control)
    {
        var (name, type, line) = (control.Name, control.Type, control.Line);

        var takesText = type is UIControlType.TextField or UIControlType.TextArea;
        if (control.Placeholder is not null && !takesText)
            throw OnlyFor(name, "placeholder", "a TextField or TextArea", line);
        if (control.MaximumLength is { } maximumLength && (maximumLength <= 0 || !takesText))
            throw new UIScriptException($"\"{name}\" can't have maximumLength {maximumLength}; only a TextField or TextArea takes one, and it must be positive.", line);
        if (control.IsReadOnly && !takesText)
            throw OnlyFor(name, "readOnly", "a TextField or TextArea", line);
        if (control.IsSecure && type != UIControlType.TextField)
            throw OnlyFor(name, "secure", "a TextField", line);
        if (control.IsSelectable && type != UIControlType.List)
            throw OnlyFor(name, "selectable", "a List", line);
        if (control.Follows && type != UIControlType.TextArea)
            throw OnlyFor(name, "follows", "a TextArea", line);

        if (control.Scrolls is { } binding)
        {
            if (Control(binding.List) is not { } target)
                throw Unknown(name, "scrolls", binding.List, line);
            var directionFits = type switch
            {
                UIControlType.Slider => binding.Direction is null,
                UIControlType.Button => binding.Direction is not null,
                _ => false,
            };
            if (target.Type is not (UIControlType.List or UIControlType.TextArea) || !directionFits)
                throw ScrollProblem(name, line);
        }

        if (control.ValueRange is not null && type is not (UIControlType.Slider or UIControlType.ProgressBar))
            throw RangeProblem(name, line);
        if (control.Orientation is not null && type != UIControlType.Slider)
            throw OnlyFor(name, "orientation", "a Slider", line);
        if (control.Style is { } style && !Styles.ContainsKey(style))
            throw Unknown(name, "style", style, line);

        if (control.Image is { } image)
        {
            if (type != UIControlType.Button)
                throw OnlyFor(name, "image", "a Button", line);
            if (!Images.ContainsKey(image))
                throw Unknown(name, "image", image, line);
        }
        else if (control.Frame.Width is null || control.Frame.Height is null)
        {
            throw new UIScriptException($"\"{name}\" needs a width and a height.", line);
        }

        if (control.Text is { IsReference: true } text && !Strings.ContainsKey(text.Value))
            throw Unknown(name, "text", text.Value, line);
        if (control.Placeholder is { IsReference: true } placeholder && !Strings.ContainsKey(placeholder.Value))
            throw Unknown(name, "placeholder", placeholder.Value, line);
        if (control.Tooltip is { } tooltip && !Strings.ContainsKey(tooltip))
            throw Unknown(name, "tooltip", tooltip, line);

        ValidateFrame(control.Frame, name, line);
    }

    private static void ValidateFrame(UIFrame frame, string owner, int line)
    {
        if (frame.Width < 0 || frame.Height < 0)
            throw new UIScriptException($"\"{owner}\" has a negative width or height.", line);
    }

    private static void Unique(HashSet<string> names, string name, int line)
    {
        if (!names.Add(name))
            throw Duplicate(name, line);
    }

    private static UIScriptException Duplicate(string name, int line) =>
        new($"The name \"{name}\" is used more than once.", line);

    private static UIScriptException Unknown(string owner, string property, string reference, int line) =>
        new($"\"{owner}\" has {property}=\"{reference}\", which isn't declared.", line);

    private static UIScriptException OnlyFor(string owner, string attribute, string kind, int line) =>
        new($"\"{owner}\" can't have {attribute}; only {kind} takes it.", line);

    private static UIScriptException ScrollProblem(string owner, int line) =>
        new($"\"{owner}\" has scrolls or direction that don't fit: a Slider scrolls a List or TextArea with no direction, and a Button scrolls one up or down.", line);

    private static UIScriptException RangeProblem(string owner, int line) =>
        new($"\"{owner}\" has a minValue and maxValue that don't fit: only a Slider or ProgressBar takes them, and minValue must be below maxValue.", line);

    private static UIScrollDirection? ParseDirection(string value) => value switch
    {
        "up" => UIScrollDirection.Up,
        "down" => UIScrollDirection.Down,
        _ => null,
    };

    private static UIFont? ParseFont(string value) => value switch
    {
        "interface" => UIFont.Interface,
        "monospace" => UIFont.Monospace,
        _ => null,
    };

    private static UITextAlignment? ParseAlignment(string value) => value switch
    {
        "left" => UITextAlignment.Left,
        "center" => UITextAlignment.Center,
        "right" => UITextAlignment.Right,
        _ => null,
    };

    private static UIOrientation? ParseOrientation(string value) => value switch
    {
        "horizontal" => UIOrientation.Horizontal,
        "vertical" => UIOrientation.Vertical,
        _ => null,
    };

    /// <summary>Reads one element's attributes as typed values, and tracks which ones were read.</summary>
    private sealed class Attributes(FlatXmlElement element)
    {
        private readonly HashSet<string> used = [];

        public string? String(string attribute)
        {
            used.Add(attribute);
            return element[attribute];
        }

        public string RequiredString(string attribute) => String(attribute) ?? throw Missing(attribute);

        public int? Integer(string attribute)
        {
            if (String(attribute) is not { } value)
                return null;
            return int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number)
                ? number
                : throw Invalid(attribute, value);
        }

        public int RequiredInteger(string attribute) => Integer(attribute) ?? throw Missing(attribute);

        public float? Number(string attribute)
        {
            if (String(attribute) is not { } value)
                return null;
            const NumberStyles Style = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent;
            return float.TryParse(value, Style, CultureInfo.InvariantCulture, out var number) && float.IsFinite(number)
                ? number
                : throw Invalid(attribute, value);
        }

        public float RequiredNumber(string attribute) => Number(attribute) ?? throw Missing(attribute);

        public bool? Boolean(string attribute) => String(attribute) switch
        {
            null => null,
            "true" => true,
            "false" => false,
            var value => throw Invalid(attribute, value),
        };

        public T? Enum<T>(string attribute, Func<string, T?> parse)
            where T : struct
        {
            if (String(attribute) is not { } value)
                return null;
            return parse(value) ?? throw Invalid(attribute, value);
        }

        public UIColor? Color(string attribute)
        {
            if (String(attribute) is not { } value)
                return null;

            var digits = value.StartsWith('#') ? value[1..] : null;
            if (digits is not { Length: 6 or 8 } || !IsHex(digits)
                || !uint.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var number))
                throw Invalid(attribute, value);

            var rgba = digits.Length == 6 ? (number << 8) | 0xFF : number;
            return new UIColor((byte)(rgba >> 24), (byte)(rgba >> 16), (byte)(rgba >> 8), (byte)rgba);
        }

        public UIText? Text(string literal, string reference) => (String(literal), String(reference)) switch
        {
            (null, null) => null,
            ({ } value, null) => UIText.Literal(value),
            (null, { } name) => UIText.Reference(name),
            _ => throw new UIScriptException(
                $"<{element.Name}> can't have both {literal} and {reference}; use one.", element.Line),
        };

        public char? Hotkey(string attribute)
        {
            if (String(attribute) is not { } value)
                return null;
            return value.Length == 1 && char.IsAsciiLetterOrDigit(value[0]) ? value[0] : throw Invalid(attribute, value);
        }

        /// <summary>A shortcut such as <c>Ctrl+N</c> or <c>Ctrl+Shift+S</c>: modifiers, then one letter or digit.</summary>
        public UIShortcut? Shortcut(string attribute)
        {
            if (String(attribute) is not { } value)
                return null;

            var parts = value.Split('+');
            var key = parts[^1];
            if (key.Length != 1 || !char.IsAsciiLetterOrDigit(key[0]))
                throw Invalid(attribute, value);

            bool control = false, shift = false, alt = false;
            for (var index = 0; index < parts.Length - 1; index++)
            {
                // Each modifier once, so Ctrl+Ctrl+N is refused rather than read as Ctrl+N.
                var seen = parts[index] switch
                {
                    "Ctrl" => Set(ref control),
                    "Shift" => Set(ref shift),
                    "Alt" => Set(ref alt),
                    _ => false,
                };
                if (!seen)
                    throw Invalid(attribute, value);
            }

            // Without Ctrl or Alt, a shortcut would take the letter from whatever's being typed.
            if (!control && !alt)
                throw new UIScriptException(
                    $"The shortcut \"{value}\" needs Ctrl or Alt, so it doesn't take a letter being typed.", element.Line);

            return new UIShortcut(control, shift, alt, char.ToUpperInvariant(key[0]));
        }

        public UIScriptException Missing(string attribute) =>
            new($"<{element.Name}> needs {attribute}=\"...\".", element.Line);

        public UIScriptException Invalid(string attribute, string value) =>
            new($"<{element.Name}> has {attribute}=\"{value}\", which isn't a valid value.", element.Line);

        /// <summary>The attributes nothing read, sorted by name, as Hudini lists them.</summary>
        public IEnumerable<UIIgnoredAttribute> Unused()
        {
            List<UIIgnoredAttribute> unused = [];
            foreach (var (name, _) in element.Attributes)
            {
                if (!used.Contains(name))
                    unused.Add(new UIIgnoredAttribute(element.Name, name, element.Line));
            }

            unused.Sort((left, right) => string.CompareOrdinal(left.Attribute, right.Attribute));
            return unused;
        }

        /// <summary>Sets a flag that wasn't set yet, and says whether it wasn't.</summary>
        private static bool Set(ref bool flag)
        {
            if (flag)
                return false;
            flag = true;
            return true;
        }

        private static bool IsHex(string digits)
        {
            foreach (var digit in digits)
            {
                if (!char.IsAsciiHexDigit(digit))
                    return false;
            }

            return true;
        }
    }
}
