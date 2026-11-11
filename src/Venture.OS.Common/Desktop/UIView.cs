using Venture.OS.Interface;

namespace Venture.OS.Desktop;

/// <summary>What a control reports for its owner to act on, as Hudini's <c>UIScriptView.Event</c> does.</summary>
public enum UIEventKind
{
    /// <summary>An enabled button without a scroll binding was pressed.</summary>
    Pressed,

    /// <summary>A text field submitted its text, with Enter.</summary>
    Submitted,

    /// <summary>Dragging moved a slider that doesn't scroll a list to a new value.</summary>
    Changed,

    /// <summary>A click on a selectable list changed its selected item.</summary>
    Selected,
}

/// <summary>An event, with the control's name and, depending on the kind, its text, value, or index.</summary>
public readonly record struct UIEvent(UIEventKind Kind, string Control, string? Text = null, float Value = 0, int Index = -1);

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

    /// <summary>The selected item's index, or -1. Setting it reports no event.</summary>
    public int SelectedIndex { get; set; } = -1;

    /// <summary>The first item showing, for scrolling.</summary>
    public int FirstVisible { get; set; }

    internal int LastCount { get; set; }
}

/// <summary>
/// Builds, updates, and draws the controls a UIScript document describes, as Hudini's <c>UIScriptView</c> does.
/// </summary>
/// <remarks>
/// <para>The view owns the controls and their interaction state: text, values, focus, lists, and what's hidden or
/// disabled. Its owner fills lists, sets text and values by name, draws into regions, and handles the events
/// <see cref="Update"/> returns. Positions are relative to the view's corner, which the owner passes in.</para>
/// <para>Images aren't drawn yet: like Hudini when a host can't supply one, a control or visual with an image shows
/// a red outline, at 24 pixels when it leaves out its size, and the image is listed in
/// <see cref="MissingImages"/>.</para>
/// </remarks>
public sealed class UIView
{
    private const int FallbackImageSize = 24;
    private const int Inset = 4;
    private const string Ellipsis = "…";

    private readonly UIScript script;
    private readonly Dictionary<string, State> states = [];
    private readonly Action<string, UIRect>? drawRegion;
    private readonly HashSet<string> missingStrings = [];
    private string? focused;
    private string? armed;
    private string? dragging;
    private (int X, int Y) pointer;
    private (string List, int Index, TimeSpan Time)? lastListClick;

    // Reused every frame, since the desktop updates views without pause; see Update.
    private readonly List<UIEvent> frameEvents = [];

    // The field that had focus before its window left the front; see RestoreFocus.
    private string? setAside;

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

