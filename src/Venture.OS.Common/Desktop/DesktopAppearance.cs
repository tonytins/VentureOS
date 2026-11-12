using System.Globalization;
using Venture.OS.Interface.Xml;

namespace Venture.OS.Desktop;

/// <summary>What <see cref="DesktopAppearance.Load"/> found: a line for the boot log, and why it failed, if it did.</summary>
public sealed record AppearanceLoad(string Description, string? Problem);

/// <summary>
/// The desktop's look as the user chose it, kept in <c>appearance.xml</c>: read at boot, and written on every change,
/// so Settings and the <c>appearance</c> command share one choice.
/// </summary>
/// <remarks>
/// <para>The file is one element, <c>&lt;Appearance version="1" mode="dark" accent="#E8922A"/&gt;</c>. A left-out
/// <c>mode</c> is light and a left-out <c>accent</c> the standard one. Unknown attributes are skipped, so a newer
/// VentureOS can add some; saving writes only the ones this version knows, so it drops theirs. A newer
/// <c>version</c>, an unknown mode, or a bad accent refuses the whole file: the look stays as it was and the reason
/// is reported.</para>
/// <para>VentureOS runs from RAM, so the file lasts until reboot, apart from the one shipped in the user's files;
/// once files are kept across reboots (Blue Water), the choice is too.</para>
/// </remarks>
public static class DesktopAppearance
{
    /// <summary>The newest file version this reads.</summary>
    public const int SupportedVersion = 1;

    /// <summary>Where the choice is kept, or <see langword="null"/> to keep it nowhere, as in a test.</summary>
    public static string? SettingsPath { get; set; }

    /// <summary>
    /// Uses a palette and saves it. The palette applies even when saving fails.
    /// </summary>
    /// <returns>Why it couldn't be saved, or <see langword="null"/> when it was, or there's nowhere to save it.</returns>
    public static string? Apply(DesktopPalette palette)
    {
        ArgumentNullException.ThrowIfNull(palette);
        DesktopTheme.Use(palette);
        if (SettingsPath is not { } path)
            return null;

        try
        {
            if (Path.GetDirectoryName(path) is { Length: > 0 } folder)
                Directory.CreateDirectory(folder);
            File.WriteAllText(path, Write(palette));
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return $"couldn't save the appearance to {path}: {exception.Message}";
        }
    }

    /// <summary>
    /// Reads the saved choice and uses it. Without a file, the look stays as it is; a file that can't be read
    /// leaves it as it is too, and says why.
    /// </summary>
    public static AppearanceLoad Load()
    {
        if (SettingsPath is not { } path || !File.Exists(path))
            return new AppearanceLoad($"{Describe(DesktopTheme.Palette)} (nothing saved)", null);

        DesktopPalette palette;
        try
        {
            palette = Read(File.ReadAllText(path));
        }
        catch (Exception exception) when (exception is FormatException or IOException or UnauthorizedAccessException)
        {
            return new AppearanceLoad(
                Describe(DesktopTheme.Palette), $"couldn't read {path}: {exception.Message} Keeping the standard look.");
        }

        DesktopTheme.Use(palette);
        return new AppearanceLoad(Describe(palette), null);
    }

    /// <summary>The file's text for a palette.</summary>
    public static string Write(DesktopPalette palette)
    {
        ArgumentNullException.ThrowIfNull(palette);
        return string.Create(CultureInfo.InvariantCulture, $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <!-- The desktop's look, which Settings and the appearance command change. -->
            <Appearance version="{SupportedVersion}" mode="{Mode(palette)}" accent="{DesktopPalette.Format(palette.ChosenAccent)}"/>

            """);
    }

    /// <summary>Reads the file's text into a palette.</summary>
    /// <exception cref="FormatException">The text isn't a valid appearance file; the message says why.</exception>
    public static DesktopPalette Read(string text)
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

        var dark = root["mode"] switch
        {
            null or "light" => false,
            "dark" => true,
            var mode => throw new FormatException($"The mode is light or dark, not \"{mode}\"."),
        };

        var accent = DesktopPalette.StandardAccent;
        if (root["accent"] is { } written && !DesktopPalette.TryParseColor(written, out accent))
            throw new FormatException($"An accent is written #RRGGBB, not \"{written}\".");

        return DesktopPalette.Create(accent, dark);
    }

    /// <summary>A palette in words, such as <c>dark, accent #E8922A</c>.</summary>
    public static string Describe(DesktopPalette palette)
    {
        ArgumentNullException.ThrowIfNull(palette);
        return $"{Mode(palette)}, accent {DesktopPalette.Format(palette.ChosenAccent)}";
    }

    private static string Mode(DesktopPalette palette) => palette.IsDark ? "dark" : "light";
}
