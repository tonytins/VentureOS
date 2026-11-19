using System.Diagnostics;
using Venture.OS.Bundles;
using Venture.OS.Desktop;
using Venture.OS.Interface;
using Venture.OS.Scripting;
using Venture.OS.Shell;
using Venture.Roundhouse.Drawing;
using Venture.Roundhouse.Logging;

namespace Venture.Roundhouse.Simulator;

/// <summary>
/// One run of VentureOS inside Roundhouse: its desktop, set up as the kernel's <c>desktop</c> command sets it up,
/// over a fresh copy of its files, with every script's paths kept within that copy. The window that shows it hands it
/// each frame's input and draws what it paints; it doesn't need a window to run, so it's tested without one.
/// </summary>
/// <remarks>
/// <para>The desktop's look lives in <see cref="DesktopAppearance"/>, which the designer's canvas uses too, so a run
/// keeps its own and puts it in place only while it works, putting the designer's back after: Settings in a run
/// doesn't recolour the designer, and the designer's Dark switch doesn't recolour the run.</para>
/// <para>Unlike the kernel, a run has no C# commands, such as <c>cd</c>; Terminal has VentureOS's Lua commands.</para>
/// </remarks>
public sealed class SimulatedRun : IDisposable, IShellHost
{
    /// <summary>The app that is the desktop, as in the kernel.</summary>
    public const string FallbackApp = "Files";

    /// <summary>The dock, which runs as VentureOS runs it.</summary>
    public const string DockApp = "Dock";

    private static readonly TimeSpan AppStartupLimit = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan UiEventLimit = TimeSpan.FromSeconds(5);

    /// <summary>
    /// How long one frame's work may take before the Lua running in it stops, in place of the kernel's Ctrl+C: all of
    /// a run's work happens in frames, on the window's thread, so without it a command stuck in a loop would hang
    /// Roundhouse.
    /// </summary>
    public static readonly TimeSpan FrameLimit = TimeSpan.FromSeconds(10);

    private readonly long started = Stopwatch.GetTimestamp();
    private readonly SkiaPainter painter;
    private readonly HeldOutput heldOutput = new();

    // What apps print: held for the desktop's message windows, and logged, under VentureOS.
    private readonly LoggedOutput appOutput;
    private readonly WindowManager windows = new(SimulatorWindow.ScreenWidth, SimulatorWindow.ScreenHeight);
    private Look look;

    // When the current frame's work began, for the frame limit.
    private long frameStarted = Stopwatch.GetTimestamp();

    private SimulatedRun(SimulatedDisk disk, SkiaFonts fonts)
    {
        appOutput = new LoggedOutput(heldOutput, RoundhouseLog.For("VentureOS"));
        Disk = disk;
        System = new SimulatedSystem(disk);
        painter = new SkiaPainter(SimulatorWindow.ScreenWidth, SimulatorWindow.ScreenHeight, fonts);
        look = new Look(
            DesktopLook.Standard,
            disk.HostPath("/mnt/user/settings/appearance.xml"),
            disk.HostPath("/mnt/system/wallpapers"),
            () => System.WallClock?.Now?.TimeOfDay);

        Func<string, DesktopImage?> loadImage = path => SkiaImages.Load(path, Messages.Add);
        CommandRegistry commands = new();
        ScriptCommands scripts = new(disk.HostPath("/mnt/system/commands"), System, FrameStop, disk.HostPath("/mnt/system/lib"));
        RuntimeCommands runtime = new(commands.IsBuiltIn, scripts, FrameStop);
        commands.AddSource(runtime);
        commands.AddSource(scripts);
        CommandShell shell = new(new ShellContext(appOutput, this, commands) { CurrentDirectory = SimulatedDisk.UserFolder, HasConsole = false });

        AppWindows appWindows = new(windows, appOutput, () => ScriptStop.After(UiEventLimit))
        {
            LoadImage = loadImage,
            SystemFolder = SimulatedDisk.SystemFolder,
            Root = System.Root,
        };
        Apps = new AppManager(
            disk.HostPath("/mnt/system/apps"),
            runtime,
            System,
            disk.HostPath("/mnt/system/lib"),
            shell.Execute,
            () => ScriptStop.Any(FrameStop, ScriptStop.After(AppStartupLimit)),
            appWindows,
            disk.HostPath("/mnt/user/apps"),
            (output, directory) => new ShellContext(output, this, commands) { CurrentDirectory = directory, HasConsole = false },
            System.Processes);
        System.Desktop = new ScriptDesktop(appWindows, () => Apps);

        Session = InLook(() =>
        {
            if (DesktopAppearance.Load().Problem is { } problem)
                Messages.Add($"Appearance: {problem}");
            return new DesktopSession(
                SimulatorWindow.ScreenWidth, SimulatorWindow.ScreenHeight, Apps, SimulatedDisk.UserFolder, heldOutput, windows, loadImage)
            {
                AboutDocument = () => LoadDocument(disk.HostPath("/mnt/system/interfaces/about.uix")),
                SystemIcon = disk.HostPath("/mnt/system/icons/VentureOS.png"),
                WallClock = System.WallClock,
                SystemVersion = $"Version {System.Version} (Build {System.BuildNumber})",
                FallbackApp = FallbackApp,
                DockApp = DockApp,
            };
        });
    }

