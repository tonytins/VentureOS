using System.Diagnostics.CodeAnalysis;
using Venture.OS.Desktop;

namespace Venture.OS.Vdb;

/// <summary>
/// Keys as <c>key</c> names them: one character, such as <c>a</c> or <c>+</c>, or <c>Space</c>, <c>Enter</c>,
/// <c>Escape</c>, <c>Tab</c>, <c>Backspace</c>, <c>Delete</c>, <c>Left</c>, <c>Right</c>, <c>Up</c>, <c>Down</c>,
/// <c>Home</c>, or <c>End</c>, after any of <c>Ctrl+</c>, <c>Shift+</c>, and <c>Alt+</c>, such as <c>Ctrl+Shift+Z</c>.
/// </summary>
/// <remarks>
/// They arrive as the kernel's keyboard gives them: a letter with Ctrl or Alt held is lowercase, as shortcuts and
/// hotkeys expect, and one with only Shift held is uppercase, as typing it would be.
/// </remarks>
public static class VdbKeys
{
    /// <summary>The most keys one <c>key</c> line presses.</summary>
    public const int MaximumKeys = 32;

    private static readonly Dictionary<string, DesktopKey> Named = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Enter"] = DesktopKey.Enter,
        ["Escape"] = DesktopKey.Escape,
        ["Tab"] = DesktopKey.Tab,
        ["Backspace"] = DesktopKey.Backspace,
        ["Delete"] = DesktopKey.Delete,
        ["Left"] = DesktopKey.Left,
        ["Right"] = DesktopKey.Right,
        ["Up"] = DesktopKey.Up,
        ["Down"] = DesktopKey.Down,
        ["Home"] = DesktopKey.Home,
        ["End"] = DesktopKey.End,
    };

    /// <summary>Reads one key's name.</summary>
    public static bool TryParse(string name, out KeyPress key, [NotNullWhen(false)] out string? problem)
    {
        ArgumentNullException.ThrowIfNull(name);
        key = default;
        bool control = false, shift = false, alt = false;
        var rest = name;

        // The modifiers come first, each ending in +; what's left, a lone + included, is the key.
        while (rest.Length > 1 && rest.IndexOf('+', StringComparison.Ordinal) is var plus and > 0 && plus < rest.Length - 1)
        {
            var modifier = rest[..plus];
            if (modifier.Equals("Ctrl", StringComparison.OrdinalIgnoreCase))
                control = true;
            else if (modifier.Equals("Shift", StringComparison.OrdinalIgnoreCase))
                shift = true;
            else if (modifier.Equals("Alt", StringComparison.OrdinalIgnoreCase))
                alt = true;
            else
            {
                problem = $"\"{Shorten(modifier)}\" isn't Ctrl, Shift, or Alt";
                return false;
            }

            rest = rest[(plus + 1)..];
        }

        if (Named.TryGetValue(rest, out var special))
        {
            key = new KeyPress(special, '\0', control, shift, alt);
            problem = null;
            return true;
        }

        var character = rest.Equals("Space", StringComparison.OrdinalIgnoreCase) ? ' ' : rest.Length == 1 ? rest[0] : '\0';
        if (character == '\0' || char.IsControl(character))
        {
            problem = $"\"{Shorten(name)}\" isn't a key; name one character, or Space, Enter, Escape, Tab, Backspace, Delete, Left, Right, Up, Down, Home, or End";
            return false;
        }

        if (char.IsLetter(character))
            character = control || alt ? char.ToLowerInvariant(character) : shift ? char.ToUpperInvariant(character) : character;
        key = new KeyPress(DesktopKey.Character, character, control, shift, alt);
        problem = null;
        return true;
    }

    private static string Shorten(string text) => text.Length <= 40 ? text : text[..40] + "…";
}
