using Venture.OS.Bundles;

namespace Venture.OS.Processes;

/// <summary>
/// Starts, keeps, and ends apps' processes: the one place that knows which apps are running, and that says when one
/// ended, and why. Opening, closing, quitting, and killing all go through it.
/// </summary>
/// <remarks>
/// It doesn't read bundles or run Lua itself: whoever starts a process hands it the startup to run, which sets up the
/// app's code and attaches it with <see cref="AppProcess.Attach"/>. <see cref="Started"/> and <see cref="Ended"/> are
/// raised after the table has changed, so a listener sees it as it now is.
/// </remarks>
/// <param name="clock">The time, on a clock that only goes forward, for when each process started.</param>
public sealed class Supervisor(Func<TimeSpan> clock)
{
    private readonly List<AppProcess> processes = [];
    private int lastId;

    /// <summary>Raised when a process has started.</summary>
    public event Action<AppProcess>? Started;

    /// <summary>Raised when a process has ended, with <see cref="AppProcess.Ending"/> saying how.</summary>
    public event Action<AppProcess>? Ended;

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

    /// <summary>Starts a process for an app, running its startup in it.</summary>
    /// <param name="startup">
    /// Sets up the app's code and runs it, attaching what it set up to the process; it gives why the app couldn't
    /// start, or <see langword="null"/> when it did.
    /// </param>
    /// <returns>The process, running, or ended as <see cref="EndingKind.FailedToStart"/> with the reason.</returns>
    /// <exception cref="InvalidOperationException">The app is already running.</exception>
    public AppProcess Start(Bundle app, RestartPolicy policy, Func<AppProcess, string?> startup)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(startup);
        if (FindApp(app.Identifier) is { } running)
            throw new InvalidOperationException($"{app.Name} is already running, as process {running.Id}.");

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
    public bool End(AppProcess process, EndingKind kind, string reason)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(reason);
        if (!process.IsAlive || !processes.Contains(process))
            return false;

        Finish(process, new ProcessEnding(kind, reason), kind == EndingKind.Crashed ? ProcessState.Crashed : ProcessState.Exited);
        return true;
    }

    private void Finish(AppProcess process, ProcessEnding ending, ProcessState state)
    {
        // Marked over first, so nothing the release sets off calls back into the app's code.
        process.State = state;
        process.Ending = ending;
        processes.Remove(process);
        process.Release();
        Ended?.Invoke(process);
    }
}
