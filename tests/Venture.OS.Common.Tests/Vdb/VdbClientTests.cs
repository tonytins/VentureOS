using System.Text;
using Venture.OS.Cryptography;
using Venture.OS.Vdb;

namespace Venture.OS.Tests.Vdb;

/// <summary>
/// <see cref="VdbClient"/>, the computer's side Roundhouse speaks: the recorded conversations replayed with the client
/// playing the computer, as the vdb tool's tests do, and what they don't show.
/// </summary>
public sealed class VdbClientTests : IDisposable
{
    private const string Identity = TestComputer.DefaultIdentity;
    private const string Hello = "hello vdb/1 0a30b19247f0d8d7 00112233445566778899aabbccddeeff";
    private const string Welcome = "hello vdb/1 f0f1f2f3f4f5f6f7f8f9fafbfcfdfeff";
    private static readonly string Folder = Path.Combine(AppContext.BaseDirectory, "vdb", "conversations");

    private readonly string temporary = Path.Combine(Path.GetTempPath(), $"venture-vdb-client-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(temporary))
            Directory.Delete(temporary, recursive: true);
    }

    /// <summary>The conversations a well-behaved computer can have, as the vdb tool's tests list them.</summary>
    public static TheoryData<string> Playable() =>
        ["install-and-launch.txt", "ask-to-trust.txt", "trust-denied.txt", "trust-unanswered.txt", "refused-app.txt", "system-app.txt"];

    [Theory]
    [MemberData(nameof(Playable))]
    public void Replays(string name)
    {
        var steps = Steps(File.ReadLines(Path.Combine(Folder, name)), out var identity);
        ScriptedVentureOS ventureOS = new(steps, identity);
        List<string> printed = [];
        var client = Client(ventureOS, printed.Add);

        VdbClientException? thrown = null;
        try
        {
            client.Hello(identity);
            if (App(steps) is { } app)
                client.Install(app);
            if (steps.FirstOrDefault(step => step.Sent?.StartsWith("launch ", StringComparison.Ordinal) == true) is { } launch)
            {
                client.Launch(launch.Sent!["launch ".Length..]);
                using CancellationTokenSource soon = new(TimeSpan.FromMilliseconds(50));
                client.Follow(soon.Token);
            }

            client.Bye();
        }
        catch (VdbClientException exception)
        {
            thrown = exception;
            if (exception.Problem == VdbClientProblem.Refused)
                client.Bye();
        }

        Assert.Empty(ventureOS.Mismatches);
        Assert.Empty(ventureOS.Unplayed);
        Assert.Equal(steps.Where(step => step.Answered?.StartsWith("out ", StringComparison.Ordinal) == true).Select(step => step.Answered![4..]), printed);
        var firstError = steps.Select(step => step.Answered).FirstOrDefault(line => line?.StartsWith("error ", StringComparison.Ordinal) == true);
        if (firstError is null)
            Assert.Null(thrown);
        else
            Assert.Equal(firstError.StartsWith("error closing ", StringComparison.Ordinal) ? firstError["error closing ".Length..] : firstError["error ".Length..], thrown?.Reason);
        Assert.DoesNotContain(ventureOS.Heard, line => line.StartsWith("hello ", StringComparison.Ordinal) && line.Contains(identity, StringComparison.Ordinal));
    }

    [Fact]
    public void SealsWhatPythonSealedByteForByte()
    {
        var steps = Steps(File.ReadLines(Path.Combine(Folder, "sealed-lines.txt")), out _);
        var raw = steps.SkipWhile(step => !step.IsRaw).Skip(1).ToList();
        var channel = VdbChannel.ForComputer(Identity, Convert.FromHexString("00112233445566778899aabbccddeeff"), Convert.FromHexString("f0f1f2f3f4f5f6f7f8f9fafbfcfdfeff"));
        string[] inside = ["ok vdb/1 install launch", "launch Dice.app", "ok", "out Rolled a 4", "", "bye", "ok"];

        Assert.Equal(inside.Length, raw.Count);
        for (var index = 0; index < raw.Count; index++)
        {
            if (raw[index].Sent is { } sent)
                Assert.Equal(sent, channel.Seal(inside[index]));
            else
                Assert.Equal(inside[index], channel.Open(raw[index].Answered!));
        }
    }

