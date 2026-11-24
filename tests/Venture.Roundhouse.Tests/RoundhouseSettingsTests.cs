using Venture.Roundhouse.Welcome;

namespace Venture.Roundhouse.Tests;

/// <summary>
/// <see cref="RoundhouseSettings"/>: recent items and "Show at startup" kept between launches, a file that can't be
/// read giving the defaults and a reason, and what a newer Roundhouse wrote kept.
/// </summary>
public sealed class RoundhouseSettingsTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), $"roundhouse-settings-{Guid.NewGuid():N}");

    public RoundhouseSettingsTests() => Directory.CreateDirectory(folder);

    private string SettingsFile => Path.Combine(folder, "Roundhouse", "settings.xml");

    public void Dispose() => Directory.Delete(folder, recursive: true);

    [Fact]
    public void NoFileIsTheDefaults()
    {
        var (settings, problem) = RoundhouseSettings.Load(SettingsFile);

        Assert.True(settings.ShowAtStartup);
        Assert.Empty(settings.Recent);
        Assert.Null(problem);
    }

    [Fact]
    public void WhatsSavedLoadsBack()
    {
        RoundhouseSettings settings = new() { ShowAtStartup = false };
        settings.Remember(Path.Combine(folder, "Dice.rproj"));
        settings.Remember(Path.Combine(folder, "Settings.app") + Path.DirectorySeparatorChar);
        settings.Remember(Path.Combine(folder, "open & save.uix"));

        settings.Save(SettingsFile);
        var (loaded, problem) = RoundhouseSettings.Load(SettingsFile);

        Assert.Null(problem);
        Assert.False(loaded.ShowAtStartup);
        Assert.Equal(
            [new(Path.Combine(folder, "open & save.uix"), RecentKind.Interface), new(Path.Combine(folder, "Settings.app"), RecentKind.App), new RecentItem(Path.Combine(folder, "Dice.rproj"), RecentKind.Project)],
            loaded.Recent);
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(SettingsFile)!, ".*"));
    }

    [Fact]
    public void RememberingMovesAnItemToTheTopAndKeepsTheNewest()
    {
        RoundhouseSettings settings = new();
        for (var number = 0; number < RoundhouseSettings.MostRecent + 3; number++)
            settings.Remember(Path.Combine(folder, $"P{number}.rproj"));
        settings.Remember(Path.Combine(folder, "P5.rproj"));

        Assert.Equal(RoundhouseSettings.MostRecent, settings.Recent.Count);
        Assert.Equal(Path.Combine(folder, "P5.rproj"), settings.Recent[0].Path);
        Assert.Single(settings.Recent, item => item.Path.EndsWith("P5.rproj", StringComparison.Ordinal));
        Assert.DoesNotContain(settings.Recent, item => item.Path.EndsWith("P0.rproj", StringComparison.Ordinal));
    }

    [Fact]
    public void OnlyProjectsAppsAndInterfacesAreRemembered()
    {
        RoundhouseSettings settings = new();

        Assert.False(settings.Remember(Path.Combine(folder, "main.lua")));
        Assert.True(settings.Remember(Path.Combine(folder, "Dice.rproj")));
        settings.Forget(Path.Combine(folder, "Dice.rproj"));
        Assert.Empty(settings.Recent);
    }

    [Theory]
    [InlineData("<Roundhouse version=\"1\"><Welcome showAtStartup=", "line 1")]
    [InlineData("<Settings/>", "not <Roundhouse>. Using the defaults.")]
    public void AFileItCantReadIsTheDefaultsAndAReason(string text, string message)
    {
        Write(text);

        var (settings, problem) = RoundhouseSettings.Load(SettingsFile);

        Assert.True(settings.ShowAtStartup);
        Assert.Empty(settings.Recent);
        Assert.True(settings.CanSave);
        Assert.Contains(message, problem);
        Assert.EndsWith("Using the defaults.", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void SkipsAnEntryItCantUseAndSaysSo()
    {
        Write($"""
            <Roundhouse version="1">
              <Welcome showAtStartup="maybe"/>
              <Recent path="{Path.Combine(folder, "notes.txt")}"/>
              <Recent path="{Path.Combine(folder, "Dice.rproj")}" kind="project"/>
            </Roundhouse>
            """);

        var (settings, problem) = RoundhouseSettings.Load(SettingsFile);

        Assert.True(settings.ShowAtStartup);
        Assert.Equal([new RecentItem(Path.Combine(folder, "Dice.rproj"), RecentKind.Project)], settings.Recent);
        Assert.Contains("showAtStartup=\"maybe\" isn't true or false", problem);
        Assert.Contains("line 3: a <Recent> without", problem);
    }

    [Fact]
    public void ANewerFilesSettingsAreTheDefaultsAndItIsntSavedOver()
    {
        Write("<Roundhouse version=\"2\"><Welcome showAtStartup=\"false\"/></Roundhouse>");

        var (settings, problem) = RoundhouseSettings.Load(SettingsFile);

        Assert.True(settings.ShowAtStartup);
        Assert.False(settings.CanSave);
        Assert.Contains("reads up to 1", problem);
        Assert.Throws<InvalidOperationException>(() => settings.Save(SettingsFile));
        Assert.Contains("version=\"2\"", File.ReadAllText(SettingsFile));
    }

    [Fact]
    public void WhatANewerRoundhouseAddedIsKept()
    {
        var dice = Path.Combine(folder, "Dice.rproj");
        Write($"""
            <Roundhouse version="1">
              <Welcome showAtStartup="true" width="640"/>
              <Recent path="{dice}" kind="project" pinned="true"/>
              <Theme accent="teal"/>
            </Roundhouse>
            """);

        var (settings, problem) = RoundhouseSettings.Load(SettingsFile);
        Assert.Null(problem);
        settings.ShowAtStartup = false;
        settings.Save(SettingsFile);
        var saved = File.ReadAllText(SettingsFile);

        Assert.Contains("<Welcome showAtStartup=\"false\" width=\"640\"/>", saved);
        Assert.Contains($"<Recent path=\"{dice}\" kind=\"project\" pinned=\"true\"/>", saved);
        Assert.Contains("<Theme accent=\"teal\"/>", saved);
    }

    private void Write(string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
        File.WriteAllText(SettingsFile, text);
    }
}
