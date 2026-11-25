using Venture.OS.Interface;

namespace Venture.OS.Desktop;

/// <summary>What a control reports for its owner to act on, as Hudini's <c>UIView.Event</c> does.</summary>
public enum UIEventKind
{
    /// <summary>An enabled button was pressed.</summary>
    Pressed,

    /// <summary>A text field submitted its text, with Enter.</summary>
    Submitted,

    /// <summary>Typing, or an edit such as Paste or Undo, changed a text area's text.</summary>
    Changed,

    /// <summary>A click on a selectable list changed its selected item.</summary>
    Selected,
}

/// <summary>An event, with the control's name and, depending on the kind, its text or index.</summary>
/// <param name="Item">The name of a grid's item that was chosen or opened, when it has one.</param>
public readonly record struct UIEvent(UIEventKind Kind, string Control, string? Text = null, int Index = -1, string? Item = null);

/// <summary>A rectangle in pixels.</summary>
public readonly record struct UIRect(float X, float Y, float Width, float Height)
{
    public bool Contains(float x, float y) => x >= X && y >= Y && x < X + Width && y < Y + Height;
}

/// <summary>The input a view handles in one frame, in screen coordinates.</summary>
/// <param name="PointerPressed">The button went down this frame.</param>
/// <param name="PointerReleased">The button came up this frame.</param>
/// <param name="Time">When the frame happened, for double-clicks; see <see cref="DesktopInput.Time"/>.</param>
public readonly record struct UIViewInput(
    int PointerX,
    int PointerY,
    bool PointerDown,
    bool PointerPressed,
    bool PointerReleased,
    int Scroll,
    KeyPress[] Keys,
    TimeSpan Time = default);

/// <summary>The items a list shows, which its owner fills, and which one is selected.</summary>
public sealed class UIListState
{
    /// <summary>The items, in order. Change them freely; the list keeps up on the next frame.</summary>
    public List<string> Items { get; } = [];

    /// <summary>
    /// Each item's icon, by the name of an <c>&lt;Image&gt;</c> in the document, or <see langword="null"/>; a
    /// sidebar shows them. Items past the end have none.
    /// </summary>
    public List<string?> Icons { get; } = [];

    /// <summary>
    /// The items a separator comes before, in order, splitting a sidebar's rows into groups, as Nautilus splits its
    /// places from its drives. Separators aren't items, so indexes count items only.
    /// </summary>
    public List<int> GroupStarts { get; } = [];

    /// <summary>The selected item's index, or -1. Setting it reports no event.</summary>
    public int SelectedIndex { get; set; } = -1;

    /// <summary>The first item showing, for scrolling.</summary>
    public int FirstVisible { get; set; }

    internal int LastCount { get; set; }
}

/// <summary>
/// Builds, updates, and draws the controls a UIScript document describes, as Hudini's <c>UIView</c> does.
/// </summary>
/// <remarks>
/// <para>The view owns the controls and their interaction state: text, values, focus, lists, and what's hidden or
/// disabled. Its owner fills lists, sets text and values by name, draws into regions, and handles the events
/// <see cref="Update"/> returns. Positions are relative to the view's corner, which the owner passes in.</para>
/// <para>A document's <c>&lt;Sidebar&gt;</c> is a selectable list the view keeps and draws to the left of its corner, or
/// past the content's right edge with <c>edge="right"</c>,
/// <see cref="SidebarWidth"/> wide, so it reads, fills, and reports like a <c>&lt;List&gt;</c> of its name; the
/// window frame draws the shade behind it.</para>
/// <para>With panes, the sidebar's rows are the panes' titles, and a control in a pane shows only while that pane's row
/// is selected, as well as when it isn't hidden; the first pane starts selected. Selecting a row, by a click or by
/// its owner, switches panes, so the owner needn't hide anything to do it.</para>
/// <para>A view doesn't read image files: its owner supplies each image with <see cref="SetImage"/>, and a
/// <c>&lt;Visual&gt;</c> draws it, scaled to the visual's frame. Like Hudini when a host can't supply one, a visual
/// whose image is missing shows a red outline, at 24 pixels when it leaves out its size, and the image is listed
/// in <see cref="MissingImages"/>. A button with an image shows it the same way, ringed in the accent when it's
/// checked.</para>
/// </remarks>
public sealed partial class UIView
{
    private const int FallbackImageSize = 24;

    // Images supplied by name, those not yet supplied, and each visual's image at the size it was last drawn.
    private readonly Dictionary<string, DesktopImage> images = [];
    private readonly List<string> missingImages;
    private readonly Dictionary<string, DesktopImage> scaledImages = [];

    // Sidebar rows' icons as symbols, by image name and color, with the image they were made from.
    private readonly Dictionary<(string Name, UIColor Color), (DesktopImage Source, DesktopImage Symbol)> sidebarSymbols = [];
    private const int Inset = 4;
    private const string Ellipsis = "…";

    // The sidebar's rows start this far below the content's top and end this far above its bottom, are this much
    // taller than a list's, and have their selection inset this far from its sides.
    private const int SidebarInset = 6;
    private const int SidebarRowExtra = 8;
    private const int SidebarSelectionInset = 6;
    private const int SidebarSelectionRadius = 6;
    private const int SidebarSeparatorRoom = 9;
    private const int SidebarIconSize = 16;
    private const int SidebarIconGap = 8;

    private readonly UIScript script;

    // The sidebar as a selectable list left of the view's corner, so list selection, scrolling, and hits serve it.
    private readonly UIControl? sidebar;
    private readonly Dictionary<string, State> states = [];
    private readonly Action<string, UIRect>? drawRegion;
    private readonly HashSet<string> missingStrings = [];
    private string? focused;
    private string? armed;

    // The text field or area a press landed in, whose selection follows the pointer until it's released.
    private string? selecting;
    private (int X, int Y) pointer;

    // Where the pointer is over the view, for drawing; see SetHover.
    private (int X, int Y)? hover;

    // The shelf icon a press landed on, which is pressed if the release lands on it too.
    private (string Shelf, int Index)? shelfPress;
    private (string List, int Index, TimeSpan Time)? lastListClick;

    // Reused every frame, since the desktop updates views without pause; see Update.
    private readonly List<UIEvent> frameEvents = [];

    // The field that had focus before its window left the front; see RestoreFocus.
    private string? setAside;

    // Styles as the palette in use resolves them, by name, worked out again when the palette changes.
    private readonly Dictionary<string, ResolvedStyle> resolvedStyles = [];
    private DesktopPalette? resolvedFor;

    // Draws and measures in the monospace font, for controls whose style asks for it; kept while the painter is.
    private MonospacePainter? monospace;

    /// <summary>The most characters a text area takes without a <c>maximumLength</c>.</summary>
    public const int TextAreaLength = 65536;

    /// <summary>The longest gap between two clicks on a list item that makes them a double-click.</summary>
    public static readonly TimeSpan DoubleClickInterval = TimeSpan.FromMilliseconds(500);

    /// <param name="script">The document.</param>
    /// <param name="resolveString">Supplies the text for a <c>&lt;String&gt;</c>; without it, references show their table and index.</param>
    /// <param name="drawRegion">Draws into a region, given its name and screen bounds.</param>
    public UIView(UIScript script, Func<UIStringResource, string?>? resolveString = null, Action<string, UIRect>? drawRegion = null)
    {
        ArgumentNullException.ThrowIfNull(script);
        this.script = script;
        this.drawRegion = drawRegion;
        WindowSize = script.Window.Size;

        foreach (var control in script.Controls)
        {
            State state = new()
            {
                Text = Resolve(control.Text, resolveString) ?? "",
                Placeholder = Resolve(control.Placeholder, resolveString),
                Tooltip = control.Tooltip is { } tooltip ? Resolve(UIText.Reference(tooltip), resolveString) : null,
                Value = control.ValueRange?.Minimum ?? 0,
                List = control.Type == UIControlType.List ? new UIListState() : null,
                Shelf = control.Type == UIControlType.Shelf ? new Shelf() : null,
                Grid = control.Grid is { } layout ? NewGrid(layout, resolveString) : null,
                Pane = control.Pane,
            };

            // A list's text is its first item.
            if (state.List is not null && control.Text is not null)
            {
                state.List.Items.Add(state.Text);
                state.Text = "";
            }

            states[control.Name] = state;
        }

        foreach (var visual in script.Visuals)
            states[visual.Name] = new State { Pane = visual.Pane };

        if (script.Sidebar is { } side)
        {
            SidebarWidth = (int)MathF.Ceiling(side.Width);
            IsSidebarOnRight = side.Edge == UIEdge.Right;

            // A sidebar that holds controls has no rows: its controls are placed by Place, from its corner.
        }

        if (script.Sidebar is { HoldsContent: false } listed)
        {
            // Left of the view's corner, or just past the content's right edge, held there as the window resizes.
            sidebar = new UIControl(
                listed.Name, UIControlType.List,
                IsSidebarOnRight
                    ? new UIFrame(0, 0, SidebarWidth, null, new UIEdgeOffsets(null, -SidebarWidth, SidebarInset, SidebarInset))
                    : new UIFrame(-SidebarWidth, 0, SidebarWidth, null, new UIEdgeOffsets(null, null, SidebarInset, SidebarInset)),
                listed.Line, IsSelectable: true);
            var rows = new UIListState();
            foreach (var pane in script.Panes)
                rows.Items.Add(pane.Title);
            if (rows.Items.Count > 0)
                rows.SelectedIndex = 0;
            states[listed.Name] = new State { List = rows };
        }

        // The title bar's parts share the namespace too: toolbar items are disabled and selected by name, and the
        // title field's text is read and set like a text field's.
        foreach (var item in script.ToolbarItems)
            states[item.Name] = new State();
        if (script.TitleField is { } titleField)
            states[titleField.Name] = new State { Placeholder = titleField.Placeholder };

        // Menu items share the namespace, so they're disabled and checked by name like controls, submenus and the
        // items in them included.
        foreach (var menu in script.Menus)
        {
            foreach (var entry in menu.AllEntries())
            {
                if (entry.Name is { } item)
                    states[item] = new State();
            }
        }

        Subtitle = script.Window.Subtitle;
        missingImages = [.. script.Images.Keys];
        missingImages.Sort(StringComparer.Ordinal);
    }

