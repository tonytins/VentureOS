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

    [Fact]
    public void TheThirdFailureInAMinuteCrashesItAtTheNextTick()
    {
        var process = supervisor.Start(App("Dice"), RestartPolicy.Never, _ => null);

        supervisor.ReportFault(process);
        now += TimeSpan.FromSeconds(20);
        supervisor.ReportFault(process);
        Assert.True(process.IsAlive);
        now += TimeSpan.FromSeconds(20);
        supervisor.ReportFault(process);

        // Marked, and taking no more work, but not ended until the tick.
        Assert.False(process.IsAlive);
        Assert.False(process.Run(() => { }));
        Assert.Equal(ProcessState.Running, process.State);

        Assert.True(supervisor.Tick());
        Assert.Equal(ProcessState.Crashed, process.State);
        Assert.Equal(new ProcessEnding(EndingKind.Crashed, "Dice crashed: its code failed 3 times within a minute."),
            process.Ending);
        Assert.Empty(supervisor.Processes);
    }

    [Fact]
    public void FailuresOlderThanAMinuteDoNotCount()
    {
        var process = supervisor.Start(App("Dice"), RestartPolicy.Never, _ => null);

        supervisor.ReportFault(process);
        now += TimeSpan.FromSeconds(30);
        supervisor.ReportFault(process);
        now += TimeSpan.FromSeconds(31);
        supervisor.ReportFault(process);
        supervisor.Tick();

        Assert.True(process.IsAlive);
    }

    [Fact]
    public void ACrashWaitsForTheAppsCodeToFinish()
    {
        var process = supervisor.Start(App("Dice"), RestartPolicy.Never, _ => null);

        process.Run(() =>
        {
            for (var fault = 0; fault < Supervisor.FaultsToCrash; fault++)
                supervisor.ReportFault(process);
            Assert.False(supervisor.Tick());
            Assert.Throws<InvalidOperationException>(() => supervisor.End(process, EndingKind.Killed, "Killed."));
        });

        Assert.Equal(ProcessState.Running, process.State);
        supervisor.Tick();
        Assert.Equal(ProcessState.Crashed, process.State);
    }

    [Theory]
    [InlineData(EndingKind.Closed)]
    [InlineData(EndingKind.Killed)]
    [InlineData(EndingKind.Quit)]
    public void AKeptAppStartsAgainAtOnceWhenItEnds(EndingKind how)
    {
        var starts = 0;
        var first = supervisor.Start(App("Files"), RestartPolicy.Always, _ =>
        {
            starts++;
            return null;
        });
        supervisor.End(first, how, "Ended.");

        Assert.Equal(now, supervisor.NextStart("dev.example.files"));
        Assert.True(supervisor.Tick());

        var second = Assert.Single(supervisor.Processes);
        Assert.Equal(2, second.Id);
        Assert.Equal(RestartPolicy.Always, second.Policy);
        Assert.Equal(2, starts);
        Assert.Null(supervisor.NextStart("dev.example.files"));
    }

    [Fact]
    public void AKeptAppThatFailsToStartWaitsLongerEachTime()
    {
        supervisor.Start(App("Files"), RestartPolicy.Always, _ => "Files is broken.");
        List<double> waits = [];
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var next = supervisor.NextStart("dev.example.files")!.Value;
            waits.Add((next - now).TotalSeconds);
            now = next - TimeSpan.FromMilliseconds(1);
            Assert.False(supervisor.Tick());
            now = next;
            Assert.True(supervisor.Tick());
        }

        Assert.Equal([2, 5, 10, 30, 30], waits);
        Assert.Empty(supervisor.Processes);
    }

    [Fact]
    public void AKeptAppThatCrashesStartsAgainAtOnceThenWaits()
    {
        supervisor.Start(App("Files"), RestartPolicy.Always, _ => null);
        List<double> waits = [];
        supervisor.Ended += _ => waits.Add((supervisor.NextStart("dev.example.files")!.Value - now).TotalSeconds);
        for (var crash = 0; crash < 3; crash++)
        {
            var process = Assert.Single(supervisor.Processes);
            for (var fault = 0; fault < Supervisor.FaultsToCrash; fault++)
                supervisor.ReportFault(process);
            supervisor.Tick();
            now += TimeSpan.FromSeconds(waits[^1]);
            supervisor.Tick();
        }

        // At once: started again in the same tick that ended it.
        Assert.Equal([0, 2, 5], waits);

        // A minute of running settles it: the next crash restarts at once again.
        now += Supervisor.SettledAfter;
        var settled = Assert.Single(supervisor.Processes);
        for (var fault = 0; fault < Supervisor.FaultsToCrash; fault++)
            supervisor.ReportFault(settled);
        supervisor.Tick();
        Assert.Equal(0, waits[^1]);
        Assert.Single(supervisor.Processes);
    }

    [Fact]
    public void AnOrdinaryAppIsNotStartedAgain()
    {
        var process = supervisor.Start(App("Dice"), RestartPolicy.Never, _ => null);
        supervisor.End(process, EndingKind.Closed, "Closed.");

        Assert.Null(supervisor.NextStart("dev.example.dice"));
        Assert.False(supervisor.Tick());
        Assert.Empty(supervisor.Processes);
    }

    [Fact]
    public void AnAppNoLongerKeptStaysEnded()
    {
        var process = supervisor.Start(App("Files"), RestartPolicy.Always, _ => null);
        supervisor.StopKeeping("dev.example.files");
        supervisor.End(process, EndingKind.Closed, "Closed.");

        Assert.False(supervisor.Tick());
        Assert.Empty(supervisor.Processes);
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
