using System.Text;
using Venture.OS.Vdb;

namespace Venture.OS.Tests.Vdb;

/// <summary>
/// What the recorded conversations don't show: limits, order, the edges of trust and output, and what isn't sealed
/// right. <see cref="sent"/> is what VentureOS sent as the computer reads it, opened; <see cref="raw"/> is what went on
/// the network.
/// </summary>
public sealed class VdbSessionTests : IDisposable
{
    private const string Identity = TestComputer.DefaultIdentity;
    private const string Info = """<App version="1"><Info identifier="dev.example.dice" name="Dice"/></App>""";

    private readonly FakeVdbHost host = new();
    private readonly VdbTrust trust = new();
    private readonly TestComputer computer = new();
    private readonly List<string> raw = [];
    private readonly VdbSession session;

    public VdbSessionTests()
    {
        session = new(host, trust, line => { raw.Add(line); computer.Hear(line); }, FixedNonce, TimeSpan.Zero);
    }

    private List<string> sent => computer.Heard;

    private static void FixedNonce(Span<byte> nonce)
    {
        for (var index = 0; index < nonce.Length; index++)
            nonce[index] = (byte)(0xf0 + index);
    }

    public void Dispose() => host.Dispose();

    [Fact]
    public void RefusesAFileLargerThanAFileMayBe()
    {
        Ready("install Dice.app", $"file Contents/Big {VdbProtocol.MaximumFileSize + 1}");

        Assert.StartsWith("error \"4194305\" isn't a size from 0 to 4194304 bytes", sent[^1]);
    }

    [Fact]
    public void RefusesMoreFilesThanAnAppMayHave()
    {
        Ready("install Dice.app");
        for (var number = 0; number < VdbProtocol.MaximumFiles; number++)
            Send($"file f{number} 0", "end");
        Assert.All(sent.Skip(3), line => Assert.Equal("ok", line));

        Send("file one-too-many 0");

        Assert.Equal("error an app sent over vdb has at most 256 files; Dice.app was thrown away", sent[^1]);
    }

    [Fact]
    public void RefusesAnAppLargerThanAnAppMayBe()
    {
        Ready("install Dice.app");
        var chunk = Convert.ToBase64String(new byte[VdbProtocol.MaximumDataBytes]);
        var perFile = (int)(VdbProtocol.MaximumFileSize / VdbProtocol.MaximumDataBytes);
        var last = (int)(VdbProtocol.MaximumFileSize % VdbProtocol.MaximumDataBytes);
        for (var number = 0; number < 4; number++)
        {
            Send($"file f{number} {VdbProtocol.MaximumFileSize}");
            for (var line = 0; line < perFile; line++)
                Send($"data {chunk}");
            Send($"data {Convert.ToBase64String(new byte[last])}", "end");
        }

        // Past hello's answer and the welcome.
        Assert.All(sent.Skip(2), line => Assert.Equal("ok", line));
        Send("file one-byte-more 1");

        Assert.Equal("error an app sent over vdb is at most 16 MiB; Dice.app was thrown away", sent[^1]);
    }

    [Fact]
    public void RefusesAPathSentTwice()
    {
        Ready("install Dice.app", "file Contents/Info.xml 0", "end", "file contents/info.xml 0");

        Assert.Equal("error contents/info.xml was already sent; Dice.app was thrown away", sent[^1]);
    }

    [Fact]
    public void RefusesDataThatIsNotBase64()
    {
        Ready("install Dice.app", "file a 3", "data AB$=", "data AAAA", "end");

        Assert.Equal(["ok", "error a data line isn't base64; Dice.app was thrown away"], sent[^2..]);
    }

    [Fact]
    public void RefusesDataThatIsNotWholeBase64()
    {
        Ready("install Dice.app", "file a 3", "data AAA");

        Assert.Equal("error a data line holds whole base64, a multiple of four characters; Dice.app was thrown away", sent[^1]);
    }

    [Fact]
    public void RefusesMoreDataThanTheFileWasSentAs()
    {
        Ready("install Dice.app", "file a 2", "data AAAA", "end");

        Assert.Equal("error a is longer than the 2 bytes it was sent as; Dice.app was thrown away", sent[^1]);
        Assert.Single(sent, line => line.StartsWith("error", StringComparison.Ordinal));
    }

