using NLog;
using Venture.OS.Shell;

namespace Venture.Roundhouse.Logging;

/// <summary>
/// Output that goes where it was going and into the log as well, so what VentureOS's apps print during a run can be
/// read after the message windows that showed it are closed.
/// </summary>
public sealed class LoggedOutput(IShellOutput output, Logger log) : IShellOutput
{
    public void WriteLine(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        output.WriteLine(text);
        log.Info(text);
    }

    public void Clear() => output.Clear();
}
