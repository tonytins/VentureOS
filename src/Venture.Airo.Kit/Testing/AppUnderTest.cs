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
    private readonly AppWindows appWindows;
    private readonly CommandShell shell;
    private readonly Printed commandOutput = new();
    private TimeSpan clock;
    private readonly (DesktopLook Look, string? Settings, string? Wallpapers, Func<TimeSpan?> TimeOfDay) before;
    private readonly string appName;

    private AppUnderTest(SimulatedDisk disk, SimulatedSystem system, string appName, string folder)
    {
        Disk = disk;
        this.appName = appName;
        before = (DesktopAppearance.Current, DesktopAppearance.SettingsPath, DesktopAppearance.WallpapersFolder, DesktopAppearance.TimeOfDay);
        DesktopAppearance.SettingsPath = disk.HostPath("/mnt/user/settings/appearance.xml");
        DesktopAppearance.WallpapersFolder = disk.HostPath("/mnt/system/wallpaper");
        DesktopAppearance.TimeOfDay = () => system.WallClock?.Now?.TimeOfDay;
        DesktopAppearance.Use(DesktopLook.Standard);
        if (DesktopAppearance.Load().Problem is { } problem)
            printed.WriteLine($"Appearance: {problem}");

        CommandRegistry commands = new();
        ScriptCommands scripts = new(disk.HostPath("/mnt/system/commands"), system, null, disk.HostPath("/mnt/system/lib"));
        RuntimeCommands runtime = new(commands.IsBuiltIn, scripts);
        commands.AddSource(runtime);
        commands.AddSource(scripts);
        appWindows = new(windows, printed, () => ScriptStop.After(EventLimit))
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
        shell = new CommandShell(new ShellContext(commandOutput, this, commands) { CurrentDirectory = SimulatedDisk.UserFolder, HasConsole = false });
        Opened = apps.OpenApp($"{folder}/{appName}{Bundle.Extension(BundleKind.App)}", SimulatedDisk.UserFolder, printed, explainNeeds: false);
    }

    /// <summary>The test's copy of VentureOS's files.</summary>
    public SimulatedDisk Disk { get; }

    /// <summary>Whether the app opened; when it didn't, <see cref="Printed"/> says why.</summary>
    public bool Opened { get; }

    /// <summary>Everything the app and VentureOS printed, a line each, errors included.</summary>
    public IReadOnlyList<string> PrintedLines => printed.Lines;

    /// <summary>
    /// The window in front, which a person would be using: the focused one, or else the newest that isn't a panel, or
    /// else the newest panel; <see langword="null"/> when none is open.
    /// </summary>
    public DesktopWindow? FrontWindow
    {
        get
        {
            if (windows.Focused is { } focused)
                return focused;
            for (var index = windows.Windows.Count - 1; index >= 0; index--)
            {
                if (!windows.Windows[index].IsPanel)
                    return windows.Windows[index];
            }

            return windows.Windows.Count > 0 ? windows.Windows[^1] : null;
        }
    }

    /// <summary>Every open window, back to front.</summary>
    public IReadOnlyList<DesktopWindow> Windows => windows.Windows;

    /// <summary>
    /// Starts a fresh VentureOS from <paramref name="rootfs"/> with <paramref name="project"/>'s app built into it, and
    /// opens the app.
    /// </summary>
    /// <exception cref="ProjectException">The project has no app, or VentureOS wouldn't read it.</exception>
    /// <exception cref="IOException">The files couldn't be copied.</exception>
    /// <param name="edits">Files' text by full path, such as unsaved ones, built into the app in place of what's saved.</param>
    public static AppUnderTest Start(
        RoundhouseProject project, string rootfs, SimulatedVersion version, IReadOnlyDictionary<string, string>? edits = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrEmpty(rootfs);
        ArgumentNullException.ThrowIfNull(version);

        var disk = SimulatedDisk.Create(rootfs);
        try
        {
            var appFolder = project.AppFolderName(edits);
            var appName = Path.GetFileNameWithoutExtension(appFolder);
            var folder = Directory.Exists(disk.HostPath($"/mnt/system/apps/{appFolder}")) ? "/mnt/system/apps" : "/mnt/user/apps";
            project.Assemble(disk.HostPath(folder), edits, replace: true);
            SimulatedSystem system = new(disk, version.Version, version.Commit, version.Host) { Developer = new() };
            return new AppUnderTest(disk, system, appName, folder);
        }
        catch
        {
            disk.Dispose();
            throw;
        }
    }

    /// <summary>Asks the app for a new window, as File ▸ New Window does.</summary>
    /// <returns>Whether the app opens new windows.</returns>
    public bool NewWindow() => appWindows.NewWindow(appName);

    /// <summary>Hands the app a file to open, named as VentureOS names it, as Files does.</summary>
    /// <returns>Whether the app opens files.</returns>
    public bool OpenFile(string venturePath) => appWindows.OpenFile(appName, venturePath);

    /// <summary>Lets <paramref name="time"/> pass for the app's timers, which run as they would have in it.</summary>
    public void Wait(TimeSpan time)
    {
        appWindows.Tick(clock);
        clock += time;
        appWindows.Tick(clock);
    }

    /// <summary>Runs a line in the test's shell, in <c>/mnt/user</c>, as typed at the prompt.</summary>
    /// <returns>What it printed, a line each.</returns>
    public IReadOnlyList<string> Command(string line)
    {
        commandOutput.Lines.Clear();
        try
        {
            shell.Execute(line);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // As the kernel's shell loop reports a command that throws.
            commandOutput.WriteLine($"{line.Split(' ')[0]}: {exception.GetType().Name}: {exception.Message}");
        }

        return [.. commandOutput.Lines];
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