    [Fact]
    public void TakesDataSentInPieces()
    {
        var bytes = Encoding.UTF8.GetBytes(Info);
        Ready("install Dice.app", $"file Contents/Info.xml {bytes.Length}");
        for (var start = 0; start < bytes.Length; start += 30)
            Send($"data {Convert.ToBase64String(bytes, start, Math.Min(30, bytes.Length - start))}");
        Send("end");

        Assert.Equal("ok", sent[^1]);
        Assert.Equal(Info, File.ReadAllText($"{host.StagingFolder}/Dice.app/Contents/Info.xml"));
    }

    [Fact]
    public void RefusesACommandInsideAFile()
    {
        Ready("install Dice.app", "file a 4", "commit");

        Assert.Equal("error commit came before the open file's end; Dice.app was thrown away", sent[^1]);
        Assert.False(Directory.Exists($"{host.StagingFolder}/Dice.app"));
    }

    [Fact]
    public void RefusesASecondInstallBeforeTheFirstEnds()
    {
        Ready("install Dice.app", "install Other.app");

        Assert.Equal("error Dice.app is still being sent; commit or abort it first; Dice.app was thrown away", sent[^1]);
    }

    [Fact]
    public void AbortThrowsTheAppAway()
    {
        Ready("install Dice.app", "file a 0", "end", "abort");

        Assert.Equal("ok", sent[^1]);
        Assert.False(Directory.Exists($"{host.StagingFolder}/Dice.app"));
    }

    [Fact]
    public void ClosesTheAppItReplacesEvenUnderAnotherIdentifier()
    {
        Directory.CreateDirectory($"{host.AppsFolder}/Dice.app/Contents/Lua");
        File.WriteAllText($"{host.AppsFolder}/Dice.app/Contents/Info.xml", Info.Replace("dev.example.dice", "dev.example.olddice", StringComparison.Ordinal));
        File.WriteAllText($"{host.AppsFolder}/Dice.app/Contents/Lua/main.lua", "");
        File.WriteAllText($"{host.AppsFolder}/Dice.app/Contents/Lua/old.lua", "");

        SendApp();

        Assert.Equal("ok installed " + host.AppsFolder + "/Dice.app", sent[^1]);
        Assert.Equal(["dev.example.dice", "dev.example.olddice"], host.Closed);
        Assert.False(File.Exists($"{host.AppsFolder}/Dice.app/Contents/Lua/old.lua"));
    }

    [Fact]
    public void ReportsWhyAnAppDidNotOpen()
    {
        host.LaunchProblem = "There's no app called Dice.app.";

        Ready("launch Dice.app");

        Assert.Equal("error There's no app called Dice.app.", sent[^1]);
    }

    [Fact]
    public void RefusesToLaunchWhatIsNotAnApp()
    {
        Ready("launch ../Files.app");

        Assert.StartsWith("error ", sent[^1]);
        Assert.Empty(host.Launched);
    }

    [Fact]
    public void PrintsEachLineOfOutputAsItsOwn()
    {
        Ready();

        session.Print("one\r\ntwo\tthree");

        Assert.Equal(["out one", "out two three"], sent[^2..]);
    }

    [Fact]
    public void CutsOutputToALine()
    {
        Ready();

        session.Print(new string('é', VdbProtocol.MaximumLineLength));

        Assert.True(Encoding.UTF8.GetByteCount(sent[^1]) <= VdbProtocol.MaximumLineLength);
        Assert.True(Encoding.UTF8.GetByteCount(sent[^1]) > VdbProtocol.MaximumLineLength - 4);
    }

    [Fact]
    public void PrintsNothingBeforeTheComputerIsTrusted()
    {
        Send(computer.Hello());

        session.Print("secret");

        Assert.Equal(["wait trust"], sent);
    }

    [Fact]
    public void WithdrawsTheQuestionWhenTheComputerGoesAway()
    {
        List<string> asked = [];
        trust.Asked += asked.Add;
        Send(computer.Hello());

        session.Close();

        Assert.Equal([computer.KeyId], asked);
        Assert.Equal(VdbTrustState.Unknown, trust.State(computer.KeyId));
    }

    [Fact]
    public void AsksAgainAfterARefusal()
    {
        List<string> asked = [];
        trust.Asked += asked.Add;
        trust.Ask(computer.KeyId);
        trust.Answer(computer.KeyId, trust: false);

        Send(computer.Hello());

        Assert.Equal([computer.KeyId, computer.KeyId], asked);
        Assert.Equal(VdbTrustState.Asking, trust.State(computer.KeyId));
    }