    /// <summary>The run's copy of VentureOS's files.</summary>
    public SimulatedDisk Disk { get; }

    /// <summary>What the run tells its scripts about itself.</summary>
    public SimulatedSystem System { get; }

    /// <summary>The run's apps.</summary>
    public AppManager Apps { get; }

    /// <summary>The desktop.</summary>
    public DesktopSession Session { get; }

    /// <summary>The screen as last drawn, 0xAARRGGBB, row by row.</summary>
    public int[] Pixels => painter.Pixels;

    /// <summary>Whether the desktop has closed, by Return to Shell or a script's <c>shutdown</c>.</summary>
    public bool IsFinished => Session.IsFinished || stopped;

    /// <summary>What went wrong setting up the run, such as a document that couldn't be read, for the window to show.</summary>
    public List<string> Messages { get; } = [];

    private bool stopped;

    /// <summary>
    /// Starts VentureOS from a fresh copy of <paramref name="rootfs"/>, and opens what's being designed: the app
    /// <paramref name="document"/> is in, with <paramref name="edits"/> in place of what's saved, or the document
    /// alone as a window when it isn't in an app.
    /// </summary>
    /// <param name="document">A file of what's being designed, by its path on this computer, or <see langword="null"/>.</param>
    /// <param name="edits">Files' edited text, by their paths on this computer, which run in place of what's saved;
    /// only those in the app, or the document itself outside one, are used.</param>
    /// <exception cref="DirectoryNotFoundException">There are no VentureOS files at <paramref name="rootfs"/>.</exception>
    /// <exception cref="IOException">The files couldn't be copied.</exception>
    public static SimulatedRun Start(
        string rootfs, SkiaFonts fonts, string? document = null, IReadOnlyDictionary<string, string>? edits = null)
    {
        ArgumentNullException.ThrowIfNull(fonts);
        var disk = SimulatedDisk.Create(rootfs);
        SimulatedRun? run = null;
        try
        {
            run = new SimulatedRun(disk, fonts);
            run.InLook(() =>
            {
                run.Session.StartFallbackApp();
                if (document is not null)
                    run.OpenDesigned(rootfs, document, edits ?? new Dictionary<string, string>());
                foreach (var message in run.Messages)
                    RoundhouseLog.For("VentureOS").Warn(message);
                run.Session.Update(new DesktopInput(SimulatorWindow.ScreenWidth / 2, SimulatorWindow.ScreenHeight / 2, false));
                run.Session.Draw(run.painter);
                return true;
            });
            return run;
        }
        catch
        {
            if (run is null)
                disk.Dispose();
            else
                run.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Runs a frame: hands the desktop this frame's input, and draws it when that changed anything.
    /// </summary>
    /// <param name="pointerX">The pointer, across the screen.</param>
    /// <param name="pointerY">The pointer, down the screen.</param>
    /// <returns>Whether the screen was drawn, so the window shows <see cref="Pixels"/> again.</returns>
    public bool Step(int pointerX, int pointerY, bool pointerDown, int scroll, KeyPress[] keys)
    {
        if (IsFinished)
            return false;

        return InLook(() =>
        {
            DesktopInput input = new(pointerX, pointerY, pointerDown, scroll, keys, Stopwatch.GetElapsedTime(started));
            if (!Session.Update(input))
                return false;

            Session.Draw(painter);
            return true;
        });
    }

    public void Dispose()
    {
        stopped = true;
        Session.Leave();
        InLook(() =>
        {
            foreach (var app in Apps.Running.ToList())
                Apps.CloseApp(app.Bundle.Identifier, appOutput);
            return true;
        });
        painter.Dispose();
        Disk.Dispose();
    }

    /// <summary>Stops Lua that has kept one frame busy for longer than <see cref="FrameLimit"/>.</summary>
    private string? FrameStop() =>
        Stopwatch.GetElapsedTime(frameStarted) >= FrameLimit ? $"Stopped after {FrameLimit.TotalSeconds:0} seconds." : null;

    void IShellHost.Halt() => stopped = true;

    void IShellHost.Reboot() => stopped = true;

    void IShellHost.Shutdown() => stopped = true;

    /// <summary>
    /// Puts the run's copy in place of the document's app, with its files' edits in place of what's saved, and opens
    /// it; a document outside any app opens as a window of its own, as <c>desktop file.uix</c> shows one.
    /// </summary>
    private void OpenDesigned(string rootfs, string document, IReadOnlyDictionary<string, string> edits)
    {
        var bundle = BundleOf(document);
        if (bundle is null)
        {
            if (LoadDocument(document, edits.GetValueOrDefault(Path.GetFullPath(document))) is { } script)
                Session.Windows.Open(new DesktopWindow(Path.GetFileName(document), new UIView(script)));
            return;
        }

        // A system app replaces the system's copy; any other app joins the user's.
        var system = Path.GetFullPath(Path.Combine(rootfs, "system", "apps"));
        var folder = Path.GetFullPath(bundle).StartsWith(system + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? "/mnt/system/apps"
            : "/mnt/user/apps";
        var copy = Disk.HostPath($"{folder}/{Path.GetFileName(bundle)}");
        if (Directory.Exists(copy))
            Directory.Delete(copy, recursive: true);
        CopyFolder(bundle, copy);
        foreach (var (file, text) in edits)
        {
            var relative = Path.GetRelativePath(bundle, Path.GetFullPath(file));
            if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
                continue;

            var target = Path.Combine(copy, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllText(target, text);
        }

        Apps.OpenApp($"{folder}/{Path.GetFileName(bundle)}", SimulatedDisk.UserFolder, appOutput, explainNeeds: false);
    }

    /// <summary>The <c>.app</c> bundle a document is inside, or <see langword="null"/>.</summary>
    public static string? BundleOf(string document)
    {
        ArgumentNullException.ThrowIfNull(document);
        for (var folder = Path.GetDirectoryName(Path.GetFullPath(document)); !string.IsNullOrEmpty(folder); folder = Path.GetDirectoryName(folder))
        {
            if (folder.EndsWith(Bundle.Extension(BundleKind.App), StringComparison.OrdinalIgnoreCase))
                return folder;
        }

        return null;
    }

    private UIScript? LoadDocument(string path, string? text = null)
    {
        try
        {
            return text is null ? UIScript.Load(path) : UIScript.Parse(text);
        }
        catch (Exception exception) when (exception is UIScriptException or IOException or UnauthorizedAccessException)
        {
            Messages.Add($"{Path.GetFileName(path)}: {exception.Message}");
            return null;
        }
    }

    private static void CopyFolder(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(to, Path.GetRelativePath(from, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    /// <summary>
    /// Does <paramref name="work"/> as part of the run, with its look in place, as a frame does: what the host does on
    /// the run's behalf, such as opening an app in it, goes through here, so it changes the run's look, not the
    /// designer's.
    /// </summary>
    public T Within<T>(Func<T> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        return InLook(work);
    }

    /// <summary>
    /// Does <paramref name="work"/> with the run's look in place, then keeps whatever the run changed it to and puts
    /// the designer's back.
    /// </summary>
    private T InLook<T>(Func<T> work)
    {
        frameStarted = Stopwatch.GetTimestamp();
        var designer = Look.Take();
        look.Put();
        try
        {
            return work();
        }
        finally
        {
            look = Look.Take();
            designer.Put();
        }
    }

    /// <summary>Everything <see cref="DesktopAppearance"/> holds for the whole process.</summary>
    private readonly record struct Look(DesktopLook Current, string? SettingsPath, string? WallpapersFolder, Func<TimeSpan?> TimeOfDay)
    {
        public static Look Take() =>
            new(DesktopAppearance.Current, DesktopAppearance.SettingsPath, DesktopAppearance.WallpapersFolder, DesktopAppearance.TimeOfDay);

        public void Put()
        {
            DesktopAppearance.SettingsPath = SettingsPath;
            DesktopAppearance.WallpapersFolder = WallpapersFolder;
            DesktopAppearance.TimeOfDay = TimeOfDay;
            DesktopAppearance.Use(Current);
        }
    }
}
