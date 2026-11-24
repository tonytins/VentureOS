using Venture.OS.Bundles;

namespace Venture.OS.Processes;

/// <summary>
/// Starts, keeps, and ends apps' processes: the one place that knows which apps are running, and that says when one
/// ended, and why. Opening, closing, quitting, and killing all go through it; it ends an app whose code keeps failing,
/// and starts the apps the desktop keeps running again.
/// </summary>
/// <remarks>
/// <para>It doesn't read bundles or run Lua itself: whoever starts a process hands it the startup to run, which sets up
/// the app's code and attaches it with <see cref="AppProcess.Attach"/>. <see cref="Started"/> and <see cref="Ended"/>
/// are raised after the table has changed, so a listener sees it as it now is.</para>
/// <para>A crash is never ended in the middle of the app's code, or of whatever called it, such as the desktop
/// handing out a frame's events: <see cref="ReportFault"/> marks it, the process takes no more work, and
/// <see cref="Tick"/> ends it. Restarts happen in <see cref="Tick"/> too, so whoever drives the system calls it
/// regularly: the desktop once a frame, the shell after each command.</para>
/// </remarks>
/// <param name="clock">The time, on a clock that only goes forward.</param>
public sealed class Supervisor(Func<TimeSpan> clock)
{
    /// <summary>How many failures of an app's code, within <see cref="FaultWindow"/>, crash it.</summary>
    public const int FaultsToCrash = 3;

    /// <summary>How far back failures count toward a crash.</summary>
    public static readonly TimeSpan FaultWindow = TimeSpan.FromMinutes(1);

    /// <summary>How long a kept app must run before its failures in a row are forgotten.</summary>
    public static readonly TimeSpan SettledAfter = TimeSpan.FromMinutes(1);

