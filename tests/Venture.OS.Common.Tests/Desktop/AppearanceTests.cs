using Venture.OS.Desktop;
using Venture.OS.Interface;
using Venture.OS.Scripting;
using Venture.OS.Shell;
using Venture.OS.Tests.Scripting;
using Venture.OS.Tests.Shell;

namespace Venture.OS.Tests.Desktop;

/// <summary>Tests that switch the desktop's palette run alone, since every drawing test reads it.</summary>
[CollectionDefinition(nameof(AppearanceCollection), DisableParallelization = true)]
public sealed class AppearanceCollection;

/// <summary>
/// Switching the desktop's look: the theme, the sky, and <c>venture.appearance</c> and <c>venture.setAppearance</c>
/// for scripts. Each test puts the standard light palette back.
/// </summary>
[Collection(nameof(AppearanceCollection))]
public sealed class AppearanceTests : IDisposable
{
    private readonly RecordingOutput output = new();

    public void Dispose()
    {
        DesktopAppearance.SettingsPath = null;
        DesktopAppearance.Use(DesktopLook.Standard);
    }

    [Fact]
    public void TheThemeReadsThePaletteInUse()
    {
        var dark = DesktopPalette.Create(DesktopPalette.StandardAccent, isDark: true);

        DesktopTheme.Use(dark);

        Assert.Same(dark, DesktopTheme.Palette);
        Assert.Equal(dark.Panel, DesktopTheme.Panel);
        Assert.Equal(dark.Text, DesktopTheme.Text);
        Assert.Equal(dark.Accent, DesktopTheme.Accent);
    }

    [Fact]
    public void TheDesktopRepaintsItsSkyWhenThePaletteChanges()
    {
        DesktopSession session = new(400, 300);
        RecordingPainter painter = new(400, 300);
        DesktopInput still = new(50, 60, false);
        session.Update(still);
        session.Draw(painter);
        var lightSky = painter.At(200, 100);
        Assert.False(session.Update(still));

        DesktopTheme.Use(DesktopPalette.Create(DesktopPalette.StandardAccent, isDark: true));

        Assert.True(session.Update(still));
        session.Draw(painter);
        Assert.True(Brightness(painter.At(200, 100)) < Brightness(lightSky));
    }

    [Fact]
    public void ScriptsReadTheLook()
    {
        Run("local look = venture.appearance() print(look.mode, look.accent)");

        Assert.Equal(["light\t#3584E4"], output.Lines);
    }

    [Fact]
    public void ScriptsChangeTheLook()
    {
        Run("""print(venture.setAppearance("dark", "#E8922A"))""");

        Assert.Equal(["true"], output.Lines);
        Assert.True(DesktopTheme.Palette.IsDark);
        Assert.Equal(new UIColor(232, 146, 42), DesktopTheme.Palette.ChosenAccent);
    }

    [Fact]
    public void ScriptsHearWhenALookCouldntBeSaved()
    {
        // A file where the settings folder should be stops the save.
        var blocked = Path.Combine(Path.GetTempPath(), $"venture-blocked-{Guid.NewGuid():N}");
        File.WriteAllText(blocked, "in the way");
        DesktopAppearance.SettingsPath = Path.Combine(blocked, "appearance.xml");
        try
        {
            Run("""print(venture.setAppearance("dark"))""");
        }
        finally
        {
            File.Delete(blocked);
        }

        Assert.StartsWith("true\tcouldn't save the appearance", output.Lines.Single());
        Assert.True(DesktopTheme.Palette.IsDark);
    }

    [Fact]
    public void ScriptsReadTheWholeLook()
    {
        Run("""
            local look = venture.appearance()
            print(look.mode, look.dark, look.accent, look.corners, look.wallpaper, look.darkFrom, look.darkUntil, look.dock)
            """);

        Assert.Equal(["light\tfalse\t#3584E4\trounded\tEarthset\t19:00\t07:00\talways"], output.Lines);
    }

