using System.Globalization;
using Venture.OS.Interface.Xml;

namespace Venture.OS.Time;

/// <summary>What <see cref="WallClock.Load"/> found: a line for the boot log, and why it failed, if it did.</summary>
public sealed record ClockLoad(string Description, string? Problem);

/// <summary>
/// The time of day as people read it: the hardware clock, which keeps UTC, moved by the offset the user chose and
/// kept in <c>time.xml</c>, so the menu bar, Settings, and the <c>clock</c> command share one choice.
/// </summary>
/// <remarks>
/// <para>There's no time zone database, so there's no daylight saving: the offset is whole hours or quarter hours,
/// from UTC−12:00 to UTC+14:00, and the user changes it when the clocks change. Without a saved choice it's UTC.</para>
/// <para>The file is one element, <c>&lt;Time version="1" utcOffset="-05:00"/&gt;</c>. Unknown attributes are
/// skipped, so a newer VentureOS can add some; saving writes only the ones this version knows. A newer
/// <c>version</c> or a bad offset refuses the whole file: the offset stays as it was and the reason is reported.</para>
/// <para>A hardware clock that was never set reads as 1970; before 2000 counts as not set, so nothing shows a time
/// that's plainly wrong.</para>
/// </remarks>
public sealed class WallClock
{
    /// <summary>The newest file version this reads.</summary>
    public const int SupportedVersion = 1;

    /// <summary>The farthest behind and ahead of UTC a clock can be set.</summary>
    public static readonly TimeSpan MinimumOffset = TimeSpan.FromHours(-12);

    /// <inheritdoc cref="MinimumOffset"/>
    public static readonly TimeSpan MaximumOffset = TimeSpan.FromHours(14);

    private static readonly string[] Days = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

    private readonly Func<DateTime> readHardware;

    /// <param name="readHardware">Reads the hardware clock, in UTC.</param>
    /// <param name="settingsPath">Where the offset is kept, or <see langword="null"/> to keep it nowhere.</param>
    public WallClock(Func<DateTime> readHardware, string? settingsPath = null)
    {
        ArgumentNullException.ThrowIfNull(readHardware);
        this.readHardware = readHardware;
        SettingsPath = settingsPath;
    }

    /// <summary>Where the offset is kept, or <see langword="null"/> to keep it nowhere.</summary>
    public string? SettingsPath { get; }

    /// <summary>How far the clock is from UTC.</summary>
    public TimeSpan UtcOffset { get; private set; }

    /// <summary>The time in UTC, or <see langword="null"/> when the hardware clock isn't set.</summary>
    public DateTime? UtcNow
    {
        get
        {
            var now = readHardware();
            return now.Year >= 2000 ? DateTime.SpecifyKind(now, DateTimeKind.Utc) : null;
        }
    }

    /// <summary>The time with the offset, or <see langword="null"/> when the hardware clock isn't set.</summary>
    public DateTime? Now => UtcNow is { } utc ? DateTime.SpecifyKind(utc + UtcOffset, DateTimeKind.Unspecified) : null;

    /// <summary>Uses an offset and saves it. The offset applies even when saving fails.</summary>
    /// <returns>Why it couldn't be saved, or <see langword="null"/> when it was, or there's nowhere to save it.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The offset isn't one a clock can have.</exception>
    public string? SetUtcOffset(TimeSpan offset)
    {
        if (CheckOffset(offset) is { } problem)
            throw new ArgumentOutOfRangeException(nameof(offset), problem);

        UtcOffset = offset;
        if (SettingsPath is not { } path)
            return null;

        try
        {
            if (Path.GetDirectoryName(path) is { Length: > 0 } folder)
                Directory.CreateDirectory(folder);
            File.WriteAllText(path, Write(offset));
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return $"couldn't save the clock to {path}: {exception.Message}";
        }
    }

    /// <summary>
    /// Reads the saved offset and uses it. Without a file, the clock stays on UTC; a file that can't be read leaves it
    /// as it is too, and says why.
    /// </summary>
    public ClockLoad Load()
    {
        if (SettingsPath is not { } path || !File.Exists(path))
            return new ClockLoad($"{Describe()} (nothing saved)", null);

        try
        {
            UtcOffset = Read(File.ReadAllText(path));
        }
        catch (Exception exception) when (exception is FormatException or IOException or UnauthorizedAccessException)
        {
            return new ClockLoad(Describe(), $"couldn't read {path}: {exception.Message} Keeping {FormatZone(UtcOffset)}.");
        }

        return new ClockLoad(Describe(), null);
    }

    /// <summary>The clock in words, such as <c>Tue 3:45 PM, UTC-05:00</c>, or that the hardware clock isn't set.</summary>
    public string Describe() => Now is { } now
        ? $"{FormatMenuTime(now)}, {FormatZone(UtcOffset)}"
        : $"the hardware clock isn't set; {FormatZone(UtcOffset)}";

