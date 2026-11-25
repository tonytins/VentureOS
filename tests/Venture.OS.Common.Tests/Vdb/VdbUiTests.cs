using Venture.OS.Desktop;
using Venture.OS.Vdb;

namespace Venture.OS.Tests.Vdb;

/// <summary>
/// What the <c>ui</c> conversations don't show: who offers it, screenshots too large for one frame, lines cut to fit,
/// and lists too long to list.
/// </summary>
public sealed class VdbUiTests : IDisposable
{
    private readonly FakeVdbHost host = new();
    private readonly FakeVdbDesktop desktop = new();
    private readonly VdbTrust trust = new();
    private readonly TestComputer computer = new();

    public VdbUiTests() => trust.Trust(TestComputer.DefaultIdentity);

    public void Dispose() => host.Dispose();

    private VdbSession Start(IVdbDesktop? with)
    {
        VdbSession session = new(host, trust, computer.Hear, Nonce, TimeSpan.Zero, with);
        session.Receive(computer.Bytes(computer.Hello()), TimeSpan.Zero);
        return session;
    }

    private static void Nonce(Span<byte> nonce) => nonce.Fill(0xf0);

    private void Send(VdbSession session, string line, TimeSpan? at = null) => session.Receive(computer.Bytes(line), at ?? TimeSpan.Zero);

    [Fact]
    public void WithoutADesktopUiIsNotOfferedAndItsCommandsAreNotCommands()
    {
        var session = Start(null);

        Assert.Equal("ok vdb/1 install launch", computer.Heard[^1]);
        Send(session, "windows");
        Assert.Equal("error \"windows\" isn't a vdb command", computer.Heard[^1]);
    }

    [Fact]
    public void AScreenshotTooLargeForOneFrameGoesOutOverSeveralAndWhatCameMeanwhileWaits()
    {
        // Enough pixels for two frames' worth of data lines and a few more.
        var bytes = VdbProtocol.MaximumDataBytes * (VdbSession.ScreenLinesPerTick * 2 + 3);
        desktop.Screen = new VdbScreenshot(bytes / 3, 1, [.. Enumerable.Range(0, bytes).Select(index => (byte)(index * 7))]);
        var session = Start(desktop);
        var before = computer.Heard.Count;

        Send(session, "screenshot");
        Send(session, "windows");
        Assert.Equal(1 + VdbSession.ScreenLinesPerTick, computer.Heard.Count - before);

        session.Tick(TimeSpan.Zero);
        session.Tick(TimeSpan.Zero);
        session.Tick(TimeSpan.Zero);

        var answer = computer.Heard.Skip(before).ToList();
        Assert.Equal($"screen width={bytes / 3} height=1", answer[0]);
        var data = answer.Skip(1).TakeWhile(line => line.StartsWith("data ", StringComparison.Ordinal)).ToList();
        Assert.Equal(VdbSession.ScreenLinesPerTick * 2 + 3, data.Count);
        Assert.Equal(desktop.Screen.Rgb, data.SelectMany(line => Convert.FromBase64String(line[5..])));

        // ok ends the screenshot, then the windows that waited for it are answered.
        Assert.Equal(["ok", "ok"], answer.Skip(1 + data.Count));
    }

    [Fact]
    public void AScreenshotWaitsOutTheIdleLimitWithoutClosing()
    {
        desktop.Screen = new VdbScreenshot(VdbProtocol.MaximumDataBytes * VdbSession.ScreenLinesPerTick, 3, new byte[VdbProtocol.MaximumDataBytes * VdbSession.ScreenLinesPerTick * 9]);
        var session = Start(desktop);
        Send(session, "screenshot");

        // Each frame that sends counts as being heard from, since the computer is waiting on VentureOS.
        // Nine frames, 20 seconds apart: three minutes, far past the limit, all of it spent sending.
        for (var seconds = 20; computer.Heard[^1] != "ok" && seconds <= 200; seconds += 20)
            session.Tick(TimeSpan.FromSeconds(seconds));

        Assert.False(session.IsClosed);
        Assert.Equal("ok", computer.Heard[^1]);
    }

    [Fact]
    public void AScreenWhosePixelsDoNotMatchItsSizeIsRefused()
    {
        desktop.Screen = new VdbScreenshot(2, 2, new byte[5]);
        var session = Start(desktop);

        Send(session, "screenshot");

        Assert.Equal("error the screen is 2 by 2, but its pixels are 5 bytes", computer.Heard[^1]);
    }

