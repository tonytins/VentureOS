using System.Globalization;
using Venture.OS.Interface.Xml;

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

    /// <summary>
    /// A row of icons with labels and open-dots, such as the dock's; the app fills it with
    /// items, as it fills a list.
    /// </summary>
    Shelf,
}

/// <summary>What a window is to the desktop.</summary>
public enum UIWindowRole
{
    /// <summary>An ordinary window, with a title bar, that can be moved and focused.</summary>
    Window,

    /// <summary>
    /// A panel pinned to an edge of the screen, such as the dock or the menu bar: no title bar, not moved or focused,
    /// kept above ordinary windows, which the desktop keeps clear of it.
    /// </summary>
    Panel,
}

/// <summary>The edge of the screen a panel is pinned to.</summary>
public enum UIEdge
{
    Top,
    Bottom,
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
/// The interface's size, the smallest size it lays out at, whether the desktop lets people resize it, and a line
/// shown under the title, which VentureOS adds. A reader that skips <c>resizable</c> or <c>subtitle</c> shows a fixed
/// window, or one without the line, that still lays out correctly, so neither is a required feature.
/// <para>A document written with <c>&lt;Panel&gt;</c> in place of <c>&lt;Window&gt;</c> reads as one of these with
/// <see cref="Role"/> <see cref="UIWindowRole.Panel"/> and an <see cref="Edge"/>, so the desktop treats both alike.
/// A reader without panels refuses the unknown element rather than showing a dock as an ordinary window.</para>
/// </summary>
public sealed record UIWindow(
    UISize Size,
    UISize? MinimumSize,
    bool IsResizable = false,
    string? Subtitle = null,
    UIWindowRole Role = UIWindowRole.Window,
    UIEdge? Edge = null);

/// <summary>An image the host loads, by path, asset ID, or both, with 1, 4, or 6 equal-width states.</summary>
public sealed record UIImageResource(string Name, string? Path, string? AssetId, int StateCount);

/// <summary>Localized text the host supplies, by table and index.</summary>
public sealed record UIStringResource(string Name, string Table, int Index);

/// <summary>
/// What a color in a style is for, which the host resolves against its palette, so the style follows light, dark,
/// and accent changes: the way an app says "quieter text" rather than which grey.
/// </summary>
public enum UIColorRole
{
    /// <summary><c>text</c>: ordinary text.</summary>
    Text,

    /// <summary><c>secondaryText</c>: quieter text, such as a status line or a placeholder.</summary>
    SecondaryText,

    /// <summary><c>disabledText</c>: text on something turned off.</summary>
    DisabledText,

    /// <summary><c>highlightedText</c>: text on the accent color.</summary>
    HighlightedText,

    /// <summary><c>accent</c>: the accent color, for focus, selection marks, and fills.</summary>
    Accent,

    /// <summary><c>panel</c>: a window's background.</summary>
    Panel,

    /// <summary><c>inputBackground</c>: the inside of text fields and lists.</summary>
    InputBackground,

    /// <summary><c>border</c>: lines around controls.</summary>
    Border,

    /// <summary><c>selection</c>: a selected item's background.</summary>
    Selection,
}

/// <summary>A color in a style: one written as <c>#RRGGBB</c> or <c>#RRGGBBAA</c>, or a role.</summary>
public readonly record struct UIStyleColor(UIColor Color, UIColorRole? Role)
{
    /// <summary>A color written in the document, the same in every palette unless a <c>dark</c> twin replaces it.</summary>
    public static UIStyleColor Literal(UIColor color) => new(color, null);

    /// <summary>A color the host resolves against its palette.</summary>
    public static UIStyleColor FromRole(UIColorRole role) => new(default, role);
}

/// <summary>
/// A style's colors, each left out when it doesn't set one: text, text on a held button, text on a button under the
/// pointer, text on a disabled control, background, border, and fill.
/// </summary>
public sealed record UIStyleColors(
    UIStyleColor? Text,
    UIStyleColor? SelectedText,
    UIStyleColor? HighlightedText,
    UIStyleColor? DisabledText,
    UIStyleColor? Background,
    UIStyleColor? Border,
    UIStyleColor? Fill)
{
    /// <summary>No colors set.</summary>
    public static UIStyleColors None { get; } = new(null, null, null, null, null, null, null);
}

/// <summary>What a text size is for, which the host turns into points: the way an app says "a title".</summary>
public enum UIFontSizeRole
{
    /// <summary><c>caption</c>: small print.</summary>
    Caption,

