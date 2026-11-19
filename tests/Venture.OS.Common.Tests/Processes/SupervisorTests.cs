using Venture.OS.Bundles;
using Venture.OS.Processes;

namespace Venture.OS.Tests.Processes;

public sealed class SupervisorTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"venture-processes-{Guid.NewGuid():N}");
    private readonly List<string> events = [];
    private readonly Supervisor supervisor;
    private TimeSpan now = TimeSpan.FromSeconds(5);

    public SupervisorTests()
    {
        supervisor = new Supervisor(() => now);
        supervisor.Started += process => events.Add($"started {process.Id} {process.Name}, {supervisor.Processes.Count} running");
        supervisor.Ended += process => events.Add($"ended {process.Id} {process.Name}: {process.Ending}, {supervisor.Processes.Count} running");
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void StartsAProcessThatRuns()
    {
        var process = supervisor.Start(App("Dice"), RestartPolicy.Never, _ => null);

        Assert.Equal(1, process.Id);
        Assert.Equal("Dice", process.Name);
        Assert.Equal(ProcessState.Running, process.State);
        Assert.True(process.IsAlive);
        Assert.Equal(TimeSpan.FromSeconds(5), process.Started);
        Assert.Null(process.Ending);
        Assert.Same(process, Assert.Single(supervisor.Processes));
        Assert.Equal(["started 1 Dice, 1 running"], events);
    }

    [Fact]
    public void CountsIdsUpAndNeverReusesThem()
    {
        var first = supervisor.Start(App("Dice"), RestartPolicy.Never, _ => null);
        supervisor.End(first, EndingKind.Quit, "Dice quit.");

        var second = supervisor.Start(App("Dice"), RestartPolicy.Never, _ => null);
        var third = supervisor.Start(App("Notes"), RestartPolicy.Always, _ => null);

        Assert.Equal([2, 3], [second.Id, third.Id]);
        Assert.Same(third, supervisor.Find(3));
        Assert.Null(supervisor.Find(1));
        Assert.Same(second, supervisor.FindApp("dev.example.dice"));
        Assert.Equal(RestartPolicy.Always, third.Policy);
    }

    [Fact]
    public void RunsTheStartupInsideTheProcess()
    {
        AppProcess? seen = null;
        var process = supervisor.Start(App("Dice"), RestartPolicy.Never, starting =>
        {
            seen = starting;
            Assert.Equal(ProcessState.Starting, starting.State);
            Assert.True(starting.Run(() => { }));
            return null;
        });

        Assert.Same(process, seen);
    }

    [Fact]
    public void AStartupThatFailsEndsTheProcessWithItsReason()
    {
        Disposable owned = new();
        var process = supervisor.Start(App("Dice"), RestartPolicy.Never, starting =>
        {
            starting.Attach(owned);
            return "Dice has no main.lua.";
        });

        Assert.Equal(ProcessState.Exited, process.State);
        Assert.Equal(new ProcessEnding(EndingKind.FailedToStart, "Dice has no main.lua."), process.Ending);
        Assert.Empty(supervisor.Processes);
        Assert.True(owned.Disposed);
        Assert.Equal(["ended 1 Dice: ProcessEnding { Kind = FailedToStart, Reason = Dice has no main.lua. }, 0 running"], events);
    }

    [Fact]
    public void AStartupThatThrowsFailsToStartWithoutTheCallerFailing()
    {
        var process = supervisor.Start(App("Dice"), RestartPolicy.Never, _ => throw new InvalidOperationException("boom"));

        Assert.Equal(EndingKind.FailedToStart, process.Ending!.Kind);
        Assert.Equal("Dice failed while starting (InvalidOperationException): boom", process.Ending.Reason);
        Assert.Empty(supervisor.Processes);
    }

    [Fact]
    public void RefusesToStartAnAppThatIsRunning()
    {
        supervisor.Start(App("Dice"), RestartPolicy.Never, _ => null);

        var refusal = Assert.Throws<InvalidOperationException>(() => supervisor.Start(App("Dice"), RestartPolicy.Never, _ => null));

        Assert.Equal("Dice is already running, as process 1.", refusal.Message);
    }

    [Theory]
    [InlineData(EndingKind.Quit, ProcessState.Exited)]
    [InlineData(EndingKind.Closed, ProcessState.Exited)]
    [InlineData(EndingKind.Killed, ProcessState.Exited)]
    [InlineData(EndingKind.Crashed, ProcessState.Crashed)]
    public void EndingDisposesWhatItOwnedAndSaysHow(EndingKind kind, ProcessState state)
    {
        Disposable owned = new();
        var process = supervisor.Start(App("Dice"), RestartPolicy.Never, starting =>
        {
            starting.Attach(owned);
            return null;
        });

        Assert.True(supervisor.End(process, kind, "Because."));

        Assert.Equal(state, process.State);
        Assert.False(process.IsAlive);
        Assert.Equal(new ProcessEnding(kind, "Because."), process.Ending);
        Assert.True(owned.Disposed);
        Assert.Empty(supervisor.Processes);
        Assert.EndsWith(", 0 running", events[^1]);
    }

    [Fact]
    public void EndingTwiceLeavesTheFirstEnding()
    {
        Disposable owned = new();
        var process = supervisor.Start(App("Dice"), RestartPolicy.Never, starting =>
        {
            starting.Attach(owned);
            return null;
        });
        supervisor.End(process, EndingKind.Quit, "Dice quit.");

        Assert.False(supervisor.End(process, EndingKind.Killed, "Killed."));

        Assert.Equal(EndingKind.Quit, process.Ending!.Kind);
        Assert.Equal(1, owned.Count);
        Assert.Single(events, line => line.StartsWith("ended", StringComparison.Ordinal));
    }

    [Fact]
    public void WorkForAProcessThatEndedIsDropped()
    {
        var process = supervisor.Start(App("Dice"), RestartPolicy.Never, _ => null);
        var ran = 0;
        Assert.True(process.Run(() => ran++));

        supervisor.End(process, EndingKind.Quit, "Dice quit.");

        Assert.False(process.Run(() => ran++));
        Assert.Equal(1, ran);
    }

    [Fact]
    public void NothingTheEndingSetsOffCallsBackIntoTheApp()
    {
        AppProcess? process = null;
        var calledBack = true;
        Disposable owned = new() { OnDispose = () => calledBack = process!.Run(() => { }) };
        process = supervisor.Start(App("Dice"), RestartPolicy.Never, starting =>
        {
            starting.Attach(owned);
            return null;
        });

        supervisor.End(process, EndingKind.Closed, "Closed.");

        Assert.False(calledBack);
    }

    private Bundle App(string name)
    {
        var folder = Path.Combine(root, $"{name}.app");
        var contents = Path.Combine(folder, "Contents");
        Directory.CreateDirectory(Path.Combine(contents, "Lua"));
        File.WriteAllText(Path.Combine(contents, "Info.xml"),
            $"""<App version="1"><Info identifier="dev.example.{name.ToLowerInvariant()}" name="{name}"/></App>""");
        File.WriteAllText(Path.Combine(contents, "Lua", "main.lua"), "");
        return Bundle.Read(folder.Replace('\\', '/'), BundleKind.App);
    }

    private sealed class Disposable : IDisposable
    {
        public int Count { get; private set; }

        public bool Disposed => Count > 0;

        public Action? OnDispose { get; init; }

        public void Dispose()
        {
            Count++;
            OnDispose?.Invoke();
        }
    }
}
