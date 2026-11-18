using Avalonia;
using Avalonia.Headless;

namespace Venture.Roundhouse.Tests;

/// <summary>
/// Roundhouse's app, off-screen: rendered with Skia, so tests can look at real frames. Avalonia starts once a
/// process, so every test shares one session and hands it its work.
/// </summary>
public static class TestApp
{
    private static readonly HeadlessUnitTestSession Session = HeadlessUnitTestSession.StartNew(typeof(TestApp));

    /// <summary>Avalonia finds the app here, as it does in Program.</summary>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });

    /// <summary>Runs work on Avalonia's thread and waits for it.</summary>
    public static void Run(Action work) => Session.Dispatch(work, CancellationToken.None).GetAwaiter().GetResult();
}
