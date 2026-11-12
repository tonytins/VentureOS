namespace Venture.OS.Scripting;

/// <summary>
/// The desktop's look as scripts read and change it: light or dark, and an accent color. The desktop implements it,
/// and the kernel hands it over through <see cref="ISystemInfo.Appearance"/>, so the Lua library doesn't depend on
/// how palettes, themes, or saving work.
/// </summary>
public interface IAppearance
{
    /// <summary>Whether the look is dark.</summary>
    bool IsDark { get; }

    /// <summary>The accent as chosen, written <c>#RRGGBB</c>.</summary>
    string Accent { get; }

    /// <summary>Switches the look and saves it.</summary>
    /// <param name="dark">Dark rather than light.</param>
    /// <param name="accent">The accent, written <c>#RRGGBB</c>, or <see langword="null"/> to keep the current one.</param>
    AppearanceChange Set(bool dark, string? accent);
}

/// <summary>
/// What <see cref="IAppearance.Set"/> did: <see cref="Refused"/> says why nothing changed, and otherwise the look
/// changed, with <see cref="Unsaved"/> saying why it couldn't be saved when that failed.
/// </summary>
public readonly record struct AppearanceChange(string? Refused, string? Unsaved);
