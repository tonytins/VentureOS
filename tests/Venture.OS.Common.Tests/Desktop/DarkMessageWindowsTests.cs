using Venture.OS.Desktop;
using Venture.OS.Interface;

namespace Venture.OS.Tests.Desktop;

/// <summary>
/// The desktop's own message windows, alerts and Quick Look's summaries, follow the palette in use: their text sits on
/// the window's own panel, so it stays readable on dark as on light, rather than on a fixed light box.
/// </summary>
[Collection(nameof(AppearanceCollection))]
public sealed class DarkMessageWindowsTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"venture-dark-{Guid.NewGuid():N}");

    public DarkMessageWindowsTests() => Directory.CreateDirectory(root);

    public void Dispose()
    {
        DesktopAppearance.Use(DesktopLook.Standard);
        Directory.Delete(root, recursive: true);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnAlertsMessageSitsOnThePanel(bool dark)
    {
        Use(dark);
        var alert = DesktopAlert.Create("Note", "Saved.", "OK");

        var (color, behind) = Drawn(alert, "Saved.");

        Assert.Equal(Rgb(DesktopTheme.Panel), behind);
        Assert.Equal(DesktopTheme.Text, color);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AQuickLookSummarySitsOnThePanel(bool dark)
    {
        Use(dark);
        File.WriteAllBytes(Path.Combine(root, "data.bin"), [0, 1, 2]);
        Assert.True(QuickLook.TryCreate(Path.Combine(root, "data.bin"), TestDesktopText.Create(), out var window, out var problem), problem);

        var (color, behind) = Drawn(window, "3 bytes");

        Assert.Equal(Rgb(DesktopTheme.Panel), behind);
        Assert.Equal(DesktopTheme.Text, color);
    }

    private static void Use(bool dark) =>
        DesktopTheme.Use(DesktopPalette.Create(DesktopPalette.StandardAccent, isDark: dark));

    // The text's color and the pixel behind its middle, as the window draws it.
    private static (UIColor Color, int Behind) Drawn(DesktopWindow window, string text)
    {
        WindowManager manager = new(800, 600);
        RecordingPainter painter = new(800, 600);
        manager.Open(window);
        manager.Draw(painter);

        var drawn = Assert.Single(painter.Texts, drawn => drawn.Text == text);
        return (drawn.Color, painter.At(drawn.X + 1, drawn.Y + (int)MathF.Ceiling(drawn.Size * 1.35f / 2)));
    }

    private static int Rgb(UIColor color) => DesktopBackground.Pack(color.Red, color.Green, color.Blue);
}
