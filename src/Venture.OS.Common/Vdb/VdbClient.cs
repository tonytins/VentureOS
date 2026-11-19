using System.Diagnostics;
using Venture.OS.Cryptography;

namespace Venture.OS.Vdb;

/// <summary>Something that carries vdb's lines both ways for a <see cref="VdbClient"/>: a socket, or a test's script.</summary>
public interface IVdbLineTransport
{
    /// <summary>Sends a line; the line break is added.</summary>
    void Send(string line);

    /// <summary>The next line, without its line break, or <see langword="null"/> when none arrives in time.</summary>
    string? Receive(TimeSpan timeout);
}

/// <summary>Why a <see cref="VdbClient"/> stopped.</summary>
public enum VdbClientProblem
{
    /// <summary>VentureOS refused a command and said why; the connection may go on.</summary>
    Refused,

    /// <summary>VentureOS closed the connection and said why.</summary>
    Closed,

    /// <summary>VentureOS said something the client doesn't understand.</summary>
    Unexpected,

    /// <summary>The connection broke, or a line from VentureOS didn't open.</summary>
    Connection,

    /// <summary>VentureOS said nothing for too long.</summary>
    TimedOut,
}

/// <summary>Why a <see cref="VdbClient"/> stopped, with VentureOS's reason when it gave one.</summary>
public sealed class VdbClientException(VdbClientProblem problem, string reason) : Exception(Describe(problem, reason))
{
    public VdbClientProblem Problem { get; } = problem;

    /// <summary>The reason alone, such as VentureOS's words after <c>error</c>.</summary>
    public string Reason { get; } = reason;

    private static string Describe(VdbClientProblem problem, string reason) => problem switch
    {
        VdbClientProblem.Refused => $"VentureOS refused: {reason}",
        VdbClientProblem.Closed => $"VentureOS closed the connection: {reason}",
        VdbClientProblem.Unexpected => $"VentureOS said something this doesn't understand: {reason}",
        VdbClientProblem.TimedOut => $"VentureOS didn't answer {reason} in time.",
        _ => reason,
    };
}

/// <summary>
/// A computer's side of vdb/1, as the <c>vdb</c> tool's <c>VdbClient</c> is: says hello by the computer's key ID,
/// pairs if VentureOS asks, seals the connection, sends an app, opens it, and passes on what apps print. Roundhouse
/// uses it, over a socket, on a thread of its own, since every call waits for VentureOS.
/// </summary>
/// <remarks>
/// Lines starting <c>out </c> can arrive at any time once VentureOS trusts the computer; they're handed to
/// <c>output</c> wherever they come, and never taken for an answer.
/// </remarks>
public sealed class VdbClient
{
    private readonly IVdbLineTransport transport;
    private readonly Action<string> output;
    private readonly Action<string> status;
    private readonly Action<Span<byte>> makeNonce;
    private HashSet<string> features = [];
    private VdbChannel? channel;