    [Fact]
    public void LongTextIsCutToFitAndSaysSo()
    {
        var window = desktop.OpenApp("Notes", new string('t', 5000));
        window.View.SetText("name", new string('é', 5000));
        var session = Start(desktop);

        Send(session, $"snapshot {window.Id}");

        var head = computer.Heard.Single(line => line.StartsWith("window ", StringComparison.Ordinal));
        Assert.EndsWith(" cut", head);
        Assert.Contains($"title=\"{new string('t', VdbWords.MaximumTextLength)}\"", head, StringComparison.Ordinal);
        var field = computer.Heard.Single(line => line.StartsWith("control name=\"name\"", StringComparison.Ordinal));
        Assert.EndsWith(" cut", field);
        Assert.All(computer.Heard, line => Assert.True(System.Text.Encoding.UTF8.GetByteCount(line) <= VdbProtocol.MaximumLineLength));
    }

    [Fact]
    public void AListTooLongToListSaysHowManyRowsItHas()
    {
        var window = desktop.OpenApp("Notes", "Notes");
        window.View.List("notes")!.Items.AddRange(Enumerable.Range(1, 500).Select(number => $"Note {number}"));
        var session = Start(desktop);

        Send(session, $"snapshot {window.Id}");

        Assert.Contains(computer.Heard, line => line == "control name=\"notes\" kind=list rows=502");
        Assert.Equal(VdbSession.MaximumSnapshotRows, computer.Heard.Count(line => line.StartsWith("row ", StringComparison.Ordinal)));
    }

    [Fact]
    public void AnErrorCarryingWhatWasSentStillFitsInALine()
    {
        var window = desktop.OpenApp("Notes", "Notes");
        var session = Start(desktop);

        Send(session, $"act {window.Id} choose notes \"{new string('x', 7000)}\"");

        Assert.StartsWith("error \"notes\" has no row", computer.Heard[^1]);
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(computer.Heard[^1]) <= VdbProtocol.MaximumLineLength);
        Assert.False(session.IsClosed);
    }

    [Fact]
    public void AnActionInAMinimizedWindowRestoresIt()
    {
        var window = desktop.OpenApp("Notes", "Notes");
        desktop.Windows.Minimize(window);
        var session = Start(desktop);

        Send(session, $"act {window.Id} press ok");

        Assert.Equal("ok", computer.Heard[^1]);
        Assert.False(window.IsMinimized);
        Assert.Same(window, desktop.Windows.Focused);
    }
}

/// <summary>A screenshot made from the desktop's frame.</summary>
public sealed class VdbScreenshotTests
{
    [Fact]
    public void TakesRedGreenAndBlueFromEachPixelAndDropsAlpha()
    {
        var shot = VdbScreenshot.FromFrame([DesktopImage.Pack(255, 1, 2, 3), DesktopImage.Pack(0, 250, 251, 252)], 2, 1)!;

        Assert.Equal([1, 2, 3, 250, 251, 252], shot.Rgb);
        Assert.Null(shot.Problem());
    }

    [Theory]
    [InlineData(2, 2)]
    [InlineData(0, 1)]
    [InlineData(-1, -2)]
    public void RefusesAFrameThatIsNotTheSizeGiven(int width, int height) =>
        Assert.Null(VdbScreenshot.FromFrame([0, 0], width, height));
}

/// <summary>The words of the <c>ui</c> feature's lines: quoting, escapes, and what isn't a word.</summary>
public sealed class VdbWordsTests
{
    [Theory]
    [InlineData("plain", "\"plain\"")]
    [InlineData("say \"hi\"", "\"say \\\"hi\\\"\"")]
    [InlineData("a\\b", "\"a\\\\b\"")]
    [InlineData("one\ntwo\r\tthree", "\"one\\ntwo\\r\\tthree\"")]
    [InlineData("bell\u0007", "\"bell \"")]
    public void QuotesWhatALineCannotHold(string text, string quoted) => Assert.Equal(quoted, VdbWords.Quote(text));

    [Theory]
    [InlineData("plain")]
    [InlineData("say \"hi\" \\ there")]
    [InlineData("one\ntwo\r\tthree")]
    [InlineData("")]
    public void WhatIsQuotedSplitsBackToItself(string text)
    {
        Assert.True(VdbWords.TrySplit($"3 {VdbWords.Quote(text)}", out var words, out var problem), problem);
        Assert.Equal([new VdbWord("3", false), new VdbWord(text, true)], words);
    }

    [Fact]
    public void ManySpacesSeparateWordsAsOneDoes()
    {
        Assert.True(VdbWords.TrySplit("  3   press  ok ", out var words, out _));
        Assert.Equal(["3", "press", "ok"], words.Select(word => word.Text));
    }

    [Fact]
    public void CuttingNeverSplitsAPair()
    {
        var cut = false;
        var quoted = VdbWords.Quote(new string('a', VdbWords.MaximumTextLength - 1) + "😀", ref cut);

        Assert.True(cut);
        Assert.Equal(VdbWords.MaximumTextLength - 1 + 2, quoted.Length);
    }

