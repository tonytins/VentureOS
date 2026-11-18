using System.Text;
using Venture.OS.Vdb;

namespace Venture.OS.Tests.Vdb;

/// <summary>What the recorded conversations don't show: limits, order, and the edges of trust and output.</summary>
public sealed class VdbSessionTests : IDisposable
{
    private const string Identity = "0123456789abcdef0123456789abcdef";
    private const string Info = """<App version="1"><Info identifier="dev.example.dice" name="Dice"/></App>""";

    private readonly FakeVdbHost host = new();
    private readonly VdbTrust trust = new();
    private readonly List<string> sent = [];
    private readonly VdbSession session;

    public VdbSessionTests()
    {
        session = new(host, trust, sent.Add, TimeSpan.Zero);
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
        Assert.All(sent.Skip(2), line => Assert.Equal("ok", line));

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

        Assert.All(sent.Skip(1), line => Assert.Equal("ok", line));
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
        Send($"hello vdb/1 {Identity}");

        session.Print("secret");

        Assert.Equal(["wait trust"], sent);
    }

    [Fact]
    public void WithdrawsTheQuestionWhenTheComputerGoesAway()
    {
        List<string> asked = [];
        trust.Asked += asked.Add;
        Send($"hello vdb/1 {Identity}");

        session.Close();

        Assert.Equal([Identity], asked);
        Assert.Equal(VdbTrustState.Unknown, trust.State(Identity));
    }

    [Fact]
    public void AsksAgainAfterARefusal()
    {
        List<string> asked = [];
        trust.Asked += asked.Add;
        trust.Ask(Identity);
        trust.Answer(Identity, trust: false);

        Send($"hello vdb/1 {Identity}");

        Assert.Equal([Identity, Identity], asked);
        Assert.Equal(VdbTrustState.Asking, trust.State(Identity));
    }

    [Fact]
    public void RefusesCommandsWhileWaitingToBeTrusted()
    {
        Send($"hello vdb/1 {Identity}", "install Dice.app");

        Assert.Equal("error closing wait to be trusted before sending commands", sent[^1]);
        Assert.True(session.IsClosed);
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("hello vdb/1")]
    [InlineData("hello vdb/1 ABCDEF0123456789ABCDEF0123456789")]
    [InlineData("hello adb/1 0123456789abcdef0123456789abcdef")]
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

        session.Receive(Encoding.ASCII.GetBytes(new string('a', VdbProtocol.MaximumLineLength + 2)), TimeSpan.Zero);

        Assert.Equal("error closing a line is longer than 8192 bytes", sent[^1]);
        Assert.True(session.IsClosed);
    }

    [Fact]
    public void KeepsTheConnectionWhileItIsUsed()
    {
        Ready();

        session.Receive("abort\n"u8, TimeSpan.FromSeconds(25));
        session.Tick(TimeSpan.FromSeconds(50));

        Assert.False(session.IsClosed);
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
        Send($"hello vdb/1 {Identity}");
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
            session.Receive(Encoding.UTF8.GetBytes(line + "\n"), TimeSpan.Zero);
    }
}
