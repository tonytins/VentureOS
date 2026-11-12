using Venture.OS.Scripting;

namespace Venture.OS.Desktop;

/// <summary>
/// The desktop's look for scripts, through <see cref="ISystemInfo.Appearance"/>: it reads the palette in use and
/// switches it through <see cref="DesktopAppearance"/>, which saves the choice.
/// </summary>
public sealed class ScriptAppearance : IAppearance
{
    /// <inheritdoc/>
    public bool IsDark => DesktopTheme.Palette.IsDark;

    /// <inheritdoc/>
    public string Accent => DesktopPalette.Format(DesktopTheme.Palette.ChosenAccent);

    /// <inheritdoc/>
    public AppearanceChange Set(bool dark, string? accent)
    {
        var color = DesktopTheme.Palette.ChosenAccent;
        if (accent is not null && !DesktopPalette.TryParseColor(accent, out color))
            return new AppearanceChange($"an accent is written #RRGGBB, such as #1E78DC, not \"{accent}\"", null);

        return new AppearanceChange(null, DesktopAppearance.Apply(DesktopPalette.Create(color, dark)));
    }
}
