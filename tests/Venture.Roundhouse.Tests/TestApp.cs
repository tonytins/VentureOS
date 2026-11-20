using Avalonia;
using Avalonia.Headless;

namespace Venture.Roundhouse.Tests;

/// <summary>Tests that draw with VentureOS's palette, which is shared, so they run alone.</summary>
[CollectionDefinition(nameof(PaletteCollection), DisableParallelization = true)]
public sealed class PaletteCollection;

/// <summary>
/// Roundhouse's app, off-screen: rendered with Skia, so tests can look at real frames. Avalonia starts once a
/// process, so every test shares one session and hands it its work.
/// </summary>
public static class TestApp
{
    private static readonly HeadlessUnitTestSession Session = HeadlessUnitTestSession.StartNew(typeof(TestApp));

    /// <summary>
    /// Avalonia finds the app here, as it does in Program. Its log is kept only for the Log tab, so tests leave no
    /// files.
    /// </summary>
    public static AppBuilder BuildAvaloniaApp()
    {
        Logging.RoundhouseLog.Configure(null);

        // Recent items go to a file of the tests' own, never the person's.
        Welcome.RoundhouseSettings.Location = Path.Combine(Path.GetTempPath(), $"roundhouse-tests-{Environment.ProcessId}", "settings.xml");
        return AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }

    /// <summary>Runs work on Avalonia's thread and waits for it.</summary>
    public static void Run(Action work) => Session.Dispatch(work, CancellationToken.None).GetAwaiter().GetResult();
}
