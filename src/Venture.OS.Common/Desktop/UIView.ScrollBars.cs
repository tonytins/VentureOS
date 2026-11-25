using Venture.OS.Interface;

namespace Venture.OS.Desktop;

/// <summary>
/// The scroll bars lists, text areas, and the sidebar's rows draw for themselves, down their right edge, once their
/// content doesn't fit, so no app has to place a control beside them to show where they are or move them.
/// </summary>
/// <remarks>
/// <para>A classic bar, as Aqua's, takes room from the content: a track with a thumb in the accent, an up arrow at
/// its top and a down arrow at its bottom. An arrow scrolls a row, the track a page, and both repeat while held;
/// the thumb drags. An automatic bar, as GNOME's and modern macOS's, is a thin thumb over the content, shown while the
/// content scrolls, however it scrolls, or the pointer moves over it, and for <see cref="ScrollBarLinger"/> after.</para>
/// <para>The sidebar's rows always have an automatic bar, whatever the style, so the sidebar keeps its width for its
/// rows, as GNOME's and Finder's do.</para>
/// </remarks>
public sealed partial class UIView
{
    /// <summary>How wide a classic bar is, and how tall its arrows: Aqua's 15 pixels.</summary>
    public const int ClassicScrollBarWidth = 15;

    /// <summary>How wide the strip is that an automatic bar shows in, and that the pointer can grab it in.</summary>
    public const int AutomaticScrollBarWidth = 10;

    /// <summary>How long an automatic bar stays after the content stops scrolling or the pointer stops moving.</summary>
    public static readonly TimeSpan ScrollBarLinger = TimeSpan.FromSeconds(1);

    private const int AutomaticThumbWidth = 6;
    private const int MinimumThumb = 18;
    private static readonly TimeSpan RepeatDelay = TimeSpan.FromMilliseconds(350);
    private static readonly TimeSpan RepeatInterval = TimeSpan.FromMilliseconds(50);

    // An arrow or the track held down, which repeats; and the thumb being dragged, with where it was grabbed.
    private (string Control, ScrollBarPart Part, TimeSpan Next)? barHold;
    private (string Control, float Grab)? barDrag;

    // Whether an automatic bar showed when the scroll bars were last checked, so the frame after one hides is drawn.
    private bool barsLingered;

    /// <summary>How the view's scroll bars look and behave, the sidebar's aside; the desktop sets it from the look.</summary>
    public ScrollBarStyle ScrollBars { get; set; } = ScrollBarStyle.Classic;

    /// <summary>
    /// When the frame being handled happened, which times how long an automatic bar stays; set by
    /// <see cref="TickScrollBars"/> and by <see cref="Update"/>, whichever is later.
    /// </summary>
    public TimeSpan Now { get; private set; }

    private enum ScrollBarPart
    {
        None,
        Up,
        Down,
        PageUp,
        PageDown,
        Thumb,
    }

    /// <summary>A bar's parts, where its control is placed, and the scroll it shows.</summary>
    private readonly record struct ScrollBarGeometry(
        UIRect Bar, UIRect Up, UIRect Down, UIRect Track, UIRect Thumb, int First, int Last, int Shown);

    /// <summary>
    /// Brings the view's clock to a frame's time, and says whether its scroll bars need the frame drawn: an automatic
    /// bar is showing or has just hidden, or an arrow or the track is held and repeating. The desktop draws only when
    /// something changes, so it asks every view on every frame, front or not.
    /// </summary>
    public bool TickScrollBars(TimeSpan now)
    {
        Now = now;
        var showing = false;
        foreach (var state in states.Values)
        {
            if (state.BarUntil > now)
            {
                showing = true;
                break;
            }
        }

        var changing = showing || barsLingered || barHold is not null || barDrag is not null;
        barsLingered = showing;
        return changing;
    }

    /// <summary>Where a control sits in the view, the sidebar's rows included.</summary>
    private UIRect PlaceOf(UIControl control) =>
        ReferenceEquals(control, sidebar) ? Place(control.Frame, false) : Place(control.Frame, control.InSidebar, control.Image is not null);