    [Fact]
    public void SplitsAFileIntoDataLinesThatFit()
    {
        var contents = Enumerable.Range(0, VdbProtocol.MaximumDataBytes + 10).Select(value => (byte)(value % 251)).ToArray();
        var app = VdbApp.Create("Big.app", [new VdbAppFile("big", contents)]);
        ScriptedVentureOS ventureOS = new(
            [.. Welcomed(), new(Sent: "install Big.app"), new(Answered: "ok"), new(Sent: "file big " + contents.Length),
             new(Sent: "data " + Convert.ToBase64String(contents, 0, VdbProtocol.MaximumDataBytes)),
             new(Sent: "data " + Convert.ToBase64String(contents, VdbProtocol.MaximumDataBytes, 10)),
             new(Sent: "end"), new(Answered: "ok"), new(Sent: "commit"), new(Answered: "ok installed /mnt/user/apps/Big.app")],
            Identity);
        var client = Client(ventureOS);
        client.Hello(Identity);

        Assert.Equal("/mnt/user/apps/Big.app", client.Install(app));
        Assert.Empty(ventureOS.Mismatches);
        Assert.All(ventureOS.Network, line => Assert.True(Encoding.UTF8.GetByteCount(line) <= VdbChannel.MaximumLineLength));
    }

    [Fact]
    public void SendsTheIdentityOnlyWhenVentureOSAsksToPair()
    {
        ScriptedVentureOS ventureOS = new(
            [new(Sent: Hello), new(Answered: "wait trust"), new(Answered: "pair"), new(Sent: $"pair {Identity}"),
             new(Answered: Welcome), new(Answered: "ok vdb/1 install launch")],
            Identity);
        List<string> status = [];

        Client(ventureOS, status: status.Add).Hello(Identity);

        Assert.Empty(ventureOS.Mismatches);
        Assert.Equal([$"pair {Identity}"], ventureOS.Network.Where(line => line.Contains(Identity, StringComparison.Ordinal)));
        Assert.Equal(["Waiting to be trusted: choose Trust on VentureOS."], status);
    }

    [Fact]
    public void EndsOnALineThatDoesNotOpen()
    {
        ScriptedVentureOS ventureOS = new([.. Welcomed(), new(Sent: "launch Dice.app"), new(Raw: true), new(Answered: "sealed AAAAAAAAAAAAAAAAAAAAAA==")], Identity);
        var client = Client(ventureOS);
        client.Hello(Identity);

        Assert.Equal(VdbClientProblem.Connection, Assert.Throws<VdbClientException>(() => client.Launch("Dice.app")).Problem);
    }

    [Fact]
    public void RefusesAFeatureVentureOSDoesNotOffer()
    {
        var client = Client(new ScriptedVentureOS([new(Sent: Hello), new(Answered: Welcome), new(Answered: "ok vdb/1 install")], Identity));
        client.Hello(Identity);

        Assert.Equal("this VentureOS doesn't offer launch", Assert.Throws<VdbClientException>(() => client.Launch("Dice.app")).Reason);
    }

    [Fact]
    public void RefusesAVersionItDoesNotSpeak()
    {
        var client = Client(new ScriptedVentureOS([new(Sent: Hello), new(Answered: "hello vdb/2 f0f1f2f3f4f5f6f7f8f9fafbfcfdfeff")], Identity));

        Assert.Equal(VdbClientProblem.Unexpected, Assert.Throws<VdbClientException>(() => client.Hello(Identity)).Problem);
    }

    [Fact]
    public void TimesOutWhenNothingAnswers()
    {
        var client = Client(new ScriptedVentureOS([new(Sent: Hello)], Identity));

        var problem = Assert.Throws<VdbClientException>(() => client.Hello(Identity));
        Assert.Equal((VdbClientProblem.TimedOut, "hello"), (problem.Problem, problem.Reason));
    }