    [Fact]
    public void RefusesCommandsWhileWaitingToBeTrusted()
    {
        Send(computer.Hello(), "install Dice.app");

        Assert.Equal("error closing wait to be trusted before sending commands", sent[^1]);
        Assert.True(session.IsClosed);
    }

    [Fact]
    public void TakesAHelloOfferingFeaturesItDoesNotUse()
    {
        trust.Trust(Identity);

        Send(computer.Hello("install launch shell"));

        Assert.Equal(["hello vdb/1 f0f1f2f3f4f5f6f7f8f9fafbfcfdfeff", "ok vdb/1 install launch"], sent);
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("hello vdb/1")]
    [InlineData("hello vdb/1 0123456789abcdef0123456789abcdef")]
    [InlineData("hello vdb/1 ABCDEF0123456789 00112233445566778899aabbccddeeff")]
    [InlineData("hello vdb/1 0123456789abcdef 0011")]
    [InlineData("hello vdb/1 0123456789abcdef")]
    [InlineData("hello adb/1 0123456789abcdef 00112233445566778899aabbccddeeff")]
    public void ClosesOnAHelloItCannotRead(string hello)
    {
        Send(hello);

        Assert.StartsWith("error closing", sent[^1]);
        Assert.True(session.IsClosed);
    }

    [Fact]
    public void ClosesOnALineTooLong()
    {
        Ready();

        session.Receive(Encoding.ASCII.GetBytes(new string('a', VdbChannel.MaximumLineLength + 2)), TimeSpan.Zero);

        Assert.Equal($"error closing a line is longer than {VdbChannel.MaximumLineLength} bytes", sent[^1]);
        Assert.True(session.IsClosed);
    }

    [Fact]
    public void KeepsTheConnectionWhileItIsUsed()
    {
        Ready();

        session.Receive(computer.Bytes("abort"), TimeSpan.FromSeconds(25));
        session.Tick(TimeSpan.FromSeconds(50));

        Assert.False(session.IsClosed);
    }

    [Fact]
    public void AnEmptyLineKeepsTheConnectionOpen()
    {
        Ready();

        session.Receive(computer.Bytes(""), TimeSpan.FromSeconds(25));
        session.Tick(TimeSpan.FromSeconds(50));

        Assert.False(session.IsClosed);
        Assert.Equal(2, sent.Count);
    }

    [Fact]
    public void TakesNothingOnceClosed()
    {
        Ready("bye");
        sent.Clear();

        Send("install Dice.app");

        Assert.Empty(sent);
    }

    private void Ready(params string[] lines)
    {
        trust.Trust(Identity);
        Send(computer.Hello());
        Assert.Equal("ok vdb/1 install launch", sent[^1]);
        Send(lines);
    }

    private void SendApp()
    {
        var info = Convert.ToBase64String(Encoding.UTF8.GetBytes(Info));
        Ready(
            "install Dice.app",
            $"file Contents/Info.xml {Encoding.UTF8.GetByteCount(Info)}", $"data {info}", "end",
            "file Contents/Lua/main.lua 0", "end",
            "commit");
    }

    private void Send(params string[] lines)
    {
        foreach (var line in lines)
            session.Receive(computer.Bytes(line), TimeSpan.Zero);
    }

    // ---- Sealing ----

    [Fact]
    public void EverythingAfterHelloIsSealedAndTheIdentityIsNeverSent()
    {
        SendApp();
        Send("launch Dice.app", "bye");

        Assert.Equal("hello vdb/1 f0f1f2f3f4f5f6f7f8f9fafbfcfdfeff", raw[0]);
        Assert.All(raw.Skip(1), line => Assert.StartsWith(VdbChannel.Prefix, line));
        Assert.DoesNotContain(raw, line => line.Contains(Identity, StringComparison.Ordinal));
        Assert.Empty(computer.Unopened);
        Assert.Equal("ok", sent[^1]);
    }

    [Fact]
    public void ClosesWithoutAWordOnAPlainLineOnceSealed()
    {
        Ready();
        var before = raw.Count;

        session.Receive("install Dice.app\n"u8, TimeSpan.Zero);

        Assert.True(session.IsClosed);
        Assert.Equal(before, raw.Count);
    }

