using Venture.OS.Bundles;
using Venture.OS.Desktop;
using Venture.Roundhouse.Bundles;
using Venture.Roundhouse.Drawing;
using Venture.Roundhouse.Simulator;

namespace Venture.Roundhouse.Tests;

/// <summary><see cref="AppTemplate"/>: a new window app that VentureOS reads and runs.</summary>
[Collection(nameof(PaletteCollection))]
public sealed class AppTemplateTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), $"roundhouse-template-{Guid.NewGuid():N}");

    public AppTemplateTests() => Directory.CreateDirectory(folder);

    public void Dispose()
    {
        Directory.Delete(folder, recursive: true);
        DesktopAppearance.Use(DesktopLook.Standard);
    }

    [Fact]
    public void ANewAppIsOneVentureOSReads()
    {
        var app = AppTemplate.Create("Counter", "dev.example.counter", folder);

        var bundle = Bundle.Read(app, BundleKind.App);
        Assert.Equal(Path.Combine(folder, "Counter.app"), app);
        Assert.Equal(("Counter", "dev.example.counter", "1.0"), (bundle.Name, bundle.Identifier, bundle.Version));
        Assert.True(File.Exists(Path.Combine(app, "Contents", "Resources", "main.uix")));
        Assert.StartsWith("-- Counter:", File.ReadAllText(Path.Combine(app, "Contents", "Lua", "main.lua")));
    }

    [Fact]
    public void ANameWithMarkupIsWrittenAsAValueIsSoItReadsBack()
    {
        var app = AppTemplate.Create("Tom & Jerry's Dice", "dev.example.tomandjerry", folder);

        Assert.Equal("Tom & Jerry's Dice", Bundle.Read(app, BundleKind.App).Name);
        Assert.Contains("Tom &amp; Jerry's Dice", File.ReadAllText(Path.Combine(app, "Contents", "Info.xml")));
    }

    [Fact]
    public void ANewAppRunsAndItsButtonCounts()
    {
        var app = AppTemplate.Create("Counter", "dev.example.counter", folder);
        using SkiaFonts fonts = SkiaFonts.LoadShipped();
        using var run = SimulatedRun.Start(SimulatedDisk.FindRootfs()!, fonts, Path.Combine(app, "Contents", "Resources", "main.uix"));

        Assert.Contains(run.Apps.Running, running => running.Bundle.Name == "Counter");
        var window = run.Session.Windows.Windows.Single(shown => shown.Title == "Counter");
        Assert.Equal("Press the button.", window.View.Text("count"));

        var (left, top) = window.ContentOrigin;
        var press = window.View.Bounds("press")!.Value;
        var (x, y) = ((int)(left + press.X + (press.Width / 2)), (int)(top + press.Y + (press.Height / 2)));
        run.Step(x, y, true, 0, []);
        run.Step(x, y, false, 0, []);
        run.Step(x, y, true, 0, []);
        run.Step(x, y, false, 0, []);

        Assert.Equal("Pressed 2 times.", window.View.Text("count"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a/b")]
    [InlineData(".hidden")]
    [InlineData("two\nlines")]
    public void ANameThatCantBeAnAppsIsRefusedAndNothingIsMade(string name)
    {
        Assert.NotNull(AppTemplate.ProblemWithName(name));
        Assert.Throws<ArgumentException>(() => AppTemplate.Create(name, "dev.example.app", folder));
        Assert.Empty(Directory.EnumerateFileSystemEntries(folder));
    }

    [Fact]
    public void AnIdentifierVentureOSRefusesLeavesNothingBehind()
    {
        var exception = Assert.Throws<ArgumentException>(() => AppTemplate.Create("Counter", "not an identifier", folder));

        Assert.Contains("reverse-DNS", exception.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(folder, "Counter.app")));
    }

    [Fact]
    public void AnAppAlreadyThereIsntReplaced()
    {
        AppTemplate.Create("Counter", "dev.example.counter", folder);
        File.WriteAllText(Path.Combine(folder, "Counter.app", "Contents", "Lua", "main.lua"), "-- mine");

        Assert.Throws<ArgumentException>(() => AppTemplate.Create("Counter", "dev.example.counter", folder));
        Assert.Equal("-- mine", File.ReadAllText(Path.Combine(folder, "Counter.app", "Contents", "Lua", "main.lua")));
    }

    [Theory]
    [InlineData("Counter", "dev.example.counter")]
    [InlineData("Dice Roller 2", "dev.example.diceroller2")]
    [InlineData("2048", "dev.example.app2048")]
    [InlineData("!!!", "dev.example.app")]
    public void AnIdentifierIsSuggestedFromTheNameThatVentureOSAccepts(string name, string identifier)
    {
        Assert.Equal(identifier, AppTemplate.IdentifierFor(name));

        // Each suggestion is a reverse-DNS identifier VentureOS reads, whatever the name.
        var app = AppTemplate.Create("Probe", identifier, folder);
        Assert.Equal(identifier, Bundle.Read(app, BundleKind.App).Identifier);
    }
}
