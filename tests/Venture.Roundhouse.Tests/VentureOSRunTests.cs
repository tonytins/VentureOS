using System.Net;
using System.Net.Sockets;
using System.Text;
using Venture.OS.Vdb;
using Venture.Roundhouse.Vdb;

namespace Venture.Roundhouse.Tests;

/// <summary>
/// Running an app on VentureOS from Roundhouse, against VentureOS's own <see cref="VdbSession"/> listening on a
/// loopback socket, so Roundhouse's TCP, client, and identity meet the code the kernel runs, sealed and all, with no
/// QEMU.
/// </summary>
public sealed class VentureOSRunTests : IDisposable
{
    private const string Identity = "0123456789abcdef0123456789abcdef";
    private const string Info = """<App version="1"><Info identifier="dev.example.dice" name="Dice"/></App>""";

    private readonly string folder = Path.Combine(Path.GetTempPath(), $"roundhouse-vdb-{Guid.NewGuid():N}");
    private readonly string app;

    public VentureOSRunTests()
    {
        app = Path.Combine(folder, "Dice.app");
        Directory.CreateDirectory(Path.Combine(app, "Contents", "Lua"));
        File.WriteAllText(Path.Combine(app, "Contents", "Info.xml"), Info);
        File.WriteAllText(Path.Combine(app, "Contents", "Lua", "main.lua"), "print('saved')");
    }

    public void Dispose() => Directory.Delete(folder, recursive: true);

    [Fact]
    public void SendsTheAppWithItsEditsOpensItAndShowsWhatItPrints()
    {
        using LoopbackVentureOS ventureOS = new(Path.Combine(folder, "venture"), trusted: true);
        var script = Path.Combine(app, "Contents", "Lua", "main.lua");
        List<VentureOSRunLine> lines = [];
        using CancellationTokenSource stop = new(TimeSpan.FromSeconds(20));

        var opened = Run(ventureOS).Run(app, new Dictionary<string, string> { [script] = "print('edited')" }, line =>
        {
            lock (lines)
                lines.Add(line);
            if (line.IsOutput)
                stop.Cancel();
        }, stop.Token);

        Assert.True(opened);
        Assert.Contains(lines, line => line is { IsOutput: true, Text: "Rolled a 4" });
        Assert.DoesNotContain(lines, line => line.IsProblem);
        Assert.Equal("Stopped.", lines[^1].Text);
        Assert.Equal("print('edited')", File.ReadAllText(Path.Combine(ventureOS.AppsFolder, "Dice.app", "Contents", "Lua", "main.lua")));
        Assert.Equal(["Dice.app"], ventureOS.Launched);
        Assert.All(ventureOS.Network.Skip(1), line => Assert.StartsWith(VdbChannel.Prefix, line));
    }