        foreach (var control in script.Controls)
        {
            State state = new()
            {
                Text = Resolve(control.Text, resolveString) ?? "",
                Placeholder = Resolve(control.Placeholder, resolveString),
                Value = control.ValueRange?.Minimum ?? 0,
                List = control.Type == UIControlType.List ? new UIListState() : null,
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
            states[visual.Name] = new State();

        // The title bar's parts share the namespace too: toolbar items are disabled and selected by name, and the
        // title field's text is read and set like a text field's.
        foreach (var item in script.ToolbarItems)
            states[item.Name] = new State();
        if (script.TitleField is { } titleField)
            states[titleField.Name] = new State { Placeholder = titleField.Placeholder };

        // Menu items share the namespace, so they're disabled and checked by name like controls.
        foreach (var menu in script.Menus)
        {
            foreach (var entry in menu.Entries)
            {
                if (entry.Name is { } item)
                    states[item] = new State();
            }
        }

        List<string> images = [.. script.Images.Keys];
        images.Sort(StringComparer.Ordinal);
        MissingImages = images;
    }

    /// <summary>The document's window size, which edge offsets measure from.</summary>
    public UISize WindowSize => script.Window.Size;

    /// <summary>The document this view shows.</summary>
    public UIScript Script => script;

    /// <summary>Image resources that couldn't be drawn, sorted. Every image, until the desktop draws them.</summary>
    public IReadOnlyList<string> MissingImages { get; }

    /// <summary>String resources there was no text for, which show their table and index instead.</summary>
    public IReadOnlyCollection<string> MissingStrings => missingStrings;

    /// <summary>The text field that has keyboard focus, or <see langword="null"/>.</summary>
    public string? FocusedControl => focused;

    /// <summary>A control's bounds relative to the view's corner, or <see langword="null"/> when nothing has this name.</summary>
    public UIRect? Bounds(string name)
    {
        if (script.Control(name) is { } control)
            return Place(control.Frame, control.Image is not null);

        foreach (var visual in script.Visuals)
        {
            if (visual.Name == name)
                return Place(visual.Frame, hasImage: true);
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

        state.Text = text;
        state.Caret = Math.Min(state.Caret, text.Length);
    }

    /// <summary>A slider's or progress bar's value, or <see langword="null"/>.</summary>
    public float? Value(string name) =>
        script.Control(name) is { Type: UIControlType.Slider or UIControlType.ProgressBar } ? Find(name)!.Value : null;

    /// <summary>Sets a slider's or progress bar's value, kept within its range. Reports no event.</summary>
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

    /// <summary>Whether a control or visual is hidden.</summary>
    public bool IsHidden(string name) => Find(name)?.Hidden ?? false;

    /// <summary>Gives a text field keyboard focus, with the caret at the end.</summary>
    public void Focus(string name)
    {
        if (script.Control(name) is { Type: UIControlType.TextField or UIControlType.TextArea, IsReadOnly: false }
            && Find(name) is { Hidden: false, Disabled: false } state)
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

        FollowLists();

        if (input.PointerPressed)
            Press(x, y, input.Time, events);

        if (dragging is not null && input.PointerDown)
            Drag(dragging, x, y, events);

        if (input.PointerReleased)
        {
            if (armed is not null && Hit(x, y) is { } released && released.Name == armed)
                Release(released, events);

            armed = null;
            dragging = null;
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

    /// <summary>Draws the visuals, then the controls in document order, with the view's corner at the origin.</summary>
    public void Draw(IPainter painter, int originX, int originY)
    {
        ArgumentNullException.ThrowIfNull(painter);
        FollowLists();

        foreach (var visual in script.Visuals)
        {
            if (states[visual.Name].Hidden)
                continue;

            var bounds = Offset(Place(visual.Frame, hasImage: true), originX, originY);
            DesktopTheme.Outline(painter, (int)bounds.X, (int)bounds.Y, (int)bounds.Width, (int)bounds.Height, DesktopTheme.MissingImage);
        }

        foreach (var control in script.Controls)
        {
            var state = states[control.Name];
            if (state.Hidden)
                continue;

            var bounds = Offset(Place(control.Frame, control.Image is not null), originX, originY);
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
                case UIControlType.Slider:
                    DrawSlider(painter, control, state, bounds);
                    break;
                case UIControlType.ProgressBar:
                    DrawProgress(painter, control, state, bounds);
                    break;
                case UIControlType.Region:
                    drawRegion?.Invoke(control.Name, bounds);
                    break;
            }
        }
    }

    // ---- Input ----

    private void Press(int x, int y, TimeSpan time, List<UIEvent> events)
    {
        var hit = Hit(x, y);
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

            case UIControlType.TextField when !hit.IsReadOnly:
                focused = hit.Name;
                state.Caret = state.Text.Length;
                break;

            case UIControlType.TextArea when !hit.IsReadOnly:
                focused = hit.Name;
                state.Caret = CaretAt(hit, state, x, y);
                break;

            case UIControlType.TextArea:
                focused = null;
                break;

            case UIControlType.List when hit.IsSelectable:
                var list = state.List!;
                var index = list.FirstVisible + (int)((y - Place(hit.Frame, false).Y - 1) / RowHeight(hit));
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

            case UIControlType.Slider:
                dragging = hit.Name;
                break;
        }
    }

    private void Release(UIControl button, List<UIEvent> events)
    {
        if (button.Scrolls is { } binding)
        {
            if (script.Control(binding.List) is { } target)
                ScrollBy(target, binding.Direction == UIScrollDirection.Up ? -1 : 1);
            return;
        }

        events.Add(new UIEvent(UIEventKind.Pressed, button.Name));
    }

    private void Drag(string name, int x, int y, List<UIEvent> events)
    {
        if (script.Control(name) is not { } slider)
            return;

        var bounds = Place(slider.Frame, false);
        var fraction = Vertical(slider, bounds)
            ? (y - bounds.Y) / Math.Max(1, bounds.Height)
            : (x - bounds.X) / Math.Max(1, bounds.Width);
        fraction = Math.Clamp(fraction, 0, 1);

        if (slider.Scrolls is { } binding && script.Control(binding.List) is { } target)
        {
            var (_, last) = ScrollPosition(target);
            ScrollTo(target, (int)MathF.Round(fraction * last));
            return;
        }

        var (minimum, maximum) = Range(slider);
        var value = minimum + (fraction * (maximum - minimum));
        var state = states[name];
        if (value != state.Value)
        {
            state.Value = value;
            events.Add(new UIEvent(UIEventKind.Changed, name, Value: value));
        }
    }

    private void ScrollOver(UIControl control, int scroll)
    {
        var target = control.Type is UIControlType.List or UIControlType.TextArea ? control
            : control.Scrolls is { } binding ? script.Control(binding.List)
            : null;
        if (target is not null)
            ScrollBy(target, scroll);
    }

    /// <summary>Where a list or text area is scrolled to, and the furthest it can go, in rows.</summary>
    private (int First, int Last) ScrollPosition(UIControl control)
    {
        var state = states[control.Name];
        if (state.List is { } list)
            return (list.FirstVisible, Math.Max(0, list.Items.Count - VisibleRows(control)));

        var lines = state.Lines?.Count ?? 1;
        return (state.FirstLine, Math.Max(0, lines - VisibleLines(control)));
    }

    private void ScrollTo(UIControl control, int first)
    {
        var state = states[control.Name];
        var (_, last) = ScrollPosition(control);
        if (state.List is { } list)
            list.FirstVisible = Math.Clamp(first, 0, last);
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
        switch (key.Key)
        {
            case DesktopKey.Character when key.Character >= ' ' && !key.Control && !key.Alt:
                if (state.Text.Length < maximum)
                {
                    state.Text = state.Text.Insert(state.Caret, key.Character.ToString());
                    state.Caret++;
                }

                break;
            case DesktopKey.Backspace when state.Caret > 0:
                state.Text = state.Text.Remove(state.Caret - 1, 1);
                state.Caret--;
                break;
            case DesktopKey.Delete when state.Caret < state.Text.Length:
                state.Text = state.Text.Remove(state.Caret, 1);
                break;
            case DesktopKey.Left:
                state.Caret = Math.Max(0, state.Caret - 1);
                break;
            case DesktopKey.Right:
                state.Caret = Math.Min(state.Text.Length, state.Caret + 1);
                break;
            case DesktopKey.Home:
                state.Caret = 0;
                break;
            case DesktopKey.End:
                state.Caret = state.Text.Length;
                break;
            case DesktopKey.Enter:
                events.Add(new UIEvent(UIEventKind.Submitted, name, Text: state.Text));
                break;
        }
    }

    // ---- Text areas ----

    private int VisibleLines(UIControl control) =>
        Math.Max(1, (int)((Place(control.Frame, false).Height - (2 * Inset)) / LineHeight(TextSize(control))));

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
            painter.DrawText(Fit(painter, placeholder, size, width - (2 * Inset)), x + Inset, y + Inset, DesktopTheme.SecondaryText, size);
            return;
        }

        // An area that follows, as Terminal's does, stays at its newest line when text arrives while it's at the end.
        var visible = VisibleLines(control);
        var wasAtEnd = state.Lines is null || state.FirstLine >= Math.Max(0, state.Lines.Count - visible);
        var textChanged = !ReferenceEquals(state.LinesText, state.Text);
        var lines = Layout(painter, control, state, width - (2 * Inset));
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
        for (var row = 0; row < visible && state.FirstLine + row < lines.Count; row++)
        {
            var (start, length) = lines[state.FirstLine + row];
            if (length > 0)
                painter.DrawText(state.Text.Substring(start, length), x + Inset, y + Inset + (row * lineHeight), color, size);
        }

        if (isFocused && caretLine >= state.FirstLine && caretLine < state.FirstLine + visible)
        {
            var (start, _) = lines[caretLine];
            var caretX = x + Inset + painter.MeasureText(state.Text[start..state.Caret], size) + 1;
            painter.FillRectangle(caretX, y + Inset + ((caretLine - state.FirstLine) * lineHeight), 1, lineHeight, TextColor(control));
        }
    }

