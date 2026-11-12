using System.Globalization;
using Venture.OS.Interface;

namespace Venture.OS.Desktop;

/// <summary>
/// Every color the desktop draws with, built from one accent color, light or dark, after Distro Tycoon 2's
/// appearance: tinted colors take the accent's hue and a share of its saturation, so any accent gives a matching
/// desktop, and neutral ones (text, panels, borders) stay put.
/// </summary>
/// <remarks>
/// <para>The light palette for <see cref="StandardAccent"/> is the desktop's look from before palettes existed, to
/// within a shade, so nothing changes for anyone who doesn't change it.</para>
/// <para>The accent itself is adjusted to stay readable: darkened on the light palette, so it reads on white, and
/// lightened on the dark one, so it reads on the dark panel. <see cref="ChosenAccent"/> keeps the color as chosen,
/// for showing and saving.</para>
/// </remarks>
public sealed record DesktopPalette
{
    /// <summary>The accent the desktop starts with, a clear sky blue.</summary>
    public static readonly UIColor StandardAccent = new(30, 120, 220);

    // How bright the accent may be on the light palette, and must be on the dark one, by DesktopPalette.Brightness.
    private const float LightAccentLimit = 150;
    private const float DarkAccentLimit = 120;

    private static readonly UIColor White = new(255, 255, 255);

    /// <summary>The light palette with the standard accent: the desktop's look until something changes it.</summary>
    public static DesktopPalette Light { get; } = Create(StandardAccent, isDark: false);

    /// <summary>
    /// Ready-made accents, after Distro Tycoon 2's: the standard sky blue, then colors after the fruit-colored
    /// desktops of the era, and graphite.
    /// </summary>
    public static IReadOnlyList<UIColor> Swatches { get; } =
    [
        StandardAccent,
        FromHsv(214, 0.85f, 0.75f),
        FromHsv(275, 0.6f, 0.75f),
        FromHsv(350, 0.7f, 0.88f),
        FromHsv(28, 0.9f, 0.95f),
        FromHsv(95, 0.7f, 0.75f),
        FromHsv(178, 0.8f, 0.65f),
        FromHsv(215, 0.1f, 0.55f),
    ];

    private DesktopPalette()
    {
    }

    /// <summary>Whether it's the dark palette.</summary>
    public bool IsDark { get; private init; }

    /// <summary>The accent as chosen, before it was adjusted to stay readable.</summary>
    public UIColor ChosenAccent { get; private init; }

    public UIColor Accent { get; private init; }
    public UIColor Text { get; private init; }
    public UIColor SecondaryText { get; private init; }
    public UIColor DisabledText { get; private init; }
    public UIColor Panel { get; private init; }
    public UIColor Border { get; private init; }
    public UIColor InputBackground { get; private init; }
    public UIColor Selection { get; private init; }
    public UIColor SidebarBackground { get; private init; }
    public UIColor SidebarSelection { get; private init; }
    public UIColor SidebarHover { get; private init; }
    public UIColor ButtonHover { get; private init; }
    public UIColor TitleBarTop { get; private init; }
    public UIColor TitleBarBottom { get; private init; }
    public UIColor CloseButton { get; private init; }
    public UIColor CloseMark { get; private init; }
    public UIColor MinimizeButton { get; private init; }
    public UIColor MinimizeMark { get; private init; }
    public UIColor ButtonPressed { get; private init; }
    public UIColor MissingImage { get; private init; }
    public UIColor Shadow { get; private init; }
    public UIColor InactiveFade { get; private init; }
    public UIColor MenuBarBottom { get; private init; }
    public UIColor MenuBackground { get; private init; }
    public UIColor HighlightedText { get; private init; }
    public UIColor Separator { get; private init; }
    public UIColor DockShelf { get; private init; }
    public UIColor DockEdge { get; private init; }
    public UIColor OpenAppDot { get; private init; }
    public UIColor DockLabel { get; private init; }
    public UIColor IconPressed { get; private init; }

    /// <summary>The top of the desktop's sky.</summary>
    public UIColor SkyTop { get; private init; }

