using System.Security.Cryptography;
using Venture.OS.Vdb;

namespace Venture.Roundhouse.Vdb;

/// <summary>What a run on VentureOS reports, a line at a time, for the person to read.</summary>
/// <param name="Text">The line.</param>
/// <param name="IsOutput">Whether the app printed it, rather than Roundhouse saying it.</param>
/// <param name="IsProblem">Whether it says why the run stopped short.</param>
public readonly record struct VentureOSRunLine(string Text, bool IsOutput = false, bool IsProblem = false);

/// <summary>
/// Runs the app being edited on a running VentureOS, over vdb, as the <c>vdb</c> tool's <c>install --launch</c> does:
/// reads the app, with Roundhouse's unsaved edits in place of its files, connects, says hello as this computer,
/// installs and opens it, and passes on what it prints until stopped. Nothing here touches the window, so it's
/// tested apart from it.
/// </summary>
public sealed class VentureOSRun
{
    /// <summary>Where VentureOS listens under QEMU, with <c>./run.sh --vdb</c> forwarding its port.</summary>
    public const string DefaultHost = "127.0.0.1";

    private readonly Func<CancellationToken, Task<IVdbLineTransport>> connect;
    private readonly Func<string> identity;
    private readonly Action<Span<byte>> nonce;

    /// <param name="connect">Opens the connection.</param>
    /// <param name="identity">This computer's identity.</param>
    /// <param name="nonce">This computer's nonce for each connection; the system's generator unless a test fixes it.</param>
    public VentureOSRun(Func<CancellationToken, Task<IVdbLineTransport>> connect, Func<string> identity, Action<Span<byte>>? nonce = null)
    {
        ArgumentNullException.ThrowIfNull(connect);
        ArgumentNullException.ThrowIfNull(identity);
        this.connect = connect;
        this.identity = identity;
        this.nonce = nonce ?? RandomNumberGenerator.Fill;
    }

    /// <summary>A run over TCP to VentureOS at <paramref name="host"/> and <paramref name="port"/>, as this computer.</summary>
    public static VentureOSRun OverTcp(string host = DefaultHost, int port = VdbProtocol.DefaultPort) =>
        new(async stop => await TcpLineTransport.ConnectAsync(host, port, TimeSpan.FromSeconds(5), stop).ConfigureAwait(false),
            () => VdbIdentityFile.Load(VdbIdentityFile.DefaultPath()));

    /// <summary>How long to wait for each answer, and to be trusted; shortened by tests.</summary>
    public TimeSpan AnswerTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <inheritdoc cref="AnswerTimeout"/>
    public TimeSpan TrustTimeout { get; init; } = VdbProtocol.TrustLimit + TimeSpan.FromSeconds(10);

    /// <summary>
    /// Runs the app in <paramref name="folder"/>, reporting through <paramref name="report"/> from the thread it runs
    /// on, until <paramref name="stop"/> is cancelled or something stops it.
    /// </summary>
    /// <param name="edits">Unsaved files' text, by full path, sent in place of what's on disk.</param>
    /// <returns>Whether the app was installed and opened.</returns>
    public bool Run(string folder, IReadOnlyDictionary<string, string> edits, Action<VentureOSRunLine> report, CancellationToken stop)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(edits);
        ArgumentNullException.ThrowIfNull(report);

        VdbApp app;
        string computer;
        try
        {
            app = VdbApp.Read(folder, edits);
            computer = identity();
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            report(new VentureOSRunLine(exception.Message, IsProblem: true));
            return false;
        }

        IVdbLineTransport? transport = null;
        var opened = false;
        try
        {
            report(new VentureOSRunLine("Connecting to VentureOS…"));
            transport = connect(stop).GetAwaiter().GetResult();
            VdbClient client = new(transport, text => report(new VentureOSRunLine(text, IsOutput: true)), nonce,
                text => report(new VentureOSRunLine(text)))
            {
                AnswerTimeout = AnswerTimeout,
                TrustTimeout = TrustTimeout,
            };

            client.Hello(computer);
            report(new VentureOSRunLine($"Sending {app.Name}…"));
            var installed = client.Install(app);
            client.Launch(app.Name);
            opened = true;
            report(new VentureOSRunLine($"Installed {installed} and opened it. What it prints shows here until you stop."));

            try
            {
                client.Follow(stop);
                client.Bye();
                report(new VentureOSRunLine("Stopped."));
            }
            catch (VdbClientException exception) when (stop.IsCancellationRequested)
            {
                // Stopping while VentureOS was saying something; the connection ends either way.
                report(new VentureOSRunLine($"Stopped. {exception.Message}"));
            }
        }
        catch (OperationCanceledException)
        {
            report(new VentureOSRunLine("Stopped."));
        }
        catch (VdbClientException exception)
        {
            report(new VentureOSRunLine(exception.Message, IsProblem: true));
        }
        finally
        {
            (transport as IDisposable)?.Dispose();
        }

        return opened;
    }
}
