using Venture.OS.Vdb;

namespace Venture.OS.Tests.Vdb;

/// <summary>A vdb host over a temporary folder that stands for <c>/mnt/user</c>, recording what it's asked to do.</summary>
internal sealed class FakeVdbHost : IVdbHost, IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"venture-vdb-{Guid.NewGuid():N}").Replace('\\', '/');

    public FakeVdbHost() => Directory.CreateDirectory(root);

    public string StagingFolder => $"{root}/.vdb";

    public string AppsFolder => $"{root}/apps";

    public HashSet<string> SystemApps { get; } = new(StringComparer.Ordinal);

    public List<string> Closed { get; } = [];

    public List<string> Launched { get; } = [];

    /// <summary>Why launching fails, or <see langword="null"/> for it to work.</summary>
    public string? LaunchProblem { get; set; }

    public bool IsSystemApp(string identifier) => SystemApps.Contains(identifier);

    public void CloseApp(string identifier) => Closed.Add(identifier);

    public string? Launch(string app)
    {
        Launched.Add(app);
        return LaunchProblem;
    }

    /// <summary>A line as it would read on VentureOS, with the temporary folder shown as <c>/mnt/user</c>.</summary>
    public string AsVentureSees(string line) => line.Replace(root, "/mnt/user", StringComparison.Ordinal);

    public void Dispose()
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
}
