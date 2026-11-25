using System.Globalization;
using Venture.OS.Interface;

namespace Venture.OS.Desktop;

/// <summary>
/// Every color the desktop draws with, light or dark, with one accent color, after GNOME's libadwaita: surfaces,
/// text, and lines are neutral, a faint blue-gray, and only the accent carries color, where it marks something:
/// selection, the open menu's highlight, and what the pointer can act on.
/// </summary>
/// <remarks>
/// <para>Surfaces and text take libadwaita's values; where libadwaita lays a translucent color over a surface, such
/// as its 15% borders, the color here is that blend, worked out once, since the canvas can't blend every line. Until
/// 0.5, the palette followed Distro Tycoon 2, with the accent's hue tinting every surface.</para>
/// <para>The accent itself is adjusted to stay readable: darkened on the light palette, so it reads on white, and
/// lightened on the dark one, so it reads on the dark panel. <see cref="ChosenAccent"/> keeps the color as chosen,
/// for showing and saving. Only the sky, which shows without a wallpaper, still takes the accent's hue.</para>
/// </remarks>
public sealed record DesktopPalette
{
    /// <summary>The accent the desktop starts with: GNOME's blue.</summary>
    public static readonly UIColor StandardAccent = new(0x35, 0x84, 0xE4);

    // How bright the accent may be on the light palette, and must be on the dark one, by DesktopPalette.Brightness.
    private const float LightAccentLimit = 150;
    private const float DarkAccentLimit = 120;

    private static readonly UIColor White = new(255, 255, 255);

    /// <summary>The light palette with the standard accent: the desktop's look until something changes it.</summary>
    public static DesktopPalette Light { get; } = Create(StandardAccent, isDark: false);

