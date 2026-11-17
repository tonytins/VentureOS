using Venture.OS.Desktop;
using Venture.OS.Interface;

namespace Venture.OS.Tests.Desktop;

/// <summary>
/// Every interface VentureOS ships stays readable in every palette: light and dark, with each of the accent swatches.
/// It draws each one in a window, with sample text in every control an app fills as it runs, and checks each piece of text against what's
/// behind it. The recording painter keeps text without painting it, so the pixel under a text is its background.
/// </summary>
/// <remarks>
/// The bar is a contrast of 3 to 1, the WCAG minimum for large text and interface parts; secondary text is meant to
/// be quieter than 4.5 to 1 allows. A change to the theme or a shipped style that makes an app hard to read fails
/// here before anyone boots.
/// <para>A control that overlaps another is drawn on its own, the rest hidden, since an app may stack controls it
/// shows one group at a time, as Settings stacks its panes; the others are drawn together.</para>
/// </remarks>
[Collection(nameof(AppearanceCollection))]
public sealed class ShippedContrastTests : IDisposable
{
    private const double Minimum = 3;

    public void Dispose() => DesktopTheme.Use(DesktopPalette.Light);

    public static TheoryData<string> Interfaces()
    {
        TheoryData<string> found = [];
        foreach (var folder in new[] { "system", "user" })
        {
            var root = Path.Combine(AppContext.BaseDirectory, folder);
            foreach (var path in Directory.EnumerateFiles(root, "*.uix", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
                found.Add(Path.GetRelativePath(AppContext.BaseDirectory, path));
        }

        return found;
    }

    [Fact]
    public void FindsTheShippedInterfaces()
    {
        Assert.True(Interfaces().Count >= 7);
    }

    [Theory]
    [MemberData(nameof(Interfaces))]
    public void StaysReadableInEveryPalette(string document)
    {
        List<string> problems = [];
        foreach (var dark in new[] { false, true })
        {
            foreach (var accent in DesktopPalette.Swatches)
            {
                var palette = DesktopPalette.Create(accent, dark);
                DesktopTheme.Use(palette);
                foreach (var (text, ratio) in Draw(Path.Combine(AppContext.BaseDirectory, document)))
                {
                    if (ratio < Minimum)
                    {
                        problems.Add(
                            $"{(dark ? "dark" : "light")} {DesktopPalette.Format(accent)}: \"{text}\" has a contrast of {ratio:0.00}");
                    }
                }
            }
        }

        Assert.True(problems.Count == 0, $"{document} is hard to read:\n{string.Join('\n', problems.Distinct())}");
    }

    /// <summary>Draws a document with sample text everywhere, and gives each text's contrast with what's behind it.</summary>
    private static List<(string Text, double Ratio)> Draw(string path)
    {
        var script = UIScript.Load(path);
        UIView view = new(script);
        foreach (var control in script.Controls)
        {
            if (view.List(control.Name) is { } list)
            {
                list.Items.AddRange(["First item", "Second item"]);
                list.SelectedIndex = 0;
            }
            else if (control.Type != UIControlType.Button && string.IsNullOrEmpty(view.Text(control.Name)))
            {
                // Text the app sets as it runs, such as a status line. A button's label is its document's, and one
                // without, such as a color swatch, has no text to read.
                view.SetText(control.Name, "Sample text");
            }
        }

        if (script.Sidebar is { } sidebar)
        {
            view.List(sidebar.Name)!.Items.AddRange(["Home", "Apps"]);
            view.List(sidebar.Name)!.SelectedIndex = 0;
        }

        if (script.TitleField is { } field)
            view.SetText(field.Name, "/mnt/sample");

        WindowManager manager = new(1024, 768);
        RecordingPainter painter = new(1024, 768);
        DesktopWindow window = new("Sample", view);
        manager.Open(window);

        // Controls that overlap another, as stacked panes do, are drawn one at a time; the rest together.
        List<UIControl> stacked = [];
        foreach (var control in script.Controls)
        {
            var bounds = view.Bounds(control.Name)!.Value;
            foreach (var other in script.Controls)
            {
                if (!ReferenceEquals(control, other) && Overlap(bounds, view.Bounds(other.Name)!.Value))
                {
                    stacked.Add(control);
                    break;
                }
            }
        }

        List<(string, double)> found = [];
        foreach (var control in stacked)
            view.SetHidden(true, control.Name);
        Measure(manager, painter, window, found);

        foreach (var shown in stacked)
        {
            // A control without text, such as a color swatch, has nothing to read.
            if (view.List(shown.Name) is null && string.IsNullOrEmpty(view.Text(shown.Name)))
                continue;

            foreach (var control in script.Controls)
                view.SetHidden(!ReferenceEquals(control, shown), control.Name);
            Measure(manager, painter, window, found);
        }

        return found;
    }

    /// <summary>Draws the window and adds each text's contrast with what's behind it.</summary>
    private static void Measure(WindowManager manager, RecordingPainter painter, DesktopWindow window, List<(string, double)> found)
    {
        painter.Texts.Clear();
        manager.Draw(painter);
        foreach (var text in painter.Texts)
        {
            if (string.IsNullOrWhiteSpace(text.Text) || !window.Bounds.Contains(text.X + 1, text.Y + 1))
                continue;

            var behind = painter.At(text.X + 1, text.Y + (int)MathF.Ceiling(text.Size * 1.35f / 2));
            found.Add((text.Text, Contrast(text.Color, behind)));
        }
    }

    private static bool Overlap(UIRect a, UIRect b) =>
        a.X < b.X + b.Width && b.X < a.X + a.Width && a.Y < b.Y + b.Height && b.Y < a.Y + a.Height;

    private static double Contrast(UIColor color, int pixel)
    {
        var (a, b) = (Luminance(color.Red, color.Green, color.Blue),
            Luminance((byte)(pixel >> 16), (byte)(pixel >> 8), (byte)pixel));
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    // Relative luminance, as WCAG defines it.
    private static double Luminance(byte red, byte green, byte blue) =>
        (0.2126 * Linear(red)) + (0.7152 * Linear(green)) + (0.0722 * Linear(blue));

    private static double Linear(byte channel)
    {
        var value = channel / 255.0;
        return value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