    [Fact]
    public void ATableChangesOnlyWhatItNames()
    {
        Run("""print(venture.setAppearance{ mode = "auto", corners = "square", wallpaper = "sky", darkFrom = "20:00", dock = "dodge" })""");

        var look = DesktopAppearance.Current;
        Assert.Equal(["true"], output.Lines);
        Assert.Equal(LookMode.Auto, look.Mode);
        Assert.False(look.RoundedCorners);
        Assert.Equal("sky", look.Wallpaper);
        Assert.Equal(TimeSpan.FromHours(20), look.DarkFrom);
        Assert.Equal(TimeSpan.FromHours(7), look.DarkUntil);
        Assert.Equal(DesktopPalette.StandardAccent, look.Accent);
        Assert.Equal(DockMode.Dodge, look.Dock);
    }

    [Theory]
    [InlineData("""print(venture.setAppearance{ corners = "round" })""", "rounded or square")]
    [InlineData("""print(venture.setAppearance{ wallpaper = "../x" })""", "isn't a wallpaper's name")]
    [InlineData("""print(venture.setAppearance{ darkFrom = "7pm" })""", "HH:MM")]
    [InlineData("""print(venture.setAppearance{ darkFrom = "07:00" })""", "never dark")]
    [InlineData("""print(venture.setAppearance{ corners = true })""", "corners must be a string")]
    [InlineData("""print(venture.setAppearance{ dock = "magnify" })""", "the dock is always, autohide, or dodge")]
    public void ATableWithAMistakeChangesNothing(string code, string reason)
    {
        Run(code);

        Assert.StartsWith("nil\t", output.Lines.Single());
        Assert.Contains(reason, output.Lines.Single());
        Assert.Equal(DesktopLook.Standard, DesktopAppearance.Current);
    }

    [Fact]
    public void LeavingOutTheAccentKeepsIt()
    {
        DesktopAppearance.Use(DesktopLook.Standard with { Accent = new UIColor(232, 146, 42) });

        Run("""venture.setAppearance("dark")""");

        Assert.Equal(new UIColor(232, 146, 42), DesktopTheme.Palette.ChosenAccent);
    }

    [Theory]
    [InlineData("""print(venture.setAppearance("dim"))""", "light, dark, or auto")]
    [InlineData("""print(venture.setAppearance("light", "blue"))""", "#RRGGBB")]
    public void ScriptsHearWhyALookIsWrong(string code, string reason)
    {
        Run(code);

        Assert.StartsWith("nil\t", output.Lines.Single());
        Assert.Contains(reason, output.Lines.Single());
        Assert.Same(DesktopPalette.Light, DesktopTheme.Palette);
    }

    [Fact]
    public void ASystemWithoutAnAppearanceSaysSo()
    {
        LuaRunner.Run(output, Path.GetTempPath(), lua => lua.DoString("""
            print(venture.appearance())
            print(venture.setAppearance("dark"))
            """), new FakeSystemInfo { Appearance = null });

        Assert.Equal(["nil", "nil\tthis system has no desktop appearance to change"], output.Lines);
        Assert.Same(DesktopPalette.Light, DesktopTheme.Palette);
    }

    [Fact]
    public void ChangingTheLookNeedsTheSystemScope()
    {
        Assert.False(Run("""venture.setAppearance("dark")""", ScriptScopes.Files));
        Assert.Contains(output.Lines, line => line.Contains("system scope", StringComparison.Ordinal));
        Assert.False(DesktopTheme.Palette.IsDark);
    }

    [Fact]
    public void StyleRolesFollowThePalette()
    {
        var view = Styled("""textColor="secondaryText" fontSize="title" """);
        var dark = DesktopPalette.Create(DesktopPalette.StandardAccent, isDark: true);

        Assert.Equal((DesktopPalette.Light.SecondaryText, DesktopTheme.TitleSize), Drawn(view));
        DesktopTheme.Use(dark);
        Assert.Equal((dark.SecondaryText, DesktopTheme.TitleSize), Drawn(view));
    }

