using Venture.OS.Bundles;
using Venture.OS.Desktop;
using Venture.OS.Interface;
using Venture.OS.Scripting;
using Venture.OS.Shell;
using Venture.OS.Tests.Scripting;
using Venture.OS.Tests.Shell;

namespace Venture.OS.Tests.Desktop;

/// <summary><c>venture.desktop</c>, from an app that has the <c>system</c> scope, and one that hasn't.</summary>
public sealed class DesktopLibraryTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"venture-desktoplib-{Guid.NewGuid():N}").Replace('\\', '/');
    private readonly RecordingOutput output = new();
    private readonly RecordingOutput events = new();
    private readonly WindowManager windows = new(800, 600);
    private readonly AppWindows host;
    private readonly AppManager manager;

    public DesktopLibraryTests()
    {
        host = new AppWindows(windows, events) { FallbackApp = "Zeta" };
        CommandRegistry registry = new();
        manager = new AppManager($"{root}/apps", new RuntimeCommands(registry.IsBuiltIn), new FakeSystemInfo(), windows: host);
        App("Alpha", "", "");
        App("Zeta", "", "", icon: true);
    }

    public void Dispose()
    {
        foreach (var app in manager.Running.ToList())
            manager.CloseApp(app.Bundle.Identifier, new RecordingOutput());
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void ListsTheAppsWithTheDesktopsFirst()
    {
        Open("""
            for _, app in ipairs(venture.desktop.apps()) do
              print(app.name, app.identifier, app.user, app.open, app.icon ~= nil, app.path:match("[^/]+$"))
            end
            """);

        Assert.Equal([
            "Zeta\tdev.example.zeta\tfalse\tfalse\ttrue\tZeta.app",
            "Alpha\tdev.example.alpha\tfalse\tfalse\tfalse\tAlpha.app",
            "Probe\tdev.example.probe\tfalse\tfalse\tfalse\tProbe.app",
        ], output.Lines);
    }

    [Fact]
    public void AnAppOutOfTheDockIsntListedEvenOnceItAsks()
    {
        App("Ghost", "", "", extra: "dock=\"hidden\"");

        Open("""
            venture.ui.setShownInDock(false)
            for _, app in ipairs(venture.desktop.apps()) do print(app.name) end
            venture.ui.setShownInDock(true)
            for _, app in ipairs(venture.desktop.apps()) do print(app.name) end
            """);

        Assert.Equal(["Zeta", "Alpha", "Zeta", "Alpha", "Probe"], output.Lines);
    }

    [Fact]
    public void OpensAnAppAndRestoresAWindow()
    {
        Open($$"""
            print(venture.desktop.open("{{root}}/apps/Alpha.app"))
            print(venture.desktop.restore(99))
            """);

        Assert.True(manager.IsOpen("dev.example.alpha"));
        Assert.Equal("Alpha", host.ActivatedApp);
        Assert.Equal(["true", "nil\tthere's no minimized window 99; venture.desktop.minimized() lists them"], output.Lines);
    }

    [Fact]
    public void ListsMinimizedWindowsByTheirIds()
    {
        DesktopWindow report = new("Report", new UIView(UIScript.Parse("""<UIScript><Window width="100" height="50"/></UIScript>""")));
        windows.Open(report);
        windows.Minimize(report);

        Open("""
            for _, window in ipairs(venture.desktop.minimized()) do print(window.id, window.title) end
            print(venture.desktop.restore(venture.desktop.minimized()[1].id))
            """);

        Assert.Equal([$"{report.Id}\tReport", "true"], output.Lines);
        Assert.False(report.IsMinimized);
    }

    [Fact]
    public void HearsAboutChangesOnceAFrame()
    {
        Open("""venture.desktop.onChange(function() print("changed") end)""");

        host.MarkDesktopChanged();
        host.MarkDesktopChanged();
        host.Tick(TimeSpan.Zero);
        host.Tick(TimeSpan.FromSeconds(1));

        Assert.Equal(["changed"], events.Lines);
    }

    [Fact]
    public void StopsHearingWhenTheAppQuits()
    {
        Open("""venture.desktop.onChange(function() print("changed") end)""");

        manager.CloseApp("Probe", new RecordingOutput());
        host.MarkDesktopChanged();
        host.Tick(TimeSpan.Zero);

        Assert.Empty(events.Lines);
    }

    [Fact]
    public void NeedsTheSystemScope()
    {
        App("Probe", "files", """print(pcall(venture.desktop.apps))""");

        Assert.True(manager.OpenApp("Probe", root, output), string.Join('\n', output.Lines));

        Assert.StartsWith("false", output.Lines.Single());
        Assert.Contains("venture.desktop.apps needs the system scope", output.Lines.Single(), StringComparison.Ordinal);
    }

    private void Open(string main)
    {
        App("Probe", "system", main);
        Assert.True(manager.OpenApp("Probe", root, output), string.Join('\n', output.Lines));
    }

    private void App(string name, string needs, string main, bool icon = false, string extra = "")
    {
        var contents = $"{root}/apps/{name}.app/Contents";
        Directory.CreateDirectory($"{contents}/Lua");
        Directory.CreateDirectory($"{contents}/Resources");
        var iconAttribute = icon ? """ icon="Contents/Resources/AppIcon.png" """ : "";
        File.WriteAllText($"{contents}/Info.xml",
            $"""<App version="1"><Info identifier="dev.example.{name.ToLowerInvariant()}" name="{name}" needs="{needs}"{iconAttribute} {extra}/></App>""");
        File.WriteAllText($"{contents}/Lua/main.lua", main + "\n");
        if (icon)
            File.WriteAllText($"{contents}/Resources/AppIcon.png", "a picture");
    }
}