    /// <summary>A control's bar style: the sidebar's rows are always automatic, and the rest follow <see cref="ScrollBars"/>.</summary>
    private ScrollBarStyle StyleOf(UIControl control) =>
        ReferenceEquals(control, sidebar) ? ScrollBarStyle.Automatic : ScrollBars;

    /// <summary>Whether a control scrolls with a bar of its own: a list, text area, or the sidebar's rows.</summary>
    private bool HasOwnScrollBar(UIControl control) =>
        control.Type is UIControlType.List or UIControlType.TextArea or UIControlType.Grid;

    /// <summary>How much room a classic bar takes from a control's content: its width while it shows, else nothing.</summary>
    private int ClassicBarRoom(UIControl control) =>
        StyleOf(control) == ScrollBarStyle.Classic && HasOwnScrollBar(control) && ScrollPosition(control).Last > 0 ? ClassicScrollBarWidth : 0;

    /// <summary>
    /// A control's bar placed in a rectangle, the control's place in the view or on the screen; <see langword="false"/>
    /// when it has none, or its content fits.
    /// </summary>
    private bool TryScrollBar(UIControl control, UIRect place, out ScrollBarGeometry geometry)
    {
        geometry = default;
        if (!HasOwnScrollBar(control))
            return false;

        var (first, last) = ScrollPosition(control);
        if (last <= 0)
            return false;

        var state = states[control.Name];
        var shown = state.List is { } list ? Math.Max(1, list.Items.Count - last)
            : state.Grid is not null ? GridVisibleRows(control)
            : VisibleLines(control);

        // Inside a list's or text area's outline; the sidebar has none.
        var edge = ReferenceEquals(control, sidebar) ? 0 : 1;
        var style = StyleOf(control);
        var width = style == ScrollBarStyle.Classic ? ClassicScrollBarWidth : AutomaticScrollBarWidth;
        UIRect bar = new(place.X + place.Width - edge - width, place.Y + edge, width, place.Height - (2 * edge));

        UIRect up = default, down = default, track;
        if (style == ScrollBarStyle.Classic)
        {
            var arrow = Math.Min(ClassicScrollBarWidth, bar.Height / 3);
            up = bar with { Height = arrow };
            down = bar with { Y = bar.Y + bar.Height - arrow, Height = arrow };
            track = bar with { Y = bar.Y + arrow, Height = bar.Height - (2 * arrow) };
        }
        else
        {
            track = bar with { Y = bar.Y + 2, Height = bar.Height - 4 };
        }

        var length = Math.Clamp(track.Height * shown / (last + shown), Math.Min(MinimumThumb, track.Height), track.Height);
        var top = track.Y + ((track.Height - length) * Math.Clamp(first, 0, last) / last);
        geometry = new ScrollBarGeometry(bar, up, down, track, track with { Y = top, Height = length }, first, last, shown);
        return true;
    }

    /// <summary>The part of a control's bar at a point in the view, or <see cref="ScrollBarPart.None"/>.</summary>
    private ScrollBarPart ScrollBarPartAt(UIControl control, float x, float y, out ScrollBarGeometry geometry)
    {
        if (!TryScrollBar(control, PlaceOf(control), out geometry) || !geometry.Bar.Contains(x, y))
            return ScrollBarPart.None;
        if (geometry.Up.Contains(x, y))
            return ScrollBarPart.Up;
        if (geometry.Down.Contains(x, y))
            return ScrollBarPart.Down;
        if (geometry.Thumb.Contains(x, y))
            return ScrollBarPart.Thumb;
        return y < geometry.Thumb.Y ? ScrollBarPart.PageUp : ScrollBarPart.PageDown;
    }

