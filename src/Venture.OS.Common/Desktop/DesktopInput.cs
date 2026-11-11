namespace Venture.OS.Desktop;

/// <summary>Keys the desktop treats specially. Anything else that types arrives as <see cref="Character"/>.</summary>
public enum DesktopKey
{
    Character,
    Backspace,
    Delete,
    Enter,
    Escape,
    Tab,
    Left,
    Right,
    Up,
    Down,
    Home,
    End,
}

/// <summary>A key pressed this frame, with the character it types, if any, and the modifiers held.</summary>
public readonly record struct KeyPress(DesktopKey Key, char Character = '\0', bool Control = false, bool Shift = false, bool Alt = false)
{
    /// <summary>A key that types <paramref name="character"/>.</summary>
    public static KeyPress Typed(char character) => new(DesktopKey.Character, character);
}

/// <summary>
/// One frame's input: where the pointer is, whether its button is down, how far the wheel turned, the keys
/// pressed since the last frame, and when the frame happened.
/// </summary>
/// <param name="Time">How long the desktop has been running, which tells a double-click from two clicks.</param>
public readonly record struct DesktopInput(
    int PointerX, int PointerY, bool PointerDown, int Scroll = 0, KeyPress[]? Keys = null, TimeSpan Time = default)
{
    /// <summary>The keys pressed this frame, never <see langword="null"/>.</summary>
    public KeyPress[] KeysPressed => Keys ?? [];

    /// <summary>Whether this frame carries anything besides the pointer's position and button.</summary>
    public bool HasEvents => Scroll != 0 || KeysPressed.Length > 0;
}
