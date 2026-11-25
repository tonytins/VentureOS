using System.Diagnostics;
using System.Text;
using Venture.OS.Scripting;
using Venture.OS.Time;

namespace Venture.Airo.Simulation;

/// <summary>
/// What a simulated VentureOS tells its scripts about itself: the same name, version, and build number the kernel
/// built from this commit would report, the computer's clock, and how long the run has lasted. There's no Cosmos,
/// network, or measured memory to report, so those are left out, as the kernel leaves out what it can't read.
/// </summary>
/// <param name="disk">The run's copy of VentureOS's files.</param>
/// <param name="version">VentureOS's version, such as <c>0.8.102</c>, which the program running it shares.</param>
/// <param name="commit">The commit it was built from, which the build number comes from.</param>
/// <param name="host">What's running it, such as <c>Roundhouse</c>, which scripts see in place of Cosmos's version.</param>
public sealed class SimulatedSystem(SimulatedDisk disk, string version, string commit, string host) : ISystemInfo
{
    private readonly long started = Stopwatch.GetTimestamp();

    public string Name => "VentureOS";

    public string Version => version;

    /// <summary>The kernel's build number for this commit: its hash's first four characters, little-endian, modulo a million.</summary>
    public uint BuildNumber
    {
        get
        {
            var commit = Encoding.UTF8.GetBytes(Commit);
            return commit.Length < sizeof(uint) ? 0 : BitConverter.ToUInt32(commit, 0) % 1_000_000;
        }
    }

    public string Commit => commit;

    /// <summary>No Cosmos runs underneath; the host says so rather than claiming a version.</summary>
    public string CosmosVersion => $"none (simulated in {host})";

    public TimeSpan? Uptime => Stopwatch.GetElapsedTime(started);

    public MemoryInfo? Memory => null;

    public NetworkInfo? Network => null;

    public string? SystemDirectory => SimulatedDisk.SystemFolder;

    /// <summary>The free and total space of the disk the run's copy is on.</summary>
    public SpaceInfo? Space(string path)
    {
        try
        {
            DriveInfo drive = new(disk.Root);
            return new SpaceInfo((ulong)drive.AvailableFreeSpace, (ulong)drive.TotalSize);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>The desktop's look, which Settings changes, as the kernel's; a run keeps its own.</summary>
    public IAppearance? Appearance { get; } = new Venture.OS.Desktop.ScriptAppearance();

    public INetworkTools? NetworkTools => null;

    /// <summary>The computer's clock, in UTC, kept nowhere: the run's settings start fresh each time.</summary>
    public WallClock? WallClock { get; } = new(() => DateTime.UtcNow);

    /// <summary>The run's alerts, Quick Look, and apps, once its desktop is set up.</summary>
    public IScriptDesktop? Desktop { get; set; }

    /// <summary>The run's copy of VentureOS's files, which scripts' paths are kept within.</summary>
    public FileRoot? Root { get; } = new(disk.Root);

    /// <summary>
    /// None unless the host gives one: a run has no network to listen on, so Settings' Developer pane says developer
    /// mode isn't here. A test's VentureOS has one, so the pane can be tested.
    /// </summary>
    public Venture.OS.Vdb.VdbDeveloperMode? Developer { get; init; }

    /// <summary>None: a run starts in the desktop already, and never boots.</summary>
    public BootDesktop? BootDesktop => null;

    /// <summary>The run's apps' processes, which its app manager runs them through.</summary>
    public Venture.OS.Processes.Supervisor Processes { get; } = new(() => global::System.Diagnostics.Stopwatch.GetElapsedTime(Started));

    Venture.OS.Processes.Supervisor? ISystemInfo.Processes => Processes;

    // When the first run's system was made, for processes' clocks, which only need to go forward.
    private static readonly long Started = global::System.Diagnostics.Stopwatch.GetTimestamp();
}
