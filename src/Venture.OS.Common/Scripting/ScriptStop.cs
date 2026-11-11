using System.Diagnostics;

namespace Venture.OS.Scripting;

/// <summary>
/// Ways to decide that running Lua should stop, for <see cref="LuaRunner"/>.
/// </summary>
/// <remarks>
/// A stop check returns <see langword="null"/> to let the code go on, or the reason it stopped, which the user sees,
/// such as <c>Interrupted.</c>. Checks run often, so they must be quick and must not block.
/// </remarks>
public static class ScriptStop
{
    /// <summary>
    /// Stops once <paramref name="limit"/> has passed since this method was called.
    /// </summary>
    /// <remarks>
    /// Time comes from <see cref="Stopwatch"/>, which Cosmos backs with the CPU's timestamp counter on x64.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> isn't positive.</exception>
    public static Func<string?> After(TimeSpan limit)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(limit, TimeSpan.Zero);

        var start = Stopwatch.GetTimestamp();
        var reason = $"Stopped after {FormatLimit(limit)}.";
        return () => Stopwatch.GetElapsedTime(start) >= limit ? reason : null;
    }

    /// <summary>
    /// Stops when any of <paramref name="checks"/> does, with the first one's reason.
    /// </summary>
    public static Func<string?> Any(params Func<string?>[] checks)
    {
        ArgumentNullException.ThrowIfNull(checks);
        foreach (var check in checks)
            ArgumentNullException.ThrowIfNull(check, nameof(checks));

        return () =>
        {
            foreach (var check in checks)
            {
                if (check() is { } reason)
                    return reason;
            }

            return null;
        };
    }

    private static string FormatLimit(TimeSpan limit) =>
        limit.TotalSeconds == 1 ? "1 second" : $"{limit.TotalSeconds:0.###} seconds";
}
