using System.Diagnostics;
using System.Text;
using Venture.OS.Scripting;
using Venture.OS.Time;

namespace Venture.Roundhouse.Simulator;

/// <summary>
/// What a simulated VentureOS tells its scripts about itself: the same name, version, and build number the kernel
/// built from this commit would report, the computer's clock, and how long the run has lasted. There's no Cosmos,
/// network, or measured memory to report, so those are left out, as the kernel leaves out what it can't read.
/// </summary>
public sealed class SimulatedSystem(SimulatedDisk disk) : ISystemInfo
{
    private readonly long started = Stopwatch.GetTimestamp();

    public string Name => "VentureOS";

    public string Version => $"{ThisAssembly.Git.SemVer.Major}.{ThisAssembly.Git.SemVer.Minor}.{ThisAssembly.Git.SemVer.Patch}";

    /// <summary>The kernel's build number for this commit: its hash's first four characters, little-endian, modulo a million.</summary>
    public uint BuildNumber
    {
        get
        {
            var commit = Encoding.UTF8.GetBytes(Commit);
            return commit.Length < sizeof(uint) ? 0 : BitConverter.ToUInt32(commit, 0) % 1_000_000;
        }
    }

    public string Commit => ThisAssembly.Git.Commit;

    /// <summary>No Cosmos runs underneath; Roundhouse says so rather than claiming a version.</summary>
    public string CosmosVersion => "none (simulated in Roundhouse)";

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

    /// <summary>The desktop's look, which Settings changes, as the kernel's; a run keeps its own (see <see cref="SimulatedRun"/>).</summary>
    public IAppearance? Appearance { get; } = new Venture.OS.Desktop.ScriptAppearance();

    public INetworkTools? NetworkTools => null;

    /// <summary>The computer's clock, in UTC, kept nowhere: the run's settings start fresh each time.</summary>
    public WallClock? WallClock { get; } = new(() => DateTime.UtcNow);

    /// <summary>The run's alerts, Quick Look, and apps, once its desktop is set up.</summary>
    public IScriptDesktop? Desktop { get; set; }

    /// <summary>The run's copy of VentureOS's files, which scripts' paths are kept within.</summary>
    public FileRoot? Root { get; } = new(disk.Root);
}
