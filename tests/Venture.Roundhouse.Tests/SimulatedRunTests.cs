using Venture.Airo.Simulation;
using Venture.OS.Desktop;
using Venture.Roundhouse.Drawing;
using Venture.Roundhouse.Simulator;

namespace Venture.Roundhouse.Tests;

/// <summary>A run of VentureOS inside Roundhouse, without a window: set up as the kernel sets up its desktop.</summary>
[Collection(nameof(PaletteCollection))]
public sealed class SimulatedRunTests : IDisposable
{
    private readonly SkiaFonts fonts = SkiaFonts.LoadShipped();
    private readonly string rootfs = SimulatedDisk.FindRootfs()!;

    public void Dispose()
    {
        fonts.Dispose();
        DesktopAppearance.Use(DesktopLook.Standard);
    }

    [Fact]
    public void ARunStartsTheDesktopWithFilesAndDrawsIt()
    {
        using var run = SimulatedRun.Start(rootfs, fonts);

        Assert.Contains(run.Apps.Running, app => app.Bundle.Name == SimulatedRun.FallbackApp);
        Assert.Contains(run.Pixels, pixel => pixel != run.Pixels[0]);
        Assert.False(run.IsFinished);
        Assert.Empty(run.Messages);
    }

    [Fact]
    public void TheAppBeingDesignedRunsAsEditedInPlaceOfItsCopy()
    {
        var calculator = Path.Combine(rootfs, "user", "apps", "Calculator.app");
        var document = Path.Combine(calculator, "Contents", "Resources", "main.uix");
        var edited = File.ReadAllText(document).Replace("text=\"=\"", "text=\"Go\"", StringComparison.Ordinal);

        using var run = SimulatedRun.Start(rootfs, fonts, document, new Dictionary<string, string> { [document] = edited });

        Assert.Contains(run.Apps.Running, app => app.Bundle.Name == "Calculator");
        Assert.Equal(edited, File.ReadAllText(run.Disk.HostPath("/mnt/user/apps/Calculator.app/Contents/Resources/main.uix")));
        Assert.DoesNotContain("text=\"Go\"", File.ReadAllText(document));
    }

    [Fact]
    public void ADocumentOutsideAnAppRunsAsAWindowOfItsOwn()
    {
        var about = Path.Combine(rootfs, "system", "interfaces", "about.uix");

        using var run = SimulatedRun.Start(rootfs, fonts, about, new Dictionary<string, string> { [about] = File.ReadAllText(about) });

        Assert.Contains(run.Session.Windows.Windows, window => window.Title == "about.uix");
    }

    [Fact]
    public void AboutVentureOSOpensTheSystemsOwnWindowWithTheRunsCredits()
    {
        using var run = SimulatedRun.Start(rootfs, fonts);
        File.WriteAllText(run.Disk.HostPath("/mnt/system/credits.txt"), "Credits for this run");

        run.Session.ShowAbout();

        var about = run.Session.Windows.Focused!;
        Assert.NotNull(about.View.SidebarName);
        Assert.Equal("Credits for this run", about.View.Text(DesktopSession.AboutCredits));
        Assert.Empty(run.Messages);
    }

    [Fact]
    public void TextVentureOSRefusesIsReportedAndTheRunStillStarts()
    {
        var about = Path.Combine(rootfs, "system", "interfaces", "about.uix");

        using var run = SimulatedRun.Start(rootfs, fonts, about, new Dictionary<string, string> { [about] = "<UIScript><Hologram/></UIScript>" });

        Assert.Contains(run.Messages, message => message.StartsWith("about.uix:", StringComparison.Ordinal));
        Assert.Contains(run.Apps.Running, app => app.Bundle.Name == SimulatedRun.FallbackApp);
    }

    [Fact]
    public void TheRunsLookAndTheDesignersStayApart()
    {
        var designer = DesktopLook.Standard with { Mode = LookMode.Dark };
        DesktopAppearance.Use(designer);

        using var run = SimulatedRun.Start(rootfs, fonts);
        Assert.Equal(designer, DesktopAppearance.Current);

        // The run's Settings changes its own look, keeps it from frame to frame, and leaves the designer's alone.
        var changed = run.Within(() => run.System.Appearance!.Set(new Venture.OS.Scripting.AppearanceRequest(Accent: "#3A944A")));
        Assert.Null(changed.Refused);
        Assert.Equal(designer, DesktopAppearance.Current);

        run.Step(512, 384, false, 0, []);
        Assert.Equal(designer, DesktopAppearance.Current);
        Assert.Equal(DesktopPalette.Swatches[2], run.Within(() => DesktopAppearance.Current.Accent));
        Assert.Contains("3A944A", File.ReadAllText(run.Disk.HostPath("/mnt/user/settings/appearance.xml")), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EndingTheRunClosesItsAppsAndDeletesItsCopy()
    {
        var run = SimulatedRun.Start(rootfs, fonts);
        var root = run.Disk.Root;

        run.Dispose();

        Assert.Empty(run.Apps.Running);
        Assert.False(Directory.Exists(root));
        Assert.True(run.IsFinished);
        Assert.False(run.Step(1, 1, false, 0, []));
    }

    [Theory]
    [InlineData("/a/Calculator.app/Contents/Resources/main.uix", "/a/Calculator.app")]
    [InlineData("/a/interfaces/about.uix", null)]
    public void ADocumentsBundleIsTheAppItsIn(string document, string? bundle)
    {
        Assert.Equal(bundle, SimulatedRun.BundleOf(document));
    }
}
