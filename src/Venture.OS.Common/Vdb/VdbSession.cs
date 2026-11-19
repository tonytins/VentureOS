using System.Text;
using Venture.OS.Cryptography;

namespace Venture.OS.Vdb;

/// <summary>
/// One vdb connection, from VentureOS's side: it's handed the bytes that arrive and the time, and answers through
/// <c>send</c>, a line at a time, without the line break. It knows nothing of sockets, so the kernel can drive it
/// from the desktop's frame loop and tests can drive it directly.
/// </summary>
/// <remarks>
/// <para>Only <c>hello</c>, and pairing a computer the person has just approved, are plain text. From VentureOS's
/// <c>hello</c> on, every line is sealed with a <see cref="VdbChannel"/>; a line that doesn't open ends the connection
/// at once, with nothing sent back, since a reason couldn't be trusted to reach the right party.</para>
/// Every command is answered with <c>ok</c> or <c>error</c>, except <c>file</c> and <c>data</c>, whose answer comes
/// at the file's <c>end</c>. An error ends the command, and a failed file or <c>commit</c> ends the install, but the
/// connection stays, unless the error starts <c>error closing</c>. After that, or <c>bye</c>,
/// <see cref="IsClosed"/> is set and the connection should be closed once what was sent has gone.
/// </remarks>
public sealed class VdbSession
{
    /// <summary>The features this VentureOS offers in its <c>hello</c>.</summary>
    public const string Features = "install launch";

    private readonly IVdbHost host;
    private readonly VdbTrust trust;
    private readonly Action<string> send;
    private readonly Action<Span<byte>> random;
    private readonly VdbLineReader reader = new(VdbChannel.MaximumLineLength);

    private string? keyId;
    private readonly byte[] clientNonce = new byte[VdbChannel.NonceSize];
    private bool askedForIdentity;
    private VdbChannel? channel;
    private bool ready;
    private TimeSpan lastHeard;
    private TimeSpan askedAt;
    private VdbInstall? install;

    // After an error inside a file, its remaining data and its end are passed over, so the file is answered once.
    private bool skippingFile;