    /// <summary><c>body</c>: ordinary text, the size controls use without a style.</summary>
    Body,

    /// <summary><c>title</c>: a heading.</summary>
    Title,

    /// <summary><c>largeTitle</c>: a large heading, such as a dialog's.</summary>
    LargeTitle,
}

/// <summary>A text size in a style: points written in the document, or a role.</summary>
public readonly record struct UIStyleSize(int Size, UIFontSizeRole? Role)
{
    /// <summary>A size in points.</summary>
    public static UIStyleSize Points(int points) => new(points, null);

    /// <summary>A size the host turns into points.</summary>
    public static UIStyleSize FromRole(UIFontSizeRole role) => new(0, role);
}

/// <summary>
/// A named set of visual properties that controls refer to. <see cref="Colors"/> are its colors; <see cref="Dark"/>
/// holds the <c>dark</c> twins, which replace them on a dark palette where they're set.
/// </summary>
public sealed record UIStyle(
    string Name,
    UIStyleColors Colors,
    UIStyleColors Dark,
    UIStyleSize? FontSize,
    UIFont? Font = null);

/// <summary>Text that's either written in the document or names a <c>&lt;String&gt;</c>.</summary>
public sealed record UIText(string Value, bool IsReference)
{
    /// <summary>Text written in the document.</summary>
    public static UIText Literal(string value) => new(value, false);

    /// <summary>The name of a <c>&lt;String&gt;</c> resource.</summary>
    public static UIText Reference(string name) => new(name, true);
}

/// <summary>A decorative image, shown with its <see cref="Pane"/> when it's in one.</summary>
public sealed record UIVisual(string Name, string Image, UIFrame Frame, int Line, string? Pane = null);

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
    bool Follows = false,
    string? Pane = null);

/// <summary>
/// Which of the desktop's standard menus a document's menu adds to, so a host can merge it rather than show two
/// File menus.
/// </summary>
public enum UIMenuRole
{
    /// <summary><c>file</c>: the File menu.</summary>
    File,

    /// <summary><c>edit</c>: the Edit menu.</summary>
    Edit,

    /// <summary><c>view</c>: the View menu.</summary>
    View,

    /// <summary><c>help</c>: the Help menu.</summary>
    Help,
}

/// <summary>
/// A drop-down menu in the global menu bar, or a submenu inside one. <see cref="Role"/> is the standard menu it
/// adds to, when it says; a role this reader doesn't know is skipped, so the menu shows as the document's own. A
/// submenu has no role.
/// </summary>
public sealed record UIMenu(
    string Name, string Title, char? Hotkey, IReadOnlyList<UIMenuEntry> Entries, int Line, UIMenuRole? Role = null)
{
    /// <summary>
    /// Every entry in the menu and, after each submenu's entry, everything in that submenu, however deep: what's
    /// disabled, checked, or named, and every shortcut, is found among these.
    /// </summary>
    public IReadOnlyList<UIMenuEntry> AllEntries()
    {
        List<UIMenuEntry> all = [];
        Collect(this, all);
        return all;

        static void Collect(UIMenu menu, List<UIMenuEntry> into)
        {
            foreach (var entry in menu.Entries)
            {
                into.Add(entry);
                if (entry.Submenu is { } submenu)
                    Collect(submenu, into);
            }
        }
    }
}

/// <summary>
/// An item, a separator, or a submenu's entry in a <see cref="UIMenu"/>. A separator has no name; a submenu's entry
/// has the submenu's name, title, and hotkey, and no shortcut.
/// </summary>
public sealed record UIMenuEntry(string? Name, string? Title, char? Hotkey, UIShortcut? Shortcut, int Line, UIMenu? Submenu = null)
{
    /// <summary>Whether it's a separator line rather than an item.</summary>
    public bool IsSeparator => Name is null;

