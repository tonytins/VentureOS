using Venture.OS.Shell;

namespace Venture.OS.Tests.Shell;

/// <summary>
/// Counts power requests instead of stopping anything.
/// </summary>
internal sealed class RecordingHost : IShellHost
{
    public int HaltCount { get; private set; }

    public int RebootCount { get; private set; }

    public int ShutdownCount { get; private set; }

    public void Halt() => HaltCount++;

    public void Reboot() => RebootCount++;

    public void Shutdown() => ShutdownCount++;
}