    /// <param name="transport">Carries the lines.</param>
    /// <param name="output">Given each line an app prints.</param>
    /// <param name="nonce">
    /// Fills this computer's nonce for the connection: the system's cryptographic generator, such as .NET's
    /// <c>RandomNumberGenerator.Fill</c>, which Common leaves to its caller since the kernel has none; a test may fix it.
    /// </param>
    /// <param name="status">Given what the client is waiting for, such as to be trusted, for the person to read.</param>
    public VdbClient(IVdbLineTransport transport, Action<string> output, Action<Span<byte>> nonce, Action<string>? status = null)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(nonce);
        this.transport = transport;
        this.output = output;
        this.status = status ?? (_ => { });
        makeNonce = nonce;
    }

    /// <summary>How long to wait for an answer. VentureOS answers at once, except while it's checking an app.</summary>
    public TimeSpan AnswerTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>How long to wait to be trusted, a little longer than VentureOS waits for the person.</summary>
    public TimeSpan TrustTimeout { get; set; } = VdbProtocol.TrustLimit + TimeSpan.FromSeconds(10);

    /// <summary>How often to send an empty line while only listening, which keeps VentureOS from closing the connection.</summary>
    public TimeSpan KeepAlive { get; set; } = VdbProtocol.IdleLimit / 3;

    /// <summary>
    /// Says hello by this computer's key ID, waiting while VentureOS asks whether to trust it and sending the identity
    /// if VentureOS then asks for it, and seals the connection from VentureOS's <c>hello</c> on.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="identity"/> isn't one.</exception>
    /// <exception cref="VdbClientException">VentureOS refused, closed, or didn't answer.</exception>
    public void Hello(string identity)
    {
        if (!VdbTrust.IsIdentity(identity))
            throw new ArgumentException("A vdb identity is 32 lowercase hexadecimal characters.", nameof(identity));

        Span<byte> nonce = stackalloc byte[VdbChannel.NonceSize];
        makeNonce(nonce);
        transport.Send($"hello {VdbProtocol.Version} {VdbTrust.KeyId(identity)} {Hex.Encode(nonce)}");

        var answer = PlainAnswer("hello", AnswerTimeout);
        if (answer == "wait trust")
        {
            status("Waiting to be trusted: choose Trust on VentureOS.");
            answer = PlainAnswer("whether to trust this computer", TrustTimeout);
        }

        if (answer == "pair")
        {
            // The one time the identity crosses the network: VentureOS can't key the connection without it.
            transport.Send($"pair {identity}");
            answer = PlainAnswer("pairing", AnswerTimeout);
        }

        var words = answer.Split(' ');
        if (words.Length != 3 || words[0] != "hello")
            throw new VdbClientException(VdbClientProblem.Unexpected, answer);
        if (words[1] != VdbProtocol.Version)
            throw new VdbClientException(VdbClientProblem.Unexpected, $"VentureOS speaks {words[1]}, and this speaks {VdbProtocol.Version}.");
        if (!Hex.IsLowercase(words[2], VdbChannel.NonceSize * 2))
            throw new VdbClientException(VdbClientProblem.Unexpected, answer);

        Span<byte> serverNonce = stackalloc byte[VdbChannel.NonceSize];
        Hex.Decode(words[2], serverNonce);
        channel = VdbChannel.ForComputer(identity, nonce, serverNonce);

        var welcome = Answer("hello", AnswerTimeout).Split(' ');
        if (welcome.Length < 2 || welcome[0] != "ok" || welcome[1] != VdbProtocol.Version)
            throw new VdbClientException(VdbClientProblem.Unexpected, string.Join(' ', welcome));
        features = [.. welcome.Skip(2)];
    }

    /// <summary>Sends an app and installs it.</summary>
    /// <returns>Where VentureOS put it, such as <c>/mnt/user/apps/Dice.app</c>.</returns>
    /// <exception cref="VdbClientException">VentureOS refused it, or the connection broke.</exception>
    public string Install(VdbApp app)
    {
        ArgumentNullException.ThrowIfNull(app);
        Require("install");
        Command($"install {app.Name}");

        foreach (var file in app.Files)
        {
            SendLine($"file {file.Path} {file.Contents.Length}");
            for (var start = 0; start < file.Contents.Length; start += VdbProtocol.MaximumDataBytes)
            {
                var length = Math.Min(VdbProtocol.MaximumDataBytes, file.Contents.Length - start);
                SendLine("data " + Convert.ToBase64String(file.Contents, start, length));
            }

            Command("end", $"{file.Path}'s end");
        }

        var answer = Command("commit", "the app's check");
        const string Installed = "ok installed ";
        return answer.StartsWith(Installed, StringComparison.Ordinal)
            ? answer[Installed.Length..]
            : throw new VdbClientException(VdbClientProblem.Unexpected, answer);
    }

    /// <summary>Opens an installed app, by its folder's name, such as <c>Dice.app</c>.</summary>
    /// <exception cref="VdbClientException">VentureOS refused, or the connection broke.</exception>
    public void Launch(string app)
    {
        Require("launch");
        Command($"launch {app}");
    }

    /// <summary>
    /// Passes on what apps print until <paramref name="stop"/> is cancelled, keeping the connection open meanwhile.
    /// </summary>
    /// <exception cref="VdbClientException">VentureOS closed the connection, or it broke.</exception>
    public void Follow(CancellationToken stop)
    {
        var quiet = Stopwatch.StartNew();
        while (!stop.IsCancellationRequested)
        {
            // Short waits, so a stop is noticed soon.
            var wait = TimeSpan.FromMilliseconds(Math.Min(250, Math.Max(1, (KeepAlive - quiet.Elapsed).TotalMilliseconds)));
            if (ReceiveLine(wait) is { } line)
            {
                if (!IsOutput(line))
                    throw new VdbClientException(VdbClientProblem.Unexpected, line);
            }
            else if (quiet.Elapsed >= KeepAlive)
            {
                SendLine("");
                quiet.Restart();
            }
        }
    }

    /// <summary>Says goodbye.</summary>
    public void Bye() => Command("bye");

    private void Require(string feature)
    {
        if (!features.Contains(feature))
            throw new VdbClientException(VdbClientProblem.Refused, $"this VentureOS doesn't offer {feature}");
    }

    /// <summary>Sends a command and takes its answer: <c>ok …</c>, or a refusal.</summary>
    private string Command(string line, string? waiting = null)
    {
        SendLine(line);
        var answer = Answer(waiting ?? line.Split(' ')[0], AnswerTimeout);
        return answer == "ok" || answer.StartsWith("ok ", StringComparison.Ordinal)
            ? answer
            : throw new VdbClientException(VdbClientProblem.Unexpected, answer);
    }

    private void SendLine(string line) => transport.Send(channel?.Seal(line) ?? line);

    /// <summary>
    /// The next line, opened once the connection is sealed. One that doesn't open ends everything: it was changed or
    /// cut off on the way, and nothing after it can be trusted.
    /// </summary>
    private string? ReceiveLine(TimeSpan timeout)
    {
        if (transport.Receive(timeout) is not { } line)
            return null;
        if (channel is null)
            return line;
        return channel.Open(line)
            ?? throw new VdbClientException(VdbClientProblem.Connection, "A line from VentureOS didn't open as this connection's; it was changed or cut off on the way.");
    }

    /// <summary>The next plain line before the connection is sealed, with refusals and closings thrown.</summary>
    private string PlainAnswer(string waiting, TimeSpan timeout)
    {
        var line = transport.Receive(timeout) ?? throw new VdbClientException(VdbClientProblem.TimedOut, waiting);
        IsOutput(line);
        return line;
    }

    /// <summary>The next line that isn't an app's output, with refusals and closings thrown.</summary>
    private string Answer(string waiting, TimeSpan timeout)
    {
        while (true)
        {
            var line = ReceiveLine(timeout) ?? throw new VdbClientException(VdbClientProblem.TimedOut, waiting);
            if (!IsOutput(line))
                return line;
        }
    }

    /// <summary>Passes on an app's output, or throws an error line; anything else is left to the caller.</summary>
    private bool IsOutput(string line)
    {
        const string Out = "out ", Closing = "error closing ", Error = "error ";
        if (line.StartsWith(Out, StringComparison.Ordinal))
        {
            output(line[Out.Length..]);
            return true;
        }

        if (line.StartsWith(Closing, StringComparison.Ordinal))
            throw new VdbClientException(VdbClientProblem.Closed, line[Closing.Length..]);
        if (line.StartsWith(Error, StringComparison.Ordinal))
            throw new VdbClientException(VdbClientProblem.Refused, line[Error.Length..]);
        return false;
    }
}