    [Fact]
    public void KeepsTheConnectionOpenWhileFollowing()
    {
        ScriptedVentureOS ventureOS = new([.. Welcomed()], Identity) { KeepEmptyLines = true };
        var client = Client(ventureOS);
        client.Hello(Identity);
        client.KeepAlive = TimeSpan.FromMilliseconds(10);

        using CancellationTokenSource soon = new(TimeSpan.FromMilliseconds(200));
        client.Follow(soon.Token);

        Assert.Contains("", ventureOS.Heard);
    }

    [Fact]
    public void ReadsAnAppsFolderWithEditsInPlaceOfItsFiles()
    {
        var app = Path.Combine(temporary, "Dice.app");
        Directory.CreateDirectory(Path.Combine(app, "Contents", "Lua"));
        File.WriteAllText(Path.Combine(app, "Contents", "Info.xml"), "<App/>");
        var script = Path.Combine(app, "Contents", "Lua", "main.lua");
        File.WriteAllText(script, "print('saved')");

        var read = VdbApp.Read(app, new Dictionary<string, string> { [script] = "print('edited')" });

        Assert.Equal("Dice.app", read.Name);
        Assert.Equal(["Contents/Info.xml", "Contents/Lua/main.lua"], read.Files.Select(file => file.Path));
        Assert.Equal("print('edited')", Encoding.UTF8.GetString(read.Files[1].Contents));
    }

