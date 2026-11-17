namespace Venture.OS.Scripting;

/// <summary>
/// Stops running Lua when the user presses Ctrl+C or Esc.
/// </summary>
/// <remarks>
/// <see cref="Check"/> never blocks: it takes every key waiting in the buffer and looks for one of those two.
/// Other keys typed while a script runs are discarded, since nothing else reads input then.
/// </remarks>
internal static class KeyboardStop
{
    /// <summary>The reason the user sees when a key stops a script.</summary>
    public const string Reason = "Interrupted.";

    /// <summary>A stop check for <see cref="LuaRunner"/>.</summary>
    public static string? Check()
    {
        if (!KeyboardManager.IsEnabled)
            return null;

        while (KeyboardManager.TryReadKey(out var key))
        {
            if (key.Type != KeyEvent.KeyEventType.Make)
                continue;

            if (key.Key == Key.Escape || (key.Key == Key.C && key.Modifiers.HasFlag(ConsoleModifiers.Control)))
                return Reason;
        }

        return null;
    }
}
