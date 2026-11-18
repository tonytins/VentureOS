using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Venture.OS.Desktop;

namespace Venture.Roundhouse.Tests;

/// <summary>Roundhouse's main window, opened and drawn off-screen: a document's text beside its live preview.</summary>
[Collection(nameof(PaletteCollection))]
public sealed class MainWindowTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), $"roundhouse-window-{Guid.NewGuid():N}");
    private readonly string document;

    public MainWindowTests()
    {
        Directory.CreateDirectory(folder);
        document = Path.Combine(folder, "main.uix");
        var calculator = Path.Combine(SkiaPainterTests.RepositoryRoot(), "rootfs", "user", "apps", "Calculator.app", "Contents", "Resources", "main.uix");
        File.Copy(calculator, document);
    }

    public void Dispose()
    {
        DesktopAppearance.Use(DesktopLook.Standard);
        Directory.Delete(folder, recursive: true);
    }

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

    [Fact]
    public void OpeningADocumentShowsItsTextAndDrawsIt() => TestApp.Run(() =>
    {
        MainWindow window = new();
        window.Show();

        window.Open(document);

        Assert.Equal(File.ReadAllText(document), window.Text);
        Assert.Equal($"main.uix — {MainWindow.ProductTitle}", window.Title);
        Assert.NotNull(window.Preview);
        Assert.Equal("VentureOS reads this document.", window.Status);
        window.Close();
    });

    [Fact]
    public void AFileThatCantBeOpenedSaysSoAndOpensNothing() => TestApp.Run(() =>
    {
        MainWindow window = new();
        window.Show();

        window.Open(Path.Combine(folder, "missing.uix"));

        Assert.Null(window.DocumentPath);
        Assert.Null(window.Preview);
        Assert.Contains("couldn't be opened", window.Status);
        window.Close();
    });

    [Fact]
    public void TextVentureOSRefusesKeepsTheLastDrawingAndNamesTheLine() => TestApp.Run(() =>
    {
        MainWindow window = new();
        window.Show();
        window.Open(document);
        var drawn = window.Preview;

        window.Text = "<?xml version=\"1.0\"?>\n<Begin>\n<Nope";
        window.RenderNow();

        Assert.Same(drawn, window.Preview);
        Assert.StartsWith("Line ", window.Status);
        Assert.Contains("last version VentureOS could read", window.Status);
        window.Close();
    });

    [Fact]
    public void TypingRedrawsAfterAPause() => TestApp.Run(() =>
    {
        MainWindow window = new();
        window.Show();
        window.Open(document);
        var drawn = window.Preview;

        window.Text += "\n";
        Assert.Same(drawn, window.Preview);

        // The redraw waits for typing to stop, then runs on the window's own thread: let it run for a while.
        DispatcherFrame frame = new();
        DispatcherTimer.RunOnce(() => frame.Continue = false, MainWindow.PreviewDelay * 3);
        Dispatcher.UIThread.PushFrame(frame);

        Assert.NotSame(drawn, window.Preview);
        window.Close();
    });

    [Fact]
    public void TheDarkSwitchRedrawsInTheDarkPalette() => TestApp.Run(() =>
    {
        MainWindow window = new();
        window.Show();
        window.Open(document);
        var light = Pixel(window.Preview!, 40, 60);

        window.FindControl<CheckBox>("DarkBox")!.IsChecked = true;

        Assert.NotEqual(light, Pixel(window.Preview!, 40, 60));
        window.Close();
    });

    [Fact]
    public void SaveWritesTheTextAsItIs() => TestApp.Run(() =>
    {
        MainWindow window = new();
        window.Show();
        window.Open(document);
        var edited = window.Text.Replace("Calculator", "Adder", StringComparison.Ordinal);

        window.Text = edited;

        Assert.True(window.Save());
        Assert.Equal(edited, File.ReadAllText(document));
        Assert.Equal("Saved main.uix.", window.Status);
        window.Close();
    });

    private static uint Pixel(WriteableBitmap bitmap, int x, int y)
    {
        using var locked = bitmap.Lock();
        var pixel = new int[1];
        System.Runtime.InteropServices.Marshal.Copy(locked.Address + (y * locked.RowBytes) + (x * 4), pixel, 0, 1);
        return (uint)pixel[0];
    }
}