    [Fact]
    public void ADarkTwinReplacesItsColorOnlyOnADarkPalette()
    {
        var view = Styled("""textColor="#112233" darkTextColor="#DDEEFF" """);

        Assert.Equal(new UIColor(0x11, 0x22, 0x33), Drawn(view).Color);
        DesktopTheme.Use(DesktopPalette.Create(DesktopPalette.StandardAccent, isDark: true));
        Assert.Equal(new UIColor(0xDD, 0xEE, 0xFF), Drawn(view).Color);
    }

    [Fact]
    public void AWrittenColorWithoutATwinStaysInEveryPalette()
    {
        var view = Styled("""textColor="#112233" """);

        DesktopTheme.Use(DesktopPalette.Create(DesktopPalette.StandardAccent, isDark: true));

        Assert.Equal(new UIColor(0x11, 0x22, 0x33), Drawn(view).Color);
    }

    private static UIView Styled(string attributes)
    {
        UIView view = new(UIScript.Parse($"""
            <UIScript>
              <Window width="200" height="50"/>
              <Style name="s" {attributes}/>
              <Text name="label" x="0" y="0" width="200" height="30" style="s" text="Hello"/>
            </UIScript>
            """));
        return view;
    }

    private static (UIColor Color, int Size) Drawn(UIView view)
    {
        RecordingPainter painter = new(200, 50);
        view.Draw(painter, 0, 0);
        var text = painter.Texts.Single(text => text.Text == "Hello");
        return (text.Color, text.Size);
    }

    [Fact]
    public void TheCommandShowsTheLook()
    {
        Command("appearance");

        Assert.Equal(["Appearance: light", "Accent:     #3584E4"], output.Lines);
    }

    [Fact]
    public void TheCommandChangesTheLook()
    {
        Command("appearance", "dark", "--accent", "#E8922A");

        Assert.True(DesktopTheme.Palette.IsDark);
        Assert.Equal(new UIColor(232, 146, 42), DesktopTheme.Palette.ChosenAccent);
        Assert.Equal(["Appearance: dark", "Accent:     #E8922A"], output.Lines);
    }

    [Fact]
    public void TheCommandSetsAutoAndItsSchedule()
    {
        Command("appearance", "auto", "--from", "20:00", "--until", "06:30");

        var look = DesktopAppearance.Current;
        Assert.Equal(LookMode.Auto, look.Mode);
        Assert.Equal((new TimeSpan(20, 0, 0), new TimeSpan(6, 30, 0)), (look.DarkFrom, look.DarkUntil));
        Assert.Equal(["Appearance: auto, dark from 20:00 until 06:30 (light now)", "Accent:     #3584E4"], output.Lines);
    }

    [Fact]
    public void TheCommandChangesOnlyTheAccent()
    {
        Command("appearance", "--accent", "#E8922A");

        Assert.False(DesktopTheme.Palette.IsDark);
        Assert.Equal(new UIColor(232, 146, 42), DesktopTheme.Palette.ChosenAccent);
    }

    [Fact]
    public void TheCommandSaysWhyALookIsWrong()
    {
        Command("appearance", "purple");

        Assert.Single(output.Lines);
        Assert.StartsWith("appearance: ", output.Lines[0]);
        Assert.Contains("light, dark, or auto", output.Lines[0]);
        Assert.Same(DesktopPalette.Light, DesktopTheme.Palette);
    }

    private void Command(string name, params string[] arguments)
    {
        var system = Path.Combine(AppContext.BaseDirectory, "system");
        ScriptCommands source = new(Path.Combine(system, "commands"), new FakeSystemInfo(), libraryDirectory: Path.Combine(system, "lib"));
        Assert.True(source.TryGet(name, out var command), $"No {name}.lua shipped.");
        command.Execute(new ShellContext(output, new RecordingHost(), new CommandRegistry()), arguments);
    }

    private static int Brightness(int pixel) => ((pixel >> 16) & 0xFF) + ((pixel >> 8) & 0xFF) + (pixel & 0xFF);

    private bool Run(string code, ScriptScopes scopes = ScriptScopes.All) =>
        LuaRunner.Run(output, Path.GetTempPath(), lua => lua.DoString(code), new FakeSystemInfo(), scopes: scopes);
}