    /// <summary>The bottom of the desktop's sky.</summary>
    public UIColor SkyBottom { get; private init; }

    /// <summary>How strong the white glow rising from the bottom of the sky is, from 0 to 1.</summary>
    public float SkyGlow { get; private init; }

    /// <summary>Builds the light or dark palette for an accent.</summary>
    public static DesktopPalette Create(UIColor accent, bool isDark)
    {
        accent = accent with { Alpha = 255 };
        var (hue, saturation, _) = ToHsv(accent);

        // A tinted color: the accent's hue, a share of its saturation, and a brightness of its own.
        UIColor Tint(float share, float value) => FromHsv(hue, saturation * share, value);

        var readable = Readable(accent, isDark);
        var text = isDark ? new UIColor(228, 231, 236) : new UIColor(24, 28, 36);
        var common = new DesktopPalette
        {
            IsDark = isDark,
            ChosenAccent = accent,
            Accent = readable,
            Text = text,
            HighlightedText = Brightness(readable) > 160 ? new UIColor(24, 28, 36) : White,
            CloseButton = new UIColor(236, 95, 86),
            CloseMark = new UIColor(90, 20, 16),
            MinimizeButton = new UIColor(245, 178, 50),
            MinimizeMark = new UIColor(120, 82, 12),
            MissingImage = new UIColor(220, 30, 30),
            IconPressed = new UIColor(0, 0, 0, 60),
            OpenAppDot = text,
        };

        return isDark
            ? common with
            {
                SecondaryText = new UIColor(150, 156, 166),
                DisabledText = new UIColor(100, 105, 113),
                Panel = new UIColor(38, 41, 46),
                Border = new UIColor(88, 94, 104),
                InputBackground = new UIColor(26, 28, 32),
                Separator = new UIColor(66, 70, 78),
                Selection = Tint(0.55f, 0.48f),
                SidebarBackground = Tint(0.12f, 0.2f),
                SidebarSelection = Tint(0.4f, 0.34f),
                SidebarHover = Tint(0.22f, 0.27f),
                ButtonHover = Tint(0.14f, 0.38f),
                TitleBarTop = Tint(0.06f, 0.31f),
                TitleBarBottom = Tint(0.1f, 0.23f),
                ButtonPressed = Tint(0.45f, 0.4f),
                MenuBarBottom = Tint(0.08f, 0.2f),
                MenuBackground = Tint(0.05f, 0.21f),
                Shadow = new UIColor(0, 0, 0, 120),
                InactiveFade = new UIColor(24, 26, 30, 100),
                DockShelf = new UIColor(255, 255, 255, 40),
                DockEdge = new UIColor(255, 255, 255, 90),
                DockLabel = new UIColor(0, 0, 0, 170),
                SkyTop = Tint(0.85f, 0.18f),
                SkyBottom = Tint(0.45f, 0.4f),
                SkyGlow = 0.18f,
            }
            : common with
            {
                SecondaryText = new UIColor(110, 116, 126),
                DisabledText = new UIColor(150, 154, 160),
                Panel = new UIColor(240, 243, 247),
                Border = new UIColor(120, 126, 136),
                InputBackground = White,
                Separator = new UIColor(214, 218, 224),

                // The shares and brightnesses that give the standard accent the desktop's original colors.
                Selection = Tint(0.402f, 0.949f),
                SidebarBackground = Tint(0.077f, 0.941f),
                SidebarSelection = Tint(0.226f, 0.925f),
                SidebarHover = Tint(0.128f, 0.925f),
                ButtonHover = Tint(0.123f, 0.957f),
                TitleBarTop = Tint(0.014f, 0.992f),
                TitleBarBottom = Tint(0.154f, 0.886f),
                ButtonPressed = Tint(0.295f, 0.894f),
                MenuBarBottom = Tint(0.069f, 0.925f),
                MenuBackground = Tint(0.014f, 0.992f),
                Shadow = new UIColor(0, 0, 0, 60),
                InactiveFade = new UIColor(255, 255, 255, 90),
                DockShelf = new UIColor(255, 255, 255, 70),
                DockEdge = new UIColor(255, 255, 255, 150),
                DockLabel = new UIColor(0, 0, 0, 150),
                SkyTop = Tint(0.893f, 0.549f),
                SkyBottom = Tint(0.297f, 0.949f),
                SkyGlow = 0.55f,
            };
    }

