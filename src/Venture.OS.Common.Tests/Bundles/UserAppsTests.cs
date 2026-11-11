using Venture.OS.Bundles;
using Venture.OS.Desktop;
using Venture.OS.Scripting;
using Venture.OS.Shell;
using Venture.OS.Tests.Desktop;
using Venture.OS.Tests.Scripting;
using Venture.OS.Tests.Shell;

namespace Venture.OS.Tests.Bundles;

/// <summary>
/// The system's apps and the user's, in two temporary folders: Clock in both, Weather only in the user's.
/// </summary>
public sealed class UserAppsTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"venture-user-apps-{Guid.NewGuid():N}");
    private readonly RecordingOutput output = new();
    private readonly AppManager manager;

    public UserAppsTests()
    {
        Write("system", "Clock", "dev.example.clock", "");
        Write("user", "Clock", "dev.example.myclock", "");
        Write("user", "Weather", "dev.example.weather", "shell");

        CommandRegistry registry = new();
        manager = new AppManager(
            Path.Combine(root, "system"),
            new RuntimeCommands(registry.IsBuiltIn),
            new FakeSystemInfo(),
            userAppsDirectory: Path.Combine(root, "user"));
    }

    public void Dispose()
    {
        foreach (var app in (AppManager.RunningApp[])[.. manager.Running])
            manager.CloseApp(app.Bundle.Identifier, new RecordingOutput());
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void ListsTheSystemsAppsThenTheUsers()
    {
        var listings = manager.Available();

        Assert.Equal(
            [("Clock.app", false), ("Clock.app", true), ("Weather.app", true)],
            listings.Select(listing => (listing.Folder, listing.IsUser)));
        Assert.Equal(Path.Combine(root, "user").Replace('\\', '/') + "/Weather.app", listings[2].Path.Replace('\\', '/'));
    }

    [Fact]
    public void ANameFindsTheSystemsAppFirst()
    {
        Assert.True(manager.OpenApp("clock", root, output));

        Assert.True(manager.IsOpen("dev.example.clock"));
        Assert.False(manager.IsOpen("dev.example.myclock"));
        Assert.Empty(output.Lines);
    }

    [Fact]
    public void UserAppsSayWhatTheyNeed()
    {
        Assert.True(manager.OpenApp("weather", root, output));

        Assert.Equal(["Weather needs shell."], output.Lines);
    }

    [Fact]
    public void TheDockPutsUserAppsAfterADividerAndOpensThemByPath()
    {
        DesktopSession session = new(800, 600, manager, root);

        var items = session.Dock.Items;
        Assert.Equal(["Clock", "Clock", "Weather"], items.Select(item => item.Name));
        Assert.Equal([false, true, true], items.Select(item => item.IsUser));
        Assert.Equal("Clock", items[0].Target);
        Assert.EndsWith("/user/Clock.app", items[1].Target.Replace('\\', '/'));

        // Two 10-pixel gaps between three icons, plus 12 for the divider.
        Assert.Equal(48 + 10, (int)(session.Dock.IconBounds(1).X - session.Dock.IconBounds(0).X) - 12);
        Assert.Equal(48 + 10, (int)(session.Dock.IconBounds(2).X - session.Dock.IconBounds(1).X));
        RecordingPainter painter = new(800, 600);
        session.Dock.Draw(painter);
    }

    private void Write(string folder, string name, string identifier, string needs)
    {
        var contents = Path.Combine(root, folder, $"{name}.app", "Contents");
        Directory.CreateDirectory(Path.Combine(contents, "Lua"));
        File.WriteAllText(Path.Combine(contents, "Info.xml"),
            $"""<App version="1"><Info identifier="{identifier}" name="{name}" needs="{needs}"/></App>""");
        File.WriteAllText(Path.Combine(contents, "Lua", "main.lua"), "");
    }
}