    [Fact]
    public void WaitsToBeTrustedAndSaysWhyItStopped()
    {
        using LoopbackVentureOS ventureOS = new(Path.Combine(folder, "venture"), trusted: false);
        List<VentureOSRunLine> lines = [];

        var opened = Run(ventureOS, trustTimeout: TimeSpan.FromMilliseconds(300)).Run(app, new Dictionary<string, string>(), lines.Add, CancellationToken.None);

        Assert.False(opened);
        Assert.Contains(lines, line => line.Text == "Waiting to be trusted: choose Trust on VentureOS.");
        Assert.True(lines[^1].IsProblem);
        Assert.Contains("whether to trust this computer", lines[^1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void SaysSoWhenNothingIsListening()
    {
        var port = FreePort();
        List<VentureOSRunLine> lines = [];
        VentureOSRun run = new(stop => Connect(port, stop), () => Identity);

        Assert.False(run.Run(app, new Dictionary<string, string>(), lines.Add, CancellationToken.None));
        Assert.True(lines[^1].IsProblem);
        Assert.Contains("Nothing answered", lines[^1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void RefusesAnAppThatCannotBeSentBeforeConnecting()
    {
        File.CreateSymbolicLink(Path.Combine(app, "passwd"), "/etc/passwd");
        var connected = false;
        List<VentureOSRunLine> lines = [];
        VentureOSRun run = new(_ => { connected = true; throw new InvalidOperationException(); }, () => Identity);

        Assert.False(run.Run(app, new Dictionary<string, string>(), lines.Add, CancellationToken.None));
        Assert.False(connected);
        Assert.Contains("symbolic link", Assert.Single(lines).Text, StringComparison.Ordinal);
    }

    [Fact]
    public void TheIdentityIsTheVdbToolsAndMadeOnceForItsOwnerAlone()
    {
        var path = VdbIdentityFile.DefaultPath(name => name is "XDG_CONFIG_HOME" or "APPDATA" ? folder : null);
        Assert.Equal(Path.Combine(folder, "vdb", "identity"), path);

        var made = VdbIdentityFile.Load(path);

        Assert.True(VdbTrust.IsIdentity(made));
        Assert.Equal(made, VdbIdentityFile.Load(path));
        if (!OperatingSystem.IsWindows())
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));

        File.WriteAllText(path, "not an identity");
        Assert.Throws<IOException>(() => VdbIdentityFile.Load(path));
    }

    private static VentureOSRun Run(LoopbackVentureOS ventureOS, TimeSpan? trustTimeout = null) =>
        new(stop => Connect(ventureOS.Port, stop), () => Identity)
        {
            AnswerTimeout = TimeSpan.FromSeconds(10),
            TrustTimeout = trustTimeout ?? TimeSpan.FromSeconds(10),
        };

    private static async Task<IVdbLineTransport> Connect(int port, CancellationToken stop) =>
        await TcpLineTransport.ConnectAsync("127.0.0.1", port, TimeSpan.FromSeconds(2), stop);

    private static int FreePort()
    {
        TcpListener probe = new(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    /// <summary>
    /// VentureOS's side on a loopback socket: one connection, handed to a <see cref="VdbSession"/> on a thread of its
    /// own, as the kernel's listener does from the desktop's frame loop. An app it opens prints a line.
    /// </summary>
    private sealed class LoopbackVentureOS : IVdbHost, IDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly Thread thread;
        private readonly VdbTrust trust = new();
        private volatile bool stopping;
        private bool launched;

        public LoopbackVentureOS(string root, bool trusted)
        {
            StagingFolder = Path.Combine(root, ".vdb");
            AppsFolder = Path.Combine(root, "apps");
            Directory.CreateDirectory(AppsFolder);
            if (trusted)
                trust.Trust(Identity);
            listener.Start();
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            thread = new Thread(Serve) { IsBackground = true };
            thread.Start();
        }

        public int Port { get; }

        public string StagingFolder { get; }

        public string AppsFolder { get; }

        public List<string> Launched { get; } = [];

        /// <summary>What VentureOS sent, as it went on the network.</summary>
        public List<string> Network { get; } = [];

        public bool IsSystemApp(string identifier) => false;

        public void CloseApp(string identifier)
        {
        }

        public string? Launch(string app)
        {
            Launched.Add(app);
            launched = true;
            return null;
        }

        public void Dispose()
        {
            stopping = true;
            listener.Stop();
            thread.Join(TimeSpan.FromSeconds(5));
        }

        private void Serve()
        {
            try
            {
                using var client = listener.AcceptTcpClient();
                using var stream = client.GetStream();
                var started = DateTime.UtcNow;
                VdbSession session = new(this, trust, line =>
                {
                    lock (Network)
                        Network.Add(line);
                    stream.Write(Encoding.UTF8.GetBytes(line + "\n"));
                }, nonce => Random.Shared.NextBytes(nonce), TimeSpan.Zero);

                var buffer = new byte[16 * 1024];
                while (!stopping && !session.IsClosed)
                {
                    var now = DateTime.UtcNow - started;
                    if (client.Client.Poll(20_000, SelectMode.SelectRead))
                    {
                        var read = stream.Read(buffer);
                        if (read == 0)
                            break;
                        session.Receive(buffer.AsSpan(0, read), now);
                    }

                    if (launched)
                    {
                        launched = false;
                        session.Print("Rolled a 4");
                    }

                    session.Tick(now);
                }

                session.Close();
            }
            catch (Exception exception) when (exception is SocketException or IOException or ObjectDisposedException)
            {
                // The test is over, or the client went away; either way the session is done.
            }
        }
    }
}
