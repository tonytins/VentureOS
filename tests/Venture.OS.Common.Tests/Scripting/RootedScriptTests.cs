using Venture.OS.Bundles;
using Venture.OS.Scripting;
using Venture.OS.Shell;
using Venture.OS.Tests.Shell;

namespace Venture.OS.Tests.Scripting;

/// <summary>
/// Scripts where VentureOS runs inside another system, as in Roundhouse: their files are the root's, and nothing
/// they can call reaches the host's files or programs.
/// </summary>
public sealed class RootedScriptTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), $"venture-rooted-{Guid.NewGuid():N}");
    private readonly string root;
    private readonly string outside;
    private readonly FakeSystemInfo system;

    public RootedScriptTests()
    {
        root = Path.Combine(folder, "root");
        outside = Path.Combine(folder, "outside");
        Directory.CreateDirectory(Path.Combine(root, "mnt", "user"));
        Directory.CreateDirectory(Path.Combine(root, "mnt", "system", "lib", "venture"));
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "secret.txt"), "the host's");
        File.WriteAllText(Path.Combine(outside, "evil.lua"), "return 'the host ran this'");
        File.WriteAllText(Path.Combine(root, "mnt", "system", "lib", "venture", "greet.lua"), "return 'hello from the library'");
        system = new FakeSystemInfo { Root = new FileRoot(root), SystemDirectory = "/mnt/system" };
    }

    public void Dispose() => Directory.Delete(folder, recursive: true);

    [Fact]
    public void AScriptsFilesAreTheRoots()
    {
        var lines = Run("""
            assert(venture.files.write("/mnt/user/note.txt", "kept"))
            print(venture.files.read("note.txt"))
            local names = {}
            for _, entry in ipairs(venture.files.list("/")) do names[#names + 1] = entry.name end
            print(table.concat(names, ","))
            """);

        Assert.Equal(["kept", "mnt"], lines);
        Assert.Equal("kept", File.ReadAllText(Path.Combine(root, "mnt", "user", "note.txt")));
    }

    [Fact]
    public void DotDotAndTheHostsOwnPathsStayInside()
    {
        var secret = Path.Combine(outside, "secret.txt").Replace('\\', '/');
        var lines = Run($$"""
            print(venture.files.read("/../../outside/secret.txt"))
            print(venture.files.read("{{secret}}"))
            print(venture.files.write("/../../escaped.txt", "out"))
            """);

        Assert.DoesNotContain(lines, line => line.Contains("the host's"));
        Assert.False(File.Exists(Path.Combine(folder, "escaped.txt")));
        Assert.True(File.Exists(Path.Combine(root, "escaped.txt")));
    }

    [Fact]
    public void ALinkPlantedTowardTheHostLeadsNowhere()
    {
        Directory.CreateSymbolicLink(Path.Combine(root, "mnt", "user", "out"), outside);

        var lines = Run("""
            print(venture.files.read("/mnt/user/out/secret.txt"))
            print(venture.files.write("/mnt/user/out/planted.txt", "x"))
            print(venture.files.remove("/mnt/user/out/secret.txt"))
            """);

        Assert.All(lines, line => Assert.Contains("leads outside VentureOS's files", line));
        Assert.True(File.Exists(Path.Combine(outside, "secret.txt")));
        Assert.False(File.Exists(Path.Combine(outside, "planted.txt")));
    }

    [Theory]
    [InlineData("io.open('/etc/passwd')", "io.open")]
    [InlineData("io.lines('/etc/passwd')", "io.lines")]
    [InlineData("io.popen('ls')", "io.popen")]
    [InlineData("os.execute('echo hi')", "os.execute")]
    [InlineData("os.remove('/tmp/x')", "os.remove")]
    [InlineData("os.rename('/tmp/x', '/tmp/y')", "os.rename")]
    [InlineData("os.getenv('HOME')", "os.getenv")]
    [InlineData("dofile('/etc/passwd')", "dofile")]
    [InlineData("loadfile('/etc/passwd')", "loadfile")]
    public void LuasOwnWaysToTheHostAreRefusedWithAReason(string call, string name)
    {
        var lines = Run($"print(pcall(function() return {call} end))");

        Assert.Contains($"{name} isn't available while VentureOS runs inside Roundhouse", lines[0]);
    }

    [Fact]
    public void DebugAndNativeLibrariesAreGone()
    {
        var lines = Run("print(debug, package.loadlib, package.searchpath, package.cpath)");

        Assert.Equal("nil\tnil\tnil\t", lines[0]);
    }

    [Fact]
    public void WriteAndLoadingTextStillWork()
    {
        var lines = Run("""
            io.write("written\n")
            print(load("return 1 + 1")())
            print(os.time() > 0)
            """);

        Assert.Equal(["written", "2", "true"], lines);
    }

    [Fact]
    public void RequireLoadsFromItsOwnFoldersOnlyEvenIfAScriptChangesThePath()
    {
        var evil = Path.Combine(outside, "?.lua").Replace('\\', '/');
        var lines = Run($$"""
            print(require("venture.greet"))
            print(select(2, require("venture.greet")))
            package.path = "{{evil}}"
            print(pcall(require, "evil"))
            """);

        // Where it was found isn't the host's path, which would say where VentureOS's files are.
        Assert.Equal("hello from the library\tventure.greet", lines[0]);
        Assert.StartsWith("false", lines[2]);
        Assert.DoesNotContain("the host ran this", lines[2]);
    }

    [Fact]
    public void WithoutARootLuasOwnLibrariesAreAsTheKernelHasThem()
    {
        RecordingOutput output = new();

        LuaRunner.Run(output, "/", lua => lua.DoString("print(type(io.open), type(os.execute), type(debug))"), new FakeSystemInfo());

        Assert.Equal(["function\tfunction\ttable"], output.Lines);
    }

    [Fact]
    public void AnAppSeesItsOwnPathAsVentureOSNamesIt()
    {
        var apps = Path.Combine(root, "mnt", "user", "apps");
        var bundle = Path.Combine(apps, "Probe.app");
        Directory.CreateDirectory(Path.Combine(bundle, "Contents", "Lua"));
        File.WriteAllText(Path.Combine(bundle, "Contents", "Info.xml"),
            "<App version='1'><Info identifier='dev.example.probe' name='Probe' version='1.0' needs=''/></App>");
        File.WriteAllText(Path.Combine(bundle, "Contents", "Lua", "main.lua"), "print(venture.app.path)");
        RecordingOutput output = new();
        AppManager manager = new(apps, new RuntimeCommands(_ => false), system);

        Assert.True(manager.OpenApp("/mnt/user/apps/Probe.app", "/mnt/user", output));
        Assert.Equal(["/mnt/user/apps/Probe.app"], output.Lines);
        Assert.Equal("/mnt/user/apps/Probe.app", manager.ScriptPath(bundle));
        Assert.False(manager.OpenApp("/../outside", "/mnt/user", output));
        manager.CloseApp("Probe", output);
    }

    [Fact]
    public void WallpapersArePathsAsTheScriptSeesThem_AndOnesOutsideAreLeftOut()
    {
        var inside = Path.Combine(root, "mnt", "system", "wallpaper", "Charger.png");
        var host = Path.Combine(outside, "Elsewhere.png");
        FakeSystemInfo shown = new()
        {
            Root = new FileRoot(root),
            SystemDirectory = "/mnt/system",
            Appearance = new WallpapersOnly([new("Charger", "Charger", inside), new("Elsewhere", "Elsewhere", host)]),
        };

        var lines = Run("""
            for _, wallpaper in ipairs(venture.wallpapers()) do print(wallpaper.name, wallpaper.path) end
            """, shown);

        Assert.Equal(["Charger\t/mnt/system/wallpaper/Charger.png"], lines);
    }

    /// <summary>An appearance with only wallpapers to give, at the host paths it's told.</summary>
    private sealed class WallpapersOnly(IReadOnlyList<WallpaperChoice> choices) : IAppearance
    {
        public bool IsDark => false;
        public string Mode => "light";
        public string Accent => "#3584E4";
        public bool RoundedCorners => true;
        public string Dock => "always";
        public string ScrollBars => "classic";
        public string Wallpaper => "sky";
        public string DarkFrom => "19:00";
        public string DarkUntil => "07:00";

        public IReadOnlyList<WallpaperChoice> Wallpapers() => choices;

        public AppearanceChange Set(AppearanceRequest request) => new("not here", null);
    }

    private List<string> Run(string code, FakeSystemInfo? on = null)
    {
        RecordingOutput output = new();
        LuaRunner.Run(output, "/mnt/user", lua => lua.DoString(code), on ?? system, scopes: ScriptScopes.All,
            libraryDirectory: Path.Combine(root, "mnt", "system", "lib"));
        return output.Lines;
    }
}
