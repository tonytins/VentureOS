using System.Globalization;
using Venture.OS.Interface.Xml;

namespace Venture.OS.Desktop;

/// <summary>What <see cref="DesktopAppearance.Load"/> found: a line for the boot log, and why it failed, if it did.</summary>
public sealed record AppearanceLoad(string Description, string? Problem);

/// <summary>
/// The desktop's look as the user chose it (<see cref="DesktopLook"/>), kept in <c>appearance.xml</c>: read at boot,
/// and written on every change, so Settings and the <c>appearance</c> command share one choice.
/// </summary>
/// <remarks>
/// <para>The file is one element: <c>&lt;Appearance version="1" mode="auto" accent="#3584E4" corners="rounded"
/// wallpaper="Earthset" darkFrom="19:00" darkUntil="07:00"/&gt;</c>. Every attribute is optional, with
/// <see cref="DesktopLook.Standard"/>'s value when left out, so a file from 0.4, with only <c>mode</c> and
/// <c>accent</c>, reads as it did. Unknown attributes are skipped, so a newer VentureOS can add some; saving writes
/// only the ones this version knows, so it drops theirs. A newer <c>version</c>, or a value this version can't
/// read, refuses the whole file: the look stays as it was and the reason is reported. A VentureOS before 0.5 refuses
/// <c>mode="auto"</c> the same way, and skips the rest.</para>
/// <para>VentureOS runs from RAM, so the file lasts until reboot, apart from the one shipped in the user's files;
/// once files are kept across reboots, the choice is too.</para>
/// </remarks>
public static class DesktopAppearance
{
    /// <summary>The newest file version this reads.</summary>
    public const int SupportedVersion = 1;

    /// <summary>Where the choice is kept, or <see langword="null"/> to keep it nowhere, as in a test.</summary>
    public static string? SettingsPath { get; set; }

    /// <summary>
    /// Where the wallpaper bundles are, such as <c>/mnt/system/wallpapers</c>, which the desktop shows the look's from
    /// and Settings offers; <see langword="null"/> for none, which shows the sky.
    /// </summary>
    public static string? WallpapersFolder { get; set; }

    /// <summary>
    /// The time of day, for a look on a schedule, or <see langword="null"/> when it isn't known, which shows the light
    /// look. The kernel gives the clock's.
    /// </summary>
    public static Func<TimeSpan?> TimeOfDay { get; set; } = () => null;

    /// <summary>The look as chosen, whose palette <see cref="DesktopTheme"/> draws with.</summary>
    public static DesktopLook Current { get; private set; } = DesktopLook.Standard;

    /// <summary>Uses a look without saving it, such as a test putting the standard one back.</summary>
    /// <remarks>
    /// The palette showing is kept when it already matches, since the desktop redraws its sky, which is slow, when
    /// the palette changes.
    /// </remarks>
    public static void Use(DesktopLook look)
    {
        ArgumentNullException.ThrowIfNull(look);
        Current = look;
        var dark = look.IsDarkAt(TimeOfDay());
        var showing = DesktopTheme.Palette;
        if (showing.IsDark != dark || showing.ChosenAccent != look.Accent
            || (!dark && look.Accent == DesktopPalette.StandardAccent && !ReferenceEquals(showing, DesktopPalette.Light)))
            DesktopTheme.Use(look.PaletteAt(TimeOfDay()));
    }

    /// <summary>
    /// Switches a look on a schedule to light or dark when the time says so; any other look stays as it is. The
    /// desktop calls it as each minute turns. It allocates nothing unless the palette changes.
    /// </summary>
    /// <returns>Whether the palette changed.</returns>
    public static bool Refresh()
    {
        var showing = DesktopTheme.Palette;
        Use(Current);
        return !ReferenceEquals(showing, DesktopTheme.Palette);
    }

    /// <summary>Uses a look and saves it. The look applies even when saving fails.</summary>
    /// <returns>Why it couldn't be saved, or <see langword="null"/> when it was, or there's nowhere to save it.</returns>
    public static string? Apply(DesktopLook look)
    {
        Use(look);
        if (SettingsPath is not { } path)
            return null;

        try
        {
            if (Path.GetDirectoryName(path) is { Length: > 0 } folder)
                Directory.CreateDirectory(folder);
            File.WriteAllText(path, Write(look));
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return $"couldn't save the appearance to {path}: {exception.Message}";
        }
    }

    /// <summary>Uses a palette's mode and accent, keeping the rest of the look, and saves it.</summary>
    /// <returns>Why it couldn't be saved, or <see langword="null"/>.</returns>
    public static string? Apply(DesktopPalette palette)
    {
        ArgumentNullException.ThrowIfNull(palette);
        return Apply(Current with { Mode = palette.IsDark ? LookMode.Dark : LookMode.Light, Accent = palette.ChosenAccent });
    }

