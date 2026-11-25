using Venture.OS.Interface;

namespace Venture.OS.Desktop;

/// <summary>
/// Sliders, and the rail they share with progress bars: a value chosen by dragging the knob, clicking the track, or
/// the keyboard, snapped to the slider's step, reporting <c>changed</c> as it moves and <c>committed</c> when it's let
/// go. See <c>docs/design/2026-11-25-slider-design.md</c>.
/// </summary>
/// <remarks>
/// The rail is libadwaita's, thin and round-ended, its empty part sunk into the window and its filled part the
/// accent with Tiger's gloss, the menu bar's band and sheen; the knob is round and white. A slider runs along its
/// longer side: across when it's wider than tall, else up from the bottom.
/// </remarks>
public sealed partial class UIView
{
    /// <summary>How thick a slider's or progress bar's rail is.</summary>
    public const int RailThickness = 6;

    /// <summary>A slider's knob's size.</summary>
    public const int KnobSize = 18;

    // The slider being dragged, whose value follows the pointer until it's released.
    private string? sliding;

    /// <summary>A value kept within the control's range and, for a slider with a step, on a step.</summary>
    private static float Snap(UIControl control, float value)
    {
        var (minimum, maximum) = Range(control);
        value = Math.Clamp(value, minimum, maximum);
        if (control.Step is not { } step)
            return value;

        // The nearest whole step, never past the last one that fits in the range.
        var steps = MathF.Round((value - minimum) / step);
        var last = MathF.Floor(((maximum - minimum) / step) + 0.0001f);
        return minimum + (Math.Min(steps, last) * step);
    }

    private static bool IsAcross(UIRect bounds) => bounds.Width >= bounds.Height;

    /// <summary>Sets a slider to the value under a point on its track, reporting it if it moved.</summary>
    private void SlideTo(UIControl slider, int x, int y, List<UIEvent> events)
    {
        var bounds = Place(slider.Frame, slider.InSidebar, false);
        var (minimum, maximum) = Range(slider);
        var room = (IsAcross(bounds) ? bounds.Width : bounds.Height) - KnobSize;
        var along = IsAcross(bounds) ? x - bounds.X - (KnobSize / 2f) : bounds.Y + bounds.Height - (KnobSize / 2f) - y;
        var share = room <= 0 ? 0 : Math.Clamp(along / room, 0, 1);
        SetSlider(slider, minimum + (share * (maximum - minimum)), events, commit: false);
    }

    /// <summary>Moves a slider to a value, snapped, reporting <c>changed</c>, and <c>committed</c> when asked, if it moved.</summary>
    private void SetSlider(UIControl slider, float value, List<UIEvent> events, bool commit)
    {
        var state = states[slider.Name];
        var snapped = Snap(slider, value);
        if (snapped == state.Value)
            return;

        state.Value = snapped;
        events.Add(new UIEvent(UIEventKind.Changed, slider.Name, Value: snapped));
        if (commit)
            events.Add(new UIEvent(UIEventKind.Committed, slider.Name, Value: snapped));
    }

    /// <summary>A key in a focused slider: the arrows move it a step, or a tenth of its range; Home and End to its ends.</summary>
    private void TypeSlider(UIControl slider, State state, KeyPress key, List<UIEvent> events)
    {
        var (minimum, maximum) = Range(slider);
        var step = slider.Step ?? (maximum - minimum) / 10;
        float? to = key.Key switch
        {
            DesktopKey.Right or DesktopKey.Up => state.Value + step,
            DesktopKey.Left or DesktopKey.Down => state.Value - step,
            DesktopKey.Home => minimum,
            DesktopKey.End => maximum,
            _ => null,
        };
        if (to is { } value)
            SetSlider(slider, value, events, commit: true);
    }

    /// <summary>The end of a drag: <c>committed</c>, with the value it ended on.</summary>
    private void ReleaseSlider(List<UIEvent> events)
    {
        if (sliding is { } name)
            events.Add(new UIEvent(UIEventKind.Committed, name, Value: states[name].Value));
        sliding = null;
    }

