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
    private readonly FakeSystemInfo system = new() { Developer = new() };

    public SettingsAppTests()
    {
        DesktopAppearance.SettingsPath = Path.Combine(root, "appearance.xml");
        var systemFolder = Path.Combine(AppContext.BaseDirectory, "system");
        DesktopAppearance.WallpapersFolder = Path.Combine(systemFolder, "wallpapers");
        CommandRegistry registry = new();
        manager = new AppManager(
            Path.Combine(systemFolder, "apps"),
            new RuntimeCommands(registry.IsBuiltIn),
            system,
            windows: new AppWindows(windows, events)
            {
                SystemFolder = systemFolder,
                LoadImage = path => File.Exists(path) ? new DesktopImage(1, 1, [unchecked((int)0xFF00FF00)]) : null,
            });
        Assert.True(manager.OpenApp("Settings", Path.GetTempPath(), output), string.Join('\n', output.Lines));
    }

    public void Dispose()
    {
        manager.CloseApp("Settings", new RecordingOutput());
        DesktopAppearance.SettingsPath = null;
        DesktopAppearance.WallpapersFolder = null;
        DesktopAppearance.Use(DesktopLook.Standard);
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    private DesktopWindow Window => Assert.Single(windows.Windows);

    [Fact]
    public void OpensShowingTheLookInUse()
    {
        Assert.True(Window.View.IsSelected("light"));
        Assert.False(Window.View.IsSelected("dark"));
        Assert.Equal("Blue", Window.View.Text("accentName"));
        Assert.Empty(output.Lines);
        Assert.Empty(events.Lines);
        Assert.Empty(Window.View.MissingStrings);
    }

    [Fact]
    public void EachSwatchsHoverTextNamesTheAccentItSets()
    {
        for (var number = 1; number <= DesktopPalette.Swatches.Count; number++)
        {
            var bounds = Window.View.Bounds($"swatch{number}")!.Value;
            var name = Window.View.TooltipAt((int)bounds.X + 4, (int)bounds.Y + 4);

            Send(new UIEvent(UIEventKind.Pressed, $"swatch{number}"));

            Assert.NotNull(name);
            Assert.Equal(name, Window.View.Text("accentName"));
        }
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

        Assert.Equal("Slate", Window.View.Text("accentName"));
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
        Assert.Equal("Blue", Window.View.Text("accentName"));
    }

    [Fact]
    public void ShowsTheClock() => Assert.Equal("Tue 8:45 PM, UTC", Window.View.Text("clockShown"));

    [Fact]
    public void AnOffsetSetsTheClock()
    {
        Window.View.SetText("utcOffset", "UTC-05:00");
        Send(new UIEvent(UIEventKind.Submitted, "utcOffset", "UTC-05:00"));

        Assert.Equal(TimeSpan.FromHours(-5), system.WallClock!.UtcOffset);
        Assert.Equal("Tue 3:45 PM, UTC-05:00", Window.View.Text("clockShown"));
        Assert.Equal("", Window.View.Text("utcOffset"));
    }

    [Fact]
    public void AWrongOffsetSaysWhyAndChangesNothing()
    {
        Window.View.SetText("utcOffset", "EST");
        Send(new UIEvent(UIEventKind.Submitted, "utcOffset", "EST"));

        Assert.Equal(TimeSpan.Zero, system.WallClock!.UtcOffset);
        Assert.Contains("written like -05:00", Window.View.Text("clockShown"));
        Assert.Equal("EST", Window.View.Text("utcOffset"));
    }

    [Fact]
    public void TheWallpaperPickerShowsTheSkyThenTheSystemsWallpapers()
    {
        // The sky, then Earthset, the only wallpaper the system ships; the other slots are hidden.
        Assert.Empty(Window.View.MissingImages);
        Assert.False(Window.View.IsHidden("wallpaper1"));
        Assert.False(Window.View.IsHidden("wallpaper2"));
        Assert.True(Window.View.IsHidden("wallpaper3"));
        Assert.True(Window.View.IsHidden("wallpaper5"));
        Assert.True(Window.View.IsSelected("wallpaper2"));
        Assert.Equal("Earthset", Window.View.Text("wallpaperName"));
        Assert.Equal("", Window.View.Text("status"));

        Send(new UIEvent(UIEventKind.Pressed, "wallpaper1"));

        Assert.Equal(DesktopLook.Sky, DesktopAppearance.Current.Wallpaper);
        Assert.True(Window.View.IsSelected("wallpaper1"));
        Assert.Equal("Sky", Window.View.Text("wallpaperName"));
        Assert.Contains("wallpaper=\"sky\"", File.ReadAllText(DesktopAppearance.SettingsPath!));

        // Coming back to Appearance from Clock keeps the unused slots hidden.
        Send(new UIEvent(UIEventKind.Selected, "panes", Index: 2));
        Send(new UIEvent(UIEventKind.Selected, "panes", Index: 0));
        Assert.True(Window.View.IsHidden("wallpaper3"));
        Assert.False(Window.View.IsHidden("wallpaper2"));
    }

    [Fact]
    public void AutoAndItsTimesSetTheSchedule()
    {
        Assert.Equal("19:00", Window.View.Text("darkFrom"));
        Assert.Equal("07:00", Window.View.Text("darkUntil"));

        Send(new UIEvent(UIEventKind.Pressed, "auto"));
        Window.View.SetText("darkFrom", "21:30");
        Send(new UIEvent(UIEventKind.Submitted, "darkFrom", "21:30"));

        Assert.Equal(LookMode.Auto, DesktopAppearance.Current.Mode);
        Assert.Equal(new TimeSpan(21, 30, 0), DesktopAppearance.Current.DarkFrom);
        Assert.True(Window.View.IsSelected("auto"));
        Assert.False(Window.View.IsSelected("light"));

        // A time it can't read says why, and the field shows the time in use again.
        Window.View.SetText("darkUntil", "7am");
        Send(new UIEvent(UIEventKind.Submitted, "darkUntil", "7am"));
        Assert.Contains("HH:MM", Window.View.Text("status"));
        Assert.Equal("07:00", Window.View.Text("darkUntil"));
    }

    [Fact]
    public void SquareAndRoundedSwitchEveryWindowsCorners()
    {
        Assert.True(Window.View.IsSelected("rounded"));

        Send(new UIEvent(UIEventKind.Pressed, "square"));

        Assert.False(DesktopAppearance.Current.RoundedCorners);
        Assert.True(Window.View.IsSelected("square"));
        Assert.False(Window.View.IsSelected("rounded"));
        Assert.Contains("corners=\"square\"", File.ReadAllText(DesktopAppearance.SettingsPath!));

        Send(new UIEvent(UIEventKind.Pressed, "rounded"));
        Assert.True(DesktopAppearance.Current.RoundedCorners);

        Send(new UIEvent(UIEventKind.Selected, "panes", Index: 2));
        Assert.True(Window.View.IsHidden("square"));
    }

    [Fact]
    public void OpensOnAppearanceWithClockInTheSidebar()
    {
        Assert.Equal(["Appearance", "Dock", "Clock", "Developer"], Window.View.List("panes")!.Items);
        Assert.Equal(0, Window.View.List("panes")!.SelectedIndex);
        Assert.Equal("Appearance", Window.Title);
        Assert.False(Window.View.IsHidden("swatch1"));
        Assert.True(Window.View.IsHidden("utcOffset"));
        Assert.True(Window.View.IsHidden("clockShown"));
    }

    [Fact]
    public void ChoosingClockShowsItsPaneAlone()
    {
        Send(new UIEvent(UIEventKind.Selected, "panes", Index: 2));

        Assert.Equal("Clock", Window.Title);
        Assert.False(Window.View.IsHidden("utcLabel"));
        Assert.False(Window.View.IsHidden("utcOffset"));
        Assert.True(Window.View.IsHidden("swatch1"));
        Assert.True(Window.View.IsHidden("custom"));

        Send(new UIEvent(UIEventKind.Selected, "panes", Index: 0));
        Assert.Equal("Appearance", Window.Title);
        Assert.True(Window.View.IsHidden("utcOffset"));
    }

    [Fact]
    public void TheOffsetIsTypedAfterTheUtcLabelWithoutOne()
    {
        Assert.Equal("UTC", Window.View.Text("utcLabel"));

        Window.View.SetText("utcOffset", "5:30");
        Send(new UIEvent(UIEventKind.Submitted, "utcOffset", "5:30"));

        Assert.Equal(TimeSpan.FromMinutes(330), system.WallClock!.UtcOffset);
        Assert.Equal("Wed 2:15 AM, UTC+05:30", Window.View.Text("clockShown"));
    }

    [Fact]
    public void DeveloperShowsItsPaneAloneAndStartsOff()
    {
        Send(new UIEvent(UIEventKind.Selected, "panes", Index: 3));

        Assert.Equal("Developer", Window.Title);
        Assert.False(Window.View.IsHidden("developerOn"));
        Assert.True(Window.View.IsHidden("utcOffset"));
        Assert.True(Window.View.IsHidden("swatch1"));
        Assert.True(Window.View.IsSelected("developerOff"));
        Assert.Equal("Off. 0 computers are trusted.", Window.View.Text("developerStatus"));
    }

    [Fact]
    public void OnTurnsDeveloperModeOnAndOffTurnsItOff()
    {
        Send(new UIEvent(UIEventKind.Pressed, "developerOn"));

        Assert.True(system.Developer!.IsEnabled);
        Assert.True(Window.View.IsSelected("developerOn"));
        Assert.Equal("Listening on port 28455. 0 computers are trusted.", Window.View.Text("developerStatus"));

        Send(new UIEvent(UIEventKind.Pressed, "developerOff"));

        Assert.False(system.Developer.IsEnabled);
        Assert.True(Window.View.IsSelected("developerOff"));
    }

    [Fact]
    public void DeveloperSaysWhyItIsNotListening()
    {
        // As the kernel's listener leaves it when there's nothing to listen on; opening the pane shows it afresh.
        Send(new UIEvent(UIEventKind.Pressed, "developerOn"));
        system.Developer!.Disable();
        system.Developer.Problem = "there's no network device to listen on";
        system.Developer.Trust.Trust("0123456789abcdef0123456789abcdef");

        Send(new UIEvent(UIEventKind.Selected, "panes", Index: 3));

        Assert.Equal("Not listening: there's no network device to listen on. 1 computer is trusted.", Window.View.Text("developerStatus"));
    }

    [Fact]
    public void DockShowsItsModesAndSavesTheOneChosen()
    {
        Send(new UIEvent(UIEventKind.Selected, "panes", Index: 1));
        Assert.Equal("Dock", Window.Title);
        Assert.True(Window.View.IsSelected("dockAlways"));
        Assert.True(Window.View.IsHidden("utcOffset"));

        Send(new UIEvent(UIEventKind.Pressed, "dockDodge"));

        Assert.Equal(DockMode.Dodge, DesktopAppearance.Current.Dock);
        Assert.True(Window.View.IsSelected("dockDodge"));
        Assert.False(Window.View.IsSelected("dockAlways"));
        Assert.StartsWith("The dock slides away while a window", Window.View.Text("dockAbout"));
        Assert.Contains("dock=\"dodge\"", File.ReadAllText(DesktopAppearance.SettingsPath!));
    }

    private void Send(UIEvent uiEvent) => Window.EventHandler!(Window, uiEvent);
}
