using Venture.Airo.Projects;
using Venture.Airo.Simulation;
using Venture.OS.Bundles;
using Venture.OS.Desktop;
using Venture.OS.Scripting;
using Venture.OS.Shell;

namespace Venture.Airo.Testing;

/// <summary>
/// One test's VentureOS: a fresh copy of its files, with the project's app built into it and opened, as Roundhouse's
/// Run opens one, but with no desktop around it and nothing drawn. A test drives the app's front window as a person
/// would, through <see cref="TestLibrary"/>, and nothing it changes outlasts it.
/// </summary>
/// <remarks>
/// <para>The desktop's look lives in <see cref="DesktopAppearance"/>, for the whole process, so a test points it at
/// its own copy's <c>appearance.xml</c> while it runs and puts back what was there when it ends. Tests run one at a
/// time.</para>
/// <para>An app the system ships replaces the copy's own, in <c>/mnt/system/apps</c>; any other joins the user's.</para>
/// </remarks>
public sealed class AppUnderTest : IDisposable, IShellHost
{
    /// <summary>The screen the app's windows are placed on, the size of the kernel's.</summary>
    public const int ScreenWidth = 1024;

    /// <summary>The screen's height.</summary>
    public const int ScreenHeight = 768;

    private static readonly TimeSpan StartupLimit = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan EventLimit = TimeSpan.FromSeconds(5);

    private readonly WindowManager windows = new(ScreenWidth, ScreenHeight);
    private readonly Printed printed = new();
    private readonly AppManager apps;
    private readonly (DesktopLook Look, string? Settings, string? Wallpapers, Func<TimeSpan?> TimeOfDay) before;
    private readonly string appName;

    private AppUnderTest(SimulatedDisk disk, SimulatedSystem system, string appName, string folder)
    {
        Disk = disk;
        this.appName = appName;
        before = (DesktopAppearance.Current, DesktopAppearance.SettingsPath, DesktopAppearance.WallpapersFolder, DesktopAppearance.TimeOfDay);
        DesktopAppearance.SettingsPath = disk.HostPath("/mnt/user/settings/appearance.xml");
        DesktopAppearance.WallpapersFolder = disk.HostPath("/mnt/system/wallpapers");
        DesktopAppearance.TimeOfDay = () => system.WallClock?.Now?.TimeOfDay;
        DesktopAppearance.Use(DesktopLook.Standard);
        if (DesktopAppearance.Load().Problem is { } problem)
            printed.WriteLine($"Appearance: {problem}");

        CommandRegistry commands = new();
        ScriptCommands scripts = new(disk.HostPath("/mnt/system/commands"), system, null, disk.HostPath("/mnt/system/lib"));
        RuntimeCommands runtime = new(commands.IsBuiltIn, scripts);
        commands.AddSource(runtime);
        commands.AddSource(scripts);
        AppWindows appWindows = new(windows, printed, () => ScriptStop.After(EventLimit))
        {
            // Pictures are checked for, not drawn: a test reads what a window says, not what it looks like.
            LoadImage = path => File.Exists(path) ? new DesktopImage(1, 1, [unchecked((int)0xFF808080)]) : null,
            SystemFolder = SimulatedDisk.SystemFolder,
            Root = system.Root,
        };
        apps = new AppManager(
            disk.HostPath("/mnt/system/apps"),
            runtime,
            system,
            disk.HostPath("/mnt/system/lib"),
            startupStop: () => ScriptStop.After(StartupLimit),
            windows: appWindows,
            userAppsDirectory: disk.HostPath("/mnt/user/apps"),
            openShell: (output, directory) => new ShellContext(output, this, commands) { CurrentDirectory = directory, HasConsole = false },
            supervisor: system.Processes);
        system.Desktop = new ScriptDesktop(appWindows, () => apps);
        Opened = apps.OpenApp($"{folder}/{appName}{Bundle.Extension(BundleKind.App)}", SimulatedDisk.UserFolder, printed, explainNeeds: false);
    }

    /// <summary>The test's copy of VentureOS's files.</summary>
    public SimulatedDisk Disk { get; }

    /// <summary>Whether the app opened; when it didn't, <see cref="Printed"/> says why.</summary>
    public bool Opened { get; }

    /// <summary>Everything the app and VentureOS printed, a line each, errors included.</summary>
    public IReadOnlyList<string> PrintedLines => printed.Lines;

    /// <summary>The window in front, which a person would be using, or <see langword="null"/> when none is open.</summary>
    public DesktopWindow? FrontWindow => windows.Focused ?? (windows.Windows.Count > 0 ? windows.Windows[^1] : null);

    /// <summary>Every open window, back to front.</summary>
    public IReadOnlyList<DesktopWindow> Windows => windows.Windows;

    /// <summary>
    /// Starts a fresh VentureOS from <paramref name="rootfs"/> with <paramref name="project"/>'s app built into it, and
    /// opens the app.
    /// </summary>
    /// <exception cref="ProjectException">The project has no app, or VentureOS wouldn't read it.</exception>
    /// <exception cref="IOException">The files couldn't be copied.</exception>
    public static AppUnderTest Start(RoundhouseProject project, string rootfs, SimulatedVersion version)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrEmpty(rootfs);
        ArgumentNullException.ThrowIfNull(version);

        var disk = SimulatedDisk.Create(rootfs);
        try
        {
            var appName = Path.GetFileNameWithoutExtension(project.AppFolderName());
            var folder = Directory.Exists(disk.HostPath($"/mnt/system/apps/{project.AppFolderName()}")) ? "/mnt/system/apps" : "/mnt/user/apps";
            project.Assemble(disk.HostPath(folder), replace: true);
            SimulatedSystem system = new(disk, version.Version, version.Commit, version.Host);
            return new AppUnderTest(disk, system, appName, folder);
        }
        catch
        {
            disk.Dispose();
            throw;
        }
    }

    /// <summary>Hands <paramref name="uiEvent"/> to <paramref name="window"/>'s app, as the desktop does after a click or a key.</summary>
    public void Send(DesktopWindow window, UIEvent uiEvent)
    {
        ArgumentNullException.ThrowIfNull(window);
        window.EventHandler?.Invoke(window, uiEvent);
    }

    /// <summary>Closes the app, puts back the look that was there before, and deletes the copy.</summary>
    public void Dispose()
    {
        apps.CloseApp(appName, new Printed());
        DesktopAppearance.SettingsPath = before.Settings;
        DesktopAppearance.WallpapersFolder = before.Wallpapers;
        DesktopAppearance.TimeOfDay = before.TimeOfDay;
        DesktopAppearance.Use(before.Look);
        Disk.Dispose();
    }

    // A test's VentureOS has nothing to power off; an app that asks is told so, and the test carries on.
    void IShellHost.Halt() => printed.WriteLine(PowerRefused);

    void IShellHost.Reboot() => printed.WriteLine(PowerRefused);

    void IShellHost.Shutdown() => printed.WriteLine(PowerRefused);

    private const string PowerRefused = "This VentureOS is a test's, so it can't halt, reboot, or shut down.";

    private sealed class Printed : IShellOutput
    {
        public List<string> Lines { get; } = [];

        public void WriteLine(string text) => Lines.Add(text);

        public void Clear() => Lines.Clear();
    }
}

/// <summary>What a simulated VentureOS reports as its version, which the program running it shares.</summary>
/// <param name="Version">Such as <c>0.8.102</c>.</param>
/// <param name="Commit">The commit it was built from.</param>
/// <param name="Host">What's running it, such as <c>airo</c>.</param>
public sealed record SimulatedVersion(string Version, string Commit, string Host);
