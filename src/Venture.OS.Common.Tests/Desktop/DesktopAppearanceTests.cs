using Venture.OS.Desktop;
using Venture.OS.Interface;

namespace Venture.OS.Tests.Desktop;

/// <summary>
/// The saved look: <c>appearance.xml</c> is read at boot and written on every change. Each test uses a folder of
/// its own and puts the standard look back.
/// </summary>
[Collection(nameof(AppearanceCollection))]
public sealed class DesktopAppearanceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"venture-look-{Guid.NewGuid():N}");

    public DesktopAppearanceTests() => DesktopAppearance.SettingsPath = Path.Combine(root, "settings", "appearance.xml");

    private string SettingsFile => DesktopAppearance.SettingsPath!;

    public void Dispose()
    {
        DesktopAppearance.SettingsPath = null;
        DesktopTheme.Use(DesktopPalette.Light);
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void ApplyingALookSavesIt()
    {
        var problem = DesktopAppearance.Apply(DesktopPalette.Create(new UIColor(232, 146, 42), isDark: true));

        Assert.Null(problem);
        Assert.True(DesktopTheme.Palette.IsDark);
        Assert.Contains("""<Appearance version="1" mode="dark" accent="#E8922A"/>""", File.ReadAllText(SettingsFile));
    }

    [Fact]
    public void LoadingRestoresASavedLook()
    {
        DesktopAppearance.Apply(DesktopPalette.Create(new UIColor(232, 146, 42), isDark: true));
        DesktopTheme.Use(DesktopPalette.Light);

        var result = DesktopAppearance.Load();

        Assert.Null(result.Problem);
        Assert.True(DesktopTheme.Palette.IsDark);
        Assert.Equal(new UIColor(232, 146, 42), DesktopTheme.Palette.ChosenAccent);
        Assert.Equal("dark, accent #E8922A", result.Description);
    }

    [Fact]
    public void WithoutAFileTheStandardLookStays()
    {
        var result = DesktopAppearance.Load();

        Assert.Null(result.Problem);
        Assert.Same(DesktopPalette.Light, DesktopTheme.Palette);
        Assert.Equal("light, accent #1E78DC (nothing saved)", result.Description);
    }

    [Fact]
    public void LeftOutAttributesTakeTheStandardOnes()
    {
        Write("""<Appearance version="1" mode="dark"/>""");

        DesktopAppearance.Load();

        Assert.True(DesktopTheme.Palette.IsDark);
        Assert.Equal(DesktopPalette.StandardAccent, DesktopTheme.Palette.ChosenAccent);
    }

    [Fact]
    public void UnknownAttributesAreSkipped()
    {
        Write("""<Appearance version="1" mode="dark" accent="#E8922A" contrast="high"/>""");

        Assert.Null(DesktopAppearance.Load().Problem);
        Assert.True(DesktopTheme.Palette.IsDark);
    }

    [Theory]
    [InlineData("""<Appearance version="2" mode="dark"/>""", "version 2")]
    [InlineData("""<Appearance version="1" mode="dim"/>""", "dim")]
    [InlineData("""<Appearance version="1" accent="blue"/>""", "blue")]
    [InlineData("""<Settings/>""", "<Appearance>")]
    [InlineData("""<Appearance version="1" mode="dark">""", "appearance.xml")]
    public void ABadFileIsReportedAndTheStandardLookStays(string text, string reason)
    {
        Write(text);

        var result = DesktopAppearance.Load();

        Assert.NotNull(result.Problem);
        Assert.Contains(reason, result.Problem, StringComparison.Ordinal);
        Assert.Same(DesktopPalette.Light, DesktopTheme.Palette);
    }

    [Fact]
    public void ALookThatCantBeSavedStillApplies()
    {
        // A file where the settings folder should be stops the save.
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "settings"), "in the way");

        var problem = DesktopAppearance.Apply(DesktopPalette.Create(DesktopPalette.StandardAccent, isDark: true));

        Assert.NotNull(problem);
        Assert.True(DesktopTheme.Palette.IsDark);
    }

    [Fact]
    public void WithoutASettingsPathNothingIsSaved()
    {
        DesktopAppearance.SettingsPath = null;

        Assert.Null(DesktopAppearance.Apply(DesktopPalette.Create(DesktopPalette.StandardAccent, isDark: true)));
        Assert.False(Directory.Exists(root));
    }

    private void Write(string element)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
        File.WriteAllText(SettingsFile, $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n{element}\n");
    }
}