    /// <summary>
    /// The size the view lays out at, which edge offsets measure from: the content's, without the sidebar. It starts
    /// at the document's size; see <see cref="SetWindowSize"/>.
    /// </summary>
    public UISize WindowSize { get; private set; }

    /// <summary>The smallest size the view lays out at: the document's minimum, or its size when it gives none.</summary>
    /// <remarks>A panel without one may be as narrow as its content, such as a shelf with few icons.</remarks>
    public UISize MinimumWindowSize => script.Window.MinimumSize
        ?? (script.Window.Role == UIWindowRole.Panel ? new UISize(0, script.Window.Size.Height) : script.Window.Size);

    /// <summary>
    /// Lays the view out at a new size, as Hudini's <c>setWindowSize</c> does, such as when its window is resized:
    /// controls with edge offsets move or stretch with it. It's never smaller than <see cref="MinimumWindowSize"/>.
    /// </summary>
    public void SetWindowSize(UISize size)
    {
        var minimum = MinimumWindowSize;
        WindowSize = new UISize(
            float.IsFinite(size.Width) ? Math.Max(size.Width, minimum.Width) : minimum.Width,
            float.IsFinite(size.Height) ? Math.Max(size.Height, minimum.Height) : minimum.Height);
    }

    /// <summary>The sidebar's name, which reads and fills like a list's, or <see langword="null"/> without one.</summary>
    public string? SidebarName => script.Sidebar?.Name;

    /// <summary>Whether the sidebar holds controls and visuals of its own in place of rows, so it isn't a list.</summary>
    public bool SidebarHoldsContent => script.Sidebar?.HoldsContent == true;

    /// <summary>How wide the sidebar is, in whole pixels, or 0 without one.</summary>
    public int SidebarWidth { get; }

    /// <summary>Whether the sidebar is on the window's right (<c>edge="right"</c>) rather than its left.</summary>
    public bool IsSidebarOnRight { get; }

    /// <summary>How much of the window's width is left of the content: the sidebar's, when it's on the left.</summary>
    public int LeadingSidebarWidth => IsSidebarOnRight ? 0 : SidebarWidth;

    /// <summary>The document this view shows.</summary>
    public UIScript Script => script;

    /// <summary>
    /// The line under the window's title, from <c>subtitle</c> on <c>&lt;Window&gt;</c>, which the owner may change;
    /// <see langword="null"/> or empty for none.
    /// </summary>
    public string? Subtitle { get; set; }

    /// <summary>Image resources nothing has been supplied for, sorted.</summary>
    public IReadOnlyList<string> MissingImages => missingImages;

    /// <summary>The picture supplied for one of the document's <c>&lt;Image&gt;</c>s, or <see langword="null"/>.</summary>
    public DesktopImage? Image(string name) => images.GetValueOrDefault(name);