    /// <summary>Whether it opens <see cref="Submenu"/> rather than being chosen.</summary>
    public bool IsSubmenu => Submenu is not null;
}

/// <summary>A key combination that chooses a menu item from anywhere, such as Ctrl+N.</summary>
public sealed record UIShortcut(bool Control, bool Shift, bool Alt, char Key)
{
    public override string ToString() =>
        $"{(Control ? "Ctrl+" : "")}{(Alt ? "Alt+" : "")}{(Shift ? "Shift+" : "")}{Key}";
}

/// <summary>
/// A button in the window's unified title bar, such as Add, or a tab when one is shown as selected. The host draws
/// the bar; items run in document order along its end, or its start with <c>placement="start"</c>.
/// </summary>
/// <remarks>
/// With an image, the item shows it as a symbol and its title as hover text. A reader that skips <c>image</c> and
/// <c>placement</c> shows a text item at the end, which still works, so neither needs a new element.
/// </remarks>
/// <param name="Image">The <c>&lt;Image&gt;</c> it shows, by name, or <see langword="null"/> for its title.</param>
public sealed record UIToolbarItem(
    string Name, string Title, int Line, string? Image = null, UIToolbarPlacement Placement = UIToolbarPlacement.End);

/// <summary>Which end of the title bar a toolbar item sits at, as GNOME's header bars pack them.</summary>
public enum UIToolbarPlacement
{
    /// <summary>The right end, the default.</summary>
    End,

    /// <summary>The left, after the window's buttons.</summary>
    Start,
}

/// <summary>
/// Makes the window's title editable in place, as an address bar is: the host draws it as a field in the title bar,
/// and Enter submits it under this name.
/// </summary>
public sealed record UITitleField(string Name, string? Placeholder, int Line);

/// <summary>
/// A list of places down the window's left, as Nautilus and Finder have. The host
/// draws it the window's full height, beside the content rather than in it, so it has a width and no position, and
/// the window's own size is the content's. Code treats it as a selectable <c>&lt;List&gt;</c> of this name.
/// </summary>
public sealed record UISidebar(string Name, float Width, int Line);

/// <summary>
/// One of a sidebar's panes: a row in the sidebar, by <see cref="Title"/>, and the
/// controls whose <c>pane</c> names it, which show only while its row is selected. The document says what each pane
/// holds, so a host, or a designer that runs no code, shows one pane at a time without the app's help.
/// </summary>
public sealed record UIPane(string Name, string Title, int Line);

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
/// controls, plus its title bar, menus, sidebar, and panes.
/// </summary>
/// <remarks>
/// <para>This reads the UIScript spec VentureOS keeps (docs/uiscript/, Draft, version 1). Hudini reads a subset of
/// it, so a document Hudini reads reads the same here. A document reads completely and validly, or throws
/// <see cref="UIScriptException"/>.</para>
/// <list type="bullet">
/// <item><description>A newer <c>version</c> or an unknown <c>requires</c> name refuses the document, before
/// anything else is checked, so the message says a newer reader is needed.</description></item>
/// <item><description>An unknown element refuses it; an unknown attribute is skipped and listed in
/// <see cref="IgnoredAttributes"/>.</description></item>
/// </list>
/// </remarks>
public sealed class UIScript
{
    /// <summary>The newest format version this reader understands.</summary>
    public const int SupportedVersion = 1;

    /// <summary>
    /// The <c>requires</c> names this reader accepts. Menus, sidebars, shelves, and panes were modules with these
    /// names until 2026-11-20, when they joined the format; documents written before still name them, so they're
    /// accepted and mean nothing. Any other name refuses the document.
    /// </summary>
    public static readonly IReadOnlyList<string> RetiredFeatures = ["menus", "sidebar", "shelf", "panes"];

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

    /// <summary>Menus in document order.</summary>
    public IReadOnlyList<UIMenu> Menus { get; private init; } = [];