    [Theory]
    [InlineData("\"open", "a quoted word has no closing quote")]
    [InlineData("\"a\\q\"", "\\q isn't an escape; a quoted word escapes \\\\, \\\", \\n, \\r, and \\t")]
    [InlineData("\"a\\", "a quoted word ends in a lone \\")]
    [InlineData("\"a\"b", "a quoted word ends at a space or the end of the line")]
    [InlineData("a\"b\"", "a quote can only start a word, not sit inside a\"b\"")]
    public void RefusesWhatIsNotAWord(string text, string reason)
    {
        Assert.False(VdbWords.TrySplit(text, out var words, out var problem));
        Assert.Null(words);
        Assert.Equal(reason, problem);
    }
}

/// <summary>Keys as <c>key</c> names them.</summary>
public sealed class VdbKeysTests
{
    [Theory]
    [InlineData("a", DesktopKey.Character, 'a', false, false, false)]
    [InlineData("Shift+a", DesktopKey.Character, 'A', false, true, false)]
    [InlineData("Ctrl+Z", DesktopKey.Character, 'z', true, false, false)]
    [InlineData("ctrl+shift+Z", DesktopKey.Character, 'z', true, true, false)]
    [InlineData("Alt+F", DesktopKey.Character, 'f', false, false, true)]
    [InlineData("+", DesktopKey.Character, '+', false, false, false)]
    [InlineData("Ctrl++", DesktopKey.Character, '+', true, false, false)]
    [InlineData("Space", DesktopKey.Character, ' ', false, false, false)]
    [InlineData("Enter", DesktopKey.Enter, '\0', false, false, false)]
    [InlineData("shift+tab", DesktopKey.Tab, '\0', false, true, false)]
    [InlineData("é", DesktopKey.Character, 'é', false, false, false)]
    public void ReadsAKeyAndItsModifiers(string name, DesktopKey key, char character, bool control, bool shift, bool alt)
    {
        Assert.True(VdbKeys.TryParse(name, out var press, out var problem), problem);
        Assert.Equal(new KeyPress(key, character, control, shift, alt), press);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("Ctrl+")]
    [InlineData("Meta+a")]
    [InlineData("\u0007")]
    public void RefusesWhatIsNotAKey(string name) => Assert.False(VdbKeys.TryParse(name, out _, out _));
}

/// <summary>What vdb tells the person at VentureOS, in toasts.</summary>
public sealed class VdbNotificationTests : IDisposable
{
    private const string Info = """<App version="1"><Info identifier="dev.example.dice" name="Dice"/></App>""";

    private readonly FakeVdbHost host = new();
    private readonly FakeVdbDesktop desktop = new();
    private readonly VdbTrust trust = new();

    public VdbNotificationTests() => trust.Trust(TestComputer.DefaultIdentity);

    public void Dispose() => host.Dispose();

    private (VdbSession Session, TestComputer Computer) Connect()
    {
        TestComputer computer = new();
        VdbSession session = new(host, trust, computer.Hear, nonce => nonce.Fill(0xf0), TimeSpan.Zero, desktop);
        session.Receive(computer.Bytes(computer.Hello()), TimeSpan.Zero);
        return (session, computer);
    }

    private IEnumerable<string> Texts => desktop.Windows.Notifications.History.Select(notification => notification.Text);

    [Fact]
    public void AComputerConnectingIsToldOnceUntilVentureOSRestarts()
    {
        Connect();
        Connect();

        Assert.Equal(["A computer connected over vdb."], Texts);
        Assert.Equal("VentureOS", desktop.Windows.Notifications.History[0].Title);
    }

    [Fact]
    public void AnInstalledAppIsToldInAToast()
    {
        var (session, computer) = Connect();
        var info = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(Info));
        foreach (var line in new[]
        {
            "install Dice.app", $"file Contents/Info.xml {System.Text.Encoding.UTF8.GetByteCount(Info)}", $"data {info}", "end",
            "file Contents/Lua/main.lua 0", "end", "commit",
        })
        {
            session.Receive(computer.Bytes(line), TimeSpan.Zero);
        }

        Assert.StartsWith("ok installed", computer.Heard[^1]);
        Assert.Equal(["vdb installed Dice.app.", "A computer connected over vdb."], Texts);
        Assert.Equal(2, desktop.Windows.Notifications.Waiting.Count);
    }

    [Fact]
    public void WithoutADesktopNothingIsPosted()
    {
        TestComputer computer = new();
        VdbSession session = new(host, trust, computer.Hear, nonce => nonce.Fill(0xf0), TimeSpan.Zero);
        session.Receive(computer.Bytes(computer.Hello()), TimeSpan.Zero);

        Assert.Empty(Texts);
    }
}