    [Fact]
    public void RefusesAnAppWithASymbolicLink()
    {
        var app = Path.Combine(temporary, "Dice.app");
        Directory.CreateDirectory(app);
        File.CreateSymbolicLink(Path.Combine(app, "passwd"), "/etc/passwd");

        Assert.Contains("symbolic link", Assert.Throws<ArgumentException>(() => VdbApp.Read(app)).Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Dice", "Contents/Info.xml")]
    [InlineData("Dice.app", "../init.lua")]
    [InlineData("Dice.app", "/etc/passwd")]
    public void RefusesWhatCannotBeSent(string name, string path)
    {
        Assert.Throws<ArgumentException>(() => VdbApp.Create(name, [new VdbAppFile(path, [])]));
    }

    [Fact]
    public void RefusesTwoPathsDifferingOnlyInCase()
    {
        Assert.Throws<ArgumentException>(() => VdbApp.Create("Dice.app", [new VdbAppFile("a", []), new VdbAppFile("A", [])]));
    }

    private static Step[] Welcomed() => [new(Sent: Hello), new(Answered: Welcome), new(Answered: "ok vdb/1 install launch")];

    private static VdbClient Client(ScriptedVentureOS ventureOS, Action<string>? printed = null, Action<string>? status = null) =>
        new(ventureOS, printed ?? (_ => { }), FixedNonce, status)
        {
            AnswerTimeout = TimeSpan.Zero,
            TrustTimeout = TimeSpan.Zero,
        };

    private static void FixedNonce(Span<byte> nonce) => Convert.FromHexString("00112233445566778899aabbccddeeff").CopyTo(nonce);

    /// <summary>A recorded conversation's lines, with VentureOS's setup and checks left out.</summary>
    private static List<Step> Steps(IEnumerable<string> lines, out string identity)
    {
        identity = Identity;
        List<Step> steps = [];
        foreach (var line in lines)
        {
            if (line.StartsWith("! identity ", StringComparison.Ordinal))
                identity = line["! identity ".Length..];
            else if (line == "! raw")
                steps.Add(new Step(Raw: true));
            else if (line.StartsWith('>'))
                steps.Add(new Step(Sent: line.Length > 2 ? line[2..] : ""));
            else if (line.StartsWith('<'))
                steps.Add(new Step(Answered: line.Length > 2 ? line[2..] : ""));
        }

        return steps;
    }

    /// <summary>The app the computer sends in a conversation, rebuilt from its <c>file</c> and <c>data</c> lines.</summary>
    private static VdbApp? App(IEnumerable<Step> steps)
    {
        var sent = steps.Select(step => step.Sent).OfType<string>().ToList();
        if (sent.FirstOrDefault(line => line.StartsWith("install ", StringComparison.Ordinal)) is not { } install)
            return null;

        List<VdbAppFile> files = [];
        string? path = null;
        List<byte> contents = [];
        foreach (var line in sent)
        {
            if (line.StartsWith("file ", StringComparison.Ordinal))
            {
                path = line[5..line.LastIndexOf(' ')];
                contents.Clear();
            }
            else if (line.StartsWith("data ", StringComparison.Ordinal))
            {
                contents.AddRange(Convert.FromBase64String(line[5..]));
            }
            else if (line == "end" && path is not null)
            {
                files.Add(new VdbAppFile(path, [.. contents]));
                path = null;
            }
        }

        return VdbApp.Create(install["install ".Length..], files);
    }

    /// <summary>One line of a conversation, or the point from which lines are written raw.</summary>
    private sealed record Step(string? Sent = null, string? Answered = null, bool Raw = false)
    {
        public bool IsRaw => Raw;
    }

    /// <summary>
    /// Plays VentureOS's side: answers as it did, sealed as VentureOS seals them, and notes any line sent that it didn't
    /// expect. Empty lines, which only keep a connection open, aren't part of a conversation unless asked for.
    /// </summary>
    private sealed class ScriptedVentureOS(IEnumerable<Step> steps, string identity) : IVdbLineTransport
    {
        private readonly Queue<Step> steps = new(steps);
        private byte[]? clientNonce;
        private VdbChannel? channel;
        private bool raw;

        public List<string> Mismatches { get; } = [];

        /// <summary>Every line sent, as VentureOS read it.</summary>
        public List<string> Heard { get; } = [];

        /// <summary>Every line sent, as it went on the network.</summary>
        public List<string> Network { get; } = [];

        public bool KeepEmptyLines { get; init; }

        public IReadOnlyCollection<Step> Unplayed => steps;

        public void Send(string line)
        {
            SkipMarkers();
            Network.Add(line);
            var read = raw ? line : Read(line);
            Heard.Add(read);
            if (read.Length == 0 && !raw)
                return;

            if (!steps.TryPeek(out var next) || next.Sent is not { } expected)
            {
                Mismatches.Add($"sent \"{read}\" where VentureOS didn't expect anything");
                return;
            }

            if (read != expected)
                Mismatches.Add($"sent \"{Shorten(read)}\", not \"{Shorten(expected)}\"");
            steps.Dequeue();
        }

        public string? Receive(TimeSpan timeout)
        {
            SkipMarkers();
            if (!steps.TryPeek(out var next) || next.Answered is not { } line)
            {
                if (KeepEmptyLines && timeout > TimeSpan.Zero)
                    Thread.Sleep(timeout);
                return null;
            }

            steps.Dequeue();
            return raw ? line : Write(line);
        }

        private string Read(string line)
        {
            if (channel is not null)
                return channel.Open(line) ?? $"(a line that didn't open: {Shorten(line)})";
            var words = line.Split(' ');
            if (words is ["hello", _, _, var nonce, ..] && Hex.IsLowercase(nonce, 32))
                clientNonce = Convert.FromHexString(nonce);
            return line;
        }

        private string Write(string line)
        {
            if (channel is not null)
                return channel.Seal(line);
            if (line.StartsWith("hello ", StringComparison.Ordinal) && clientNonce is not null
                && line.Split(' ') is [_, _, var server] && Hex.IsLowercase(server, 32))
                channel = VdbChannel.ForVentureOS(identity, clientNonce, Convert.FromHexString(server));
            return line;
        }

        private void SkipMarkers()
        {
            while (steps.TryPeek(out var next) && next.IsRaw)
            {
                raw = true;
                steps.Dequeue();
            }
        }

        private static string Shorten(string text) => text.Length <= 80 ? text : text[..80];
    }
}
