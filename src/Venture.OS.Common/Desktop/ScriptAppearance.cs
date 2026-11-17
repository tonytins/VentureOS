using Venture.OS.Scripting;

namespace Venture.OS.Desktop;

/// <summary>
/// The desktop's look for scripts, through <see cref="ISystemInfo.Appearance"/>: it reads the look as chosen and the
/// palette showing, and changes the look through <see cref="DesktopAppearance"/>, which saves it.
/// </summary>
public sealed class ScriptAppearance : IAppearance
{
    /// <inheritdoc/>
    public bool IsDark => DesktopTheme.Palette.IsDark;

    /// <inheritdoc/>
    public string Mode => DesktopLook.FormatMode(DesktopAppearance.Current.Mode);

    /// <inheritdoc/>
    public string Accent => DesktopPalette.Format(DesktopAppearance.Current.Accent);

    /// <inheritdoc/>
    public bool RoundedCorners => DesktopAppearance.Current.RoundedCorners;

    /// <inheritdoc/>
    public string Wallpaper => DesktopAppearance.Current.Wallpaper;

    /// <inheritdoc/>
    public string DarkFrom => DesktopLook.FormatTime(DesktopAppearance.Current.DarkFrom);

    /// <inheritdoc/>
    public string DarkUntil => DesktopLook.FormatTime(DesktopAppearance.Current.DarkUntil);

    /// <inheritdoc/>
    public IReadOnlyList<WallpaperChoice> Wallpapers()
    {
        if (DesktopAppearance.WallpapersFolder is not { } folder)
            return [];

        List<WallpaperChoice> choices = [];
        foreach (var wallpaper in WallpaperCatalog.Read(folder))
            choices.Add(new WallpaperChoice(wallpaper.Name, wallpaper.Bundle.Name, wallpaper.Thumbnail, wallpaper.Bundle.Description));
        return choices;
    }

    /// <inheritdoc/>
    public AppearanceChange Set(AppearanceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var look = DesktopAppearance.Current;

        if (request.Mode is { } mode)
        {
            if (!DesktopLook.TryParseMode(mode, out var parsed))
                return Refuse($"the appearance is light, dark, or auto, not \"{mode}\"");
            look = look with { Mode = parsed };
        }

        if (request.Accent is { } accent)
        {
            if (!DesktopPalette.TryParseColor(accent, out var color))
                return Refuse($"an accent is written #RRGGBB, such as #3584E4, not \"{accent}\"");
            look = look with { Accent = color };
        }

        if (request.Corners is { } corners)
        {
            if (corners is not ("rounded" or "square"))
                return Refuse($"corners are rounded or square, not \"{corners}\"");
            look = look with { RoundedCorners = corners == "rounded" };
        }

        if (request.Wallpaper is { } wallpaper)
        {
            if (!DesktopLook.IsWallpaperName(wallpaper))
                return Refuse($"\"{wallpaper}\" isn't a wallpaper's name");
            if (wallpaper != DesktopLook.Sky && DesktopAppearance.WallpapersFolder is { } folder
                && WallpaperCatalog.Find(folder, wallpaper) is null)
                return Refuse($"there's no wallpaper named {wallpaper}");
            look = look with { Wallpaper = wallpaper };
        }

        if (request.DarkFrom is { } from)
        {
            if (!DesktopLook.TryParseTime(from, out var time))
                return Refuse($"a time is written HH:MM, such as 19:00, not \"{from}\"");
            look = look with { DarkFrom = time };
        }

        if (request.DarkUntil is { } until)
        {
            if (!DesktopLook.TryParseTime(until, out var time))
                return Refuse($"a time is written HH:MM, such as 07:00, not \"{until}\"");
            look = look with { DarkUntil = time };
        }

        if (look.DarkFrom == look.DarkUntil)
            return Refuse($"dark from {DesktopLook.FormatTime(look.DarkFrom)} until the same time is never dark; choose two times");

        return new AppearanceChange(null, DesktopAppearance.Apply(look));
    }

    private static AppearanceChange Refuse(string reason) => new(reason, null);
}