    /// <summary>The title bar's buttons, in document order, which is left to right.</summary>
    public IReadOnlyList<UIToolbarItem> ToolbarItems { get; private init; } = [];

    /// <summary>The editable title, or <see langword="null"/> for a plain one.</summary>
    public UITitleField? TitleField { get; private init; }

    /// <summary>The sidebar, or <see langword="null"/>.</summary>
    public UISidebar? Sidebar { get; private init; }

    /// <summary>The sidebar's panes, in document order, which is the sidebar's.</summary>
    public IReadOnlyList<UIPane> Panes { get; private init; } = [];

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
            document = FlatXml.Parse(text, allowNesting: true);
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

    internal static UIScript Build(FlatXmlDocument document)
    {
        var root = document.Root;
        if (root.Name != "UIScript")
            throw new UIScriptException($"The root element must be <UIScript>, not <{root.Name}>.", root.Line);

        // Version and requires come first: a newer document may hold elements this reader would otherwise refuse.
        Attributes rootReader = new(root);
        var version = rootReader.Integer("version") ?? 1;
        if (version < 1)
            throw rootReader.Invalid("version", root["version"]!);
        if (version > SupportedVersion)
            throw new UIScriptException(
                $"This is UIScript version {version}, and this reader reads up to {SupportedVersion}.");

        var features = (rootReader.String("requires") ?? "").Split([' ', '\t', '\n', '\r'],
            StringSplitOptions.RemoveEmptyEntries);
        foreach (var feature in features)
        {
            if (!RetiredFeatures.Contains(feature))
                throw new UIScriptException(
                    $"This document requires \"{feature}\", which this reader doesn't support.");
        }

        List<UIIgnoredAttribute> ignored = [.. rootReader.Unused()];

        UIWindow? window = null;
        Dictionary<string, UIImageResource> images = [];
        Dictionary<string, UIStringResource> strings = [];
        Dictionary<string, UIStyle> styles = [];
        List<UIVisual> visuals = [];
        List<UIControl> controls = [];
        Dictionary<int, MenuDraft> menus = [];
        Dictionary<int, string> paneNames = [];
        HashSet<string> holdingPanes = [];
        List<(string Pane, UIControl Control)> flatPaned = [];
        List<MenuDraft> bar = [];
        List<(string Menu, UIMenuEntry Entry)> entries = [];
        List<UIToolbarItem> toolbar = [];
        UITitleField? titleField = null;
        UISidebar? sidebar = null;
        List<UIPane> panes = [];

        for (var at = 0; at < document.Children.Count; at++)
        {
            var element = document.Children[at];

            // Only a menu and a pane hold elements: a menu its items, separators, and submenus, and a pane its
            // controls and visuals. Everything else sits in <UIScript>.
            MenuDraft? holder = null;
            string? inPane = null;
            if (element.Parent != FlatXmlDocument.RootIndex)
            {
                var parent = document.Children[element.Parent];
                switch (parent.Name)
                {
                    case "Menu" when element.Name is "Menu" or "MenuItem" or "MenuSeparator":
                        holder = menus[element.Parent];
                        break;
                    case "Pane" when element.Name == "Visual" || ControlType(element.Name) is not null:
                        inPane = paneNames[element.Parent];
                        break;
                    default:
                        throw new UIScriptException(
                            parent.Name switch
                            {
                                "Menu" => $"<{element.Name}> can't be inside <Menu>; a menu holds menu items, separators, and submenus.",
                                "Pane" => $"<{element.Name}> can't be inside <Pane>; a pane holds controls and visuals.",
                                _ => $"<{element.Name}> can't be inside <{parent.Name}>; only a <Menu> or a <Pane> holds other elements.",
                            },
                            element.Line);
                }
            }

            Attributes reader = new(element);
            switch (element.Name)
            {
                case "Window":
                case "Panel":
                    if (window is not null)
                        throw new UIScriptException("There's more than one <Window> or <Panel>.", element.Line);
                    window = element.Name == "Panel" ? ReadPanel(reader, element.Line) : ReadWindow(reader);
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
                        ReadColors(reader, ""),
                        ReadColors(reader, "dark"),
                        reader.Size("fontSize"),
                        reader.Enum("font", ParseFont));
                    break;
                }

                case "Visual":
                    visuals.Add(new UIVisual(
                        reader.RequiredString("name"), reader.RequiredString("image"), ReadFrame(reader), element.Line, inPane));
                    break;

                case "Menu":
                {
                    // A submenu sits in its parent menu and goes nowhere else, so it has no standard menu to join.
                    if (holder is not null && element["role"] is not null)
                        throw new UIScriptException("A submenu can't have a role; only a menu in the bar joins a standard menu.", element.Line);

                    // An unknown role is a newer document's; its menu still works as the document's own.
                    var role = reader.String("role") is { } written ? ParseMenuRole(written) : null;
                    if (role is null && element["role"] is not null)
                        ignored.Add(new UIIgnoredAttribute("Menu", "role", element.Line));
                    MenuDraft draft = new(reader.RequiredString("name"), reader.RequiredString("title"), reader.Hotkey("hotkey"),
                        element.Line, role, IsSubmenu: holder is not null);
                    menus[at] = draft;
                    if (holder is null)
                        bar.Add(draft);
                    else
                        holder.Contents.Add(draft);
                    break;
                }

                case "MenuItem":
                {
                    UIMenuEntry item = new(
                        reader.RequiredString("name"),
                        reader.RequiredString("title"),
                        reader.Hotkey("hotkey"),
                        reader.Shortcut("shortcut"),
                        element.Line);
                    Place(holder, item, reader, entries);
                    break;
                }

                case "ToolbarItem":
                    toolbar.Add(new UIToolbarItem(
                        reader.RequiredString("name"), reader.RequiredString("title"), element.Line, reader.String("image"),
                        reader.String("placement") switch
                        {
                            null or "end" => UIToolbarPlacement.End,
                            "start" => UIToolbarPlacement.Start,
                            var other => throw new UIScriptException(
                                $"A toolbar item's placement is start or end, not \"{other}\".", element.Line),
                        }));
                    break;

                case "TitleField":
                    if (titleField is not null)
                        throw new UIScriptException("There's more than one <TitleField>; a window has one title.", element.Line);
                    titleField = new UITitleField(reader.RequiredString("name"), reader.String("placeholder"), element.Line);
                    break;

                case "Sidebar":
                    if (sidebar is not null)
                        throw new UIScriptException("There's more than one <Sidebar>; a window has one.", element.Line);
                    sidebar = new UISidebar(reader.RequiredString("name"), reader.RequiredNumber("width"), element.Line);
                    break;

                case "Pane":
                {
                    var pane = new UIPane(reader.RequiredString("name"), reader.RequiredString("title"), element.Line);
                    panes.Add(pane);
                    paneNames[at] = pane.Name;
                    if (document.ChildrenOf(at).Length > 0)
                        holdingPanes.Add(pane.Name);
                    break;
                }

                case "MenuSeparator":
                    Place(holder, new UIMenuEntry(null, null, null, null, element.Line), reader, entries);
                    break;

                default:
                    if (ControlType(element.Name) is not { } type)
                        throw new UIScriptException($"<{element.Name}> isn't a UIScript element.", element.Line);

                    // A control in a pane is in it; one written flat names its pane, which mustn't also hold controls.
                    var control = ReadControl(type, reader, element.Line);
                    if (inPane is not null)
                    {
                        if (control.Pane is not null)
                            throw new UIScriptException(
                                $"\"{control.Name}\" is inside the pane \"{inPane}\", so it doesn't name one with pane.", element.Line);
                        control = control with { Pane = inPane };
                    }
                    else if (control.Pane is { } named)
                    {
                        flatPaned.Add((named, control));
                    }

                    controls.Add(control);
                    break;
            }