    /// <summary>
    /// Ready-made accents, GNOME's nine, in its order: blue (the standard), teal, green, yellow, orange, red, pink,
    /// purple, and slate.
    /// </summary>
    public static IReadOnlyList<UIColor> Swatches { get; } =
    [
        StandardAccent,
        new(0x21, 0x90, 0xA4),
        new(0x3A, 0x94, 0x4A),
        new(0xC8, 0x88, 0x00),
        new(0xED, 0x5B, 0x00),
        new(0xE6, 0x2D, 0x42),
        new(0xD5, 0x61, 0x99),
        new(0x91, 0x41, 0xAC),
        new(0x6F, 0x83, 0x96),
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
    public UIColor ZoomButton { get; private init; }
    public UIColor ZoomMark { get; private init; }
    public UIColor ButtonPressed { get; private init; }
    public UIColor MissingImage { get; private init; }
    public UIColor Shadow { get; private init; }
    public UIColor InactiveFade { get; private init; }
    public UIColor MenuBarBottom { get; private init; }

    /// <summary>A window's flat header bar, which blends with the content under it.</summary>
    public UIColor HeaderBar { get; private init; }

    /// <summary>The menu bar's gloss: white laid over its top half, Tiger's bright band.</summary>
    public UIColor MenuBarGloss { get; private init; }

    /// <summary>The sheen along the menu bar's top edge, where the light catches it.</summary>
    public UIColor MenuBarSheen { get; private init; }
    public UIColor MenuBackground { get; private init; }
    public UIColor HighlightedText { get; private init; }
    public UIColor Separator { get; private init; }
    public UIColor DockShelf { get; private init; }
    public UIColor DockEdge { get; private init; }
    public UIColor OpenAppDot { get; private init; }
    public UIColor DockLabel { get; private init; }

    /// <summary>
    /// A toast's and a history card's body: the dock's shelf made nearly opaque, so <see cref="Text"/> and
    /// <see cref="SecondaryText"/> read on it over any wallpaper. Light in the light palette and dark in the dark one.
    /// </summary>
    public UIColor NotificationBackground { get; private init; }

    /// <summary>The bright edge around a toast or a card, the dock's.</summary>
    public UIColor NotificationEdge { get; private init; }
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

        // A tinted color, for the sky: the accent's hue, a share of its saturation, and a brightness of its own.
        UIColor Tint(float share, float value) => FromHsv(hue, saturation * share, value);

        var readable = Readable(accent, isDark);

        // libadwaita's text: 80% of near-black on light, white on dark.
        var text = isDark ? White : new UIColor(51, 51, 56);
        var common = new DesktopPalette
        {
            IsDark = isDark,
            ChosenAccent = accent,
            Accent = readable,
            Text = text,
            HighlightedText = Brightness(readable) > 160 ? new UIColor(51, 51, 56) : White,
            CloseButton = new UIColor(236, 95, 86),
            CloseMark = new UIColor(90, 20, 16),
            MinimizeButton = new UIColor(245, 178, 50),
            MinimizeMark = new UIColor(120, 82, 12),
            ZoomButton = new UIColor(98, 197, 85),
            ZoomMark = new UIColor(22, 88, 20),
            MissingImage = new UIColor(220, 30, 30),
            IconPressed = new UIColor(0, 0, 0, 60),
            OpenAppDot = text,
        };

        // Each surface, then what's laid over it, as libadwaita names them; the comments give its translucent value.
        return isDark
            ? common with
            {
                Panel = new UIColor(0x22, 0x22, 0x26),               // window-bg
                HeaderBar = new UIColor(0x2E, 0x2E, 0x32),           // headerbar-bg
                InputBackground = new UIColor(0x1D, 0x1D, 0x20),     // view-bg
                SidebarBackground = new UIColor(0x2E, 0x2E, 0x32),   // sidebar-bg
                MenuBackground = new UIColor(0x36, 0x36, 0x3A),      // popover-bg
                SecondaryText = new UIColor(156, 156, 158),          // text at the dim opacity, 55%
                DisabledText = new UIColor(145, 145, 147),           // text at the disabled opacity, 50%
                Border = new UIColor(78, 78, 82),                    // text at 20%, for outlines
                Separator = new UIColor(29, 29, 34),                 // headerbar-shade, 36% near-black
                Selection = Mix(new UIColor(0x1D, 0x1D, 0x20), accent, 0.35f),
                SidebarSelection = new UIColor(67, 67, 71),          // text at 10% on the sidebar
                SidebarHover = new UIColor(61, 61, 65),              // text at 7%
                ButtonHover = new UIColor(61, 61, 64),               // text at 7% on the header bar
                ButtonPressed = new UIColor(79, 79, 82),             // text at 16%
                TitleBarTop = new UIColor(64, 64, 68),               // buttons' and the menu bar's gradient
                TitleBarBottom = new UIColor(52, 52, 56),
                MenuBarBottom = new UIColor(0x2E, 0x2E, 0x32),
                MenuBarGloss = new UIColor(255, 255, 255, 18),
                MenuBarSheen = new UIColor(255, 255, 255, 46),
                Shadow = new UIColor(0, 0, 0, 120),
                InactiveFade = new UIColor(24, 24, 28, 100),
                DockShelf = new UIColor(255, 255, 255, 40),
                DockEdge = new UIColor(255, 255, 255, 90),
                NotificationBackground = new UIColor(34, 34, 38, 215),
                NotificationEdge = new UIColor(255, 255, 255, 90),
                DockLabel = new UIColor(0, 0, 0, 170),
                SkyTop = Tint(0.85f, 0.18f),
                SkyBottom = Tint(0.45f, 0.4f),
                SkyGlow = 0.18f,
            }
            : common with
            {
                Panel = new UIColor(0xFA, 0xFA, 0xFB),               // window-bg
                HeaderBar = White,                                   // headerbar-bg
                InputBackground = White,                             // view-bg
                SidebarBackground = new UIColor(0xEB, 0xEB, 0xED),   // sidebar-bg
                MenuBackground = White,                              // popover-bg
                SecondaryText = new UIColor(143, 143, 146),          // text at the dim opacity, 55%
                DisabledText = new UIColor(153, 153, 156),           // text at the disabled opacity, 50%
                Border = new UIColor(205, 205, 208),                 // text at 20%, for outlines
                Separator = new UIColor(224, 224, 225),              // headerbar-shade, 12% near-black
                Selection = Mix(White, accent, 0.3f),
                SidebarSelection = new UIColor(216, 216, 218),       // text at 10% on the sidebar
                SidebarHover = new UIColor(222, 222, 224),           // text at 7%
                ButtonHover = new UIColor(241, 241, 242),            // text at 7% on the header bar
                ButtonPressed = new UIColor(222, 222, 223),          // text at 16%
                TitleBarTop = new UIColor(252, 252, 253),            // buttons' and the menu bar's gradient
                TitleBarBottom = new UIColor(227, 227, 229),
                MenuBarBottom = new UIColor(222, 222, 224),          // low enough for the gloss's band to show
                MenuBarGloss = new UIColor(255, 255, 255, 120),
                MenuBarSheen = new UIColor(255, 255, 255, 230),
                Shadow = new UIColor(0, 0, 0, 60),
                InactiveFade = new UIColor(255, 255, 255, 90),
                DockShelf = new UIColor(255, 255, 255, 70),
                DockEdge = new UIColor(255, 255, 255, 150),
                // Opaque white: the light palette's secondary text reaches 3 to 1 on white, and only just.
                NotificationBackground = new UIColor(255, 255, 255, 255),
                NotificationEdge = new UIColor(255, 255, 255, 150),
                DockLabel = new UIColor(0, 0, 0, 150),
                SkyTop = Tint(0.893f, 0.549f),
                SkyBottom = Tint(0.297f, 0.949f),
                SkyGlow = 0.55f,
            };
    }

    /// <summary>A color part of the way from one to another, as a translucent layer over a surface.</summary>
    private static UIColor Mix(UIColor under, UIColor over, float amount) => new(
        (byte)MathF.Round(under.Red + ((over.Red - under.Red) * amount)),
        (byte)MathF.Round(under.Green + ((over.Green - under.Green) * amount)),
        (byte)MathF.Round(under.Blue + ((over.Blue - under.Blue) * amount)));

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
