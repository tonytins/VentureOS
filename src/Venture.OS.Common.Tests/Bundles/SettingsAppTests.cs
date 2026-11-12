using Venture.OS.Bundles;
using Venture.OS.Desktop;
using Venture.OS.Interface;
using Venture.OS.Scripting;
using Venture.OS.Shell;
using Venture.OS.Tests.Desktop;
using Venture.OS.Tests.Scripting;
using Venture.OS.Tests.Shell;

namespace Venture.OS.Tests.Bundles;

/// <summary>
/// Opens the shipped <c>Settings.app</c>, which switches the desktop's look and saves it. It changes the desktop's
/// palette, so it runs with the other appearance tests, alone, and saves to a folder of its own.
/// </summary>
[Collection(nameof(AppearanceCollection))]
public sealed class SettingsAppTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"venture-settings-{Guid.NewGuid():N}");
    private readonly RecordingOutput output = new();
    private readonly RecordingOutput events = new();
    private readonly WindowManager windows = new(1024, 768);
    private readonly AppManager manager;

    public SettingsAppTests()
    {
        DesktopAppearance.SettingsPath = Path.Combine(root, "appearance.xml");
        CommandRegistry registry = new();
        manager = new AppManager(
            Path.Combine(AppContext.BaseDirectory, "system", "apps"),
            new RuntimeCommands(registry.IsBuiltIn),
            new FakeSystemInfo(),
            windows: new AppWindows(windows, events));
        Assert.True(manager.OpenApp("Settings", Path.GetTempPath(), output), string.Join('\n', output.Lines));
    }

    public void Dispose()
    {
        manager.CloseApp("Settings", new RecordingOutput());
        DesktopAppearance.SettingsPath = null;
        DesktopTheme.Use(DesktopPalette.Light);
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    private DesktopWindow Window => Assert.Single(windows.Windows);

    [Fact]
    public void OpensShowingTheLookInUse()
    {
        Assert.True(Window.View.IsSelected("light"));
        Assert.False(Window.View.IsSelected("dark"));
        Assert.Equal("Sky Blue", Window.View.Text("accentName"));
        Assert.Empty(output.Lines);
    }

    [Fact]
    public void DarkSwitchesTheDesktopAndSavesIt()
    {
        Send(new UIEvent(UIEventKind.Pressed, "dark"));

        Assert.True(DesktopTheme.Palette.IsDark);
        Assert.True(Window.View.IsSelected("dark"));
        Assert.False(Window.View.IsSelected("light"));
        Assert.Contains("mode=\"dark\"", File.ReadAllText(DesktopAppearance.SettingsPath!));
        Assert.Equal("", Window.View.Text("status"));
    }

    [Fact]
    public void EachSwatchSetsItsAccentAndKeepsTheMode()
    {
        Send(new UIEvent(UIEventKind.Pressed, "dark"));

        for (var number = 1; number <= DesktopPalette.Swatches.Count; number++)
        {
            Send(new UIEvent(UIEventKind.Pressed, $"swatch{number}"));

            Assert.Equal(DesktopPalette.Swatches[number - 1], DesktopTheme.Palette.ChosenAccent);
            Assert.True(DesktopTheme.Palette.IsDark);
        }

        Assert.Equal("Graphite", Window.View.Text("accentName"));
    }

    [Fact]
    public void TheSwatchesLookLikeTheAccentsTheySet()
    {
        var script = UIScript.Load(
            Path.Combine(AppContext.BaseDirectory, "system", "apps", "Settings.app", "Contents", "Resources", "main.uix"));

        for (var number = 1; number <= DesktopPalette.Swatches.Count; number++)
        {
            var background = script.Styles[$"swatch{number}"].Colors.Background;
            Assert.Equal(UIStyleColor.Literal(DesktopPalette.Swatches[number - 1]), background);
        }
    }

    [Fact]
    public void ACustomAccentApplies()
    {
        Window.View.SetText("custom", " #12abef ");
        Send(new UIEvent(UIEventKind.Submitted, "custom", " #12abef "));

        Assert.Equal(new UIColor(0x12, 0xAB, 0xEF), DesktopTheme.Palette.ChosenAccent);
        Assert.Equal("Custom, #12ABEF", Window.View.Text("accentName"));
        Assert.Equal("", Window.View.Text("custom"));
    }

    [Fact]
    public void AWrongCustomAccentSaysWhyAndChangesNothing()
    {
        Window.View.SetText("custom", "blue");
        Send(new UIEvent(UIEventKind.Submitted, "custom", "blue"));

        Assert.Same(DesktopPalette.Light, DesktopTheme.Palette);
        Assert.Contains("#RRGGBB", Window.View.Text("status"));
        Assert.Equal("blue", Window.View.Text("custom"));
        Assert.Equal("Sky Blue", Window.View.Text("accentName"));
    }

    private void Send(UIEvent uiEvent) => Window.EventHandler!(Window, uiEvent);
}