            ignored.AddRange(reader.Unused());
        }

        // A pane uses one form: one that holds its controls can't also be named by a control written flat.
        foreach (var (pane, control) in flatPaned)
        {
            if (holdingPanes.Contains(pane))
                throw new UIScriptException(
                    $"\"{control.Name}\" names the pane \"{pane}\", which holds its controls inside it; a pane uses one form or the other.",
                    control.Line);
        }

        if (window is null)
            throw new UIScriptException("A UIScript document needs a <Window> or a <Panel>.");

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
            Menus = BuildMenus(bar, menus.Values, entries),
            ToolbarItems = toolbar,
            TitleField = titleField,
            Sidebar = sidebar,
            Panes = panes,
            IgnoredAttributes = ignored,
        };
        script.Validate();
        return script;
    }

    /// <summary>
    /// A panel has no title bar, so nothing that lives in one. Its own element can't say <c>resizable</c> or
    /// <c>subtitle</c>, but the parts of a title bar are separate elements, so they're checked here.
    /// </summary>
    private void ValidatePanel()
    {
        if (ToolbarItems.Count > 0)
            throw new UIScriptException("A panel has no title bar, so no toolbar items.", ToolbarItems[0].Line);
        if (TitleField is { } field)
            throw new UIScriptException("A panel has no title bar, so no title field.", field.Line);
        if (Sidebar is { } side)
            throw new UIScriptException("A panel can't have a sidebar.", side.Line);
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
        "Shelf" => UIControlType.Shelf,
        _ => null,
    };

    private static UIWindow ReadWindow(Attributes reader)
    {
        UISize size = new(reader.RequiredNumber("width"), reader.RequiredNumber("height"));
        var minimum = ReadMinimum(reader);
        var resizable = reader.Boolean("resizable") ?? false;
        var subtitle = reader.String("subtitle");
        return new UIWindow(size, minimum, resizable, subtitle);
    }

    // minWidth and minHeight, which a window and a panel take alike: both or neither.
    private static UISize? ReadMinimum(Attributes reader) => (reader.Number("minWidth"), reader.Number("minHeight")) switch
    {
        (null, null) => null,
        ({ } width, { } height) => new UISize(width, height),
        (null, _) => throw reader.Missing("minWidth"),
        _ => throw reader.Missing("minHeight"),
    };

    /// <summary>
    /// A panel: a size, the smallest it lays out at, and the edge it's pinned to, and nothing a title bar or resizing
    /// would need, so a panel can't say them. A panel stretched across the screen uses its minimum so its anchored
    /// controls don't run into each other on a narrow one. An edge this reader doesn't know is refused rather than guessed, so a panel never lands where it
    /// wasn't meant to.
    /// </summary>
    private static UIWindow ReadPanel(Attributes reader, int line)
    {
        UISize size = new(reader.RequiredNumber("width"), reader.RequiredNumber("height"));
        var minimum = ReadMinimum(reader);
        var edge = reader.RequiredString("edge") switch
        {
            "top" => UIEdge.Top,
            "bottom" => UIEdge.Bottom,
            var other => throw new UIScriptException($"A panel's edge is top or bottom, not \"{other}\".", line),
        };
        return new UIWindow(size, minimum, Role: UIWindowRole.Panel, Edge: edge);
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
            Follows: reader.Boolean("follows") ?? false,
            Pane: reader.String("pane"));
    }

    /// <summary>
    /// A menu as it's read, before it's built: what it holds in document order, each an entry or a submenu, whether
    /// it holds them nested, and whether it's itself a submenu.
    /// </summary>
    private sealed record MenuDraft(string Name, string Title, char? Hotkey, int Line, UIMenuRole? Role, bool IsSubmenu)
    {
        public List<object> Contents { get; } = [];

        // Flat items join a menu by name after everything is read; nested ones are in it as they're read.
        public bool HoldsNested { get; set; }
    }

    /// <summary>
    /// Puts an item or separator where it belongs: inside the menu that holds it, which it must not also name, or,
    /// written flat, in the menu its <c>menu</c> names, once every menu is known.
    /// </summary>
    private static void Place(MenuDraft? holder, UIMenuEntry entry, Attributes reader, List<(string Menu, UIMenuEntry Entry)> flat)
    {
        if (holder is null)
        {
            flat.Add((reader.RequiredString("menu"), entry));
            return;
        }

        if (reader.String("menu") is not null)
            throw new UIScriptException(
                $"{(entry.IsSeparator ? "A separator" : $"\"{entry.Name}\"")} is inside the menu \"{holder.Name}\", so it doesn't name one with menu.",
                entry.Line);
        holder.Contents.Add(entry);
        holder.HoldsNested = true;
    }

    private static IReadOnlyList<UIMenu> BuildMenus(
        List<MenuDraft> bar, IEnumerable<MenuDraft> all, List<(string Menu, UIMenuEntry Entry)> flat)
    {
        // Submenus are entries in their parents, so a parent holding one holds all it has, nested.
        foreach (var draft in all)
        {
            if (draft.IsSubmenu)
                continue;
            foreach (var content in draft.Contents)
            {
                if (content is MenuDraft)
                    draft.HoldsNested = true;
            }
        }

        foreach (var (menuName, entry) in flat)
        {
            var draft = all.FirstOrDefault(menu => menu.Name == menuName);
            var what = entry.IsSeparator ? "A separator" : $"\"{entry.Name}\"";
            if (draft is null)
                throw new UIScriptException($"{what} is in the menu \"{menuName}\", which isn't declared.", entry.Line);
            if (draft.IsSubmenu)
                throw new UIScriptException(
                    $"{what} names the submenu \"{menuName}\"; a submenu holds its items inside it.", entry.Line);
            if (draft.HoldsNested)
                throw new UIScriptException(
                    $"{what} names the menu \"{menuName}\", which holds its items inside it; a menu uses one form or the other.",
                    entry.Line);
            draft.Contents.Add(entry);
        }

        List<UIMenu> built = [];
        HashSet<char> barHotkeys = [];
        foreach (var draft in bar)
        {
            if (draft.Hotkey is { } key && !barHotkeys.Add(char.ToUpperInvariant(key)))
                throw new UIScriptException($"Two menus use the hotkey {key}.", draft.Line);
            built.Add(Build(draft));
        }

        return built;

        static UIMenu Build(MenuDraft draft)
        {
            List<UIMenuEntry> entries = [];
            HashSet<char> hotkeys = [];
            foreach (var content in draft.Contents)
            {
                UIMenuEntry entry;
                if (content is MenuDraft inner)
                {
                    var submenu = Build(inner);
                    entry = new UIMenuEntry(submenu.Name, submenu.Title, submenu.Hotkey, null, submenu.Line, submenu);
                }
                else
                {
                    entry = (UIMenuEntry)content;
                }

                if (entry.Hotkey is { } key && !hotkeys.Add(char.ToUpperInvariant(key)))
                    throw new UIScriptException($"Two entries in the {draft.Title} menu use the hotkey {key}.", entry.Line);
                entries.Add(entry);
            }

            return new UIMenu(draft.Name, draft.Title, draft.Hotkey, entries, draft.Line, draft.Role);
        }
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
            if (style.FontSize is { Role: null, Size: <= 0 })
                throw new UIScriptException($"The style \"{style.Name}\" needs a positive fontSize.");
        }

        foreach (var image in Images.Values)
        {
            if (image.Path is null && image.AssetId is null)
                throw new UIScriptException($"The image \"{image.Name}\" needs a path, an assetID, or both.");
            if (image.StateCount is not (1 or 4 or 6))
                throw new UIScriptException($"The image \"{image.Name}\" has stateCount {image.StateCount}; use 1, 4, or 6.");
        }

        if (Window.Role == UIWindowRole.Panel)
            ValidatePanel();

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
        foreach (var pane in Panes)
            Unique(names, pane.Name, pane.Line);
        // A submenu's name is its entry's, so it's counted once, however deep.
        foreach (var menu in Menus)
        {
            Unique(names, menu.Name, menu.Line);
            foreach (var entry in menu.AllEntries())
            {
                if (entry.Name is { } entryName)
                    Unique(names, entryName, entry.Line);
            }
        }

        HashSet<string> shortcuts = [];
        foreach (var menu in Menus)
        {
            foreach (var entry in menu.AllEntries())
            {
                if (entry.Shortcut is { } shortcut && !shortcuts.Add(shortcut.ToString()))
                    throw new UIScriptException($"Two menu items use the shortcut {shortcut}.", entry.Line);
            }
        }

        foreach (var item in ToolbarItems)
        {
            if (item.Image is { } itemImage && !Images.ContainsKey(itemImage))
                throw Unknown(item.Name, "image", itemImage, item.Line);
        }

        foreach (var visual in Visuals)
        {
            if (!Images.ContainsKey(visual.Image))
                throw Unknown(visual.Name, "image", visual.Image, visual.Line);
            ValidateFrame(visual.Frame, visual.Name, visual.Line);
        }

        if (Panes.Count > 0 && Sidebar is null)
            throw new UIScriptException("Panes are a sidebar's rows, so a document with panes needs a <Sidebar>.", Panes[0].Line);

        foreach (var control in Controls)
            ValidateControl(control);
    }

    private void ValidateControl(UIControl control)
    {
        var (name, type, line) = (control.Name, control.Type, control.Line);

        // Without the feature, pane is an unknown attribute and was skipped, so only a known one is checked.
        if (control.Pane is { } pane && !Panes.Any(each => each.Name == pane))
            throw Unknown(name, "pane", pane, line);

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

    // A style's colors, each attribute's name after the prefix: textColor, or with "dark", darkTextColor.
    private static UIStyleColors ReadColors(Attributes reader, string prefix)
    {
        string Name(string color) => prefix.Length == 0 ? color : prefix + char.ToUpperInvariant(color[0]) + color[1..];
        return new UIStyleColors(
            reader.StyleColor(Name("textColor")),
            reader.StyleColor(Name("selectedTextColor")),
            reader.StyleColor(Name("highlightedTextColor")),
            reader.StyleColor(Name("disabledTextColor")),
            reader.StyleColor(Name("backgroundColor")),
            reader.StyleColor(Name("borderColor")),
            reader.StyleColor(Name("fillColor")));
    }

    private static UIMenuRole? ParseMenuRole(string value) => value switch
    {
        "file" => UIMenuRole.File,
        "edit" => UIMenuRole.Edit,
        "view" => UIMenuRole.View,
        "help" => UIMenuRole.Help,
        _ => null,
    };

    private static UIColorRole? ParseColorRole(string value) => value switch
    {
        "text" => UIColorRole.Text,
        "secondaryText" => UIColorRole.SecondaryText,
        "disabledText" => UIColorRole.DisabledText,
        "highlightedText" => UIColorRole.HighlightedText,
        "accent" => UIColorRole.Accent,
        "panel" => UIColorRole.Panel,
        "inputBackground" => UIColorRole.InputBackground,
        "border" => UIColorRole.Border,
        "selection" => UIColorRole.Selection,
        _ => null,
    };

    private static UIFontSizeRole? ParseSizeRole(string value) => value switch
    {
        "caption" => UIFontSizeRole.Caption,
        "body" => UIFontSizeRole.Body,
        "title" => UIFontSizeRole.Title,
        "largeTitle" => UIFontSizeRole.LargeTitle,
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

        /// <summary>A style's color: written as a color, or a role's name.</summary>
        public UIStyleColor? StyleColor(string attribute)
        {
            if (String(attribute) is not { } value)
                return null;
            if (!value.StartsWith('#'))
            {
                return ParseColorRole(value) is { } role
                    ? UIStyleColor.FromRole(role)
                    : throw new UIScriptException(
                        $"{attribute}=\"{value}\" isn't a color: use #RRGGBB, #RRGGBBAA, or a role such as secondaryText.",
                        element.Line);
            }

            return UIStyleColor.Literal(Color(attribute)!.Value);
        }

        /// <summary>A style's text size: points, or a role's name.</summary>
        public UIStyleSize? Size(string attribute)
        {
            if (String(attribute) is not { } value)
                return null;
            if (ParseSizeRole(value) is { } role)
                return UIStyleSize.FromRole(role);
            return Integer(attribute) is { } points
                ? UIStyleSize.Points(points)
                : null;
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
