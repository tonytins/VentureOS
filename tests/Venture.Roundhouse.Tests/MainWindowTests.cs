using Avalonia.Headless;

namespace Venture.Roundhouse.Tests;

/// <summary>Roundhouse's main window, opened and drawn off-screen.</summary>
public sealed class MainWindowTests
{
    [Fact]
    public void IsTitledWithTheVersionItSharesWithVentureOS() => TestApp.Run(() =>
    {
        MainWindow window = new();

        window.Show();

        Assert.Matches(@"^Roundhouse 0\.6\.\d+$", window.Title);
        Assert.Equal(MainWindow.ProductTitle, window.Title);
        window.Close();
    });

    [Fact]
    public void DrawsAFrame() => TestApp.Run(() =>
    {
        MainWindow window = new();
        window.Show();

        var frame = window.CaptureRenderedFrame();

        Assert.NotNull(frame);
        Assert.Equal((1100, 720), (frame.PixelSize.Width, frame.PixelSize.Height));
        window.Close();
    });
}