    /// <summary>The file's text for an offset.</summary>
    public static string Write(TimeSpan offset) => $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <!-- The clock's offset from UTC, which Settings and the clock command change. -->
        <Time version="{SupportedVersion}" utcOffset="{FormatOffset(offset)}"/>

        """;

    /// <summary>Reads the file's text into an offset.</summary>
    /// <exception cref="FormatException">The text isn't a valid clock file; the message says why.</exception>
    public static TimeSpan Read(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var root = FlatXml.Parse(text).Root;
        if (root.Name != "Time")
            throw new FormatException($"The root element must be <Time>, not <{root.Name}>.");

        var version = root["version"] ?? "1";
        if (!int.TryParse(version, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number < 1)
            throw new FormatException($"\"{version}\" isn't a version.");
        if (number > SupportedVersion)
            throw new FormatException($"This is version {number}, and this VentureOS reads up to {SupportedVersion}.");

        if (root["utcOffset"] is not { } written)
            return TimeSpan.Zero;
        if (!TryParseOffset(written, out var offset, out var problem))
            throw new FormatException(problem);
        return offset;
    }

    /// <summary>
    /// Reads an offset as people write one: <c>-05:00</c>, <c>+5:30</c>, <c>5:30</c> (ahead, without a sign),
    /// <c>-5</c>, <c>UTC-05:00</c>, or <c>UTC</c> or <c>0</c> for none.
    /// </summary>
    public static bool TryParseOffset(string text, out TimeSpan offset, out string? problem)
    {
        ArgumentNullException.ThrowIfNull(text);
        offset = TimeSpan.Zero;
        var written = text.Trim();
        if (written.Length == 0)
        {
            problem = "an offset from UTC is written like -05:00 or +5:30, and none was given";
            return false;
        }

        var rest = written.StartsWith("UTC", StringComparison.OrdinalIgnoreCase) ? written[3..] : written;
        if (rest.Length == 0 || rest == "0")
        {
            problem = null;
            return true;
        }

        // Without a sign, it's ahead of UTC, as 5:30 is +5:30; a typographic minus counts as a minus.
        var signed = rest[0] is '+' or '-' or '\u2212';
        var sign = rest[0] is '-' or '\u2212' ? -1 : 1;
        var digits = signed ? rest[1..] : rest;
        var colon = digits.IndexOf(':');
        var hoursText = colon < 0 ? digits : digits[..colon];
        var minutesText = colon < 0 ? "0" : digits[(colon + 1)..];
        if (hoursText.Length is 0 or > 2 || !int.TryParse(hoursText, NumberStyles.None, CultureInfo.InvariantCulture, out var hours)
            || (colon >= 0 && minutesText.Length != 2)
            || !int.TryParse(minutesText, NumberStyles.None, CultureInfo.InvariantCulture, out var minutes))
        {
            problem = $"an offset from UTC is written like -05:00 or +5:30, not \"{text}\"";
            return false;
        }

        var parsed = TimeSpan.FromMinutes(sign * ((hours * 60) + minutes));
        if (CheckOffset(parsed) is { } outOfRange)
        {
            problem = outOfRange;
            return false;
        }

        offset = parsed;
        problem = null;
        return true;
    }

    /// <summary>An offset as the file keeps it, such as <c>-05:00</c> or <c>+00:00</c>.</summary>
    public static string FormatOffset(TimeSpan offset)
    {
        var minutes = (int)Math.Round(Math.Abs(offset.TotalMinutes));
        return string.Create(CultureInfo.InvariantCulture, $"{(offset < TimeSpan.Zero ? '-' : '+')}{minutes / 60:00}:{minutes % 60:00}");
    }

    /// <summary>An offset as people read it, such as <c>UTC-05:00</c>, or <c>UTC</c> for none.</summary>
    public static string FormatZone(TimeSpan offset) => offset == TimeSpan.Zero ? "UTC" : $"UTC{FormatOffset(offset)}";

    /// <summary>The time as the menu bar shows it, after Tiger: <c>Tue 3:45 PM</c>.</summary>
    /// <remarks>Written out by hand rather than with a format string, so it doesn't depend on culture data.</remarks>
    public static string FormatMenuTime(DateTime time)
    {
        var hour = time.Hour % 12 == 0 ? 12 : time.Hour % 12;
        return string.Create(CultureInfo.InvariantCulture, $"{Days[(int)time.DayOfWeek]} {hour}:{time.Minute:00} {(time.Hour < 12 ? "AM" : "PM")}");
    }

    /// <summary>The short name of the day, such as <c>Tue</c>.</summary>
    public static string DayName(DateTime time) => Days[(int)time.DayOfWeek];

    private static string? CheckOffset(TimeSpan offset)
    {
        if (offset < MinimumOffset || offset > MaximumOffset)
            return $"an offset from UTC is from -12:00 to +14:00, not {FormatOffset(offset)}";
        if (offset.Ticks % TimeSpan.FromMinutes(15).Ticks != 0)
            return $"an offset from UTC is in quarter hours, such as +05:45, not {FormatOffset(offset)}";
        return null;
    }
}
