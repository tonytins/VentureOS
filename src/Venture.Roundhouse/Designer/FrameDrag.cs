using Venture.OS.Desktop;

namespace Venture.Roundhouse.Designer;

/// <summary>A selection's eight resize handles, or none for moving the whole frame.</summary>
public enum Handle
{
    /// <summary>No handle: dragging moves the frame.</summary>
    None,

    TopLeft,
    Top,
    TopRight,
    Right,
    BottomRight,
    Bottom,
    BottomLeft,
    Left,
}

/// <summary>How a drag on the canvas changes a frame: which handle it started on, and what moves.</summary>
public static class FrameDrag
{
    /// <summary>How wide a handle is drawn, in the canvas's pixels.</summary>
    public const float HandleSize = 7;

    /// <summary>Every handle, in the order they're drawn.</summary>
    public static IReadOnlyList<Handle> Handles { get; } =
        [Handle.TopLeft, Handle.Top, Handle.TopRight, Handle.Right, Handle.BottomRight, Handle.Bottom, Handle.BottomLeft, Handle.Left];

    /// <summary>Where a handle's centre sits on a frame.</summary>
    public static (float X, float Y) Centre(UIRect frame, Handle handle)
    {
        var (left, middle, right) = (frame.X, frame.X + (frame.Width / 2), frame.X + frame.Width);
        var (top, centre, bottom) = (frame.Y, frame.Y + (frame.Height / 2), frame.Y + frame.Height);
        return handle switch
        {
            Handle.TopLeft => (left, top),
            Handle.Top => (middle, top),
            Handle.TopRight => (right, top),
            Handle.Right => (right, centre),
            Handle.BottomRight => (right, bottom),
            Handle.Bottom => (middle, bottom),
            Handle.BottomLeft => (left, bottom),
            Handle.Left => (left, centre),
            _ => (middle, centre),
        };
    }

    /// <summary>The handle under a point, or <see cref="Handle.None"/>. A point a little outside a handle still counts.</summary>
    public static Handle HandleAt(UIRect frame, float x, float y)
    {
        const float Reach = (HandleSize / 2) + 2;
        foreach (var handle in Handles)
        {
            var (centreX, centreY) = Centre(frame, handle);
            if (MathF.Abs(x - centreX) <= Reach && MathF.Abs(y - centreY) <= Reach)
                return handle;
        }

        return Handle.None;
    }

    /// <summary>
    /// What a drag of (<paramref name="dx"/>, <paramref name="dy"/>) from <paramref name="handle"/> does to a frame:
    /// how far its corner moves and how much it grows. A frame is never made narrower or shorter than a pixel.
    /// </summary>
    public static FrameChange Change(UIRect frame, Handle handle, float dx, float dy)
    {
        if (handle == Handle.None)
            return new FrameChange(dx, dy, 0, 0);

        float moveX = 0, moveY = 0, growX = 0, growY = 0;
        if (handle is Handle.Left or Handle.TopLeft or Handle.BottomLeft)
        {
            moveX = MathF.Min(dx, frame.Width - 1);
            growX = -moveX;
        }
        else if (handle is Handle.Right or Handle.TopRight or Handle.BottomRight)
        {
            growX = MathF.Max(dx, 1 - frame.Width);
        }

        if (handle is Handle.Top or Handle.TopLeft or Handle.TopRight)
        {
            moveY = MathF.Min(dy, frame.Height - 1);
            growY = -moveY;
        }
        else if (handle is Handle.Bottom or Handle.BottomLeft or Handle.BottomRight)
        {
            growY = MathF.Max(dy, 1 - frame.Height);
        }

        return new FrameChange(moveX, moveY, growX, growY);
    }
}

/// <summary>A change to a frame: how far its corner moves, and how much wider and taller it gets.</summary>
public readonly record struct FrameChange(float X, float Y, float Width, float Height)
{
    /// <summary>Whether it changes nothing.</summary>
    public bool IsEmpty => X == 0 && Y == 0 && Width == 0 && Height == 0;

    /// <summary>The frame changed.</summary>
    public UIRect ApplyTo(UIRect frame) => new(frame.X + X, frame.Y + Y, frame.Width + Width, frame.Height + Height);
}