    /// <param name="host">Where apps go, and the apps that are there.</param>
    /// <param name="trust">The computers VentureOS trusts.</param>
    /// <param name="send">Sends a line to the computer, as it goes on the network: sealed, once the channel is.</param>
    /// <param name="random">Fills VentureOS's nonce for the connection.</param>
    /// <param name="now">The time the connection opened, on any clock that only goes forward.</param>
    public VdbSession(IVdbHost host, VdbTrust trust, Action<string> send, Action<Span<byte>> random, TimeSpan now)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(trust);
        ArgumentNullException.ThrowIfNull(send);
        ArgumentNullException.ThrowIfNull(random);
        this.host = host;
        this.trust = trust;
        this.send = send;
        this.random = random;
        lastHeard = now;
    }

    /// <summary>Whether the connection is over and should be closed.</summary>
    public bool IsClosed { get; private set; }

    /// <summary>The computer's key ID, once it has said hello, which names it without giving its identity away.</summary>
    public string? KeyId => keyId;

    /// <summary>Whether the computer is trusted and may send commands.</summary>
    public bool IsReady => ready && !IsClosed;

    /// <summary>Handles bytes that arrived, as far as they make whole lines.</summary>
    public void Receive(ReadOnlySpan<byte> bytes, TimeSpan now)
    {
        if (IsClosed)
            return;

        lastHeard = now;
        reader.Feed(bytes);
        while (!IsClosed && reader.Take() is { } line)
            Handle(line);

        if (!IsClosed && reader.Problem is { } problem)
            Close($"error closing {problem}");
    }

    /// <summary>Checks the time: a computer that has said nothing for too long, or a question nobody answered.</summary>
    public void Tick(TimeSpan now)
    {
        if (IsClosed)
            return;

        if (keyId is not null && !ready)
        {
            CheckTrust(now);
            return;
        }

        if (now - lastHeard >= VdbProtocol.IdleLimit)
            Close($"error closing nothing was sent for {VdbProtocol.IdleLimit.TotalSeconds:0} seconds");
    }

    /// <summary>Sends what an app printed, as <c>out</c> lines, once the computer is trusted.</summary>
    public void Print(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!IsReady)
            return;

        // "out " and the text make a line, with control characters, which could end it early, made spaces, and
        // anything past the line's length cut off.
        const int Room = VdbProtocol.MaximumLineLength - 4;
        foreach (var part in text.Split('\n'))
        {
            var characters = part.TrimEnd('\r').ToCharArray();
            for (var index = 0; index < characters.Length; index++)
            {
                if (char.IsControl(characters[index]))
                    characters[index] = ' ';
            }

            var clean = new string(characters, 0, Math.Min(characters.Length, Room));
            for (var excess = Encoding.UTF8.GetByteCount(clean) - Room; excess > 0; excess = Encoding.UTF8.GetByteCount(clean) - Room)
                clean = clean[..^Math.Max(1, excess / 3)];
            Send($"out {clean}");
        }
    }

    /// <summary>
    /// The connection ended from the computer's side, or VentureOS is closing it: an app being sent is thrown away,
    /// and a question about trust is withdrawn.
    /// </summary>
    public void Close()
    {
        IsClosed = true;
        EndInstall();
        if (keyId is not null && !ready)
            trust.Withdraw(keyId);
    }

    private void Handle(string line)
    {
        if (channel is not null)
        {
            // Nothing that doesn't open is acted on or answered: the connection just ends.
            if (channel.Open(line) is { } opened)
                HandleSealed(opened);
            else
                Close();
            return;
        }

        if (line.Length == 0)
            return;
        if (Encoding.UTF8.GetByteCount(line) > VdbProtocol.MaximumLineLength)
        {
            Close($"error closing a line is longer than {VdbProtocol.MaximumLineLength} bytes");
            return;
        }

        var (command, rest) = Split(line);
        if (keyId is null)
        {
            if (command == "hello")
                Hello(rest);
            else
                Close("error closing say hello first");
            return;
        }

        if (askedForIdentity && command == "pair")
        {
            Pair(rest);
            return;
        }

        Close("error closing wait to be trusted before sending commands");
    }

    private static (string Command, string Argument) Split(string line)
    {
        var space = line.IndexOf(' ');
        return space < 0 ? (line, "") : (line[..space], line[(space + 1)..]);
    }

    private void HandleSealed(string line)
    {
        // An empty line keeps the connection open, and asks nothing.
        if (line.Length == 0)
            return;

        var (command, rest) = Split(line);

        if (skippingFile)
        {
            if (command == "data")
                return;
            skippingFile = false;
            if (command == "end")
                return;
        }

        if (install is { HasOpenFile: true } && command is not ("data" or "end"))
        {
            Fail($"{command} came before the open file's end");
            return;
        }

        switch (command)
        {
            case "install":
                Install(rest);
                break;
            case "file":
                BeginFile(rest);
                break;
            case "data":
                Answer(install is null ? "no file is open" : install.AddData(rest), inFile: true);
                break;
            case "end":
                Answer(install is null ? "no file is open" : install.EndFile(), inFile: false, ok: true);
                break;
            case "commit":
                Commit();
                break;
            case "abort":
                EndInstall();
                Send("ok");
                break;
            case "launch":
                Launch(rest);
                break;
            case "bye":
                Send("ok");
                Close();
                break;
            default:
                Send($"error \"{Shorten(command)}\" isn't a vdb command");
                break;
        }
    }

    private void Hello(string rest)
    {
        var words = rest.Split(' ');
        if (words.Length < 1 || !words[0].StartsWith("vdb/", StringComparison.Ordinal))
        {
            Close($"error closing hello names the protocol, the computer, and a nonce, such as hello {VdbProtocol.Version} <key-id> <nonce>");
            return;
        }

        if (words[0] != VdbProtocol.Version)
        {
            Close($"error closing unsupported version, this VentureOS speaks {VdbProtocol.Version}");
            return;
        }

        if (words.Length < 3 || !VdbTrust.IsKeyId(words[1]))
        {
            Close($"error closing hello names the computer by its key ID, {VdbTrust.KeyIdLength} lowercase hexadecimal characters, then a nonce");
            return;
        }

        if (!Hex.IsLowercase(words[2], VdbChannel.NonceSize * 2))
        {
            Close($"error closing a nonce is {VdbChannel.NonceSize * 2} lowercase hexadecimal characters");
            return;
        }

        // Features the computer offers are for later versions to use; this one asks nothing of them.
        keyId = words[1];
        Hex.Decode(words[2], clientNonce);
        if (trust.Find(keyId) is { } identity)
        {
            Welcome(identity);
            return;
        }

        trust.Withdraw(keyId);
        trust.Ask(keyId);
        askedAt = lastHeard;
        send("wait trust");
    }

    /// <summary>
    /// A computer the person approved sends its identity, once, in the clear, so VentureOS can key the connection;
    /// it's trusted only if the identity gives the key ID it said hello with.
    /// </summary>
    private void Pair(string identity)
    {
        if (!VdbTrust.IsIdentity(identity) || VdbTrust.KeyId(identity) != keyId)
        {
            trust.Withdraw(keyId!);
            Close("error closing that identity isn't the one this computer said hello with");
            return;
        }

        trust.Trust(identity);
        Welcome(identity);
    }

    private void CheckTrust(TimeSpan now)
    {
        switch (trust.State(keyId!))
        {
            case VdbTrustState.Trusted when !askedForIdentity:
                lastHeard = now;
                Welcome(trust.Find(keyId!)!);
                break;
            case VdbTrustState.Approved when !askedForIdentity:
                lastHeard = now;
                askedForIdentity = true;
                send("pair");
                break;
            case VdbTrustState.Approved or VdbTrustState.Trusted:
                if (now - lastHeard >= VdbProtocol.IdleLimit)
                    Close($"error closing nothing was sent for {VdbProtocol.IdleLimit.TotalSeconds:0} seconds");
                break;
            case VdbTrustState.Asking when now - askedAt >= VdbProtocol.TrustLimit:
                trust.Answer(keyId!, trust: false);
                Close($"error closing nobody answered within {VdbProtocol.TrustLimit.TotalSeconds:0} seconds whether to trust this computer");
                break;
            case VdbTrustState.Asking:
                break;
            default:
                Close("error closing this computer wasn't trusted");
                break;
        }
    }

    /// <summary>Answers hello with VentureOS's nonce, then seals everything after it.</summary>
    private void Welcome(string identity)
    {
        Span<byte> serverNonce = stackalloc byte[VdbChannel.NonceSize];
        random(serverNonce);
        send($"hello {VdbProtocol.Version} {Hex.Encode(serverNonce)}");
        channel = VdbChannel.ForVentureOS(identity, clientNonce, serverNonce);
        ready = true;
        Send($"ok {VdbProtocol.Version} {Features}");
    }

    /// <summary>Sends a line, sealed once the channel is.</summary>
    private void Send(string line)
    {
        if (channel is null)
        {
            send(line);
            return;
        }

        try
        {
            send(channel.Seal(line));
        }
        catch (InvalidOperationException)
        {
            // The key has sealed all it may; the computer can start a new connection.
            Close();
        }
    }

    private void Install(string app)
    {
        if (install is not null)
        {
            Fail($"{install.App} is still being sent; commit or abort it first");
            return;
        }

        if (VdbNames.ProblemWithApp(app) is { } problem)
        {
            Send($"error {problem}");
            return;
        }

        try
        {
            install = VdbInstall.Begin(host, app);
            Send("ok");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Send($"error {app} couldn't be staged: {exception.Message}");
        }
    }

    private void BeginFile(string rest)
    {
        if (install is null)
        {
            Send("error no app is being sent; start one with install");
            skippingFile = true;
            return;
        }

        // The size is the last word, so the path may hold spaces.
        var space = rest.LastIndexOf(' ');
        if (space <= 0)
        {
            Fail("file names a path and a size, such as file Contents/Info.xml 241", inFile: true);
            return;
        }

        if (install.BeginFile(rest[..space], rest[(space + 1)..]) is { } problem)
            Fail(problem, inFile: true);
    }

    private void Answer(string? problem, bool inFile, bool ok = false)
    {
        if (problem is not null)
            Fail(problem, inFile);
        else if (ok)
            Send("ok");
    }

    private void Commit()
    {
        if (install is null)
        {
            Send("error no app is being sent; start one with install");
            return;
        }

        if (install.Commit(out var installed) is { } problem)
        {
            Fail(problem);
            return;
        }

        install = null;
        Send($"ok installed {installed}");
    }

    private void Launch(string app)
    {
        if (VdbNames.ProblemWithApp(app) is { } problem)
        {
            Send($"error {problem}");
            return;
        }

        Send(host.Launch(app) is { } refused ? $"error {refused}" : "ok");
    }

    /// <summary>Ends the install with an error; inside a file, the rest of the file is passed over.</summary>
    private void Fail(string problem, bool inFile = false)
    {
        var app = install?.App;
        EndInstall();
        skippingFile = inFile;
        // Reasons from elsewhere, such as Bundle's, end in a full stop, which the clause after it doesn't want.
        problem = problem.TrimEnd('.');
        Send(app is null ? $"error {problem}" : $"error {problem}; {app} was thrown away");
    }

    private void EndInstall()
    {
        install?.Abort();
        install = null;
    }

    private void Close(string message)
    {
        Send(message);
        Close();
    }

    private static string Shorten(string text) => text.Length <= 40 ? text : text[..40] + "…";
}