    private void DrawSlider(IPainter painter, UIControl control, State state, UIRect bounds)
    {
        var (minimum, maximum) = Range(control);
        var share = maximum > minimum ? (state.Value - minimum) / (maximum - minimum) : 0;
        var across = IsAcross(bounds);

        // The rail runs between the knob's centers at either end, so the knob never hangs off the control.
        var inset = KnobSize / 2;
        var rail = across
            ? new UIRect(bounds.X + inset, bounds.Y + ((bounds.Height - RailThickness) / 2), Math.Max(0, bounds.Width - KnobSize), RailThickness)
            : new UIRect(bounds.X + ((bounds.Width - RailThickness) / 2), bounds.Y + inset, RailThickness, Math.Max(0, bounds.Height - KnobSize));
        DrawRail(painter, rail, share, across, Style(control), state.Disabled);

        var center = across
            ? (X: (int)(rail.X + (rail.Width * share)), Y: (int)(bounds.Y + (bounds.Height / 2)))
            : (X: (int)(bounds.X + (bounds.Width / 2)), Y: (int)(rail.Y + rail.Height - (rail.Height * share)));
        var (left, top) = (center.X - (KnobSize / 2), center.Y - (KnobSize / 2));

        // Focused, a ring in the accent, as a focused field's outline is.
        if (focused == control.Name)
            DesktopTheme.FillRoundedGradient(painter, left - 2, top - 2, KnobSize + 4, KnobSize + 4, (KnobSize / 2) + 2, DesktopTheme.Accent, DesktopTheme.Accent);
        DesktopTheme.FillRoundedGradient(painter, left, top + 1, KnobSize, KnobSize, KnobSize / 2, DesktopTheme.Shadow, DesktopTheme.Shadow);
        DesktopTheme.FillRoundedGradient(painter, left, top, KnobSize, KnobSize, KnobSize / 2, DesktopTheme.KnobEdge, DesktopTheme.KnobEdge);
        var knob = state.Disabled ? DesktopTheme.InputBackground : new UIColor(255, 255, 255);
        DesktopTheme.FillRoundedGradient(painter, left + 1, top + 1, KnobSize - 2, KnobSize - 2, (KnobSize / 2) - 1, knob, knob);
    }

    private void DrawProgress(IPainter painter, UIControl control, State state, UIRect bounds)
    {
        var (minimum, maximum) = Range(control);
        var share = maximum > minimum ? (state.Value - minimum) / (maximum - minimum) : 0;
        var across = IsAcross(bounds);
        var thickness = Math.Min(RailThickness, across ? bounds.Height : bounds.Width);
        var rail = across
            ? new UIRect(bounds.X, bounds.Y + ((bounds.Height - thickness) / 2), bounds.Width, thickness)
            : new UIRect(bounds.X + ((bounds.Width - thickness) / 2), bounds.Y, thickness, bounds.Height);
        DrawRail(painter, rail, share, across, Style(control), state.Disabled);
    }

    /// <summary>
    /// The rail a slider and a progress bar share: the empty part sunk into the window, then the filled part, from the
    /// minimum, the accent with Tiger's gloss over it. A style's background and fill colors take the place of the
    /// rail's and the accent; disabled, the fill is the disabled text's color, without gloss.
    /// </summary>
    private static void DrawRail(IPainter painter, UIRect rail, float share, bool across, ResolvedStyle? style, bool disabled)
    {
        var (x, y, width, height) = Ints(rail);
        if (width <= 0 || height <= 0)
            return;

        var radius = Math.Min(width, height) / 2;
        var (top, bottom) = style?.BackgroundColor is { } background ? (background, background) : (DesktopTheme.RailTop, DesktopTheme.RailBottom);
        DesktopTheme.FillRoundedGradient(painter, x, y, width, height, radius, top, bottom);

        share = Math.Clamp(share, 0, 1);
        var (fillX, fillY, fillWidth, fillHeight) = across
            ? (x, y, (int)MathF.Round(width * share), height)
            : (x, y + height - (int)MathF.Round(height * share), width, (int)MathF.Round(height * share));
        if (fillWidth <= 0 || fillHeight <= 0)
            return;

        var fill = disabled ? DesktopTheme.DisabledText : style?.FillColor ?? DesktopTheme.Accent;
        DesktopTheme.FillRoundedGradient(painter, fillX, fillY, fillWidth, fillHeight, Math.Min(radius, Math.Min(fillWidth, fillHeight) / 2),
            Lighter(fill), fill);
        if (disabled)
            return;

        // Tiger's gloss, as the menu bar has it: a bright band over the top half, and a sheen along the top edge.
        painter.FillRectangle(fillX + 1, fillY, Math.Max(0, fillWidth - 2), Math.Max(1, fillHeight / 2), DesktopTheme.MenuBarGloss);
        painter.FillRectangle(fillX + radius, fillY, Math.Max(0, fillWidth - (2 * radius)), 1, DesktopTheme.MenuBarSheen);
    }

    // A color a little toward white, for the top of the filled rail's gradient.
    private static UIColor Lighter(UIColor color) => new(
        (byte)(color.Red + ((255 - color.Red) / 6)),
        (byte)(color.Green + ((255 - color.Green) / 6)),
        (byte)(color.Blue + ((255 - color.Blue) / 6)),
        color.Alpha);
}
