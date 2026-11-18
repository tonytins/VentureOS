using Venture.OS.Vdb;

namespace Venture.OS.Tests.Vdb;

/// <summary>
/// Replays the recorded conversations in <c>docs/vdb/conversations</c>, which the vdb tool's tests read too, so the
/// two sides can't drift apart. In each, <c>&gt;</c> is a line the computer sends, <c>&lt;</c> one VentureOS sends
/// back, in order, and <c>!</c> sets up or checks VentureOS's side; <c>#</c> starts a comment.
/// </summary>
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
        Queue<string> sent = new();
        var now = TimeSpan.Zero;
        VdbSession session = new(host, trust, line => sent.Enqueue(host.AsVentureSees(line)), now);

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
                    session.Receive(System.Text.Encoding.UTF8.GetBytes(text + "\n"), now);
                    break;
                case '<':
                    Assert.True(sent.Count > 0, $"{at}: VentureOS sent nothing, not \"{text}\".");
                    Assert.Equal(text, sent.Dequeue());
                    break;
                case '!':
                    now = Act(text, at, host, trust, session, now);
                    break;
                default:
                    Assert.Fail($"{at}: a line starts with >, <, !, or #.");
                    break;
            }
        }

        Assert.True(sent.Count == 0, $"{name}: VentureOS also sent \"{string.Join("\", \"", sent)}\".");
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