    /// <summary>
    /// Handles a press on a control's bar, if it landed on one: an arrow or the track scrolls and starts repeating, and
    /// the thumb starts a drag.
    /// </summary>
    /// <returns>Whether the press was the bar's, so the control under it doesn't also take it.</returns>
    private bool PressScrollBar(UIControl control, float x, float y, TimeSpan time)
    {
        var part = ScrollBarPartAt(control, x, y, out var geometry);
        if (part == ScrollBarPart.None)
            return false;

        if (part == ScrollBarPart.Thumb)
        {
            barDrag = (control.Name, y - geometry.Thumb.Y);
        }
        else
        {
            StepScrollBar(control, part, geometry.Shown);
            barHold = (control.Name, part, time + RepeatDelay);
        }

        ShowScrollBar(control);
        return true;
    }

    /// <summary>Repeats a held arrow or track, and follows a dragged thumb, while the pointer stays down.</summary>
    private void HoldScrollBar(float x, float y, TimeSpan time)
    {
        if (barHold is { } hold && time >= hold.Next && FindScrollable(hold.Control) is { } held)
        {
            barHold = hold with { Next = time + RepeatInterval };

            // The track pages toward the pointer and stops once the thumb reaches it, as Aqua's does.
            var part = ScrollBarPartAt(held, x, y, out var geometry);
            if (part == hold.Part)
                StepScrollBar(held, part, geometry.Shown);
            ShowScrollBar(held);
        }

        if (barDrag is { } drag && FindScrollable(drag.Control) is { } dragged
            && TryScrollBar(dragged, PlaceOf(dragged), out var bar))
        {
            var travel = Math.Max(1, bar.Track.Height - bar.Thumb.Height);
            var fraction = Math.Clamp((y - drag.Grab - bar.Track.Y) / travel, 0, 1);
            ScrollTo(dragged, (int)MathF.Round(fraction * bar.Last));
            ShowScrollBar(dragged);
        }
    }

    private void ReleaseScrollBar()
    {
        barHold = null;
        barDrag = null;
    }

    private UIControl? FindScrollable(string name) =>
        sidebar is not null && sidebar.Name == name ? sidebar : script.Control(name);

    private void StepScrollBar(UIControl control, ScrollBarPart part, int shown)
    {
        var page = Math.Max(1, shown - 1);
        ScrollBy(control, part switch
        {
            ScrollBarPart.Up => -1,
            ScrollBarPart.Down => 1,
            ScrollBarPart.PageUp => -page,
            ScrollBarPart.PageDown => page,
            _ => 0,
        });
    }

    /// <summary>Keeps an automatic bar showing for a while, after its content scrolled or the pointer moved over it.</summary>
    private void ShowScrollBar(UIControl control)
    {
        // One whose content fits draws no bar, so it needn't keep the desktop drawing.
        if (StyleOf(control) == ScrollBarStyle.Automatic && ScrollPosition(control).Last > 0)
            states[control.Name].BarUntil = Now + ScrollBarLinger;
    }

    /// <summary>Shows the automatic bar of whatever scrolls under the pointer, as it moves.</summary>
    private void ShowScrollBarsUnder((int X, int Y)? from, (int X, int Y)? to)
    {
        if (to is not { } point || from == to)
            return;

        var controls = script.Controls;
        for (var index = 0; index < controls.Count; index++)
        {
            var control = controls[index];
            if (StyleOf(control) == ScrollBarStyle.Automatic && HasOwnScrollBar(control) && Showing(states[control.Name]) && PlaceOf(control).Contains(point.X, point.Y))
                ShowScrollBar(control);
        }

        if (sidebar is not null && PlaceOf(sidebar).Contains(point.X, point.Y))
            ShowScrollBar(sidebar);
    }

