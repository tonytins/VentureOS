using System.Globalization;
using Venture.OS.Interface;

namespace Venture.OS.Desktop;

/// <summary>Whether the desktop is light, dark, or dark on a schedule.</summary>
public enum LookMode
{
    /// <summary>Always light.</summary>
    Light,

    /// <summary>Always dark.</summary>
    Dark,

    /// <summary>Dark from <see cref="DesktopLook.DarkFrom"/> until <see cref="DesktopLook.DarkUntil"/>, light otherwise.</summary>
    Auto,
}

/// <summary>How the dock behaves.</summary>
public enum DockMode
{
    /// <summary>Always shown; zoomed and new windows stop above it.</summary>
    Always,

    /// <summary>Hidden until the pointer reaches the bottom of the screen, and hidden again a moment after it leaves.</summary>
    AutoHide,

    /// <summary>Shown unless a window would cover it, and shown when the pointer reaches the bottom of the screen.</summary>
    Dodge,
}

/// <summary>
/// The desktop's look as the user chose it: light, dark, or on a schedule; the accent; rounded or square window
/// corners; and the wallpaper, by name, or the sky.
/// </summary>
/// <param name="Mode">Light, dark, or on a schedule.</param>
/// <param name="Accent">The accent as chosen.</param>
/// <param name="RoundedCorners">Whether windows have rounded corners.</param>
/// <param name="Wallpaper">The wallpaper bundle's name, such as <c>Earthset</c>, or <see cref="Sky"/>.</param>
/// <param name="DarkFrom">When a scheduled look turns dark, as a time of day.</param>
/// <param name="DarkUntil">When it turns light again; before <paramref name="DarkFrom"/>, the dark runs overnight.</param>
/// <param name="Dock">How the dock behaves.</param>
public sealed record DesktopLook(
    LookMode Mode, UIColor Accent, bool RoundedCorners, string Wallpaper, TimeSpan DarkFrom, TimeSpan DarkUntil,
    DockMode Dock = DockMode.Always)
{
    /// <summary>A dock mode as written: <c>always</c>, <c>autohide</c>, or <c>dodge</c>.</summary>
    public static string FormatDock(DockMode mode) => mode switch
    {
        DockMode.AutoHide => "autohide",
        DockMode.Dodge => "dodge",
        _ => "always",
    };

    /// <summary>Reads a dock mode as written, or gives <see langword="false"/>.</summary>
    public static bool TryParseDock(string? text, out DockMode mode)
    {
        (var known, mode) = text switch
        {
            "always" => (true, DockMode.Always),
            "autohide" => (true, DockMode.AutoHide),
            "dodge" => (true, DockMode.Dodge),
            _ => (false, DockMode.Always),
        };
        return known;
    }

    /// <summary>The wallpaper name for the sky, the gradient the palette draws, in place of a picture.</summary>
    public const string Sky = "sky";

    /// <summary>The look a desktop starts with: light, GNOME blue, rounded corners, Earthset, and dark 7 PM to 7 AM.</summary>
    public static DesktopLook Standard { get; } = new(
        LookMode.Light, DesktopPalette.StandardAccent, RoundedCorners: true, "Earthset",
        TimeSpan.FromHours(19), TimeSpan.FromHours(7));

    /// <summary>
    /// Whether it's dark at a time of day: always for <see cref="LookMode.Dark"/>, never for
    /// <see cref="LookMode.Light"/>, and on the schedule for <see cref="LookMode.Auto"/>, which is light when the
    /// time isn't known.
    /// </summary>
    public bool IsDarkAt(TimeSpan? timeOfDay) => Mode switch
    {
        LookMode.Dark => true,
        LookMode.Auto when timeOfDay is { } time => DarkFrom <= DarkUntil
            ? time >= DarkFrom && time < DarkUntil
            : time >= DarkFrom || time < DarkUntil,
        _ => false,
    };

    /// <summary>
    /// The palette for this look at a time of day; for the standard light look, the shared
    /// <see cref="DesktopPalette.Light"/>.
    /// </summary>
    public DesktopPalette PaletteAt(TimeSpan? timeOfDay)
    {
        var dark = IsDarkAt(timeOfDay);
        return !dark && Accent == DesktopPalette.StandardAccent ? DesktopPalette.Light : DesktopPalette.Create(Accent, dark);
    }

    /// <summary>A mode as the file and scripts write it: <c>light</c>, <c>dark</c>, or <c>auto</c>.</summary>
    public static string FormatMode(LookMode mode) => mode switch
    {
        LookMode.Dark => "dark",
        LookMode.Auto => "auto",
        _ => "light",
    };

    /// <summary>Reads a mode as the file and scripts write it.</summary>
    public static bool TryParseMode(string? text, out LookMode mode)
    {
        (var known, mode) = text switch
        {
            "light" => (true, LookMode.Light),
            "dark" => (true, LookMode.Dark),
            "auto" => (true, LookMode.Auto),
            _ => (false, LookMode.Light),
        };
        return known;
    }

    /// <summary>A time of day as the file and scripts write it, 24-hour: <c>19:00</c>.</summary>
    public static string FormatTime(TimeSpan time) =>
        string.Create(CultureInfo.InvariantCulture, $"{time.Hours:00}:{time.Minutes:00}");

    /// <summary>Reads a time of day written <c>HH:MM</c>, 24-hour, from <c>00:00</c> to <c>23:59</c>.</summary>
    public static bool TryParseTime(string? text, out TimeSpan time)
    {
        time = default;
        if (text is not { Length: 5 } || text[2] != ':'
            || !int.TryParse(text.AsSpan(0, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var hours)
            || !int.TryParse(text.AsSpan(3, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var minutes)
            || hours > 23 || minutes > 59)
            return false;

        time = new TimeSpan(hours, minutes, 0);
        return true;
    }

    /// <summary>
    /// Whether a wallpaper name can name a bundle: <see cref="Sky"/>, or a plain name without a slash or a dot at
    /// its start, which would leave the wallpapers folder.
    /// </summary>
    public static bool IsWallpaperName(string? name) =>
        !string.IsNullOrWhiteSpace(name) && name.Length <= 64 && !name.Contains('/') && !name.Contains('\\')
        && name[0] != '.' && name.Trim() == name;
}