    // The waits before starting a kept app again after each failure in a row; the last one repeats.
    private static readonly TimeSpan[] Waits =
        [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)];

    private readonly List<AppProcess> processes = [];
    private readonly Dictionary<string, Kept> kept = new(StringComparer.Ordinal);
    private int lastId;

    /// <summary>Raised when a process has started.</summary>
    public event Action<AppProcess>? Started;

    /// <summary>Raised when a process has ended, with <see cref="AppProcess.Ending"/> saying how.</summary>
    public event Action<AppProcess>? Ended;

    /// <summary>The time now, on the supervisor's clock.</summary>
    public TimeSpan Now => clock();

    /// <summary>The processes that are starting or running, oldest first.</summary>
    public IReadOnlyList<AppProcess> Processes => processes;

    /// <summary>A process by its ID, or <see langword="null"/>.</summary>
    public AppProcess? Find(int id)
    {
        foreach (var process in processes)
        {
            if (process.Id == id)
                return process;
        }

        return null;
    }

    /// <summary>The process running an app, by its identifier, or <see langword="null"/>.</summary>
    public AppProcess? FindApp(string identifier)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        foreach (var process in processes)
        {
            if (process.Bundle.Identifier == identifier)
                return process;
        }

        return null;
    }

    /// <summary>
    /// When a kept app that isn't running will be started again, or <see langword="null"/> when it's running or isn't
    /// kept.
    /// </summary>
    public TimeSpan? NextStart(string identifier) =>
        kept.TryGetValue(identifier, out var app) && FindApp(identifier) is null ? app.Next : null;

    /// <summary>Starts a process for an app, running its startup in it.</summary>
    /// <param name="policy">
    /// <see cref="RestartPolicy.Always"/> keeps the app: from now on it's started again whenever it ends, until
    /// <see cref="StopKeeping"/>.
    /// </param>
    /// <param name="startup">
    /// Sets up the app's code and runs it, attaching what it set up to the process; it gives why the app couldn't
    /// start, or <see langword="null"/> when it did. A kept app is restarted with the same startup.
    /// </param>
    /// <returns>The process, running, or ended as <see cref="EndingKind.FailedToStart"/> with the reason.</returns>
    /// <exception cref="InvalidOperationException">The app is already running.</exception>
    public AppProcess Start(Bundle app, RestartPolicy policy, Func<AppProcess, string?> startup)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(startup);
        if (FindApp(app.Identifier) is { } running)
            throw new InvalidOperationException($"{app.Name} is already running, as process {running.Id}.");

        if (policy == RestartPolicy.Always)
        {
            if (kept.TryGetValue(app.Identifier, out var known))
                (known.Bundle, known.Startup) = (app, startup);
            else
                kept[app.Identifier] = new Kept(app, startup);
        }

        AppProcess process = new(++lastId, app, policy, clock());
        processes.Add(process);

        string? problem;
        try
        {
            problem = startup(process);
        }
        catch (Exception exception)
        {
            // The supervisor is a boundary: whatever a startup throws ends that process, never the caller.
            problem = $"{app.Name} failed while starting ({exception.GetType().Name}): {exception.Message}";
        }

        if (problem is not null)
        {
            Finish(process, new ProcessEnding(EndingKind.FailedToStart, problem), ProcessState.Exited);
            return process;
        }

        process.State = ProcessState.Running;
        Started?.Invoke(process);
        return process;
    }

    /// <summary>Ends a process, disposing what it set up, which closes its windows.</summary>
    /// <returns>Whether it ended now: a process that had already ended is left as it was.</returns>
    /// <exception cref="InvalidOperationException">Its code is running; a process can't end from inside itself.</exception>
    public bool End(AppProcess process, EndingKind kind, string reason)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(reason);
        if (!processes.Contains(process) || process.State is ProcessState.Exited or ProcessState.Crashed)
            return false;
        if (process.IsBusy)
            throw new InvalidOperationException($"{process.Name} can't end while its own code is running.");

        Finish(process, new ProcessEnding(kind, reason), kind == EndingKind.Crashed ? ProcessState.Crashed : ProcessState.Exited);
        return true;
    }

    /// <summary>
    /// Records that a running process's code failed: an error escaped it, or it ran past its time. The
    /// <see cref="FaultsToCrash"/>th within <see cref="FaultWindow"/> crashes it: it takes no more work, and the next
    /// <see cref="Tick"/> ends it.
    /// </summary>
    public void ReportFault(AppProcess process)
    {
        ArgumentNullException.ThrowIfNull(process);
        if (process.State != ProcessState.Running || process.CrashReason is not null)
            return;

        var now = clock();
        var faults = process.Faults;
        faults.RemoveAll(time => now - time >= FaultWindow);
        faults.Add(now);
        if (faults.Count >= FaultsToCrash)
        {
            process.CrashReason =
                $"{process.Name} crashed: its code failed {faults.Count} times within a minute.";
        }
    }

    /// <summary>
    /// Ends the processes that crashed since the last tick, and starts the kept apps whose wait is over.
    /// </summary>
    /// <returns>Whether anything started or ended.</returns>
    public bool Tick()
    {
        var changed = false;
        for (var index = processes.Count - 1; index >= 0; index--)
        {
            var process = processes[index];
            if (process.CrashReason is { } reason && !process.IsBusy)
            {
                Finish(process, new ProcessEnding(EndingKind.Crashed, reason), ProcessState.Crashed);
                changed = true;
            }
        }

        if (kept.Count == 0)
            return changed;

        var now = clock();
        foreach (var app in kept.Values)
        {
            if (app.Next is { } next && next <= now && FindApp(app.Bundle.Identifier) is null)
            {
                app.Next = null;
                Start(app.Bundle, RestartPolicy.Always, app.Startup);
                changed = true;
            }
        }

        return changed;
    }

    /// <summary>
    /// Stops keeping an app: it isn't started again when it ends, such as when the system it ran in is closing.
    /// </summary>
    public void StopKeeping(string identifier)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        kept.Remove(identifier);
    }

    private void Finish(AppProcess process, ProcessEnding ending, ProcessState state)
    {
        // Marked over first, so nothing the release sets off calls back into the app's code.
        process.State = state;
        process.Ending = ending;
        processes.Remove(process);
        process.Release();
        Schedule(process, ending.Kind);
        Ended?.Invoke(process);
    }

    /// <summary>When to start a kept app again: at once after it's closed, later after failing.</summary>
    private void Schedule(AppProcess process, EndingKind how)
    {
        if (process.Policy != RestartPolicy.Always || !kept.TryGetValue(process.Bundle.Identifier, out var app))
            return;

        var now = clock();
        switch (how)
        {
            case EndingKind.FailedToStart:
                app.Next = now + Waits[Math.Min(app.Failures, Waits.Length - 1)];
                app.Failures++;
                break;
            case EndingKind.Crashed:
                if (now - process.Started >= SettledAfter)
                    app.Failures = 0;
                app.Next = app.Failures == 0 ? now : now + Waits[Math.Min(app.Failures - 1, Waits.Length - 1)];
                app.Failures++;
                break;
            default:
                if (now - process.Started >= SettledAfter)
                    app.Failures = 0;
                app.Next = now;
                break;
        }
    }

    /// <summary>A kept app: how to start it, and when next.</summary>
    private sealed class Kept(Bundle bundle, Func<AppProcess, string?> startup)
    {
        public Bundle Bundle { get; set; } = bundle;

        public Func<AppProcess, string?> Startup { get; set; } = startup;

        public int Failures { get; set; }

        public TimeSpan? Next { get; set; }
    }
}