    /// <summary>
    /// Reads the saved choice and uses it. Without a file, the look stays as it is; a file that can't be read
    /// leaves it as it is too, and says why.
    /// </summary>
    public static AppearanceLoad Load()
    {
        if (SettingsPath is not { } path || !File.Exists(path))
            return new AppearanceLoad($"{Describe(Current)} (nothing saved)", null);

        DesktopLook look;
        try
        {
            look = Read(File.ReadAllText(path));
        }
        catch (Exception exception) when (exception is FormatException or IOException or UnauthorizedAccessException)
        {
            return new AppearanceLoad(
                Describe(Current), $"couldn't read {path}: {exception.Message} Keeping the standard look.");
        }

        Use(look);
        return new AppearanceLoad(Describe(look), null);
    }

    /// <summary>The file's text for a look.</summary>
    public static string Write(DesktopLook look)
    {
        ArgumentNullException.ThrowIfNull(look);
        return string.Create(CultureInfo.InvariantCulture, $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <!-- The desktop's look, which Settings and the appearance command change. -->
            <Appearance version="{SupportedVersion}" mode="{DesktopLook.FormatMode(look.Mode)}" accent="{DesktopPalette.Format(look.Accent)}" corners="{(look.RoundedCorners ? "rounded" : "square")}" wallpaper="{look.Wallpaper}" darkFrom="{DesktopLook.FormatTime(look.DarkFrom)}" darkUntil="{DesktopLook.FormatTime(look.DarkUntil)}" dock="{DesktopLook.FormatDock(look.Dock)}" scrollbars="{DesktopLook.FormatScrollBars(look.ScrollBars)}"/>

            """);
    }

    /// <summary>Reads the file's text into a look; what it leaves out is the standard look's.</summary>
    /// <exception cref="FormatException">The text isn't a valid appearance file; the message says why.</exception>
    public static DesktopLook Read(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var root = FlatXml.Parse(text).Root;
        if (root.Name != "Appearance")
            throw new FormatException($"The root element must be <Appearance>, not <{root.Name}>.");

        var version = root["version"] ?? "1";
        if (!int.TryParse(version, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number < 1)
            throw new FormatException($"\"{version}\" isn't a version.");
        if (number > SupportedVersion)
            throw new FormatException($"This is version {number}, and this VentureOS reads up to {SupportedVersion}.");

        var look = DesktopLook.Standard;
        if (root["mode"] is { } mode)
        {
            if (!DesktopLook.TryParseMode(mode, out var parsed))
                throw new FormatException($"The mode is light, dark, or auto, not \"{mode}\".");
            look = look with { Mode = parsed };
        }

        if (root["accent"] is { } written)
        {
            if (!DesktopPalette.TryParseColor(written, out var accent))
                throw new FormatException($"An accent is written #RRGGBB, not \"{written}\".");
            look = look with { Accent = accent };
        }

        look = root["corners"] switch
        {
            null => look,
            "rounded" => look with { RoundedCorners = true },
            "square" => look with { RoundedCorners = false },
            var corners => throw new FormatException($"Corners are rounded or square, not \"{corners}\"."),
        };

        // A dock mode this VentureOS doesn't know is a newer one's; the dock stays always visible, as it always was.
        look = look with { Dock = DesktopLook.TryParseDock(root["dock"], out var dock) ? dock : DockMode.Always };

        // Likewise a scroll bar style; scroll bars stay classic, always shown, so nothing that scrolls looks as if it doesn't.
        look = look with { ScrollBars = DesktopLook.TryParseScrollBars(root["scrollbars"], out var bars) ? bars : ScrollBarStyle.Classic };

        if (root["wallpaper"] is { } wallpaper)
        {
            if (!DesktopLook.IsWallpaperName(wallpaper))
                throw new FormatException($"\"{wallpaper}\" isn't a wallpaper's name.");
            look = look with { Wallpaper = wallpaper };
        }

        return look with { DarkFrom = Time(root["darkFrom"], "darkFrom", look.DarkFrom), DarkUntil = Time(root["darkUntil"], "darkUntil", look.DarkUntil) };
    }

    /// <summary>A look in words, such as <c>dark, accent #E8922A</c>.</summary>
    public static string Describe(DesktopLook look)
    {
        ArgumentNullException.ThrowIfNull(look);
        return $"{DesktopLook.FormatMode(look.Mode)}, accent {DesktopPalette.Format(look.Accent)}";
    }

    private static TimeSpan Time(string? written, string attribute, TimeSpan standard)
    {
        if (written is null)
            return standard;
        if (!DesktopLook.TryParseTime(written, out var time))
            throw new FormatException($"{attribute} is a time written HH:MM, such as 19:00, not \"{written}\".");
        return time;
    }
}
