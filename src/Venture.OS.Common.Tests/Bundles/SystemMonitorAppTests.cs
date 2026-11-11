using Venture.OS.Bundles;
using Venture.OS.Desktop;
using Venture.OS.Scripting;
using Venture.OS.Shell;
using Venture.OS.Tests.Shell;

namespace Venture.OS.Tests.Bundles;

/// <summary>
/// Opens the shipped <c>System Monitor.app</c> from <c>src/Venture.OS/system/apps</c> on a system whose readings the
/// tests change between ticks.
/// </summary>
public sealed class SystemMonitorAppTests : IDisposable
{
    private const ulong Mebibyte = 1024 * 1024;

    private readonly ChangingSystem system = new();
    private readonly RecordingOutput output = new();
    private readonly RecordingOutput events = new();
    private readonly WindowManager windows = new(1024, 768);
    private readonly AppWindows host;
    private readonly AppManager manager;

    public SystemMonitorAppTests()
    {
        CommandRegistry registry = new();
        host = new AppWindows(windows, events);
        manager = new AppManager(
            Path.Combine(AppContext.BaseDirectory, "system", "apps"),
            new RuntimeCommands(registry.IsBuiltIn),
            system,
            windows: host);
        Assert.True(manager.OpenApp("System Monitor", Path.GetTempPath(), output), string.Join('\n', output.Lines));
    }

    public void Dispose() => manager.CloseApp("System Monitor", new RecordingOutput());

    private DesktopWindow Window => Assert.Single(windows.Windows);

    [Fact]
    public void ShowsTheSystemWhenItOpens()
    {
        Assert.Equal("VentureOS 0.3.5 (Build 42)", Window.View.Text("version"));
        Assert.Equal("01:02:05", Window.View.Text("uptime"));
        Assert.Equal("384 of 512 MiB used", Window.View.Text("memory"));
        Assert.Equal(0.75f, Window.View.Value("memoryBar"));
        Assert.Equal("e1000e, 10.0.2.15", Window.View.Text("network"));
        Assert.Empty(output.Lines);
    }

    [Fact]
    public void KeepsCurrentEverySecond()
    {
        host.Tick(TimeSpan.Zero);
        system.Uptime = TimeSpan.FromSeconds(90061);
        system.Memory = new MemoryInfo(256 * Mebibyte, 512 * Mebibyte);
        system.Network = null;

        host.Tick(TimeSpan.FromSeconds(0.5));
        Assert.Equal("01:02:05", Window.View.Text("uptime"));

        host.Tick(TimeSpan.FromSeconds(1));
        Assert.Equal("1d 01:01:01", Window.View.Text("uptime"));
        Assert.Equal("256 of 512 MiB used", Window.View.Text("memory"));
        Assert.Equal("No network device.", Window.View.Text("network"));
        Assert.Empty(events.Lines);
    }

    [Fact]
    public void ShowsWhatItCantReadAsUnknown()
    {
        host.Tick(TimeSpan.Zero);
        system.Uptime = null;
        system.Memory = null;
        system.NetworkFails = true;

        host.Tick(TimeSpan.FromSeconds(1));

        Assert.Equal("unknown", Window.View.Text("uptime"));
        Assert.Equal("unknown", Window.View.Text("memory"));
        Assert.Equal(0f, Window.View.Value("memoryBar"));
        Assert.Equal("unknown", Window.View.Text("network"));
        Assert.Empty(events.Lines);
    }

    [Fact]
    public void DescribesANetworkThatIsntReady()
    {
        host.Tick(TimeSpan.Zero);
        system.Network = new NetworkInfo("e1000e", "52:54:00:12:34:56", LinkUp: true, Ready: false, null, null, null);
        host.Tick(TimeSpan.FromSeconds(1));
        Assert.Equal("e1000e, waiting for an address", Window.View.Text("network"));

        system.Network = system.Network with { LinkUp = false };
        host.Tick(TimeSpan.FromSeconds(2));
        Assert.Equal("e1000e, no link", Window.View.Text("network"));
    }

    private sealed class ChangingSystem : ISystemInfo
    {
        public string Name => "VentureOS";
        public string Version => "0.3.5";
        public uint BuildNumber => 42;
        public string Commit => "abc1234";
        public string CosmosVersion => "3.0.94";
        public TimeSpan? Uptime { get; set; } = TimeSpan.FromSeconds(3725);
        public MemoryInfo? Memory { get; set; } = new(128 * Mebibyte, 512 * Mebibyte);
        public bool NetworkFails { get; set; }

        public NetworkInfo? Network
        {
            get => NetworkFails ? throw new InvalidOperationException("The adapter stopped answering.") : network;
            set => network = value;
        }

        public string? SystemDirectory => null;

        public SpaceInfo? Space(string path) => null;

        private NetworkInfo? network = new("e1000e", "52:54:00:12:34:56", LinkUp: true, Ready: true, "10.0.2.15", "255.255.255.0", "10.0.2.2");
    }
}
