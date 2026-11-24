using Venture.OS.Bundles;
using Venture.OS.Scripting;

namespace Venture.OS.Desktop;

/// <summary>
/// The desktop for any script, through <see cref="ISystemInfo.Desktop"/>: alerts, Quick Look, and opening apps,
/// through the same windows and apps the desktop has.
/// </summary>
/// <param name="host">The desktop's windows.</param>
/// <param name="apps">The system's apps, for launching, once they're ready.</param>
public sealed class ScriptDesktop(AppWindows host, Func<AppManager?> apps) : IScriptDesktop
{
    /// <inheritdoc/>
    public bool IsShowing => host.Windows.IsShowing;

    /// <inheritdoc/>
    public void Alert(string title, string message, string? app = null) =>
        host.Windows.Open(DesktopAlert.Create(title, message, host.Text.Get("ok"), app));

    /// <inheritdoc/>
    public string? Preview(string path) => host.Preview(path);

    /// <inheritdoc/>
    public string? Launch(string app, string? file, string workingDirectory) =>
        apps() is { } manager
            ? UiLibrary.LaunchApp(host, manager, app, file, workingDirectory)
            : "venture.ui.launch needs the system's apps, which aren't available";
}