    /// <summary>
    /// Supplies the picture for one of the document's <c>&lt;Image&gt;</c> resources, or takes it away with
    /// <see langword="null"/>, which outlines it again.
    /// </summary>
    /// <returns>Whether the document has an image of that name.</returns>
    public bool SetImage(string name, DesktopImage? image)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!script.Images.ContainsKey(name))
            return false;

        if (image is null)
        {
            images.Remove(name);
            if (!missingImages.Contains(name))
            {
                missingImages.Add(name);
                missingImages.Sort(StringComparer.Ordinal);
            }
        }
        else
        {
            images[name] = image;
            missingImages.Remove(name);
        }

        // Visuals and buttons showing it are scaled again on the next draw.
        foreach (var visual in script.Visuals)
        {
            if (visual.Image == name)
                scaledImages.Remove(visual.Name);
        }

        foreach (var control in script.Controls)
        {
            if (control.Image == name)
                scaledImages.Remove(control.Name);
        }

        return true;
    }

    /// <summary>String resources there was no text for, which show their table and index instead.</summary>
    public IReadOnlyCollection<string> MissingStrings => missingStrings;

    /// <summary>The text field that has keyboard focus, or <see langword="null"/>.</summary>
    public string? FocusedControl => focused;

    /// <summary>A control's bounds relative to the view's corner, or <see langword="null"/> when nothing has this name.</summary>
    public UIRect? Bounds(string name)
    {
        if (script.Control(name) is { } control)
            return Place(control.Frame, control.InSidebar, control.Image is not null);

        foreach (var visual in script.Visuals)
        {
            if (visual.Name == name)
                return Place(visual.Frame, visual.InSidebar, hasImage: true);
        }

        return null;
    }

    /// <summary>A text, formatted text, button, or text field's text, or <see langword="null"/>.</summary>
    public string? Text(string name) => Find(name) is { List: null } state ? state.Text : null;

    /// <summary>Replaces a control's text, cheap enough to call every frame.</summary>
    public void SetText(string name, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (Find(name) is not { } state)
            return;

        // Text an app sets is a new starting point, so what could be undone before no longer applies.
        if (!string.Equals(state.Text, text, StringComparison.Ordinal))
            state.Cursor.History.Clear();
        state.Text = text;
        state.Cursor.Clamp(text.Length);
    }

    /// <summary>Whether the focused text field or area has an edit to undo.</summary>
    public bool CanUndoFocused => focused is not null && states[focused].Cursor.History.CanUndo;

    /// <summary>Whether the focused text field or area has an undone edit to redo.</summary>
    public bool CanRedoFocused => focused is not null && states[focused].Cursor.History.CanRedo;

    /// <summary>Takes back the focused text field's or area's newest edit, as Undo does.</summary>
    /// <returns>A changed event for a text area whose text changed, as typing reports one.</returns>
    public IReadOnlyList<UIEvent> UndoFocused() => StepFocused(undo: true);

    /// <summary>Applies again the focused text field's or area's newest undone edit, as Redo does.</summary>
    /// <returns>A changed event for a text area whose text changed.</returns>
    public IReadOnlyList<UIEvent> RedoFocused() => StepFocused(undo: false);

    private IReadOnlyList<UIEvent> StepFocused(bool undo)
    {
        if (focused is null || script.Control(focused) is not { } control)
            return [];

        var state = states[focused];
        var before = state.Text;
        state.Text = undo ? state.Cursor.Undo(before) : state.Cursor.Redo(before);
        state.RevealCaret = true;
        return control.Type == UIControlType.TextArea && !string.Equals(before, state.Text, StringComparison.Ordinal)
            ? [new UIEvent(UIEventKind.Changed, focused, state.Text)]
            : [];
    }

    /// <summary>
    /// The selected text's start and length in a text field or area, or <see langword="null"/> for anything else;
    /// with nothing selected, an empty one at the caret.
    /// </summary>
    public (int Start, int Length)? Selection(string name) =>
        script.Control(name) is { Type: UIControlType.TextField or UIControlType.TextArea } && Find(name) is { } state
            ? state.Cursor.Selection
            : null;

    /// <summary>Whether the focused text field or area has text selected that can be copied: not a secure field's.</summary>
    public bool HasFocusedSelection =>
        focused is not null && script.Control(focused) is { IsSecure: false } && states[focused].Cursor.HasSelection;

    /// <summary>
    /// The focused text field's or area's selected text, for Copy and Cut, or <see langword="null"/> when nothing is
    /// focused or selected, or the field is secure, whose text never leaves it.
    /// </summary>
    public string? FocusedSelection()
    {
        if (focused is null || script.Control(focused) is not { } control || control.IsSecure)
            return null;

        var state = states[focused];
        state.Cursor.Clamp(state.Text.Length);
        var (start, length) = state.Cursor.Selection;
        return length > 0 ? state.Text.Substring(start, length) : null;
    }

    /// <summary>
    /// Puts text in place of the focused text field's or area's selection, or at its caret, as Paste does, or removes
    /// the selection with empty text, as Cut does. A field keeps to one line, with line breaks as spaces; other
    /// control characters are left out; and as much goes in as the control's maximum length allows.
    /// </summary>
    /// <returns>A changed event for a text area whose text changed, as typing reports one.</returns>
    public IReadOnlyList<UIEvent> ReplaceFocusedSelection(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (focused is null || script.Control(focused) is not { } control)
            return [];

        var area = control.Type == UIControlType.TextArea;
        var state = states[focused];
        var cursor = state.Cursor;
        cursor.Clamp(state.Text.Length);

        var cleaned = Clean(text, area);
        var maximum = control.MaximumLength ?? (area ? TextAreaLength : 256);
        var room = Math.Max(0, maximum - (state.Text.Length - cursor.Selection.Length));
        if (cleaned.Length > room)
            cleaned = cleaned[..room];

        var before = state.Text;
        state.Text = cursor.Replace(before, cleaned, maximum);
        state.RevealCaret = true;
        return area && !string.Equals(before, state.Text, StringComparison.Ordinal)
            ? [new UIEvent(UIEventKind.Changed, focused, state.Text)]
            : [];
    }

    /// <summary>Selects all of the focused text field's or area's text, as Select All does.</summary>
    public void SelectAllFocused()
    {
        if (focused is not null)
            SelectAll(focused);
    }

    // Text as it can go into a control: line breaks as LF in an area and as spaces in a field, tabs as spaces, and
    // other control characters left out.
    private static string Clean(string text, bool area)
    {
        text = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var builder = new System.Text.StringBuilder(text.Length);
        foreach (var character in text)
        {
            if (character == '\n')
                builder.Append(area ? '\n' : ' ');
            else if (character == '\t')
                builder.Append(' ');
            else if (!char.IsControl(character))
                builder.Append(character);
        }

        return builder.ToString();
    }

    /// <summary>Selects all of a text field's or area's text, as Ctrl+A does.</summary>
    public void SelectAll(string name)
    {
        if (script.Control(name) is { Type: UIControlType.TextField or UIControlType.TextArea } && Find(name) is { } state)
            state.Cursor.SelectAll(state.Text.Length);
    }

    /// <summary>A progress bar's value, or <see langword="null"/>.</summary>
    public float? Value(string name) =>
        script.Control(name) is { Type: UIControlType.ProgressBar } ? Find(name)!.Value : null;

    /// <summary>Sets a progress bar's value, kept within its range. Reports no event.</summary>
    public void SetValue(string name, float value)
    {
        if (script.Control(name) is { } control && Find(name) is { } state)
        {
            var (minimum, maximum) = Range(control);
            state.Value = Math.Clamp(value, minimum, maximum);
        }
    }

    /// <summary>The list with this name, or <see langword="null"/>.</summary>
    public UIListState? List(string name) => Find(name)?.List;

    /// <summary>A shelf, which its owner fills, by name, or <see langword="null"/> when the name isn't one.</summary>
    public Shelf? Shelf(string name) => Find(name)?.Shelf;

    /// <summary>
    /// How wide the view's first shelf is with its icons, for a panel that's as wide as its content, or
    /// <see langword="null"/> without a shelf.
    /// </summary>
    public int? ShelfWidth
    {
        get
        {
            foreach (var control in script.Controls)
            {
                if (control.Type == UIControlType.Shelf)
                    return states[control.Name].Shelf!.Width;
            }

            return null;
        }
    }

    /// <summary>Shows or hides a control or visual.</summary>
    public void SetHidden(bool hidden, string name)
    {
        if (Find(name) is { } state)
        {
            state.Hidden = hidden;
            if (hidden && focused == name)
                focused = null;
        }
    }

    /// <summary>Turns a control or menu item off or on. A disabled one draws dimmed and takes no input.</summary>
    public void SetDisabled(bool disabled, string name)
    {
        if (Find(name) is { } state)
        {
            state.Disabled = disabled;
            if (disabled && focused == name)
                focused = null;
        }
    }

    /// <summary>
    /// Marks a button or menu item as chosen, such as a toggle that's on: a button draws as if held down, and a menu
    /// item gets a check mark.
    /// </summary>
    public void SetSelected(bool selected, string name)
    {
        if (Find(name) is { } state)
            state.Selected = selected;
    }

    /// <summary>Whether a control or menu item is disabled.</summary>
    public bool IsDisabled(string name) => Find(name)?.Disabled ?? false;

    /// <summary>Whether a button or menu item is marked as chosen.</summary>
    public bool IsSelected(string name) => Find(name)?.Selected ?? false;

    /// <summary>Whether a control or visual is hidden by its owner, whatever pane is showing.</summary>
    public bool IsHidden(string name) => Find(name)?.Hidden ?? false;

    /// <summary>
    /// Whether a control or visual is drawn now: not hidden, and in no pane or in the one showing. A name the view
    /// doesn't know isn't.
    /// </summary>
    public bool IsShowing(string name) => Find(name) is { } state && Showing(state);

    /// <summary>
    /// The pane whose row is selected, or <see langword="null"/> without panes or with no row selected, when only
    /// controls in no pane show.
    /// </summary>
    public UIPane? SelectedPane =>
        sidebar is not null && states[sidebar.Name].List!.SelectedIndex is var index && index >= 0 && index < script.Panes.Count
            ? script.Panes[index]
            : null;

    /// <summary>
    /// Where a sidebar row is drawn, relative to the view's corner, by the arithmetic a click uses, or
    /// <see langword="null"/> for one scrolled out of sight or past the last, or without a sidebar. A designer
    /// outlines a pane's row with it.
    /// </summary>
    public UIRect? SidebarRowBounds(int row)
    {
        if (sidebar is null)
            return null;

        var list = states[sidebar.Name].List!;
        var place = Place(sidebar.Frame, false);
        var rowHeight = RowHeight(sidebar);
        var top = 1;
        for (var index = list.FirstVisible; index < list.Items.Count; index++)
        {
            if (index > list.FirstVisible && list.GroupStarts.Contains(index))
                top += SidebarSeparatorRoom;
            if (top + rowHeight > place.Height)
                return null;
            if (index == row)
                return new UIRect(place.X + SidebarSelectionInset, place.Y + top, place.Width - (2 * SidebarSelectionInset), rowHeight);
            top += rowHeight;
        }

        return null;
    }

    /// <summary>Whether the sidebar's rows come from the document's panes, so its owner can't set them.</summary>
    public bool HasPanes => script.Panes.Count > 0;

    private bool Showing(State state) => !state.Hidden && (state.Pane is null || state.Pane == SelectedPane?.Name);

    /// <summary>Gives a text field keyboard focus, with the caret at the end.</summary>
    public void Focus(string name)
    {
        if (script.Control(name) is { Type: UIControlType.TextField or UIControlType.TextArea, IsReadOnly: false }
            && Find(name) is { Disabled: false } state && Showing(state))
        {
            focused = name;
            state.Caret = state.Text.Length;
        }
    }

    /// <summary>
    /// Takes keyboard focus away, such as when another window comes forward, remembering where it was so
    /// <see cref="RestoreFocus"/> can put it back.
    /// </summary>
    public void ClearFocus()
    {
        if (focused is not null)
            setAside = focused;
        focused = null;
    }

    /// <summary>
    /// Gives focus back to the field that had it before <see cref="ClearFocus"/>, as when its window comes to the front
    /// again, so typing goes where it went before. Nothing happens if something else has focus, or the field can't
    /// take it now.
    /// </summary>
    public void RestoreFocus()
    {
        if (focused is null && setAside is { } name)
            Focus(name);
        setAside = null;
    }

    /// <summary>
    /// The hover text of the frontmost shown control at a point relative to the view's corner, or
    /// <see langword="null"/>. A disabled control still has it, to say what it would do.
    /// </summary>
    public string? TooltipAt(int x, int y)
    {
        for (var index = script.Controls.Count - 1; index >= 0; index--)
        {
            var control = script.Controls[index];
            var state = states[control.Name];
            if (Showing(state) && Place(control.Frame, control.InSidebar, control.Image is not null).Contains(x, y))
                return (state.Grid is not null ? GridTooltipAt(control, x, y) : null) ?? state.Tooltip;
        }

        return null;
    }

    /// <summary>
    /// Says where the pointer is, relative to the view's corner, or <see langword="null"/> when it's somewhere else,
    /// such as over another window, so drawing shows what it rests on. The desktop sets it for every window, since
    /// only the front one gets input.
    /// </summary>
    public void SetHover((int X, int Y)? point)
    {
        ShowScrollBarsUnder(hover, point);
        hover = point;
    }

    /// <summary>
    /// Handles a frame's input with the view's corner at (<paramref name="originX"/>, <paramref name="originY"/>).
    /// </summary>
    /// <returns>The events the input caused, in order.</returns>
    /// <remarks>
    /// The desktop calls this on every pass of its frame loop, so a frame where nothing happens allocates nothing:
    /// the events are gathered in a reused list and copied out only when there are some.
    /// </remarks>
    public IReadOnlyList<UIEvent> Update(UIViewInput input, int originX, int originY)
    {
        var events = frameEvents;
        events.Clear();
        var (x, y) = (input.PointerX - originX, input.PointerY - originY);
        pointer = (x, y);
        if (input.Time > Now)
            Now = input.Time;

        FollowLists();

        // A field in a pane that's no longer showing, since its owner or a click switched panes, loses the keyboard.
        if (focused is { } field && !Showing(states[field]))
            focused = null;

        if (input.PointerPressed)
            Press(x, y, input.Time, events, originX, originY);

        if (input.PointerDown)
            HoldScrollBar(x, y, input.Time);

        // Dragging from a press in text selects up to the pointer.
        if (selecting is not null && input.PointerDown && !input.PointerPressed && script.Control(selecting) is { } text)
            states[selecting].Cursor.MoveTo(CaretAt(text, states[selecting], x, y), extend: true);

        if (input.PointerReleased)
        {
            if (armed is not null && Hit(x, y) is { } released && released.Name == armed)
                Release(released, events);

            // A shelf icon is pressed when the release lands on the icon the press did.
            if (shelfPress is { } press && states[press.Shelf].Shelf is { } shelf
                && shelf.IndexAt(x + originX, y + originY) == press.Index)
                events.Add(new UIEvent(UIEventKind.Pressed, press.Shelf, shelf.Items[press.Index].Title, Index: press.Index));

            shelfPress = null;
            ReleaseScrollBar();
            armed = null;
            selecting = null;
        }

        if (input.Scroll != 0 && Hit(x, y) is { } scrolled)
            ScrollOver(scrolled, input.Scroll);

        if (focused is not null)
        {
            foreach (var key in input.Keys)
                Type(focused, key, events);
        }

        return events.Count == 0 ? [] : [.. events];
    }

    /// <summary>
    /// A visual's image at the size it's drawn, scaled once and kept, since scaling is slow; <see langword="null"/>
    /// when it has none or the frame is empty.
    /// </summary>
    private DesktopImage? ScaledImage(UIVisual visual, int width, int height) => ScaledImage(visual.Name, visual.Image, width, height);

    /// <summary>
    /// A visual's or button's image at the size it's drawn, by the owner's name, scaled once and kept; an image with
    /// several states shows its first, the leftmost of its equal-width strips.
    /// </summary>
    private DesktopImage? ScaledImage(string owner, string imageName, int width, int height)
    {
        if (!images.TryGetValue(imageName, out var image) || width <= 0 || height <= 0
            || width > DesktopImage.MaxSide || height > DesktopImage.MaxSide)
            return null;

        if (scaledImages.TryGetValue(owner, out var scaled) && scaled.Width == width && scaled.Height == height)
            return scaled;

        var states = script.Images.TryGetValue(imageName, out var resource) ? Math.Max(1, resource.StateCount) : 1;
        if (states > 1 && image.Width >= states)
            image = image.Cropped(0, 0, image.Width / states, image.Height);

        scaled = image.Scaled(width, height);
        scaledImages[owner] = scaled;
        return scaled;
    }

    /// <summary>Draws the visuals, then the controls in document order, with the view's corner at the origin.</summary>
    public void Draw(IPainter painter, int originX, int originY)
    {
        ArgumentNullException.ThrowIfNull(painter);
        FollowLists();

        foreach (var visual in script.Visuals)
        {
            if (!Showing(states[visual.Name]))
                continue;

            var bounds = Offset(Place(visual.Frame, visual.InSidebar, hasImage: true), originX, originY);
            var (x, y, width, height) = ((int)bounds.X, (int)bounds.Y, (int)bounds.Width, (int)bounds.Height);
            if (ScaledImage(visual, width, height) is { } image)
                painter.DrawImage(image, x, y);
            else
                DesktopTheme.Outline(painter, x, y, width, height, DesktopTheme.MissingImage);
        }

        foreach (var control in script.Controls)
        {
            var state = states[control.Name];
            if (!Showing(state))
                continue;

            var bounds = Offset(Place(control.Frame, control.InSidebar, control.Image is not null), originX, originY);
            var target = PainterFor(control, painter);
            switch (control.Type)
            {
                case UIControlType.Text:
                    DrawLabel(target, control, state, bounds);
                    break;
                case UIControlType.FormattedText:
                    DrawFormatted(target, control, state, bounds);
                    break;
                case UIControlType.Button:
                    DrawButton(target, control, state, bounds);
                    break;
                case UIControlType.TextField:
                    DrawField(target, control, state, bounds);
                    break;
                case UIControlType.TextArea:
                    DrawArea(target, control, state, bounds);
                    break;
                case UIControlType.List:
                    DrawList(target, control, state, bounds);
                    break;
                case UIControlType.ProgressBar:
                    DrawProgress(painter, control, state, bounds);
                    break;
                case UIControlType.Region:
                    drawRegion?.Invoke(control.Name, bounds);
                    break;
                case UIControlType.Shelf:
                    DrawShelf(painter, control, state.Shelf!, bounds, originX, originY);
                    break;
                case UIControlType.Grid:
                    DrawGrid(target, control, state, bounds);
                    break;
            }
        }

        if (sidebar is not null && !states[sidebar.Name].Hidden)
            DrawSidebar(painter, sidebar, states[sidebar.Name], Offset(Place(sidebar.Frame, false), originX, originY));
    }

    // ---- Input ----

    private void Press(int x, int y, TimeSpan time, List<UIEvent> events, int originX = 0, int originY = 0)
    {
        var hit = Hit(x, y);

        // A press on a bar is the bar's, and leaves the keyboard where it was.
        if (hit is not null && PressScrollBar(hit, x, y, time))
            return;

        if (hit is null || hit.Type is not (UIControlType.TextField or UIControlType.TextArea))
            focused = null;
        if (hit is null)
            return;

        var state = states[hit.Name];
        switch (hit.Type)
        {
            case UIControlType.Button:
                armed = hit.Name;
                break;

            case UIControlType.TextField or UIControlType.TextArea when !hit.IsReadOnly:
                focused = hit.Name;
                state.Caret = CaretAt(hit, state, x, y);
                selecting = hit.Name;
                break;

            case UIControlType.TextArea:
                focused = null;
                break;

            case UIControlType.Grid:
                PressGrid(hit, state.Grid!, x, y, time, events);
                break;

            case UIControlType.List when hit.IsSelectable:
                var list = state.List!;
                var index = RowAt(hit, list, y - Place(hit.Frame, hit.InSidebar, false).Y);
                if (index < 0 || index >= list.Items.Count)
                {
                    lastListClick = null;
                    break;
                }

                if (index != list.SelectedIndex)
                {
                    list.SelectedIndex = index;
                    events.Add(new UIEvent(UIEventKind.Selected, hit.Name, Index: index));
                }

                // A second press on the same item soon after opens it, as Enter submits a text field. A third
                // starts over, so a triple-click doesn't open it twice.
                if (lastListClick is { } previous && previous.List == hit.Name && previous.Index == index
                    && time - previous.Time <= DoubleClickInterval)
                {
                    events.Add(new UIEvent(UIEventKind.Submitted, hit.Name, list.Items[index], Index: index));
                    lastListClick = null;
                }
                else
                {
                    lastListClick = (hit.Name, index, time);
                }

                break;

            // Shelves are placed on the screen as they're drawn, so the press is found in the screen's terms.
            case UIControlType.Shelf when state.Shelf!.IndexAt(x + originX, y + originY) is >= 0 and var icon:
                shelfPress = (hit.Name, icon);
                break;
        }
    }

    private void Release(UIControl button, List<UIEvent> events)
    {
        events.Add(new UIEvent(UIEventKind.Pressed, button.Name));
    }

    private void ScrollOver(UIControl control, int scroll)
    {
        if (control.Type is UIControlType.List or UIControlType.TextArea or UIControlType.Grid)
        {
            ScrollBy(control, scroll);
            ShowScrollBar(control);
        }
    }

    /// <summary>Where a list or text area is scrolled to, and the furthest it can go, in rows.</summary>
    private (int First, int Last) ScrollPosition(UIControl control)
    {
        var state = states[control.Name];
        if (state.List is { } list)
            return (list.FirstVisible, LastFirstRow(control, list));
        if (state.Grid is { } grid)
            return (grid.FirstRow, Math.Max(0, GridShape(control).Rows - GridVisibleRows(control)));

        var lines = state.Lines?.Count ?? 1;
        return (state.FirstLine, Math.Max(0, lines - VisibleLines(control)));
    }

    private void ScrollTo(UIControl control, int first)
    {
        var state = states[control.Name];
        var (_, last) = ScrollPosition(control);
        if (state.List is { } list)
            list.FirstVisible = Math.Clamp(first, 0, last);
        else if (state.Grid is { } grid)
            grid.FirstRow = Math.Clamp(first, 0, last);
        else
            state.FirstLine = Math.Clamp(first, 0, last);
    }

    private void ScrollBy(UIControl control, int rows) => ScrollTo(control, ScrollPosition(control).First + rows);

    private void Type(string name, KeyPress key, List<UIEvent> events)
    {
        if (script.Control(name) is not { } field)
            return;

        var state = states[name];
        if (field.Type == UIControlType.TextArea)
        {
            TypeArea(field, state, key, events);
            return;
        }

        var maximum = field.MaximumLength ?? 256;
        var cursor = state.Cursor;
        cursor.Clamp(state.Text.Length);
        switch (key.Key)
        {
            case DesktopKey.Character when key.Control && !key.Alt && key.Character is 'a' or 'A':
                cursor.SelectAll(state.Text.Length);
                break;
            case DesktopKey.Character when key.Character >= ' ' && !key.Control && !key.Alt:
                state.Text = cursor.Replace(state.Text, key.Character.ToString(), maximum, TextChangeKind.Typing);
                break;
            case DesktopKey.Backspace:
                state.Text = cursor.DeleteBackward(state.Text);
                break;
            case DesktopKey.Delete:
                state.Text = cursor.DeleteForward(state.Text);
                break;
            case DesktopKey.Left:
                cursor.Left(key.Shift);
                break;
            case DesktopKey.Right:
                cursor.Right(state.Text.Length, key.Shift);
                break;
            case DesktopKey.Home:
                cursor.MoveTo(0, key.Shift);
                break;
            case DesktopKey.End:
                cursor.MoveTo(state.Text.Length, key.Shift);
                break;
            case DesktopKey.Enter:
                events.Add(new UIEvent(UIEventKind.Submitted, name, Text: state.Text));
                break;
        }
    }

    // ---- Text areas ----

    private int VisibleLines(UIControl control) =>
        Math.Max(1, (int)((Place(control.Frame, control.InSidebar, false).Height - (2 * Inset)) / LineHeight(TextSize(control))));

    /// <summary>
    /// The text's wrapped lines, as (start, length) into it, worked out again only when the text or width changed.
    /// Lines break at line breaks, then at the last space that fits; a word too long for a line is cut where it
    /// stops fitting. Words are measured one at a time, so wrapping costs the same however long the text gets.
    /// </summary>
    private List<(int Start, int Length)> Layout(IPainter painter, UIControl control, State state, int room)
    {
        if (state.Lines is { } cached && ReferenceEquals(state.LinesText, state.Text) && state.LinesWidth == room)
            return cached;

        var text = state.Text;
        var size = TextSize(control);
        var space = painter.MeasureText(" ", size);
        List<(int Start, int Length)> lines = [];
        var paragraph = 0;
        while (true)
        {
            var end = text.IndexOf('\n', paragraph);
            if (end < 0)
                end = text.Length;

            var lineStart = paragraph;
            var width = 0;
            var position = paragraph;
            while (position < end)
            {
                var wordEnd = text.IndexOf(' ', position, end - position);
                if (wordEnd < 0)
                    wordEnd = end;

                var wordWidth = painter.MeasureText(text[position..wordEnd], size);
                if (position > lineStart && width + wordWidth > room)
                {
                    lines.Add((lineStart, position - lineStart));
                    lineStart = position;
                    width = 0;
                }

                // A word wider than the whole line is cut where it stops fitting.
                while (position == lineStart && wordWidth > room && wordEnd - position > 1)
                {
                    var cut = position + 1;
                    while (cut < wordEnd && painter.MeasureText(text[position..(cut + 1)], size) <= room)
                        cut++;
                    lines.Add((lineStart, cut - lineStart));
                    position = lineStart = cut;
                    wordWidth = painter.MeasureText(text[position..wordEnd], size);
                }

                width += wordWidth + space;
                position = Math.Min(end, wordEnd + 1);
            }

            lines.Add((lineStart, end - lineStart));
            if (end >= text.Length)
                break;
            paragraph = end + 1;
        }

        state.Lines = lines;
        state.LinesText = text;
        state.LinesWidth = room;
        return lines;
    }

    /// <summary>The line holding a caret position: the last that starts at or before it.</summary>
    private static int LineOf(List<(int Start, int Length)> lines, int caret)
    {
        var line = 0;
        while (line + 1 < lines.Count && lines[line + 1].Start <= caret)
            line++;
        return line;
    }

    private void DrawArea(IPainter painter, UIControl control, State state, UIRect bounds)
    {
        state.Measurer = painter;
        var (x, y, width, height) = Ints(bounds);
        var style = Style(control);
        var isFocused = focused == control.Name;
        painter.FillRectangle(x, y, width, height, style?.BackgroundColor ?? DesktopTheme.InputBackground);
        DesktopTheme.Outline(painter, x, y, width, height, isFocused ? DesktopTheme.Accent : style?.BorderColor ?? DesktopTheme.Border);

        var size = TextSize(control);
        var lineHeight = LineHeight(size);
        if (state.Text.Length == 0 && !isFocused && state.Placeholder is { } placeholder)
        {
            var fitted = Fit(painter, placeholder, size, width - (2 * Inset));
            painter.DrawText(fitted, x + Inset + LineShift(painter, control, fitted, size, width - (2 * Inset)), y + Inset,
                DesktopTheme.SecondaryText, size);
            return;
        }

        // An area that follows, as Terminal's does, stays at its newest line when text arrives while it's at the end.
        var visible = VisibleLines(control);
        var wasAtEnd = state.Lines is null || state.FirstLine >= Math.Max(0, state.Lines.Count - visible);
        var textChanged = !ReferenceEquals(state.LinesText, state.Text);
        // A classic bar takes room once the text doesn't fit, and the text wraps again in what's left. Wrapping shorter
        // only adds lines, so the choice holds until the text changes.
        var reserve = StyleOf(control) == ScrollBarStyle.Classic && HasOwnScrollBar(control);
        var lines = Layout(painter, control, state, TextRoom(control, state));
        if ((reserve && lines.Count > visible) != state.BarShown)
        {
            state.BarShown = !state.BarShown;
            lines = Layout(painter, control, state, TextRoom(control, state));
        }

        if (control.Follows && textChanged && wasAtEnd)
            state.FirstLine = Math.Max(0, lines.Count - visible);

        var caretLine = LineOf(lines, Math.Clamp(state.Caret, 0, state.Text.Length));
        if (state.RevealCaret)
        {
            if (caretLine < state.FirstLine)
                state.FirstLine = caretLine;
            else if (caretLine >= state.FirstLine + visible)
                state.FirstLine = caretLine - visible + 1;
            state.RevealCaret = false;
        }

        state.FirstLine = Math.Clamp(state.FirstLine, 0, Math.Max(0, lines.Count - visible));
        var color = state.Disabled ? DesktopTheme.DisabledText : TextColor(control);
        var (from, selected) = state.Cursor.Selection;
        var room = TextRoom(control, state);
        for (var row = 0; row < visible && state.FirstLine + row < lines.Count; row++)
        {
            var (start, length) = lines[state.FirstLine + row];
            var lineX = x + Inset + LineShift(painter, control, state.Text.Substring(start, length), size, room);

            // The part of the selection on this line, behind its text.
            var (first, last) = (Math.Max(from, start), Math.Min(from + selected, start + length));
            if (isFocused && last > first)
            {
                var left = painter.MeasureText(state.Text[start..first], size);
                painter.FillRectangle(lineX + left, y + Inset + (row * lineHeight),
                    painter.MeasureText(state.Text[first..last], size), lineHeight, DesktopTheme.Selection);
            }

            if (length > 0)
                painter.DrawText(state.Text.Substring(start, length), lineX, y + Inset + (row * lineHeight), color, size);
        }

        if (isFocused && caretLine >= state.FirstLine && caretLine < state.FirstLine + visible)
        {
            var (start, length) = lines[caretLine];
            var lineX = x + Inset + LineShift(painter, control, state.Text.Substring(start, length), size, room);
            var caretX = lineX + painter.MeasureText(state.Text[start..state.Caret], size) + 1;
            painter.FillRectangle(caretX, y + Inset + ((caretLine - state.FirstLine) * lineHeight), 1, lineHeight, TextColor(control));
        }

        DrawScrollBar(painter, control, state, bounds);
    }

    /// <summary>How wide a text area's lines may be: inside its insets, and short of a classic bar while one shows.</summary>
    private int TextRoom(UIControl area, State state) =>
        (int)Place(area.Frame, area.InSidebar, false).Width - (2 * Inset) - (state.BarShown ? ClassicScrollBarWidth : 0);

    private void TypeArea(UIControl area, State state, KeyPress key, List<UIEvent> events)
    {
        var maximum = area.MaximumLength ?? TextAreaLength;
        var text = state.Text;
        var cursor = state.Cursor;
        cursor.Clamp(text.Length);
        var lines = state.Lines is { } cached && ReferenceEquals(state.LinesText, text) ? cached : null;
        switch (key.Key)
        {
            case DesktopKey.Character when key.Control && !key.Alt && key.Character is 'a' or 'A':
                cursor.SelectAll(text.Length);
                break;
            case DesktopKey.Character when key.Character >= ' ' && !key.Control && !key.Alt:
                text = cursor.Replace(text, key.Character.ToString(), maximum, TextChangeKind.Typing);
                break;
            case DesktopKey.Enter:
                text = cursor.Replace(text, "\n", maximum, TextChangeKind.Typing);
                break;
            case DesktopKey.Backspace:
                text = cursor.DeleteBackward(text);
                break;
            case DesktopKey.Delete:
                text = cursor.DeleteForward(text);
                break;
            case DesktopKey.Left:
                cursor.Left(key.Shift);
                break;
            case DesktopKey.Right:
                cursor.Right(text.Length, key.Shift);
                break;
            case DesktopKey.Home or DesktopKey.End or DesktopKey.Up or DesktopKey.Down:
                cursor.MoveTo(MoveCaret(text, lines, cursor.Caret, key.Key), key.Shift);
                break;
            default:
                return;
        }

        state.RevealCaret = true;
        if (!ReferenceEquals(text, state.Text))
        {
            state.Text = text;
            events.Add(new UIEvent(UIEventKind.Changed, area.Name, text));
        }
    }

    /// <summary>
    /// Home, End, Up, and Down, by the lines as last drawn, or by line breaks before the area has been drawn. Up and
    /// Down keep the caret's place along the line, as far as the other line reaches.
    /// </summary>
    private static int MoveCaret(string text, List<(int Start, int Length)>? lines, int caret, DesktopKey key)
    {
        if (lines is null)
        {
            var start = caret == 0 ? 0 : text.LastIndexOf('\n', caret - 1) + 1;
            var end = text.IndexOf('\n', caret);
            lines = [(start, (end < 0 ? text.Length : end) - start)];
            if (key is DesktopKey.Up or DesktopKey.Down)
            {
                var column = caret - start;
                if (key == DesktopKey.Up && start > 0)
                {
                    var above = start == 1 ? 0 : text.LastIndexOf('\n', start - 2) + 1;
                    return Math.Min(above + column, start - 1);
                }

                if (key == DesktopKey.Down && end >= 0)
                {
                    var below = text.IndexOf('\n', end + 1);
                    return Math.Min(end + 1 + column, below < 0 ? text.Length : below);
                }

                return caret;
            }
        }

        var line = LineOf(lines, caret);
        var (lineStart, length) = lines[line];
        return key switch
        {
            DesktopKey.Home => lineStart,
            DesktopKey.End => lineStart + length,
            DesktopKey.Up when line > 0 => lines[line - 1].Start + Math.Min(caret - lineStart, lines[line - 1].Length),
            DesktopKey.Down when line + 1 < lines.Count => lines[line + 1].Start + Math.Min(caret - lineStart, lines[line + 1].Length),
            _ => caret,
        };
    }

    /// <summary>
    /// Where a point in a text field or area puts the caret: the character nearest it, on the line it's on. Before the
    /// control has been drawn, the end of its text.
    /// </summary>
    private int CaretAt(UIControl area, State state, int x, int y)
    {
        if (area.Type == UIControlType.TextField)
            return FieldCaretAt(area, state, x);
        if (state.Measurer is not { } painter || state.Lines is not { } lines || !ReferenceEquals(state.LinesText, state.Text))
            return state.Text.Length;

        var bounds = Place(area.Frame, area.InSidebar, false);
        var size = TextSize(area);
        var line = Math.Clamp(state.FirstLine + (int)((y - bounds.Y - Inset) / LineHeight(size)), 0, lines.Count - 1);
        var (start, length) = lines[line];
        var along = x - bounds.X - Inset
            - LineShift(painter, area, state.Text.Substring(start, length), size, TextRoom(area, state));
        for (var count = 0; count < length; count++)
        {
            var before = painter.MeasureText(state.Text.Substring(start, count), size);
            var after = painter.MeasureText(state.Text.Substring(start, count + 1), size);
            if (along < (before + after) / 2f)
                return start + count;
        }

        return start + length;
    }

    /// <summary>Where a point along a text field puts the caret, from the first character it last showed.</summary>
    private int FieldCaretAt(UIControl field, State state, int x)
    {
        if (state.Measurer is not { } painter)
            return state.Text.Length;

        var shown = field.IsSecure ? new string('*', state.Text.Length) : state.Text;
        var start = Math.Clamp(state.FirstShown, 0, shown.Length);
        var size = TextSize(field);
        var along = x - Place(field.Frame, field.InSidebar, false).X - Inset - state.ShownShift;
        for (var count = start; count < shown.Length; count++)
        {
            var before = painter.MeasureText(shown[start..count], size);
            var after = painter.MeasureText(shown[start..(count + 1)], size);
            if (along < (before + after) / 2f)
                return count;
        }

        return shown.Length;
    }

    /// <summary>The topmost visible, enabled control under a point, or <see langword="null"/>.</summary>
    private UIControl? Hit(float x, float y)
    {
        for (var index = script.Controls.Count - 1; index >= 0; index--)
        {
            var control = script.Controls[index];
            var state = states[control.Name];
            if (Showing(state) && !state.Disabled && control.Type != UIControlType.Text
                && control.Type != UIControlType.Region && Place(control.Frame, control.InSidebar, control.Image is not null).Contains(x, y))
                return control;
        }

        if (sidebar is not null && states[sidebar.Name] is { Hidden: false, Disabled: false }
            && Place(sidebar.Frame, false).Contains(x, y))
            return sidebar;

        return null;
    }

    /// <summary>Keeps logs at their newest items as items arrive, and every list's scroll in range.</summary>
    private void FollowLists()
    {
        // By index, since a foreach over the interface would allocate on every frame.
        var controls = script.Controls;
        for (var index = 0; index < controls.Count; index++)
        {
            var state = states[controls[index].Name];
            if (state.List is { } list)
                FollowList(controls[index], list);
            else if (state.Grid is { } grid)
                FollowGrid(controls[index], grid);
        }

        if (sidebar is not null)
            FollowList(sidebar, states[sidebar.Name].List!);
    }

    private void FollowList(UIControl control, UIListState list)
    {
        var visible = VisibleRows(control);
        var last = LastFirstRow(control, list);
        if (!control.IsSelectable && list.Items.Count != list.LastCount
            && list.FirstVisible >= Math.Max(0, list.LastCount - visible))
            list.FirstVisible = last;

        list.FirstVisible = Math.Clamp(list.FirstVisible, 0, last);
        if (list.SelectedIndex >= list.Items.Count)
            list.SelectedIndex = -1;
        list.LastCount = list.Items.Count;
    }

    // ---- Drawing ----

    private void DrawLabel(IPainter painter, UIControl control, State state, UIRect bounds)
    {
        var size = TextSize(control);
        var text = Fit(painter, state.Text, size, bounds.Width);
        var color = state.Disabled ? Style(control)?.DisabledTextColor ?? DesktopTheme.DisabledText : TextColor(control);
        painter.DrawText(text, Align(painter, control, text, size, bounds, 0), (int)(bounds.Y + ((bounds.Height - LineHeight(size)) / 2)), color, size);
    }

    private void DrawFormatted(IPainter painter, UIControl control, State state, UIRect bounds)
    {
        var (x, y, width, height) = Ints(bounds);
        painter.FillRectangle(x, y, width, height, Style(control)?.BackgroundColor ?? DesktopTheme.InputBackground);
        if (Style(control)?.BorderColor is { } border)
            DesktopTheme.Outline(painter, x, y, width, height, border);

        var size = TextSize(control);
        var lineY = y + Inset;
        foreach (var line in Wrap(painter, state.Text, size, width - (2 * Inset)))
        {
            if (lineY + LineHeight(size) > y + height)
                break;

            painter.DrawText(line, Align(painter, control, line, size, bounds, Inset), lineY, TextColor(control), size);
            lineY += LineHeight(size);
        }
    }

    private void DrawButton(IPainter painter, UIControl control, State state, UIRect bounds)
    {
        var (x, y, width, height) = Ints(bounds);
        var place = Place(control.Frame, control.InSidebar, control.Image is not null);
        if (control.Image is { } imageName)
        {
            DrawImageButton(painter, control, state, place, imageName, x, y, width, height);
            return;
        }

        var held = state.Selected || (armed == control.Name && place.Contains(pointer.X, pointer.Y));
        var hovered = !held && !state.Disabled && hover is { } at && place.Contains(at.X, at.Y);
        var style = Style(control);

        if (style?.BackgroundColor is { } background)
            painter.FillRectangle(x, y, width, height, held ? DesktopTheme.ButtonPressed : background);
        else if (held)
            painter.FillRectangle(x, y, width, height, DesktopTheme.ButtonPressed);
        else
            DesktopTheme.FillGradient(painter, x, y, width, height, DesktopTheme.TitleBarTop,
                hovered ? DesktopTheme.ButtonHover : DesktopTheme.TitleBarBottom);

        DesktopTheme.Outline(painter, x, y, width, height, style?.BorderColor ?? DesktopTheme.Border);

        var color = state.Disabled ? style?.DisabledTextColor ?? DesktopTheme.DisabledText
            : held ? style?.SelectedTextColor ?? TextColor(control)
            : hovered ? style?.HighlightedTextColor ?? TextColor(control)
            : TextColor(control);
        var size = TextSize(control);
        var text = Fit(painter, state.Text, size, width - (2 * Inset));
        painter.DrawText(text, x + ((width - painter.MeasureText(text, size)) / 2), y + ((height - LineHeight(size)) / 2), color, size);
    }

    /// <summary>
    /// A button showing its image, scaled to it: shaded while held, faded when disabled, and ringed in the accent when
    /// it's checked, as a picker marks its choice; a faint ring shows under the pointer. Without its image, it's
    /// outlined in red, as Hudini shows a missing one.
    /// </summary>
    private void DrawImageButton(IPainter painter, UIControl control, State state, UIRect place, string imageName, int x, int y, int width, int height)
    {
        if (ScaledImage(control.Name, imageName, width, height) is not { } image)
        {
            DesktopTheme.Outline(painter, x, y, width, height, DesktopTheme.MissingImage);
            return;
        }

        painter.DrawImage(image, x, y);
        var held = armed == control.Name && place.Contains(pointer.X, pointer.Y);
        var hovered = !held && !state.Disabled && hover is { } at && place.Contains(at.X, at.Y);
        if (held)
            painter.FillRectangle(x, y, width, height, DesktopTheme.IconPressed);
        if (state.Disabled)
            painter.FillRectangle(x, y, width, height, DesktopTheme.InactiveFade);

        // A two-pixel ring just outside it: the accent when chosen, a faint one under the pointer.
        if (state.Selected || hovered)
        {
            var ring = state.Selected ? DesktopTheme.Accent : DesktopTheme.Border;
            DesktopTheme.Outline(painter, x - 2, y - 2, width + 4, height + 4, ring);
            DesktopTheme.Outline(painter, x - 3, y - 3, width + 6, height + 6, ring);
        }
    }

    private void DrawField(IPainter painter, UIControl control, State state, UIRect bounds)
    {
        var (x, y, width, height) = Ints(bounds);
        var style = Style(control);
        var isFocused = focused == control.Name;
        painter.FillRectangle(x, y, width, height, style?.BackgroundColor ?? DesktopTheme.InputBackground);
        DesktopTheme.Outline(painter, x, y, width, height, isFocused ? DesktopTheme.Accent : style?.BorderColor ?? DesktopTheme.Border);

        var size = TextSize(control);
        var textY = y + ((height - LineHeight(size)) / 2);
        var shown = control.IsSecure ? new string('*', state.Text.Length) : state.Text;
        var room = width - (2 * Inset);

        if (shown.Length == 0 && !isFocused && state.Placeholder is { } placeholder)
        {
            var fitted = Fit(painter, placeholder, size, room);
            painter.DrawText(fitted, x + Inset + LineShift(painter, control, fitted, size, room), textY, DesktopTheme.SecondaryText, size);
            return;
        }

        // Show the end that holds the caret when the text is wider than the field.
        state.Cursor.Clamp(shown.Length);
        var start = 0;
        while (start < state.Caret && painter.MeasureText(shown[start..state.Caret], size) > room)
            start++;
        var visible = shown[start..];
        while (visible.Length > 0 && painter.MeasureText(visible, size) > room)
            visible = visible[..^1];
        // Text that fits sits as the field is aligned; text that doesn't fills it, so there's nothing to shift.
        var textX = x + Inset + (visible.Length == shown.Length ? LineShift(painter, control, visible, size, room) : 0);
        (state.FirstShown, state.ShownShift, state.Measurer) = (start, textX - x - Inset, painter);

        // The selection, behind the text it covers.
        var (from, length) = state.Cursor.Selection;
        var (first, last) = (Math.Max(from, start), Math.Min(from + length, start + visible.Length));
        if (isFocused && last > first)
        {
            var left = painter.MeasureText(shown[start..first], size);
            painter.FillRectangle(textX + left, textY, painter.MeasureText(shown[first..last], size), LineHeight(size), DesktopTheme.Selection);
        }

        var color = state.Disabled ? DesktopTheme.DisabledText : TextColor(control);
        painter.DrawText(visible, textX, textY, color, size);

        if (isFocused)
        {
            // A pixel past the advance, since glyphs can reach the end of their advance and would touch the caret.
            var caretX = textX + painter.MeasureText(shown[start..state.Caret], size) + 1;
            painter.FillRectangle(caretX, textY, 1, LineHeight(size), TextColor(control));
        }
    }

    private void DrawList(IPainter painter, UIControl control, State state, UIRect bounds)
    {
        var (x, y, width, height) = Ints(bounds);
        var style = Style(control);
        painter.FillRectangle(x, y, width, height, style?.BackgroundColor ?? DesktopTheme.InputBackground);
        DesktopTheme.Outline(painter, x, y, width, height, style?.BorderColor ?? DesktopTheme.Border);

        var list = state.List!;
        var size = TextSize(control);
        var rowHeight = RowHeight(control);
        var rows = VisibleRows(control);
        var barRoom = ClassicBarRoom(control);
        for (var row = 0; row < rows && list.FirstVisible + row < list.Items.Count; row++)
        {
            var index = list.FirstVisible + row;
            var rowY = y + 1 + (row * rowHeight);
            if (index == list.SelectedIndex)
                painter.FillRectangle(x + 1, rowY, width - 2 - barRoom, rowHeight, DesktopTheme.Selection);

            var text = Fit(painter, list.Items[index], size, width - (2 * Inset) - barRoom);
            painter.DrawText(text, x + Inset, rowY + ((rowHeight - LineHeight(size)) / 2), TextColor(control), size);
        }

        DrawScrollBar(painter, control, state, bounds);
    }

    /// <summary>
    /// Draws the sidebar's rows, without a box, over the shade the frame drew, as Nautilus does: the selected row and
    /// the one under the pointer get a rounded shade, a row's icon shows as a symbol in the text's color before its
    /// text, and a faint line splits one group from the next.
    /// </summary>
    private void DrawSidebar(IPainter painter, UIControl control, State state, UIRect bounds)
    {
        var (x, y, width, height) = Ints(bounds);
        var list = state.List!;
        var size = DesktopTheme.TextSize;
        var rowHeight = RowHeight(control);
        var ink = state.Disabled ? DesktopTheme.DisabledText : DesktopTheme.Text;

        // Every row's text lines up, after an icon's room when any row shows one.
        var iconRoom = AnySidebarIcon(list) ? SidebarIconSize + SidebarIconGap : 0;
        var textX = x + (2 * SidebarSelectionInset) + iconRoom;
        var barRoom = ClassicBarRoom(control);
        var room = width - (4 * SidebarSelectionInset) - iconRoom - barRoom;

        // The row under the pointer, by the same arithmetic a click uses.
        var hovered = -1;
        var place = Place(control.Frame, control.InSidebar, false);
        if (!state.Disabled && hover is { } at && place.Contains(at.X, at.Y))
            hovered = RowAt(control, list, at.Y - place.Y);

        var top = 1;
        for (var index = list.FirstVisible; index < list.Items.Count; index++)
        {
            if (index > list.FirstVisible && list.GroupStarts.Contains(index))
            {
                if (top + SidebarSeparatorRoom > height)
                    break;
                painter.FillRectangle(x + (2 * SidebarSelectionInset), y + top + (SidebarSeparatorRoom / 2),
                    width - (4 * SidebarSelectionInset), 1, DesktopTheme.Separator);
                top += SidebarSeparatorRoom;
            }

            if (top + rowHeight > height)
                break;

            var rowY = y + top;
            if (index == list.SelectedIndex || index == hovered)
            {
                var shade = index == list.SelectedIndex ? DesktopTheme.SidebarSelection : DesktopTheme.SidebarHover;
                DesktopTheme.FillRoundedGradient(painter, x + SidebarSelectionInset, rowY, width - (2 * SidebarSelectionInset) - barRoom,
                    rowHeight, SidebarSelectionRadius, shade, shade);
            }

            if (SidebarSymbol(list, index, ink) is { } symbol)
                painter.DrawImage(symbol, x + (2 * SidebarSelectionInset), rowY + ((rowHeight - symbol.Height) / 2));

            var text = Fit(painter, list.Items[index], size, room);
            painter.DrawText(text, textX, rowY + ((rowHeight - LineHeight(size)) / 2), ink, size);
            top += rowHeight;
        }

        DrawScrollBar(painter, control, state, bounds);
    }

    /// <summary>Whether any of a list's rows shows an icon: one named, and supplied.</summary>
    private bool AnySidebarIcon(UIListState list)
    {
        for (var index = 0; index < list.Icons.Count; index++)
        {
            if (list.Icons[index] is { } name && images.ContainsKey(name))
                return true;
        }

        return false;
    }

    /// <summary>
    /// A sidebar row's icon as a symbol in a color, made once for each image and color and kept, or
    /// <see langword="null"/> when the row has none or its image wasn't supplied.
    /// </summary>
    private DesktopImage? SidebarSymbol(UIListState list, int index, UIColor color)
    {
        if (index >= list.Icons.Count || list.Icons[index] is not { } name || !images.TryGetValue(name, out var source))
            return null;

        if (sidebarSymbols.TryGetValue((name, color), out var kept) && ReferenceEquals(kept.Source, source))
            return kept.Symbol;

        var symbol = DesktopIcon.Symbol(source, SidebarIconSize).Silhouette(color.Red, color.Green, color.Blue);
        sidebarSymbols[(name, color)] = (source, symbol);
        return symbol;
    }

    private void DrawProgress(IPainter painter, UIControl control, State state, UIRect bounds)
    {
        var (x, y, width, height) = Ints(bounds);
        var style = Style(control);
        painter.FillRectangle(x, y, width, height, style?.BackgroundColor ?? DesktopTheme.InputBackground);
        var (minimum, maximum) = Range(control);
        var filled = (int)((width - 2) * ((state.Value - minimum) / (maximum - minimum)));
        painter.FillRectangle(x + 1, y + 1, filled, height - 2, style?.FillColor ?? DesktopTheme.Accent);
        DesktopTheme.Outline(painter, x, y, width, height, style?.BorderColor ?? DesktopTheme.Border);
    }

    // ---- Layout and helpers ----

    /// <summary>Places a frame within the window, as Hudini's <c>placement</c> does.</summary>
    private UIRect Place(UIFrame frame, bool hasImage) => Place(frame, inSidebar: false, hasImage);

    /// <summary>
    /// Where a frame is, relative to the view's corner. One in the sidebar is measured from the sidebar's corner,
    /// level with the content's top, and anchored to its edges, then moved to where the sidebar is: left of the
    /// corner, or past the content's right edge.
    /// </summary>
    private UIRect Place(UIFrame frame, bool inSidebar, bool hasImage)
    {
        var width = frame.Width ?? (hasImage ? FallbackImageSize : 0);
        var height = frame.Height ?? (hasImage ? FallbackImageSize : 0);
        var across = inSidebar ? SidebarWidth : WindowSize.Width;
        var (x, placedWidth) = Placement(frame.X, width, frame.EdgeOffsets?.Left, frame.EdgeOffsets?.Right, across);
        var (y, placedHeight) = Placement(frame.Y, height, frame.EdgeOffsets?.Top, frame.EdgeOffsets?.Bottom, WindowSize.Height);
        if (inSidebar)
            x += IsSidebarOnRight ? WindowSize.Width : -SidebarWidth;
        return new UIRect(x, y, placedWidth, placedHeight);
    }

    private static (float Position, float Size) Placement(float position, float size, float? leading, float? trailing, float available) =>
        (leading, trailing) switch
        {
            ({ } start, { } end) => (start, Math.Max(0, available - start - end)),
            (null, { } end) => (available - end - size, size),
            ({ } start, null) => (start, size),
            _ => (position, size),
        };

    private static UIRect Offset(UIRect bounds, int x, int y) => bounds with { X = bounds.X + x, Y = bounds.Y + y };

    private static (int, int, int, int) Ints(UIRect bounds) =>
        ((int)bounds.X, (int)bounds.Y, (int)bounds.Width, (int)bounds.Height);

    private State? Find(string name) => states.GetValueOrDefault(name);

    /// <summary>
    /// A control's style with its roles and <c>dark</c> twins resolved for the palette in use, so every color and size
    /// is a plain one to draw with. Resolving allocates, so it happens once a style per palette.
    /// </summary>
    private ResolvedStyle? Style(UIControl control)
    {
        if (control.Style is not { } name || script.Styles.GetValueOrDefault(name) is not { } style)
            return null;

        var palette = DesktopTheme.Palette;
        if (!ReferenceEquals(palette, resolvedFor))
        {
            resolvedStyles.Clear();
            resolvedFor = palette;
        }

        if (!resolvedStyles.TryGetValue(name, out var resolved))
        {
            resolved = ResolvedStyle.For(style, palette.IsDark);
            resolvedStyles[name] = resolved;
        }

        return resolved;
    }

    /// <summary>A style's colors and size as plain values for one palette.</summary>
    private sealed record ResolvedStyle(
        UIColor? TextColor,
        UIColor? SelectedTextColor,
        UIColor? HighlightedTextColor,
        UIColor? DisabledTextColor,
        UIColor? BackgroundColor,
        UIColor? BorderColor,
        UIColor? FillColor,
        int? FontSize,
        UIFont? Font)
    {
        public static ResolvedStyle For(UIStyle style, bool dark)
        {
            // On a dark palette, a dark twin wins where it's set; otherwise the color as written, roles resolved.
            UIColor? Pick(UIStyleColor? light, UIStyleColor? darkTwin) =>
                (dark && darkTwin is { } twin ? twin : light) is { } color
                    ? color.Role is { } role ? DesktopTheme.Resolve(role) : color.Color
                    : null;

            var (colors, twins) = (style.Colors, style.Dark);
            return new ResolvedStyle(
                Pick(colors.Text, twins.Text),
                Pick(colors.SelectedText, twins.SelectedText),
                Pick(colors.HighlightedText, twins.HighlightedText),
                Pick(colors.DisabledText, twins.DisabledText),
                Pick(colors.Background, twins.Background),
                Pick(colors.Border, twins.Border),
                Pick(colors.Fill, twins.Fill),
                style.FontSize is { } size ? size.Role is { } role ? DesktopTheme.Resolve(role) : size.Size : null,
                style.Font);
        }
    }

    private UIColor TextColor(UIControl control) => Style(control)?.TextColor ?? DesktopTheme.Text;

    private int TextSize(UIControl control) => Style(control)?.FontSize ?? DesktopTheme.TextSize;

    private static int LineHeight(int size) => (int)MathF.Ceiling(size * 1.35f);

    private int RowHeight(UIControl control) =>
        LineHeight(TextSize(control)) + (ReferenceEquals(control, sidebar) ? SidebarRowExtra : 2);

    private int VisibleRows(UIControl control) =>
        Math.Max(1, (int)((Place(control.Frame, control.InSidebar, false).Height - 2) / RowHeight(control)));

    /// <summary>
    /// The row at a height from a list's top, counting the room a separator takes before a group, or -1 for none.
    /// Only a sidebar has separators, so for any other list this is plain division.
    /// </summary>
    private int RowAt(UIControl control, UIListState list, float y)
    {
        var rowHeight = RowHeight(control);
        var top = 1;
        for (var index = list.FirstVisible; index < list.Items.Count; index++)
        {
            if (index > list.FirstVisible && list.GroupStarts.Contains(index))
                top += SidebarSeparatorRoom;
            if (y < top)
                return -1;
            if (y < top + rowHeight)
                return index;
            top += rowHeight;
        }

        return y >= 1 ? list.FirstVisible + (int)((y - 1) / rowHeight) : -1;
    }

    /// <summary>The furthest a list can scroll: the first row from which every later row, and its separators, fits.</summary>
    private int LastFirstRow(UIControl control, UIListState list)
    {
        var rowHeight = RowHeight(control);
        var room = Place(control.Frame, control.InSidebar, false).Height - 2;
        var used = 0f;
        for (var first = list.Items.Count - 1; first >= 0; first--)
        {
            used += rowHeight + (list.GroupStarts.Contains(first + 1) && first + 1 < list.Items.Count ? SidebarSeparatorRoom : 0);
            if (used > room)
                return Math.Min(first + 1, list.Items.Count - 1);
        }

        return 0;
    }

    private static (float Minimum, float Maximum) Range(UIControl control) => control.ValueRange ?? (0, 1);

    private static int Align(IPainter painter, UIControl control, string text, int size, UIRect bounds, int inset) =>
        control.Alignment switch
        {
            UITextAlignment.Center => (int)(bounds.X + ((bounds.Width - painter.MeasureText(text, size)) / 2)),
            UITextAlignment.Right => (int)(bounds.X + bounds.Width - inset - painter.MeasureText(text, size)),
            _ => (int)bounds.X + inset,
        };

    /// <summary>
    /// How far a text field's or area's line starts past its inset for its <c>alignment</c>: nothing on the left, half
    /// the spare room centred, and all of it on the right. Drawing, the selection, the caret, and clicks share it.
    /// </summary>
    private static int LineShift(IPainter painter, UIControl control, string line, int size, int room) =>
        control.Alignment switch
        {
            UITextAlignment.Center => Math.Max(0, (room - painter.MeasureText(line, size)) / 2),
            UITextAlignment.Right => Math.Max(0, room - painter.MeasureText(line, size)),
            _ => 0,
        };

    /// <summary>The text, cut short with an ellipsis if it's wider than the room.</summary>
    internal static string Fit(IPainter painter, string text, int size, float room)
    {
        if (painter.MeasureText(text, size) <= room)
            return text;

        var cut = text;
        while (cut.Length > 0 && painter.MeasureText(cut + Ellipsis, size) > room)
            cut = cut[..^1];
        return cut.Length == 0 ? "" : cut + Ellipsis;
    }

    /// <summary>Breaks text into lines that fit, at spaces, and at line breaks it already has.</summary>
    internal static List<string> Wrap(IPainter painter, string text, int size, int room)
    {
        List<string> lines = [];
        foreach (var paragraph in text.Split('\n'))
        {
            var line = "";
            foreach (var word in paragraph.Split(' '))
            {
                var candidate = line.Length == 0 ? word : $"{line} {word}";
                if (line.Length > 0 && painter.MeasureText(candidate, size) > room)
                {
                    lines.Add(line);
                    line = word;
                }
                else
                {
                    line = candidate;
                }
            }

            lines.Add(line);
        }

        return lines;
    }

    private string? Resolve(UIText? text, Func<UIStringResource, string?>? resolveString)
    {
        if (text is null)
            return null;
        if (!text.IsReference)
            return text.Value;

        var resource = script.Strings[text.Value];
        if (resolveString?.Invoke(resource) is { } resolved)
            return resolved;

        missingStrings.Add(text.Value);
        return $"{resource.Table}:{resource.Index}";
    }

    private sealed class State
    {
        public string Text { get; set; } = "";
        public string? Placeholder { get; init; }
        public string? Tooltip { get; init; }
        public float Value { get; set; }
        public bool Hidden { get; set; }
        public bool Disabled { get; set; }
        public bool Selected { get; set; }
        /// <summary>The caret and selection in a text field or area.</summary>
        public TextCursor Cursor { get; } = new();

        /// <summary>Where typing goes; setting it drops any selection.</summary>
        public int Caret { get => Cursor.Caret; set => Cursor.Caret = value; }

        /// <summary>The first character a text field showed when last drawn, for placing the caret at a click.</summary>
        public int FirstShown { get; set; }

        /// <summary>How far past its inset a text field's text was drawn for its alignment, for placing the caret at a click.</summary>
        public int ShownShift { get; set; }
        public UIListState? List { get; init; }

        // A shelf control's icons, which its owner fills.
        public Shelf? Shelf { get; init; }

        // A grid's items, which the document lists and its owner may replace.
        public UIGridState? Grid { get; init; }

        // The pane a control is in, which shows it only while that pane's row is selected.
        public string? Pane { get; init; }

        // A text area's wrapped lines as last drawn, as (start, length) into Text, and what they were made from.
        public List<(int Start, int Length)>? Lines { get; set; }
        public string? LinesText { get; set; }
        public int LinesWidth { get; set; }
        public int FirstLine { get; set; }
        public bool RevealCaret { get; set; }

        // Whether a text area's lines were last wrapped short of a classic bar, and when an automatic bar hides.
        public bool BarShown { get; set; }
        public TimeSpan BarUntil { get; set; }

        // The first row or line a bar last showed, so a scroll by any means is noticed when it's drawn.
        public int BarFirst { get; set; }

        // The painter a text area was last drawn with, which measures text for clicks between draws.
        public IPainter? Measurer { get; set; }
    }

    /// <summary>
    /// Draws a shelf from its frame's left, as wide as its icons, the icon under the pointer named above it and shaded
    /// while it's pressed. A panel holding it is made as wide, and centred.
    /// </summary>
    private void DrawShelf(IPainter painter, UIControl control, Shelf shelf, UIRect bounds, int originX, int originY)
    {
        shelf.Place((int)bounds.X, (int)bounds.Y);
        var hovered = hover is { } point ? shelf.IndexAt(point.X + originX, point.Y + originY) : -1;
        var pressed = shelfPress is { } press && press.Shelf == control.Name ? press.Index : -1;
        shelf.Draw(painter, hovered, pressed, painter.Width);
    }

    /// <summary>The painter to draw a control with: one in the monospace font when its style asks for it.</summary>
    private IPainter PainterFor(UIControl control, IPainter painter)
    {
        if (Style(control)?.Font != UIFont.Monospace)
            return painter;

        if (monospace is null || !ReferenceEquals(monospace.Inner, painter))
            monospace = new MonospacePainter(painter);
        return monospace;
    }

    /// <summary>Passes drawing on, with all text in the monospace font.</summary>
    private sealed class MonospacePainter(IPainter inner) : IPainter
    {
        public IPainter Inner => inner;

        public int Width => inner.Width;

        public int Height => inner.Height;

        public void DrawPixels(int[] pixels, int x, int y, int width, int height) => inner.DrawPixels(pixels, x, y, width, height);

        public void DrawImage(DesktopImage image, int x, int y) => inner.DrawImage(image, x, y);

        public void ReadPixels(int[] into, int x, int y, int width, int height) => inner.ReadPixels(into, x, y, width, height);

        public void FillRectangle(int x, int y, int width, int height, UIColor color) => inner.FillRectangle(x, y, width, height, color);

        public void DrawText(string text, int x, int y, UIColor color, int size, DesktopFont font = DesktopFont.Interface) =>
            inner.DrawText(text, x, y, color, size, DesktopFont.Monospace);

        public int MeasureText(string text, int size, DesktopFont font = DesktopFont.Interface) =>
            inner.MeasureText(text, size, DesktopFont.Monospace);
    }
}
