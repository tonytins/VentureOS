using Venture.OS.Vdb;

namespace Venture.OS.Tests.Vdb;

/// <summary>
/// Replays the recorded conversations in <c>docs/vdb/conversations</c>, which the vdb tool's tests read too, so the
/// two sides can't drift apart. In each, <c>&gt;</c> is a line the computer sends, <c>&lt;</c> one VentureOS sends
/// back, in order, and <c>!</c> sets up or checks VentureOS's side; <c>#</c> starts a comment.
/// </summary>
/// <remarks>
/// <c>! identity</c> names the computer's identity, which the replay keys the connection from; VentureOS's nonce is
/// always <c>f0f1…ff</c>. Lines after VentureOS's <c>hello</c> are written as they read inside the seal, and the
/// replay seals and opens them, unless <c>! raw</c> says the lines from there on are written as they go on the
/// network.
/// </remarks>
public sealed class VdbConversationTests
{
    private static readonly string Folder = Path.Combine(AppContext.BaseDirectory, "vdb", "conversations");

    public static TheoryData<string> Conversations()
    {
        TheoryData<string> names = [];
        foreach (var file in Directory.GetFiles(Folder, "*.txt").Order(StringComparer.Ordinal))
            names.Add(Path.GetFileName(file));
        return names;
    }

    [Fact]
    public void ThereAreConversationsToReplay() => Assert.NotEmpty(Conversations());

    [Theory]
    [MemberData(nameof(Conversations))]
    public void Replays(string name)
    {
        using FakeVdbHost host = new();
        VdbTrust trust = new();
        var now = TimeSpan.Zero;
        var identity = TestComputer.DefaultIdentity;
        var computer = new TestComputer();
        var isRaw = false;
        List<string> network = [];
        VdbSession session = new(host, trust, line => { network.Add(line); computer.Hear(line); }, ServerNonce, now);
        var heard = 0;

        var number = 0;
        foreach (var line in File.ReadLines(Path.Combine(Folder, name)))
        {
            number++;
            var at = $"{name}, line {number}";
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            var text = line.Length > 2 ? line[2..] : "";
            switch (line[0])
            {
                case '>':
                    if (text.StartsWith("hello ", StringComparison.Ordinal) && text.Split(' ') is { Length: >= 4 } hello
                        && Venture.OS.Cryptography.Hex.IsLowercase(hello[3], 32))
                        computer = new TestComputer(identity, hello[3]);
                    var bytes = isRaw ? System.Text.Encoding.UTF8.GetBytes(text + "\n") : computer.Bytes(text);
                    session.Receive(bytes, now);
                    break;
                case '<':
                    Assert.True(heard < network.Count, $"{at}: VentureOS sent nothing, not \"{text}\".");
                    Assert.Equal(text, host.AsVentureSees(isRaw ? network[heard] : computer.Heard[heard]));
                    heard++;
                    break;
                case '!' when text.StartsWith("identity ", StringComparison.Ordinal):
                    identity = text["identity ".Length..];
                    computer = new TestComputer(identity);
                    break;
                case '!' when text == "raw":
                    isRaw = true;
                    break;
                case '!':
                    now = Act(text, at, host, trust, session, now);
                    break;
                default:
                    Assert.Fail($"{at}: a line starts with >, <, !, or #.");
                    break;
            }
        }

        Assert.True(heard == network.Count, $"{name}: VentureOS also sent \"{string.Join("\", \"", computer.Heard.Skip(heard))}\".");
        Assert.Empty(computer.Unopened);
    }

    // VentureOS's nonce in every conversation.
    private static void ServerNonce(Span<byte> nonce)
    {
        for (var index = 0; index < nonce.Length; index++)
            nonce[index] = (byte)(0xf0 + index);
    }

    private static TimeSpan Act(string text, string at, FakeVdbHost host, VdbTrust trust, VdbSession session, TimeSpan now)
    {
        var words = text.Split(' ', 2);
        var argument = words.Length > 1 ? words[1] : "";
        switch (words[0])
        {
            case "trusted":
                trust.Trust(argument);
                break;
            case "answer":
                var answer = argument.Split(' ');
                trust.Answer(answer[1], answer[0] == "trust");
                break;
            case "wait":
                now += TimeSpan.FromSeconds(int.Parse(argument, System.Globalization.CultureInfo.InvariantCulture));
                session.Tick(now);
                break;
            case "system-app":
                host.SystemApps.Add(argument);
                break;
            case "print":
                session.Print(argument);
                break;
            case "disconnect":
                session.Close();
                break;
            case "closed":
                Assert.True(session.IsClosed, $"{at}: the connection is still open.");
                break;
            case "installed":
                Assert.True(Directory.Exists(Path.Combine(host.AppsFolder, argument)), $"{at}: {argument} isn't installed.");
                break;
            case "not-installed":
                Assert.False(Directory.Exists(Path.Combine(host.AppsFolder, argument)), $"{at}: {argument} is installed.");
                break;
            case "staging-empty":
                Assert.True(!Directory.Exists(host.StagingFolder) || Directory.GetFileSystemEntries(host.StagingFolder).Length == 0,
                    $"{at}: something was left in staging.");
                break;
            case "launched":
                Assert.Contains(argument, host.Launched);
                break;
            case "closed-app":
                Assert.Contains(argument, host.Closed);
                break;
            default:
                Assert.Fail($"{at}: \"{words[0]}\" isn't something a conversation can do.");
                break;
        }

        return now;
    }
}