    [Fact]
    public void ClosesWithoutAWordOnAChangedLine()
    {
        Ready();
        var line = computer.Say("install Dice.app");
        var changed = line[..^3] + (line[^3] == 'A' ? 'B' : 'A') + line[^2..];

        session.Receive(Encoding.ASCII.GetBytes(changed + "\n"), TimeSpan.Zero);

        Assert.True(session.IsClosed);
        Assert.False(Directory.Exists(Path.Combine(host.StagingFolder, "Dice.app")));
    }

    [Fact]
    public void ClosesOnALineSentAgain()
    {
        Ready();
        var line = Encoding.ASCII.GetBytes(computer.Say("abort") + "\n");

        session.Receive(line, TimeSpan.Zero);
        Assert.False(session.IsClosed);
        session.Receive(line, TimeSpan.Zero);

        Assert.True(session.IsClosed);
    }

    [Fact]
    public void ClosesOnALineSealedForAnotherComputer()
    {
        trust.Trust(Identity);
        Send(computer.Hello());
        TestComputer stranger = new();
        stranger.Hear($"hello vdb/1 {Convert.ToHexStringLower(Enumerable.Range(0, 16).Select(value => (byte)value).ToArray())}");

        session.Receive(stranger.Bytes("abort"), TimeSpan.Zero);

        Assert.True(session.IsClosed);
    }

    [Fact]
    public void TheKeysDifferWithEachConnection()
    {
        Ready("abort");
        List<string> otherRaw = [];
        TestComputer other = new(nonce: "ffeeddccbbaa99887766554433221100");
        VdbSession second = new(host, trust, line => { otherRaw.Add(line); other.Hear(line); }, FixedNonce, TimeSpan.Zero);
        second.Receive(Encoding.ASCII.GetBytes(other.Hello() + "\n"), TimeSpan.Zero);
        second.Receive(other.Bytes("abort"), TimeSpan.Zero);

        Assert.Equal(sent, other.Heard.Select(line => line.StartsWith("hello ", StringComparison.Ordinal) ? sent[0] : line));
        Assert.NotEqual(raw[1], otherRaw[1]);
    }

    // ---- Pairing ----

    [Fact]
    public void PairsAComputerThePersonApproved()
    {
        Send(computer.Hello());
        trust.Answer(computer.KeyId, trust: true);
        session.Tick(TimeSpan.FromSeconds(1));
        Assert.Equal(["wait trust", "pair"], sent);

        Send($"pair {Identity}");

        Assert.Equal("ok vdb/1 install launch", sent[^1]);
        Assert.Equal(VdbTrustState.Trusted, trust.State(computer.KeyId));
        Assert.Equal(Identity, trust.Find(computer.KeyId));
        Send("abort");
        Assert.Equal("ok", sent[^1]);
    }

    [Fact]
    public void RefusesAnIdentityThatIsNotTheOneItSaidHelloWith()
    {
        Send(computer.Hello());
        trust.Answer(computer.KeyId, trust: true);
        session.Tick(TimeSpan.FromSeconds(1));

        Send("pair ffffffffffffffffffffffffffffffff");

        Assert.Equal("error closing that identity isn't the one this computer said hello with", sent[^1]);
        Assert.True(session.IsClosed);
        Assert.Equal(0, trust.TrustedCount);
        Assert.Equal(VdbTrustState.Unknown, trust.State(computer.KeyId));
    }

    [Fact]
    public void RefusesAnIdentityBeforeThePersonApproves()
    {
        Send(computer.Hello(), $"pair {Identity}");

        Assert.Equal("error closing wait to be trusted before sending commands", sent[^1]);
        Assert.Equal(0, trust.TrustedCount);
    }

    [Fact]
    public void AnApprovedComputerThatSendsNothingIsClosed()
    {
        Send(computer.Hello());
        trust.Answer(computer.KeyId, trust: true);
        session.Tick(TimeSpan.FromSeconds(1));

        session.Tick(TimeSpan.FromSeconds(1) + VdbProtocol.IdleLimit);

        Assert.True(session.IsClosed);
        Assert.Equal(VdbTrustState.Unknown, trust.State(computer.KeyId));
    }

    [Fact]
    public void AComputerTrustedWhileWaitingIsWelcomedWithoutPairing()
    {
        Send(computer.Hello());
        trust.Trust(Identity);

        session.Tick(TimeSpan.FromSeconds(1));

        Assert.Equal("ok vdb/1 install launch", sent[^1]);
        Assert.DoesNotContain("pair", sent);
    }
}
