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
        if (script.Control(name) is { Type: UIControlType.TextField, IsReadOnly: false } && Find(name) is { Hidden: false, Disabled: false } state)
        {
            focused = name;
            state.Caret = state.Text.Length;
        }
    }

    /// <summary>Takes keyboard focus away, such as when another window comes forward.</summary>
    public void ClearFocus() => focused = null;

    /// <summary>
    /// Handles a frame's input with the view's corner at (<paramref name="originX"/>, <paramref name="originY"/>).
    /// </summary>
    /// <returns>The events the input caused, in order.</returns>
    public IReadOnlyList<UIEvent> Update(UIViewInput input, int originX, int originY)
    {
        List<UIEvent> events = [];
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

        return events;
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
            switch (control.Type)
            {
                case UIControlType.Text:
                    DrawLabel(painter, control, state, bounds);
                    break;
                case UIControlType.FormattedText:
                    DrawFormatted(painter, control, state, bounds);
                    break;
                case UIControlType.Button:
                    DrawButton(painter, control, state, bounds);
                    break;
                case UIControlType.TextField:
                    DrawField(painter, control, state, bounds);
                    break;
                case UIControlType.List:
                    DrawList(painter, control, state, bounds);
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
        if (hit is null || hit.Type != UIControlType.TextField)
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
            if (Find(binding.List)?.List is { } list && script.Control(binding.List) is { } target)
                ScrollList(target, list, binding.Direction == UIScrollDirection.Up ? -1 : 1);
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

        if (slider.Scrolls is { } binding && script.Control(binding.List) is { } target && Find(binding.List)?.List is { } list)
        {
            var last = Math.Max(0, list.Items.Count - VisibleRows(target));
            list.FirstVisible = (int)MathF.Round(fraction * last);
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
        var target = control.Type == UIControlType.List ? control
            : control.Scrolls is { } binding ? script.Control(binding.List)
            : null;
        if (target is not null && Find(target.Name)?.List is { } list)
            ScrollList(target, list, scroll);
    }

    private void ScrollList(UIControl control, UIListState list, int rows)
    {
        var last = Math.Max(0, list.Items.Count - VisibleRows(control));
        list.FirstVisible = Math.Clamp(list.FirstVisible + rows, 0, last);
    }

    private void Type(string name, KeyPress key, List<UIEvent> events)
    {
        if (script.Control(name) is not { } field)
            return;

        var state = states[name];
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
        foreach (var control in script.Controls)
        {
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
        if (control.Scrolls is { } binding && script.Control(binding.List) is { } target && Find(binding.List)?.List is { } list)
        {
            var last = Math.Max(0, list.Items.Count - VisibleRows(target));
            fraction = last == 0 ? 0 : (float)list.FirstVisible / last;
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
    }
}