    private void TypeArea(UIControl area, State state, KeyPress key, List<UIEvent> events)
    {
        var maximum = area.MaximumLength ?? TextAreaLength;
        var text = state.Text;
        state.Caret = Math.Clamp(state.Caret, 0, text.Length);
        var lines = state.Lines is { } cached && ReferenceEquals(state.LinesText, text) ? cached : null;
        switch (key.Key)
        {
            case DesktopKey.Character when key.Character >= ' ' && !key.Control && !key.Alt && text.Length < maximum:
                text = text.Insert(state.Caret, key.Character.ToString());
                state.Caret++;
                break;
            case DesktopKey.Enter when text.Length < maximum:
                text = text.Insert(state.Caret, "\n");
                state.Caret++;
                break;
            case DesktopKey.Backspace when state.Caret > 0:
                text = text.Remove(state.Caret - 1, 1);
                state.Caret--;
                break;
            case DesktopKey.Delete when state.Caret < text.Length:
                text = text.Remove(state.Caret, 1);
                break;
            case DesktopKey.Left:
                state.Caret = Math.Max(0, state.Caret - 1);
                break;
            case DesktopKey.Right:
                state.Caret = Math.Min(text.Length, state.Caret + 1);
                break;
            case DesktopKey.Home or DesktopKey.End or DesktopKey.Up or DesktopKey.Down:
                state.Caret = MoveCaret(text, lines, state.Caret, key.Key);
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

    /// <summary>Where a click in a text area puts the caret: the character nearest the point, on the line it's on.</summary>
    private int CaretAt(UIControl area, State state, int x, int y)
    {
        if (state.Measurer is not { } painter || state.Lines is not { } lines || !ReferenceEquals(state.LinesText, state.Text))
            return state.Text.Length;

        var bounds = Place(area.Frame, false);
        var size = TextSize(area);
        var line = Math.Clamp(state.FirstLine + (int)((y - bounds.Y - Inset) / LineHeight(size)), 0, lines.Count - 1);
        var (start, length) = lines[line];
        var along = x - bounds.X - Inset;
        for (var count = 0; count < length; count++)
        {
            var before = painter.MeasureText(state.Text.Substring(start, count), size);
            var after = painter.MeasureText(state.Text.Substring(start, count + 1), size);
            if (along < (before + after) / 2f)
                return start + count;
        }

        return start + length;
    }

    /// <summary>The topmost visible, enabled control under a point, or <see langword="null"/>.</summary>
    private UIControl? Hit(float x, float y)
    {
        for (var index = script.Controls.Count - 1; index >= 0; index--)
        {
            var control = script.Controls[index];
            var state = states[control.Name];
            if (!state.Hidden && !state.Disabled && control.Type != UIControlType.Text
                && control.Type != UIControlType.Region && Place(control.Frame, control.Image is not null).Contains(x, y))
                return control;
        }

        return null;
    }

    /// <summary>Keeps logs at their newest items as items arrive, and every list's scroll in range.</summary>
    private void FollowLists()
    {
        // By index, since a foreach over the interface would allocate on every frame.
        var controls = script.Controls;
        for (var index = 0; index < controls.Count; index++)
        {
            var control = controls[index];
            if (states[control.Name].List is not { } list)
                continue;

            var visible = VisibleRows(control);
            var last = Math.Max(0, list.Items.Count - visible);
            if (!control.IsSelectable && list.Items.Count != list.LastCount
                && list.FirstVisible >= Math.Max(0, list.LastCount - visible))
                list.FirstVisible = last;

            list.FirstVisible = Math.Clamp(list.FirstVisible, 0, last);
            if (list.SelectedIndex >= list.Items.Count)
                list.SelectedIndex = -1;
            list.LastCount = list.Items.Count;
        }
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
        if (control.Image is not null)
        {
            DesktopTheme.Outline(painter, x, y, width, height, DesktopTheme.MissingImage);
            return;
        }

        var under = Place(control.Frame, control.Image is not null).Contains(pointer.X, pointer.Y);
        var held = state.Selected || (armed == control.Name && under);
        var hovered = !held && under;
        var style = Style(control);

        if (style?.BackgroundColor is { } background)
            painter.FillRectangle(x, y, width, height, held ? DesktopTheme.ButtonPressed : background);
        else if (held)
            painter.FillRectangle(x, y, width, height, DesktopTheme.ButtonPressed);
        else
            DesktopTheme.FillGradient(painter, x, y, width, height, DesktopTheme.TitleBarTop, DesktopTheme.TitleBarBottom);

        DesktopTheme.Outline(painter, x, y, width, height, style?.BorderColor ?? DesktopTheme.Border);

        var color = state.Disabled ? style?.DisabledTextColor ?? DesktopTheme.DisabledText
            : held ? style?.SelectedTextColor ?? TextColor(control)
            : hovered ? style?.HighlightedTextColor ?? TextColor(control)
            : TextColor(control);
        var size = TextSize(control);
        var text = Fit(painter, state.Text, size, width - (2 * Inset));
        painter.DrawText(text, x + ((width - painter.MeasureText(text, size)) / 2), y + ((height - LineHeight(size)) / 2), color, size);
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
            painter.DrawText(Fit(painter, placeholder, size, room), x + Inset, textY, DesktopTheme.SecondaryText, size);
            return;
        }

        // Show the end that holds the caret when the text is wider than the field.
        var start = 0;
        while (start < state.Caret && painter.MeasureText(shown[start..state.Caret], size) > room)
            start++;
        var visible = shown[start..];
        while (visible.Length > 0 && painter.MeasureText(visible, size) > room)
            visible = visible[..^1];

        var color = state.Disabled ? DesktopTheme.DisabledText : TextColor(control);
        painter.DrawText(visible, x + Inset, textY, color, size);

        if (isFocused)
        {
            // A pixel past the advance, since glyphs can reach the end of their advance and would touch the caret.
            var caretX = x + Inset + painter.MeasureText(shown[start..state.Caret], size) + 1;
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
        for (var row = 0; row < rows && list.FirstVisible + row < list.Items.Count; row++)
        {
            var index = list.FirstVisible + row;
            var rowY = y + 1 + (row * rowHeight);
            if (index == list.SelectedIndex)
                painter.FillRectangle(x + 1, rowY, width - 2, rowHeight, DesktopTheme.Selection);

            var text = Fit(painter, list.Items[index], size, width - (2 * Inset));
            painter.DrawText(text, x + Inset, rowY + ((rowHeight - LineHeight(size)) / 2), TextColor(control), size);
        }
    }

    private void DrawSlider(IPainter painter, UIControl control, State state, UIRect bounds)
    {
        var (x, y, width, height) = Ints(bounds);
        var style = Style(control);
        var vertical = Vertical(control, bounds);
        painter.FillRectangle(x, y, width, height, style?.BackgroundColor ?? DesktopTheme.Panel);
        DesktopTheme.Outline(painter, x, y, width, height, style?.BorderColor ?? DesktopTheme.Border);

        float fraction;
        if (control.Scrolls is { } binding && script.Control(binding.List) is { } target)
        {
            var (first, last) = ScrollPosition(target);
            fraction = last == 0 ? 0 : (float)first / last;
        }
        else
        {
            var (minimum, maximum) = Range(control);
            fraction = (state.Value - minimum) / (maximum - minimum);
        }

        const int Thumb = 10;
        var fill = style?.FillColor ?? DesktopTheme.Accent;
        if (vertical)
            painter.FillRectangle(x + 1, y + (int)(fraction * (height - Thumb)), width - 2, Thumb, fill);
        else
            painter.FillRectangle(x + (int)(fraction * (width - Thumb)), y + 1, Thumb, height - 2, fill);
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
    private UIRect Place(UIFrame frame, bool hasImage)
    {
        var width = frame.Width ?? (hasImage ? FallbackImageSize : 0);
        var height = frame.Height ?? (hasImage ? FallbackImageSize : 0);
        var (x, placedWidth) = Placement(frame.X, width, frame.EdgeOffsets?.Left, frame.EdgeOffsets?.Right, WindowSize.Width);
        var (y, placedHeight) = Placement(frame.Y, height, frame.EdgeOffsets?.Top, frame.EdgeOffsets?.Bottom, WindowSize.Height);
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

    private UIStyle? Style(UIControl control) =>
        control.Style is { } name ? script.Styles.GetValueOrDefault(name) : null;

    private UIColor TextColor(UIControl control) => Style(control)?.TextColor ?? DesktopTheme.Text;

    private int TextSize(UIControl control) => Style(control)?.FontSize ?? DesktopTheme.TextSize;

    private static int LineHeight(int size) => (int)MathF.Ceiling(size * 1.35f);

    private int RowHeight(UIControl control) => LineHeight(TextSize(control)) + 2;

    private int VisibleRows(UIControl control) =>
        Math.Max(1, (int)((Place(control.Frame, false).Height - 2) / RowHeight(control)));

    private static (float Minimum, float Maximum) Range(UIControl control) => control.ValueRange ?? (0, 1);

    private static bool Vertical(UIControl control, UIRect bounds) =>
        control.Scrolls is not null || control.Orientation switch
        {
            UIOrientation.Vertical => true,
            UIOrientation.Horizontal => false,
            _ => bounds.Height > bounds.Width,
        };

    private static int Align(IPainter painter, UIControl control, string text, int size, UIRect bounds, int inset) =>
        control.Alignment switch
        {
            UITextAlignment.Center => (int)(bounds.X + ((bounds.Width - painter.MeasureText(text, size)) / 2)),
            UITextAlignment.Right => (int)(bounds.X + bounds.Width - inset - painter.MeasureText(text, size)),
            _ => (int)bounds.X + inset,
        };

    /// <summary>The text, cut short with an ellipsis if it's wider than the room.</summary>
    private static string Fit(IPainter painter, string text, int size, float room)
    {
        if (painter.MeasureText(text, size) <= room)
            return text;

        var cut = text;
        while (cut.Length > 0 && painter.MeasureText(cut + Ellipsis, size) > room)
            cut = cut[..^1];
        return cut.Length == 0 ? "" : cut + Ellipsis;
    }

    /// <summary>Breaks text into lines that fit, at spaces, and at line breaks it already has.</summary>
    private static List<string> Wrap(IPainter painter, string text, int size, int room)
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
        public float Value { get; set; }
        public bool Hidden { get; set; }
        public bool Disabled { get; set; }
        public bool Selected { get; set; }
        public int Caret { get; set; }
        public UIListState? List { get; init; }

        // A text area's wrapped lines as last drawn, as (start, length) into Text, and what they were made from.
        public List<(int Start, int Length)>? Lines { get; set; }
        public string? LinesText { get; set; }
        public int LinesWidth { get; set; }
        public int FirstLine { get; set; }
        public bool RevealCaret { get; set; }

        // The painter a text area was last drawn with, which measures text for clicks between draws.
        public IPainter? Measurer { get; set; }
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

        public void FillRectangle(int x, int y, int width, int height, UIColor color) => inner.FillRectangle(x, y, width, height, color);

        public void DrawText(string text, int x, int y, UIColor color, int size, DesktopFont font = DesktopFont.Interface) =>
            inner.DrawText(text, x, y, color, size, DesktopFont.Monospace);

        public int MeasureText(string text, int size, DesktopFont font = DesktopFont.Interface) =>
            inner.MeasureText(text, size, DesktopFont.Monospace);
    }
}
