using Venture.OS.Interface;

namespace Venture.OS.Desktop;

/// <summary>
/// The desktop while it's open: the sky, the windows, and the pointer, and what each frame's input does to them.
/// </summary>
/// <remarks>
/// The kernel's frame loop reads the input, hands it to <see cref="Update"/>, and draws only when that says
/// something changed, so an idle desktop doesn't redraw. Esc closes the desktop, back to the shell, until the menu
/// bar offers a way out.
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
        last = new DesktopInput(width / 2, height / 2, false);
        Windows = new WindowManager(width, height) { TopInset = 48 };
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>The desktop's windows.</summary>
    public WindowManager Windows { get; }

    /// <summary>Whether the user has left the desktop.</summary>
    public bool IsFinished { get; private set; }

    /// <summary>The pointer's position, kept on the screen.</summary>
    public (int X, int Y) PointerPosition => (last.PointerX, last.PointerY);

    /// <summary>What happened to windows in the last update.</summary>
    public IReadOnlyList<WindowEvent> LastEvents { get; private set; } = [];

    /// <summary>Takes a frame's input.</summary>
    /// <returns>Whether the desktop needs drawing again.</returns>
    public bool Update(DesktopInput input)
    {
        foreach (var key in input.KeysPressed)
        {
            if (key.Key == DesktopKey.Escape)
            {
                IsFinished = true;
                return false;
            }
        }

        var clamped = input with
        {
            PointerX = Math.Clamp(input.PointerX, 0, Width - 1),
            PointerY = Math.Clamp(input.PointerY, 0, Height - 1),
        };

        LastEvents = Windows.Update(clamped);

        var changed = !drawn || clamped.HasEvents || LastEvents.Count > 0
            || clamped.PointerX != last.PointerX || clamped.PointerY != last.PointerY
            || clamped.PointerDown != last.PointerDown;
        last = clamped with { Keys = null, Scroll = 0 };
        return changed;
    }

    /// <summary>Draws the whole desktop: the sky, the hint, the windows, then the pointer on top.</summary>
    public void Draw(IPainter painter)
    {
        ArgumentNullException.ThrowIfNull(painter);

        painter.DrawPixels(background, 0, 0, Width, Height);

        const int HintSize = 16;
        var hintWidth = painter.MeasureText(LeaveHint, HintSize);
        var (hintX, hintY) = ((Width - hintWidth) / 2, 18);
        painter.DrawText(LeaveHint, hintX + 1, hintY + 1, new UIColor(0, 0, 0, 255), HintSize);
        painter.DrawText(LeaveHint, hintX, hintY, new UIColor(255, 255, 255), HintSize);

        Windows.Draw(painter);
        Pointer.Draw(painter, last.PointerX, last.PointerY);
        drawn = true;
    }
}