    /// <summary>Draws a control's bar over what it drew, if it has one showing, at its place on the screen.</summary>
    private void DrawScrollBar(IPainter painter, UIControl control, State state, UIRect bounds)
    {
        if (!TryScrollBar(control, bounds, out var bar))
            return;

        // However it scrolled, by the caret, by following new text, or by the app, an automatic bar shows for a moment,
        // as macOS's and GNOME's do.
        if (bar.First != state.BarFirst)
        {
            state.BarFirst = bar.First;
            ShowScrollBar(control);
        }

        if (StyleOf(control) == ScrollBarStyle.Classic)
        {
            DrawClassicScrollBar(painter, bar, state.Disabled);
            return;
        }

        var held = barDrag?.Control == control.Name || barHold?.Control == control.Name;
        var grabbable = hover is { } at && TryScrollBar(control, PlaceOf(control), out var placed) && placed.Bar.Contains(at.X, at.Y);
        if (!held && !grabbable && state.BarUntil <= Now)
            return;

        // Wider while the pointer can grab it, as GTK's widens.
        var width = grabbable || held ? AutomaticScrollBarWidth - 2 : AutomaticThumbWidth;
        var (_, thumbY, _, thumbHeight) = Ints(bar.Thumb);
        var x = (int)(bar.Bar.X + bar.Bar.Width) - 2 - width;
        var background = ReferenceEquals(control, sidebar) ? DesktopTheme.SidebarBackground : DesktopTheme.InputBackground;
        DesktopTheme.FillRoundedGradient(painter, x, thumbY, width, thumbHeight, width / 2,
            DesktopTheme.Blend(background, DesktopTheme.Text, 0.45f), DesktopTheme.Blend(background, DesktopTheme.Text, 0.45f));
    }

    /// <summary>
    /// Aqua's bar: a shaded track, its arrows at either end, and a glossy thumb in the accent, so it follows the
    /// accent and light and dark as checked buttons and selections do.
    /// </summary>
    private static void DrawClassicScrollBar(IPainter painter, ScrollBarGeometry bar, bool disabled)
    {
        var (x, y, width, height) = Ints(bar.Bar);
        DesktopTheme.FillGradient(painter, x, y, width, height,
            DesktopTheme.Blend(DesktopTheme.InputBackground, DesktopTheme.Border, 0.35f),
            DesktopTheme.Blend(DesktopTheme.InputBackground, DesktopTheme.Border, 0.15f));
        painter.FillRectangle(x, y, 1, height, DesktopTheme.Border);

        var ink = disabled ? DesktopTheme.DisabledText : DesktopTheme.Text;
        DrawArrow(painter, bar.Up, pointsUp: true, ink);
        DrawArrow(painter, bar.Down, pointsUp: false, ink);

        var (thumbX, thumbY, thumbWidth, thumbHeight) = Ints(bar.Thumb);
        thumbX += 3;
        thumbWidth -= 5;
        var accent = disabled ? DesktopTheme.DisabledText : DesktopTheme.Accent;
        var white = new UIColor(255, 255, 255);
        DesktopTheme.FillRoundedGradient(painter, thumbX, thumbY + 1, thumbWidth, thumbHeight - 2, thumbWidth / 2,
            DesktopTheme.Blend(accent, white, 0.35f), accent);

        // The gloss: a bright stripe down the thumb's left, short of its rounded ends.
        if (thumbHeight > thumbWidth + 2)
            painter.FillRectangle(thumbX + 2, thumbY + 1 + (thumbWidth / 2), 2, thumbHeight - 2 - thumbWidth,
                DesktopTheme.Blend(accent, white, 0.6f));
    }

    /// <summary>An arrow's box, a line between it and the track, and a small triangle in it.</summary>
    private static void DrawArrow(IPainter painter, UIRect box, bool pointsUp, UIColor ink)
    {
        var (x, y, width, height) = Ints(box);
        if (height < 4)
            return;

        painter.FillRectangle(x + 1, y, width - 1, height, DesktopTheme.Blend(DesktopTheme.InputBackground, DesktopTheme.Border, 0.1f));
        painter.FillRectangle(x + 1, pointsUp ? y + height - 1 : y, width - 1, 1, DesktopTheme.Border);

        // Four rows, one to seven pixels wide, centred.
        const int Rows = 4;
        var middle = x + 1 + ((width - 1) / 2);
        var top = y + ((height - Rows) / 2);
        for (var row = 0; row < Rows; row++)
        {
            var half = pointsUp ? row : Rows - 1 - row;
            painter.FillRectangle(middle - half, top + row, (2 * half) + 1, 1, ink);
        }
    }
}
