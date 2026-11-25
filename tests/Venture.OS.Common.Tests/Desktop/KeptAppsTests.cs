using Venture.OS.Bundles;
using Venture.OS.Desktop;
using Venture.OS.Processes;
using Venture.OS.Shell;
using Venture.OS.Scripting;
using Venture.OS.Tests.Scripting;
using Venture.OS.Tests.Shell;

namespace Venture.OS.Tests.Desktop;

/// <summary>
/// The apps the desktop keeps running are the system apps whose <c>Info.xml</c> says <c>kept="true"</c>, not names in
/// its code: Shelf, the fallback app, and Watch, another system app, are kept; Plain, a system app that doesn't say so,
/// isn't; and Sneaky, a user app that says so, isn't either, since an app can't make itself unkillable.
/// </summary>
public sealed class KeptAppsTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"venture-kept-{Guid.NewGuid():N}").Replace('\\', '/');
    private readonly HeldOutput held = new();
    private readonly WindowManager windows = new(800, 600);
    private readonly AppManager apps;
    private readonly DesktopSession session;
    private TimeSpan now;

    public KeptAppsTests()
    {
        App("apps", "Shelf", kept: true);
        App("apps", "Watch", kept: true);
        App("apps", "Plain", kept: false);
        App("user/apps", "Sneaky", kept: true);

        CommandRegistry registry = new();
        apps = new AppManager(
            $"{root}/apps", new RuntimeCommands(registry.IsBuiltIn), new FakeSystemInfo(), windows: new AppWindows(windows, held),
            userAppsDirectory: $"{root}/user/apps", supervisor: new Supervisor(() => now));
        session = new DesktopSession(800, 600, apps, root, held, windows) { FallbackApp = "Shelf", Clock = () => now };
        Frame();
    }

    public void Dispose()
    {
        session.Leave();
        foreach (var app in apps.Running.ToList())
        {
            apps.Supervisor.StopKeeping(app.Bundle.Identifier);
            apps.CloseApp(app.Bundle.Identifier, new RecordingOutput());
        }

        Directory.Delete(root, recursive: true);
    }

    private IEnumerable<string> Running => apps.Running.Select(app => app.Bundle.Name);

    private void Frame()
    {
        now += TimeSpan.FromMilliseconds(16);
        session.Update(new DesktopInput(400, 300, false, Time: now));
    }

    [Fact]
    public void TheSystemsKeptAppsStartWithTheDesktopTheFallbackFirst()
    {
        Assert.Equal(["Shelf", "Watch"], Running);
    }

    [Fact]
    public void AKeptAppComesBackWhenItEnds()
    {
        Assert.True(apps.CloseApp("dev.example.watch", new RecordingOutput()));
        Frame();

        Assert.Contains("Watch", Running);
    }

    [Fact]
    public void AUserAppCantKeepItselfAndAnUnkeptAppStaysEnded()
    {
        Assert.True(apps.OpenApp("Sneaky", root, new RecordingOutput()));
        Assert.True(apps.OpenApp("Plain", root, new RecordingOutput()));
        Assert.True(apps.CloseApp("dev.example.sneaky", new RecordingOutput()));
        Assert.True(apps.CloseApp("dev.example.plain", new RecordingOutput()));
        Frame();

        Assert.DoesNotContain("Sneaky", Running);
        Assert.DoesNotContain("Plain", Running);
    }

    private void App(string folder, string name, bool kept)
    {
        var contents = $"{root}/{folder}/{name}.app/Contents";
        Directory.CreateDirectory($"{contents}/Lua");
        File.WriteAllText($"{contents}/Info.xml",
            $"""<App version="1"><Info identifier="dev.example.{name.ToLowerInvariant()}" name="{name}"{(kept ? " kept=\"true\"" : "")}/></App>""");
        File.WriteAllText($"{contents}/Lua/main.lua", "\n");
    }
}
