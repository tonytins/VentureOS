using Venture.OS.Interface;

namespace Venture.OS.Desktop;

/// <summary>One frame's input: where the pointer is, whether its button is down, and whether Esc was pressed.</summary>
public readonly record struct DesktopInput(int PointerX, int PointerY, bool PointerDown, bool EscapePressed);

/// <summary>
/// The desktop while it's open: what it shows, and what the input does to it.
/// </summary>
/// <remarks>
/// <para>The kernel's frame loop reads the input, hands it to <see cref="Update"/>, and draws only when that says
/// something changed, so an idle desktop doesn't redraw. Esc closes it, back to the shell.</para>
/// <para>For now it shows the sky, a line saying how to leave, and the pointer. Windows, the menu bar, and the dock
/// come next.</para>
/// </remarks>
public sealed class DesktopSession
{
    /// <summary>What the desktop says while there's no menu bar to leave from.</summary>
    public const string LeaveHint = "Press Esc to go back to the shell.";

    private readonly int[] background;
    private DesktopInput last;
    private bool drawn;

    /// <summary>Opens the desktop on a screen of this size.</summary>
    public DesktopSession(int width, int height)
    {
        Width = width;
        Height = height;
        background = DesktopBackground.Render(width, height);
        last = new DesktopInput(width / 2, height / 2, false, false);
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>Whether the user has left the desktop.</summary>
    public bool IsFinished { get; private set; }

    /// <summary>The pointer's position, kept on the screen.</summary>
    public (int X, int Y) PointerPosition => (last.PointerX, last.PointerY);

    /// <summary>Takes a frame's input.</summary>
    /// <returns>Whether the desktop needs drawing again.</returns>
    public bool Update(DesktopInput input)
    {
        if (input.EscapePressed)
        {
            IsFinished = true;
            return false;
        }

        var clamped = input with
        {
            PointerX = Math.Clamp(input.PointerX, 0, Width - 1),
            PointerY = Math.Clamp(input.PointerY, 0, Height - 1),
        };

        var changed = !drawn || clamped != last;
        last = clamped;
        return changed;
    }

    /// <summary>Draws the whole desktop.</summary>
    public void Draw(IPainter painter)
    {
        ArgumentNullException.ThrowIfNull(painter);

        painter.DrawPixels(background, 0, 0, Width, Height);

        const int HintSize = 16;
        var hintWidth = painter.MeasureText(LeaveHint, HintSize);
        var (hintX, hintY) = ((Width - hintWidth) / 2, 24);
        painter.DrawText(LeaveHint, hintX + 1, hintY + 1, new UIColor(0, 0, 0, 255), HintSize);
        painter.DrawText(LeaveHint, hintX, hintY, new UIColor(255, 255, 255), HintSize);

        Pointer.Draw(painter, last.PointerX, last.PointerY);
        drawn = true;
    }
}
