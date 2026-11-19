using Venture.OS.Bundles;

namespace Venture.OS.Processes;

/// <summary>Where a process is in its life.</summary>
public enum ProcessState
{
    /// <summary>Its startup is running.</summary>
    Starting,

    /// <summary>It started, and hasn't ended.</summary>
    Running,

    /// <summary>It ended without crashing: it quit, was closed, killed, or couldn't start.</summary>
    Exited,

    /// <summary>It ended because its code kept failing.</summary>
    Crashed,
}

/// <summary>What the supervisor does when a process ends.</summary>
public enum RestartPolicy
{
    /// <summary>Nothing: an ordinary app stays ended.</summary>
    Never,

    /// <summary>Starts it again, as the desktop does for the apps it can't do without.</summary>
    Always,
}

/// <summary>How a process ended.</summary>
public enum EndingKind
{
    /// <summary>The app quit, such as from its menu or by closing its last window.</summary>
    Quit,

    /// <summary>Something closed it on purpose, such as <c>close</c>, or vdb replacing it.</summary>
    Closed,

    /// <summary><c>kill</c> ended it.</summary>
    Killed,

    /// <summary>Its startup failed, so it never ran.</summary>
    FailedToStart,

    /// <summary>Its code kept failing, so the supervisor ended it.</summary>
    Crashed,
}

/// <summary>How a process ended, and why, in words a person can read.</summary>
public sealed record ProcessEnding(EndingKind Kind, string Reason);

/// <summary>
/// A running app, as the <see cref="Supervisor"/> keeps it: an ID that's never reused while VentureOS runs, the app,
/// where it is in its life, and, once it's over, how it ended.
/// </summary>
/// <remarks>
/// Every call into the app's code goes through <see cref="Run"/>, never straight to its interpreter. Today that runs
/// the work at once, on the caller's thread; it's the one place that would change for apps to run on threads of their
/// own.
/// </remarks>
public sealed class AppProcess
{
    private IDisposable? resources;

    internal AppProcess(int id, Bundle bundle, RestartPolicy policy, TimeSpan started)
    {
        Id = id;
        Bundle = bundle;
        Policy = policy;
        Started = started;
    }

    /// <summary>Its ID: a whole number from 1, counting up, never reused while VentureOS runs.</summary>
    public int Id { get; }

    /// <summary>The app it runs.</summary>
    public Bundle Bundle { get; }

    /// <summary>The app's name, from its <c>Info.xml</c>.</summary>
    public string Name => Bundle.Name;

    /// <summary>What happens when it ends.</summary>
    public RestartPolicy Policy { get; }

    /// <summary>When it started, on the supervisor's clock.</summary>
    public TimeSpan Started { get; }

    /// <summary>Where it is in its life.</summary>
    public ProcessState State { get; internal set; } = ProcessState.Starting;

    /// <summary>How it ended, or <see langword="null"/> while it hasn't.</summary>
    public ProcessEnding? Ending { get; internal set; }

    /// <summary>Whether it's starting or running, so its code may be called.</summary>
    public bool IsAlive => State is ProcessState.Starting or ProcessState.Running;

    /// <summary>
    /// Gives the process what its code set up, such as its interpreter, to be disposed when it ends. One thing at a
    /// time; attaching another replaces it, without disposing the first.
    /// </summary>
    public void Attach(IDisposable owned)
    {
        ArgumentNullException.ThrowIfNull(owned);
        resources = owned;
    }

    /// <summary>Runs work in the app's code, if it's alive.</summary>
    /// <returns>Whether it ran: work for a process that has ended is dropped.</returns>
    public bool Run(Action work)
    {
        ArgumentNullException.ThrowIfNull(work);
        if (!IsAlive)
            return false;

        work();
        return true;
    }

    /// <summary>Disposes what it owned, once.</summary>
    internal void Release()
    {
        var owned = resources;
        resources = null;
        owned?.Dispose();
    }

    public override string ToString() => $"{Id} {Name} ({State})";
}
