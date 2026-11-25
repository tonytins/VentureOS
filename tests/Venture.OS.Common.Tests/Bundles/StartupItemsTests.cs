using Venture.OS.Bundles;

namespace Venture.OS.Tests.Bundles;

/// <summary>The person's list of apps to open at startup: read, changed, saved, and what it refuses.</summary>
public sealed class StartupItemsTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"venture-startup-{Guid.NewGuid():N}");
    private readonly StartupItems items;

    public StartupItemsTests()
    {
        Directory.CreateDirectory(root);
        items = new StartupItems(Path.Combine(root, "settings", "startup.xml"), Find);
    }

    public void Dispose() => Directory.Delete(root, recursive: true);

    // Two apps, found by name or identifier, as the system's apps are.
    private Bundle? Find(string app) => app switch
    {
        "Weather" or "dev.example.weather" => App("dev.example.weather", "Weather"),
        "Timer" or "dev.example.timer" => App("dev.example.timer", "Timer"),
        _ => null,
    };

    private Bundle App(string identifier, string name)
    {
        var path = Path.Combine(root, "apps", $"{name}.app");
        Directory.CreateDirectory(Path.Combine(path, "Contents", "Lua"));
        File.WriteAllText(Path.Combine(path, "Contents", "Info.xml"), $"""<App><Info identifier="{identifier}" name="{name}"/></App>""");
        File.WriteAllText(Path.Combine(path, "Contents", "Lua", "main.lua"), "");
        return Bundle.Read(path, BundleKind.App);
    }

    [Fact]
    public void NoFileIsAnEmptyList()
    {
        Assert.Empty(items.Load().Identifiers);
        Assert.Null(items.Load().Problem);
    }

    [Fact]
    public void AddingAndRemovingByNameOrIdentifierSavesTheList()
    {
        Assert.True(items.TrySet("Weather", true, out var problem), problem);
        Assert.True(items.TrySet("dev.example.timer", true, out problem), problem);
        Assert.True(items.TrySet("Weather", true, out problem), problem);
        Assert.Equal(["dev.example.timer", "dev.example.weather"], items.Load().Identifiers);

        Assert.True(items.TrySet("Timer", false, out problem), problem);
        Assert.Equal(["dev.example.weather"], new StartupItems(items.Path, Find).Load().Identifiers);
        Assert.True(items.Contains("dev.example.weather"));
    }

    [Fact]
    public void AnAppThatIsntInstalledCantBeAddedButCanBeTakenOff()
    {
        Assert.False(items.TrySet("Ghost", true, out var problem));
        Assert.Equal("there's no app called Ghost", problem);

        Write("""<StartupItems version="1"><App identifier="dev.example.gone"/></StartupItems>""");
        Assert.True(items.TrySet("dev.example.gone", false, out problem), problem);
        Assert.Empty(items.Load().Identifiers);
    }

    [Fact]
    public void WhatItDoesntKnowIsSkipped()
    {
        Write("""<StartupItems version="1" later="yes"><App identifier="dev.example.weather" hidden="true"/><Group/><App/></StartupItems>""");

        Assert.Equal(["dev.example.weather"], items.Load().Identifiers);
        Assert.Null(items.Load().Problem);
    }

    [Theory]
    [InlineData("""<Appearance version="1"/>""", "isn't a list of startup items")]
    [InlineData("""<StartupItems version="2"/>""", "reads up to 1")]
    [InlineData("""<StartupItems""", "couldn't read")]
    public void AFileItCantReadSaysWhyAndIsntOverwritten(string text, string reason)
    {
        Write(text);

        Assert.Contains(reason, items.Load().Problem);
        Assert.False(items.TrySet("Weather", true, out var problem));
        Assert.Contains(reason, problem);
        Assert.Equal(text, File.ReadAllText(items.Path!));
    }

    private void Write(string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(items.Path!)!);
        File.WriteAllText(items.Path!, text);
    }
}