    /// <summary>Reads a color written as <c>#RRGGBB</c>, as the <c>appearance</c> command and Settings take it.</summary>
    public static bool TryParseColor(string? text, out UIColor color)
    {
        color = default;
        if (text is not { Length: 7 } || text[0] != '#')
            return false;

        foreach (var digit in text.AsSpan(1))
        {
            if (!char.IsAsciiHexDigit(digit))
                return false;
        }

        var rgb = int.Parse(text.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
        color = new UIColor((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
        return true;
    }

    /// <summary>Writes a color as <c>#RRGGBB</c>, ignoring its alpha.</summary>
    public static string Format(UIColor color) =>
        string.Create(CultureInfo.InvariantCulture, $"#{color.Red:X2}{color.Green:X2}{color.Blue:X2}");

    /// <summary>How bright a color looks, from 0 to 255, weighting green most, as the eye does.</summary>
    public static float Brightness(UIColor color) =>
        (0.299f * color.Red) + (0.587f * color.Green) + (0.114f * color.Blue);

    /// <summary>
    /// The accent made readable: on the light palette, no brighter than <see cref="LightAccentLimit"/>; on the dark
    /// one, at least <see cref="DarkAccentLimit"/>, brightening first and then giving up saturation if brightening
    /// alone can't get there. Each loop takes at most 50 steps.
    /// </summary>
    private static UIColor Readable(UIColor accent, bool isDark)
    {
        var (hue, saturation, value) = ToHsv(accent);
        var color = accent;
        for (var step = 0; step < 50; step++)
        {
            var fine = isDark ? Brightness(color) >= DarkAccentLimit : Brightness(color) <= LightAccentLimit;
            if (fine)
                break;

            if (!isDark)
                value = Math.Max(0, value - 0.02f);
            else if (value < 1)
                value = Math.Min(1, value + 0.02f);
            else
                saturation = Math.Max(0, saturation - 0.02f);

            color = FromHsv(hue, saturation, value);
        }

        return color;
    }

    private static (float Hue, float Saturation, float Value) ToHsv(UIColor color)
    {
        var (red, green, blue) = (color.Red / 255f, color.Green / 255f, color.Blue / 255f);
        var max = Math.Max(red, Math.Max(green, blue));
        var min = Math.Min(red, Math.Min(green, blue));
        var range = max - min;

        float hue;
        if (range == 0)
            hue = 0;
        else if (max == red)
            hue = 60 * (((green - blue) / range) % 6);
        else if (max == green)
            hue = 60 * (((blue - red) / range) + 2);
        else
            hue = 60 * (((red - green) / range) + 4);

        return (hue < 0 ? hue + 360 : hue, max == 0 ? 0 : range / max, max);
    }

    private static UIColor FromHsv(float hue, float saturation, float value)
    {
        saturation = Math.Clamp(saturation, 0, 1);
        value = Math.Clamp(value, 0, 1);
        var chroma = value * saturation;
        var sector = ((hue % 360) + 360) % 360 / 60;
        var x = chroma * (1 - Math.Abs((sector % 2) - 1));
        var (red, green, blue) = (int)sector switch
        {
            0 => (chroma, x, 0f),
            1 => (x, chroma, 0f),
            2 => (0f, chroma, x),
            3 => (0f, x, chroma),
            4 => (x, 0f, chroma),
            _ => (chroma, 0f, x),
        };

        var lift = value - chroma;
        return new UIColor(Channel(red + lift), Channel(green + lift), Channel(blue + lift));
    }

    private static byte Channel(float value) => (byte)MathF.Round(Math.Clamp(value, 0, 1) * 255);
}
