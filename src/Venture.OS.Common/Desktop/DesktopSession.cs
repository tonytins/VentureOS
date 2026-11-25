using System.Diagnostics;
using Venture.OS.Bundles;
using Venture.OS.Interface;
using Venture.OS.Processes;
using Venture.OS.Time;

namespace Venture.OS.Desktop;

/// <summary>
/// The desktop while it's open: the sky, the menu bar, the dock, the windows, and the pointer, and what each frame's
/// input does to them.
/// </summary>
/// <remarks>
/// <para>The kernel's frame loop reads the input, hands it to <see cref="Update"/>, and draws only when that says
/// something changed, so an idle desktop doesn't redraw.</para>
/// <para>Input goes to the open menu first, then to menu shortcuts, then to the bar, the dock, and the windows. A
/// press belongs to whatever it landed on until the button comes up, so dragging a window over the dock still drags
/// the window.</para>
/// <para>The bar shows the VentureOS menu, then the active app's menu, then the focused window's own menus. The
/// active app is the one whose window was last brought to the front, or that was last opened from the dock.
/// VentureOS ▸ Return to Shell, or Ctrl+Shift+Q, closes the desktop.</para>
/// <para>Apps' windows outlast the desktop, in a <see cref="WindowManager"/> the system keeps, so an app opened from
/// the shell shows its window the next time the desktop does. An app quits when the last window it opened closes.
/// What apps write while the desktop shows appears in a message window.</para>
/// </remarks>
public sealed class DesktopSession
{
    /// <summary>The VentureOS menu's items, by name.</summary>
    public const string AboutItem = "about";

    /// <summary>The About document's visual, and its image, that show the app's icon.</summary>
    public const string AboutIcon = "icon";

    /// <summary>About VentureOS's text area that shows <see cref="Credits"/>.</summary>
    public const string AboutCredits = "creditsArea";

    /// <summary>The item that closes the desktop.</summary>
    public const string ReturnToShellItem = "returnToShell";

    /// <summary>The app menu's item that closes the app.</summary>
    public const string QuitItem = "quit";

    /// <summary>The app menu's item that asks the app for a new window, for apps that said how.</summary>
    public const string NewWindowItem = "newWindow";

    private static readonly long Started = Stopwatch.GetTimestamp();

    // The standard File menu's own item, which closes the window in front, whoever's it is. Its words, and the
    // desktop's others, come from the desktop's table (DesktopText) when the desktop opens.
    private static readonly UIShortcut CloseShortcut = new(Control: true, Shift: false, Alt: false, 'W');
    private readonly UIMenuEntry CloseWindowEntry;

    // The standard Edit menu's own items, for whatever text has focus, in any window.
    private readonly UIMenuEntry UndoEntry;
    private readonly UIMenuEntry RedoEntry;
    private readonly UIMenuEntry CutEntry;
    private readonly UIMenuEntry CopyEntry;
    private readonly UIMenuEntry PasteEntry;
    private static readonly UIMenuEntry EditSeparator = new(null, null, null, null, 0);
    private readonly UIMenuEntry SelectAllEntry;

    // Help's item when no app is in front, so Help is never empty.
    private readonly UIMenuEntry AboutSystemEntry;

    private readonly UIMenu SystemMenuItems;

    /// <summary>The VentureOS menu's icon in the bar, in place of its name.</summary>
    public const int SystemIconSize = 16;

    // The sky or the wallpaper as the screen shows it, worked out on the first draw rather than when the desktop
    // opens, since the wallpaper is only known once the properties are set.
    private int[]? background;

    // Reads a picture file, or gives null having said why; null for a desktop without pictures.
    private readonly Func<string, DesktopImage?>? loadImage;

    // Wallpapers and icons read so far, by path and the size they're drawn at, including those that couldn't be
    // read, so a missing icon isn't looked for on every refresh of the dock.
    private readonly Dictionary<(string Path, int Width, int Height), DesktopImage?> wallpapers = [];
    private readonly Dictionary<(string Path, int Size, bool Shadow), DesktopIcon?> icons = [];

    // The minute the bar's time shows, with the offset, or -1 for none.
    private long shownMinute = -1;

    // The VentureOS menu, made on first use, with its icon when there's one.
    private BarMenu? systemMenu;
    private readonly string? systemIcon;

    // The hover text last drawn, so its showing or going redraws the desktop.
    private string? drawnTooltip;

    // The palette the sky was worked out in; a different one in use means working it out again.
    private DesktopPalette backgroundPalette;

    // The wallpaper the background was worked out with, by name, and each name's bundle, looked up once.
    private string? backgroundWallpaper;
    private readonly Dictionary<string, Wallpaper?> wallpaperFiles = [];
    private readonly AppManager? apps;
    private readonly string workingDirectory;
    private readonly HeldOutput appOutput;
    private readonly Dictionary<(string App, bool NewWindow, bool Quits), BarMenu> appMenus = [];

    // Whether one of the desktop's own items in a standard menu can act now; made once, since the bar asks each draw.
    private readonly Func<UIMenuEntry, bool> ownEnabled;

    // Help's About item for each app, kept so the bar sees the same item, and tells it apart, from frame to frame.
    private readonly Dictionary<string, UIMenuEntry> aboutEntries = [];

    // The open About windows: VentureOS's under "", and each app's under its name.
    private readonly Dictionary<string, DesktopWindow> aboutWindows = [];

    // Reused every frame: the frame loop runs without pause, so a frame where nothing changes allocates nothing.
    private readonly List<WindowEvent> happened = [];
    private readonly List<KeyPress> forWindows = [];

    // What the bar's menus were built from, so they're rebuilt only when one of these changes: the app shown, its
    // window in front, and whether the app opens new windows. Whether the standard items can act is asked as the bar
    // draws, so selecting text, for one, needs no rebuild.
    private (string? App, DesktopWindow? Window, bool NewWindow, bool Built) menuSource;
    private DesktopInput last;
    private bool drawn;

    // Set by RequestRedraw, and cleared once a frame has said whether to draw.
    private bool redrawRequested;
    private PointerOwner owner;

    // The window in front when focus was last checked, so a window opened between frames, such as About when the
    // desktop opens, still makes its app the active one.
    private DesktopWindow? lastFocused;

    // Whether the fallback app has been handed to the supervisor to keep, which then starts it again whenever it ends.
    private bool fallbackKept;

    // Apps that crashed, and kept apps that failed to start, since the last frame, to report after its messages; and
    // the last reason a kept app couldn't start, so a reason is shown once.
    private readonly List<AppProcess> crashed = [];
    private readonly List<AppProcess> failedToStart = [];
    private readonly Dictionary<string, string> startProblems = new(StringComparer.Ordinal);
    private bool watching;

    // How many apps were open, and which windows were minimized, when the dock last heard, so it hears again only when
    // one of those changes.
    private int dockedRunning = -1;
    private readonly List<DesktopWindow> dockedWindows = [];

    private enum PointerOwner
    {
        None,
        Bar,
        Windows,

        /// <summary>A toast, which a press dismisses; the windows under it never see it.</summary>
        Toast,
    }

    /// <summary>Opens the desktop on a screen of this size.</summary>
    /// <param name="width">The screen's width.</param>
    /// <param name="height">The screen's height.</param>
    /// <param name="apps">The system's apps, for the dock, or <see langword="null"/> for a desktop without one.</param>
    /// <param name="workingDirectory">Where apps opened from the dock run from.</param>
    /// <param name="appOutput">Where apps opened from the dock write; what they write shows in a window.</param>
    /// <param name="windows">The system's windows, which last between visits, or <see langword="null"/> for new ones.</param>
    /// <param name="loadImage">Reads a picture file by its full path, such as an app's icon, or gives
    /// <see langword="null"/> when it can't, having said why. Without it, apps show tiles and the desktop the sky.</param>
    /// <param name="text">The desktop's words, or <see langword="null"/> for the system's (<see cref="AppWindows.Text"/>).</param>
    public DesktopSession(
        int width,
        int height,
        AppManager? apps = null,
        string workingDirectory = "/",
        HeldOutput? appOutput = null,
        WindowManager? windows = null,
        Func<string, DesktopImage?>? loadImage = null,
        DesktopText? text = null)
    {
        ArgumentNullException.ThrowIfNull(workingDirectory);
        ownEnabled = IsOwnEnabled;
        Width = width;
        Height = height;
        this.apps = apps;
        this.workingDirectory = workingDirectory;
        this.appOutput = appOutput ?? new HeldOutput();
        this.loadImage = loadImage;
        Text = text ?? apps?.Windows?.Text ?? new DesktopText(null, this.appOutput.WriteLine);
        CloseWindowEntry = new("closeWindow", Text.Get("closeWindow"), 'C', CloseShortcut, 0);
        UndoEntry = new("undo", Text.Get("undo"), 'U', Control('Z'), 0);
        RedoEntry = new("redo", Text.Get("redo"), 'R', new UIShortcut(Control: true, Shift: true, Alt: false, 'Z'), 0);
        CutEntry = new("cut", Text.Get("cut"), 'T', Control('X'), 0);
        CopyEntry = new("copy", Text.Get("copy"), 'C', Control('C'), 0);
        PasteEntry = new("paste", Text.Get("paste"), 'P', Control('V'), 0);
        SelectAllEntry = new("selectAll", Text.Get("selectAll"), 'A', Control('A'), 0);
        AboutSystemEntry = new("about", Text.Get("aboutSystem"), 'A', null, 0);
        SystemMenuItems = new UIMenu("venture", "VentureOS", null,
        [
            new UIMenuEntry(AboutItem, Text.Get("aboutSystem"), 'A', null, 0),
            new UIMenuEntry(null, null, null, null, 0),
            new UIMenuEntry(ReturnToShellItem, Text.Get("returnToShell"), 'R', new UIShortcut(Control: true, Shift: true, Alt: false, 'Q'), 0),
        ], 0);
        backgroundPalette = DesktopTheme.Palette;
        last = new DesktopInput(width / 2, height / 2, false);
        Windows = windows ?? new WindowManager(width, height);
        Windows.SetScreenSize(width, height);
        Windows.TopInset = MenuBar.Height + BarGap;
        ActiveApp = Windows.Focused?.App;
        lastFocused = Windows.Focused;
        apps?.Windows?.MarkDesktopChanged();
        RefreshMenus();
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>The desktop's windows.</summary>
    public WindowManager Windows { get; }

    /// <summary>The menu bar along the top.</summary>
    public MenuBar MenuBar { get; } = new();

    /// <summary>
    /// The desktop's own words, such as its menu items: the system's, shared with the apps' windows, or, for a desktop
    /// without the system's apps, a table-less one whose every word shows as its key.
    /// </summary>
    public DesktopText Text { get; }

    /// <summary>
    /// Reads every app's About window's document, or gives <see langword="null"/> when it can't, having said why. It
    /// has a <c>name</c>, a <c>version</c>, a <c>blurb</c>, an <c>icon</c> visual, and an <c>ok</c> button. About
    /// VentureOS uses it too when <see cref="AboutSystemDocument"/> gives nothing.
    /// </summary>
    public Func<UIScript?>? AboutDocument { get; init; }

    /// <summary>
    /// Reads About VentureOS's own document, the system's window rather than an app's, or gives
    /// <see langword="null"/> when it can't, having said why; About VentureOS then opens
    /// <see cref="AboutDocument"/>'s, plainer but working. It fills the same controls, and <see cref="AboutCredits"/>
    /// when it has one.
    /// </summary>
    public Func<UIScript?>? AboutSystemDocument { get; init; }

    /// <summary>
    /// Reads the credits About VentureOS shows, or gives <see langword="null"/> when it can't, having said why; the
    /// credits area is then left empty.
    /// </summary>
    public Func<string?>? Credits { get; init; }

    /// <summary>
    /// The text Cut and Copy last took, which Paste puts in, shared by every window and app; <see langword="null"/>
    /// until something is copied. It lasts while the desktop is open.
    /// </summary>
    public string? Clipboard { get; private set; }


    /// <summary>
    /// The full path of VentureOS's own icon, which the menu bar shows in place of the VentureOS menu's name, as
    /// Tiger shows the Apple, and About VentureOS shows; <see langword="null"/>, or one that can't be read, keeps
    /// the name.
    /// </summary>
    public string? SystemIcon
    {
        get => systemIcon;
        init
        {
            // The bar was built when the desktop opened; build it again with the icon.
            systemIcon = value;
            systemMenu = null;
            menuSource = default;
        }
    }

    /// <summary>
    /// The clock, whose time shows on the right of the menu bar, as Tiger's did; <see langword="null"/>, or a hardware
    /// clock that isn't set, shows none.
    /// </summary>
    public WallClock? WallClock { get; init; }

    /// <summary>VentureOS's version as About VentureOS shows it, such as <c>Version 0.4.101 (Build 1234)</c>.</summary>
    public string? SystemVersion { get; init; }

    /// <summary>The app whose menu the bar shows, by name, or <see langword="null"/>.</summary>
    public string? ActiveApp { get; private set; }

    /// <summary>
    /// The app that is the desktop, as Finder is on a Mac, by name, such as <c>Files</c>. The desktop starts it,
    /// starts it again if it stops, never quits it, and shows its menu when no other app is in front. It's first in
    /// the dock. <see langword="null"/> for none.
    /// </summary>
    public string? FallbackApp { get; init; }

    /// <summary>
    /// The app whose top panel is the menu bar, by name, such as <c>Files</c>: the bar shows, and takes the pointer,
    /// only while that panel is open, where it is, and the panel keeps windows below it. With <see langword="null"/>,
    /// the desktop shows the bar itself, always, and keeps room for it.
    /// </summary>
    public string? MenuBarApp { get; init; }

    /// <summary>Whether the menu bar shows: always without a <see cref="MenuBarApp"/>, else while its top panel is open.</summary>
    public bool BarShows => MenuBarApp is null || Windows.TopPanel is not null;

    /// <summary>The room below the bar that windows keep clear of, so a window's title bar doesn't touch it.</summary>
    public const int BarGap = 4;

    /// <summary>
    /// The app that's the dock, by name, such as <c>Dock</c>, which the desktop starts and keeps running as it keeps
    /// the fallback app; <see langword="null"/> for none.
    /// </summary>
    public string? DockApp { get; init; }

    /// <summary>The time, for waiting between attempts to start the fallback app.</summary>
    public Func<TimeSpan> Clock { get; init; } = () => Stopwatch.GetElapsedTime(Started);

    /// <summary>Whether the user has left the desktop.</summary>
    public bool IsFinished { get; private set; }

    /// <summary>The pointer's position, kept on the screen.</summary>
    public (int X, int Y) PointerPosition => (last.PointerX, last.PointerY);

    /// <summary>What happened to windows in the last update, including menu items their menus chose.</summary>
    public IReadOnlyList<WindowEvent> LastEvents { get; private set; } = [];

    /// <summary>
    /// Opens About VentureOS, or About an app when <paramref name="app"/> names one, or brings it to the front if
    /// it's open. An app's About shows its name, its version, and its description from <c>Info.xml</c>, leaving out
    /// what it doesn't say, and belongs to the app, so it goes when the app quits.
    /// </summary>
    public void ShowAbout(string? app = null)
    {
        var key = app ?? "";
        if (!aboutWindows.TryGetValue(key, out var window) || !Windows.Windows.Contains(window))
        {
            if (MakeAbout(app) is not { } made)
                return;
            window = made;
            aboutWindows[key] = window;
        }

        Windows.Open(window);
    }

    /// <summary>
    /// The wallpaper, or the sky without one. A wallpaper with clear parts shows the sky through them, since the
    /// background is copied to the screen without blending.
    /// </summary>
    private int[] RenderBackground()
    {
        var wallpaper = WallpaperPicture();
        if (wallpaper is { IsOpaque: true })
            return wallpaper.Pixels;

        var sky = DesktopBackground.Render(Width, Height, backgroundPalette);
        wallpaper?.DrawOnto(sky, Width, 0, 0);
        return sky;
    }

    /// <summary>
    /// The wallpaper covering the screen, read and scaled the first time and kept after, or <see langword="null"/>
    /// when there's none or it couldn't be read.
    /// </summary>
    /// <remarks>
    /// The picture is the look's wallpaper from <see cref="DesktopAppearance.WallpapersFolder"/>. The sky, a wallpaper that isn't there, or a picture that can't be
    /// read shows the sky; a missing wallpaper is said once in a message window.
    /// </remarks>
    private DesktopImage? WallpaperPicture()
    {
        var name = DesktopAppearance.Current.Wallpaper;
        if (name == DesktopLook.Sky || DesktopAppearance.WallpapersFolder is not { } folder || loadImage is null)
            return null;

        if (!wallpaperFiles.TryGetValue(name, out var wallpaper))
        {
            List<string> problems = [];
            wallpaper = WallpaperCatalog.Find(folder, name, problems);
            wallpaperFiles[name] = wallpaper;
            foreach (var problem in problems)
                appOutput.WriteLine($"Wallpaper: {problem}; showing the sky.");
        }

        if (wallpaper?.Path is not { } path)
            return null;
        if (wallpapers.TryGetValue((path, Width, Height), out var known))
            return known;

        var picture = loadImage(path)?.Covering(Width, Height);
        wallpapers[(path, Width, Height)] = picture;
        return picture;
    }

    /// <summary>
    /// An app's icon at a size, read and made the first time and kept after, or <see langword="null"/> when it has
    /// none or it couldn't be read.
    /// </summary>
    /// <summary>Posts the system's own notification about an app, titled with its name and showing its icon.</summary>
    private void Post(string title, string text, string? icon)
    {
        // The system's words are never empty; were they, there'd be nothing to say, and nothing is posted.
        _ = Windows.Notifications.TryPost(title, text, null, icon, show: true, out _, out _);
    }

    /// <summary>The toasts showing in the top-right corner, from <see cref="WindowManager.Notifications"/>.</summary>
    public Toasts Toasts => toasts ??= new Toasts(Windows.Notifications);

    private Toasts? toasts;

    // A toast's icon: the app's, or VentureOS's for the system's own, at the toast's size.
    private DesktopImage? ToastIcon(string? path) => Icon(path ?? systemIcon, Toasts.IconSize)?.Image;

    private DesktopIcon? Icon(string? path, int size, bool shadow = true)
    {
        if (path is null || loadImage is null)
            return null;
        if (icons.TryGetValue((path, size, shadow), out var known))
            return known;

        var icon = loadImage(path) is { } picture ? DesktopIcon.From(picture, size, shadow) : null;
        icons[(path, size, shadow)] = icon;
        return icon;
    }

    private BarMenu SystemMenu =>
        systemMenu ??= new BarMenu(SystemMenuItems, MenuGroup.System, Icon: Icon(systemIcon, SystemIconSize, shadow: false)?.Image);

    private DesktopWindow? MakeAbout(string? app)
    {
        var script = app is null ? AboutSystemDocument?.Invoke() ?? AboutDocument?.Invoke() : AboutDocument?.Invoke();
        if (script is null)
            return null;

        UIView view = new(script);
        if (app is null)
        {
            view.SetText("version", SystemVersion ?? "");
            ShowAboutIcon(view, systemIcon);
            if (script.Control(AboutCredits) is not null && Credits?.Invoke() is { } credits)
                view.SetText(AboutCredits, credits);
        }
        else
        {
            Bundle? bundle = null;
            foreach (var running in apps?.Running ?? [])
            {
                if (running.Bundle.Name == app)
                    bundle = running.Bundle;
            }

            view.SetText("name", app);
            ShowAboutIcon(view, bundle?.Icon);
            view.SetText("version", bundle?.Version is { } version ? Text.Get("version", version) : "");
            if (bundle?.Description is { } description)
                view.SetText("blurb", description);
            else
                view.SetHidden(true, "blurb");
        }

        return new DesktopWindow(app is null ? Text.Get("aboutSystem") : Text.Get("aboutApp", app), view)
        {
            App = app,
            EventHandler = (about, uiEvent) =>
            {
                if (uiEvent is { Kind: UIEventKind.Pressed, Control: "ok" })
                    about.RequestClose();
            },
        };
    }

    /// <summary>
    /// Puts an app's icon in About's <c>icon</c> visual, at the visual's size, or hides the visual when the app has
    /// none.
    /// </summary>
    private void ShowAboutIcon(UIView view, string? iconPath)
    {
        if (view.Bounds(AboutIcon) is not { } bounds)
            return;

        // The visual is square, so the icon is made at its size and the view needn't scale it again.
        var icon = Icon(iconPath, (int)Math.Min(bounds.Width, bounds.Height))?.Image;
        view.SetImage(AboutIcon, icon);
        view.SetHidden(icon is null, AboutIcon);
    }

    /// <summary>
    /// Starts the fallback app now rather than on the first frame, so windows opened after it, such as previews,
    /// land in front of its window.
    /// </summary>
    public void StartFallbackApp() => KeepFallbackRunning();

    /// <summary>
    /// Stops listening to the apps' processes, as the desktop closes. The apps keep running, and the supervisor keeps
    /// the fallback app, so the next desktop picks them up.
    /// </summary>
    public void Leave()
    {
        if (!watching || apps is null)
            return;

        apps.Supervisor.Started -= OnProcessStarted;
        apps.Supervisor.Ended -= OnProcessEnded;
        watching = false;
    }

    /// <summary>
    /// Asks for the next frame to be drawn, for a change made from outside a frame, such as vdb opening an alert or
    /// an app between frames.
    /// </summary>
    public void RequestRedraw() => redrawRequested = true;

    /// <summary>Takes a frame's input.</summary>
    /// <returns>Whether the desktop needs drawing again.</returns>
    public bool Update(DesktopInput input)
    {
        var (x, y) = (Math.Clamp(input.PointerX, 0, Width - 1), Math.Clamp(input.PointerY, 0, Height - 1));
        var pressed = input.PointerDown && !last.PointerDown;
        var released = !input.PointerDown && last.PointerDown;
        happened.Clear();
        forWindows.Clear();

        // Apps and windows may have changed since the last frame, such as the fallback app starting.
        RefreshMenus();

        // With an app holding the bar in its top panel, the panel keeps windows below it; otherwise the desktop does.
        Windows.TopInset = MenuBarApp is null ? MenuBar.Height + BarGap : 0;
        Windows.DockMode = DesktopAppearance.Current.Dock;
        Windows.ScrollBars = DesktopAppearance.Current.ScrollBars;

        // A menu left open when the bar's app stopped goes with the bar.
        if (!BarShows && MenuBar.IsOpen)
            MenuBar.Close();

        // Keys: the open menu takes them all; otherwise shortcuts and menu hotkeys come before the windows.
        foreach (var key in input.KeysPressed)
        {
            if (MenuBar.IsOpen)
            {
                if (MenuBar.UpdateKey(key) is { } chosen)
                    Choose(chosen, happened);
            }
            else if (MenuBar.FindShortcut(key) is { } shortcut)
            {
                Choose(shortcut, happened);
            }
            else if (!(key is { Key: DesktopKey.Character, Alt: true, Control: false } && BarShows && MenuBar.OpenByHotkey(key.Character)))
            {
                forWindows.Add(key);
            }
        }

        if (IsFinished)
            return false;

        // The pointer: a press belongs to what it lands on until it's released.
        if (pressed)
        {
            owner = MenuBar.IsOpen || (BarShows && MenuBar.Contains(x, y)) ? PointerOwner.Bar
                : Toasts.Press(x, y, Clock()) ? PointerOwner.Toast
                : PointerOwner.Windows;
        }

        if (owner == PointerOwner.Bar || MenuBar.IsOpen)
        {
            var mine = owner == PointerOwner.Bar;
            if (MenuBar.UpdatePointer(x, y, pressed && mine, released && mine) is { } chosen)
                Choose(chosen, happened);
        }

        var overWindows = !MenuBar.IsOpen && !(BarShows && MenuBar.Contains(x, y)) && owner != PointerOwner.Toast;
        Windows.ShowsHover = overWindows;
        DesktopInput windowInput = new(
            x,
            y,
            input.PointerDown && owner == PointerOwner.Windows,
            overWindows ? input.Scroll : 0,
            forWindows.Count == 0 ? [] : [.. forWindows],
            input.Time);
        happened.AddRange(Windows.Update(windowInput));

        // Timers run once a frame; one may change a window, or close it.
        var ticked = apps?.Windows?.Tick(Clock()) ?? false;
        if (ticked)
            happened.AddRange(Windows.CloseRequested());

        QuitAppsWithoutWindows(happened);
        var restarted = KeepFallbackRunning();
        restarted |= apps?.Supervisor.Tick() ?? false;

        // Before the apps' messages, since it shows what the app said while it failed.
        ReportStartFailures();

        // An app the dock opened or brought forward is the active one, with or without a window in front.
        if (apps?.Windows?.TakeActivatedApp() is { } activated)
        {
            ActiveApp = activated;
            lastFocused = Windows.Focused;
        }
        if ((apps is not null && apps.Running.Count != dockedRunning) || MinimizedChanged())
            TellDock();
        ShowMessages(ActiveApp ?? "VentureOS", ActiveApp);

        // After the apps' own messages, so the crash that explains them is in front of them.
        ReportCrashes();

        if (Windows.Focused != lastFocused)
        {
            lastFocused = Windows.Focused;
            if (lastFocused is not null)
                ActiveApp = lastFocused.App;
        }

        LastEvents = happened.Count == 0 ? [] : [.. happened];
        RefreshMenus();

        // The clock is brought up to date every frame, even when something else already calls for a redraw.
        var clockChanged = UpdateClock();
        var toastsChanged = Toasts.Update(Clock());
        var changed = !drawn || clockChanged || toastsChanged || input.HasEvents || redrawRequested || Windows.IsSliding || Windows.ScrollBarsChanging
            || !string.Equals(DesktopAppearance.Current.Wallpaper, backgroundWallpaper, StringComparison.Ordinal) || happened.Count > 0 || restarted || ticked
            || !ReferenceEquals(DesktopTheme.Palette, backgroundPalette)
            || !ReferenceEquals(Windows.Tooltip, drawnTooltip)
            || x != last.PointerX || y != last.PointerY || input.PointerDown != last.PointerDown;
        last = new DesktopInput(x, y, input.PointerDown);
        redrawRequested = false;
        if (released)
            owner = PointerOwner.None;
        return changed;
    }

    /// <summary>
    /// Shows the time on the right of the menu bar when the minute changes, or the offset does, so the frame loop
    /// redraws once a minute for it and an idle frame allocates nothing.
    /// </summary>
    /// <returns>Whether the bar's time changed.</returns>
    private bool UpdateClock()
    {
        var now = WallClock?.Now;
        var minute = now is { } time ? time.Ticks / TimeSpan.TicksPerMinute : -1;
        if (minute == shownMinute)
            return false;

        shownMinute = minute;
        MenuBar.Status = now is { } shown ? WallClock.FormatMenuTime(shown) : null;

        // A look on a schedule turns light or dark as the minute it's set for comes; the frame redraws for the
        // palette as it does for any change of look.
        DesktopAppearance.Refresh();
        return true;
    }

    /// <summary>Draws the whole desktop: the sky, the windows, the dock, the menu bar, then the pointer on top.</summary>
    public void Draw(IPainter painter)
    {
        ArgumentNullException.ThrowIfNull(painter);

        // Working the background out allocates, so only on the first draw and when the palette changes.
        var wallpaper = DesktopAppearance.Current.Wallpaper;
        if (background is null || !ReferenceEquals(DesktopTheme.Palette, backgroundPalette)
            || !string.Equals(wallpaper, backgroundWallpaper, StringComparison.Ordinal))
        {
            backgroundPalette = DesktopTheme.Palette;
            backgroundWallpaper = wallpaper;
            background = RenderBackground();
        }

        painter.DrawPixels(background, 0, 0, Width, Height);
        Windows.Draw(painter);

        // Over every window and panel, and under the bar's open menus.
        Toasts.Draw(painter, MenuBar.Height, Clock(), ToastIcon, Text);
        if (BarShows)
            MenuBar.Draw(painter);
        Windows.DrawTooltip(painter);
        drawnTooltip = Windows.Tooltip;
        Pointer.Draw(painter, last.PointerX, last.PointerY);
        drawn = true;
    }

    private void Choose(MenuChoice choice, List<WindowEvent> happened)
    {
        var item = choice.Item.Name!;
        switch (choice.Menu.Group)
        {
            case MenuGroup.System when item == AboutItem:
                ShowAbout();
                break;
            case MenuGroup.System when item == ReturnToShellItem:
                IsFinished = true;
                break;
            case MenuGroup.App when item == QuitItem:
                Quit(choice.Menu.Menu.Title);
                break;
            case MenuGroup.App when item == NewWindowItem:
                apps?.Windows?.NewWindow(choice.Menu.Menu.Title);
                happened.AddRange(Windows.CloseRequested());
                break;
            case MenuGroup.Standard when ReferenceEquals(choice.Item, CloseWindowEntry):
                Windows.Focused?.RequestClose();
                happened.AddRange(Windows.CloseRequested());
                break;
            case MenuGroup.Standard when ReferenceEquals(choice.Item, UndoEntry):
                if (Windows.Focused is { } undoIn)
                    Report(undoIn, undoIn.Undo(), happened);
                break;
            case MenuGroup.Standard when ReferenceEquals(choice.Item, RedoEntry):
                if (Windows.Focused is { } redoIn)
                    Report(redoIn, redoIn.Redo(), happened);
                break;
            case MenuGroup.Standard when ReferenceEquals(choice.Item, CutEntry):
                if (Windows.Focused is { } cutFrom && cutFrom.SelectedText() is { } cut)
                {
                    Clipboard = cut;
                    Report(cutFrom, cutFrom.ReplaceSelectedText(""), happened);
                }

                break;
            case MenuGroup.Standard when ReferenceEquals(choice.Item, CopyEntry):
                if (Windows.Focused?.SelectedText() is { } copied)
                    Clipboard = copied;
                break;
            case MenuGroup.Standard when ReferenceEquals(choice.Item, PasteEntry):
                if (Windows.Focused is { } pasteInto && Clipboard is { } pasted)
                    Report(pasteInto, pasteInto.ReplaceSelectedText(pasted), happened);
                break;
            case MenuGroup.Standard when ReferenceEquals(choice.Item, SelectAllEntry):
                Windows.Focused?.SelectAllText();
                break;
            case MenuGroup.Standard when ReferenceEquals(choice.Item, AboutSystemEntry):
                ShowAbout();
                break;
            case MenuGroup.Standard when choice.Menu.IsOwn(choice.Item):
                // Help's About item, for the app the bar shows.
                ShowAbout(menuSource.App);
                break;
            case MenuGroup.Window or MenuGroup.Standard:
                ReportToWindow(choice.Menu.View, item, happened);
                break;
        }
    }

    // What an edit in a window's content reported, such as a text area's change, goes to the window as typing's would.
    private void Report(DesktopWindow window, IReadOnlyList<UIEvent> events, List<WindowEvent> happened)
    {
        foreach (var uiEvent in events)
        {
            happened.Add(new WindowEvent(window, uiEvent));
            window.EventHandler?.Invoke(window, uiEvent);
        }

        happened.AddRange(Windows.CloseRequested());
    }

    private static UIShortcut Control(char key) => new(Control: true, Shift: false, Alt: false, key);

    // A chosen item reports like a pressed button, by its name, to the window whose menu it's in.
    private void ReportToWindow(UIView? view, string item, List<WindowEvent> happened)
    {
        foreach (var window in Windows.Windows)
        {
            if (window.View != view)
                continue;

            UIEvent chosen = new(UIEventKind.Pressed, item);
            happened.Add(new WindowEvent(window, chosen));
            window.EventHandler?.Invoke(window, chosen);
            break;
        }

        happened.AddRange(Windows.CloseRequested());
    }

    /// <summary>
    /// Closes the desktop's own windows, such as About and messages, when it closes. Apps' windows stay for next time.
    /// </summary>
    public void CloseDesktopWindows()
    {
        foreach (var window in (DesktopWindow[])[.. Windows.Windows])
        {
            if (!window.OpenedByApp)
                Windows.Close(window);
        }
    }

    /// <summary>Quits each app whose last own window closed this frame.</summary>
    private void QuitAppsWithoutWindows(List<WindowEvent> happened)
    {
        foreach (var closed in happened)
        {
            if (!closed.Closed || !closed.Window.OpenedByApp || closed.Window.App is not { } app)
                continue;

            var hasAnother = false;
            foreach (var window in Windows.Windows)
                hasAnother |= window.OpenedByApp && window.App == app;

            // The fallback stays, as Finder does with no windows open.
            if (!hasAnother && app != FallbackApp && IsAppOpen(app))
                Quit(app);
        }
    }

    private bool IsAppOpen(string name)
    {
        if (apps is null)
            return false;

        // By index, since a foreach over the interface would allocate on every frame.
        var running = apps.Running;
        for (var index = 0; index < running.Count; index++)
        {
            if (running[index].Bundle.Name == name)
                return true;
        }

        return false;
    }


    private void Quit(string app)
    {
        if (app == FallbackApp)
            return;

        apps?.CloseApp(app, appOutput, EndingKind.Quit);

        // Its windows go with it.
        foreach (var window in (DesktopWindow[])[.. Windows.Windows])
        {
            if (window.App == app)
                Windows.Close(window);
        }

        ActiveApp = null;
        ShowMessages(app, null);
        TellDock();
    }

    /// <summary>Shows what apps wrote, in a window titled for the app, so nothing they said is lost.</summary>
    private void ShowMessages(string title, string? app)
    {
        if (appOutput.HasLines)
            Windows.Open(DesktopAlert.Create(title, string.Join('\n', appOutput.Take()), Text.Get("ok"), app));
    }

    /// <summary>
    /// Starts the fallback app when it isn't running, waiting longer after each failure in a row, so a broken app
    /// can't keep the desktop busy. A failure is shown once, and again only when the reason changes.
    /// </summary>
    /// <returns>Whether it started.</returns>
    /// <summary>
    /// Hands the fallback app to the supervisor to keep, the first time: from then on the supervisor starts it again
    /// whenever it ends, at once after it's closed, with growing waits while it fails.
    /// </summary>
    /// <returns>Whether it started now.</returns>
    private bool KeepFallbackRunning()
    {
        if (apps is null || fallbackKept)
            return false;

        Watch();
        fallbackKept = true;
        if (apps.Windows is { } host)
            host.FallbackApp = FallbackApp;

        // The fallback app first, so its windows land behind the dock's panel and anything opened after.
        var started = false;
        foreach (var kept in (ReadOnlySpan<string?>)[FallbackApp, DockApp])
        {
            if (kept is null || IsAppOpen(kept))
                continue;
            if (!apps.OpenApp(kept, workingDirectory, appOutput, explainNeeds: true, RestartPolicy.Always))
                continue;

            ShowMessages(kept, kept);
            started = true;
        }

        if (started)
            TellDock();
        return started;
    }

    private void Watch()
    {
        if (watching || apps is null)
            return;

        apps.Supervisor.Started += OnProcessStarted;
        apps.Supervisor.Ended += OnProcessEnded;
        watching = true;
    }

    private void OnProcessStarted(AppProcess process)
    {
        startProblems.Remove(process.Bundle.Identifier);
        TellDock();
    }

    private void OnProcessEnded(AppProcess process)
    {
        TellDock();
        switch (process.Ending)
        {
            case { Kind: EndingKind.Crashed }:
                crashed.Add(process);
                break;

            // Reported after the frame, with what the app said while it failed.
            case { Kind: EndingKind.FailedToStart } when process.Policy == RestartPolicy.Always:
                failedToStart.Add(process);
                break;
        }
    }

    /// <summary>Says which apps crashed, and that a kept one is starting again.</summary>
    private void ReportCrashes()
    {
        if (crashed.Count == 0)
            return;

        // A heads-up, not a question, so a toast: it needs no app, so it shows even for the app that drew toasts.
        foreach (var process in crashed)
        {
            var reason = process.Ending!.Reason;
            Post(process.Name, process.Policy == RestartPolicy.Always ? $"{reason} It's starting again." : reason, process.Bundle.Icon);
        }

        crashed.Clear();
    }

    /// <summary>Says why a kept app couldn't start, and when it'll be tried again, once for each new reason.</summary>
    private void ReportStartFailures()
    {
        if (failedToStart.Count == 0 || apps is null)
            return;

        var problem = string.Join('\n', appOutput.Take());
        foreach (var process in failedToStart)
        {
            var identifier = process.Bundle.Identifier;
            if (startProblems.TryGetValue(identifier, out var shown) && shown == problem)
                continue;

            startProblems[identifier] = problem;
            var wait = (apps.Supervisor.NextStart(identifier) ?? apps.Supervisor.Now) - apps.Supervisor.Now;
            Post(process.Name, $"{process.Name} didn't start. Trying again in {wait.TotalSeconds:0} seconds.\n{problem}".TrimEnd(),
                process.Bundle.Icon);
        }

        failedToStart.Clear();
    }

    /// <summary>Whether the minimized windows differ from those the dock shows.</summary>
    private bool MinimizedChanged()
    {
        var count = 0;
        var windows = Windows.Windows;
        for (var index = 0; index < windows.Count; index++)
        {
            var window = windows[index];
            if (!window.IsMinimized)
                continue;
            if (count >= dockedWindows.Count || dockedWindows[count] != window)
                return true;
            count++;
        }

        return count != dockedWindows.Count;
    }

    /// <summary>
    /// Tells the dock, and any app listening through <c>venture.desktop.onChange</c>, that what it shows changed, and
    /// notes what it was told about, so it's told again only when that changes.
    /// </summary>
    private void TellDock()
    {
        dockedRunning = apps?.Running.Count ?? 0;
        dockedWindows.Clear();
        foreach (var window in Windows.Windows)
        {
            if (window.IsMinimized)
                dockedWindows.Add(window);
        }

        apps?.Windows?.MarkDesktopChanged();
    }

    private void RefreshMenus()
    {
        // With no other app in front, the fallback's menu shows.
        var shown = ActiveApp ?? (FallbackApp is { } fallback && IsAppOpen(fallback) ? fallback : null);
        var front = Windows.Focused;
        var focused = front is not null && front.App == ActiveApp ? front : null;
        var newWindow = shown is not null && (apps?.Windows?.HasNewWindow(shown) ?? false);
        if (menuSource == (shown, focused, newWindow, true))
            return;

        menuSource = (shown, focused, newWindow, true);
        List<BarMenu> menus = [SystemMenu];
        if (shown is not null)
            menus.Add(AppMenu(shown));

        // The standard menus, with the window's menus of the same role merged in, then its others, with Help last.
        // File, Edit, and Help are always there, their items dimmed when they can't act; View only when a window
        // adds to it.
        var view = focused?.View;
        IReadOnlyList<UIMenu> document = view?.Script.Menus ?? [];
        UIMenuEntry[] closing = Takes(document, CloseShortcut) ? [] : [CloseWindowEntry];
        UIMenuEntry[] helping = [shown is not null ? AboutEntry(shown) : AboutSystemEntry];

        AddStandard(menus, document, view, UIMenuRole.File, Text.Get("file"), 'F', closing);
        AddStandard(menus, document, view, UIMenuRole.Edit, Text.Get("edit"), 'E', EditItems(document));
        AddStandard(menus, document, view, UIMenuRole.View, Text.Get("view"), 'V', []);
        foreach (var menu in document)
        {
            if (RoleOf(menu) is null)
                menus.Add(new BarMenu(menu, MenuGroup.Window, view));
        }

        AddStandard(menus, document, view, UIMenuRole.Help, Text.Get("help"), 'H', helping);
        MenuBar.SetMenus(menus);
    }

    /// <summary>
    /// Whether one of the desktop's own items can act now: Close Window with a window in front; Cut and Copy with
    /// text selected; Paste with text focused and something to paste; Select All with text focused.
    /// </summary>
    private bool IsOwnEnabled(UIMenuEntry entry)
    {
        var front = Windows.Focused;
        if (ReferenceEquals(entry, CloseWindowEntry))
            return front is not null;
        if (ReferenceEquals(entry, UndoEntry))
            return front?.CanUndo ?? false;
        if (ReferenceEquals(entry, RedoEntry))
            return front?.CanRedo ?? false;
        if (ReferenceEquals(entry, CutEntry) || ReferenceEquals(entry, CopyEntry))
            return front?.HasSelectedText ?? false;
        if (ReferenceEquals(entry, PasteEntry))
            return Clipboard is not null && (front?.HasEditableText ?? false);
        if (ReferenceEquals(entry, SelectAllEntry))
            return front?.HasEditableText ?? false;
        return true;
    }

    /// <summary>
    /// Adds a standard menu: the document's items of that role, in order, then the desktop's own after a separator.
    /// An empty one is left out, so a menu shows only when there's something in it.
    /// </summary>
    private void AddStandard(
        List<BarMenu> menus, IReadOnlyList<UIMenu> document, UIView? view, UIMenuRole role, string title, char hotkey,
        UIMenuEntry[] own)
    {
        List<UIMenuEntry> entries = [];
        foreach (var menu in document)
        {
            if (RoleOf(menu) == role)
                entries.AddRange(menu.Entries);
        }

        if (entries.Count > 0 && own.Length > 0)
            entries.Add(new UIMenuEntry(null, null, null, null, 0));
        entries.AddRange(own);
        if (entries.Count > 0)
            menus.Add(new BarMenu(new UIMenu(title.ToLowerInvariant(), title, hotkey, entries, 0, role), MenuGroup.Standard, view,
                Own: own, OwnEnabled: ownEnabled));
    }

    /// <summary>
    /// The standard menu a document's menu adds to: its <c>role</c>, or without one, its title when that's exactly a
    /// standard menu's, so menus written before roles merge too.
    /// </summary>
    private static UIMenuRole? RoleOf(UIMenu menu) => menu.Role ?? menu.Title switch
    {
        "File" => UIMenuRole.File,
        "Edit" => UIMenuRole.Edit,
        "View" => UIMenuRole.View,
        "Help" => UIMenuRole.Help,
        _ => null,
    };

    /// <summary>Whether a document has an item of its own with this shortcut, which then takes the standard item's place.</summary>
    private static bool Takes(IReadOnlyList<UIMenu> document, UIShortcut shortcut)
    {
        foreach (var menu in document)
        {
            foreach (var entry in menu.AllEntries())
            {
                if (entry.Shortcut is { } own && own.Control == shortcut.Control && own.Shift == shortcut.Shift
                    && own.Alt == shortcut.Alt && char.ToUpperInvariant(own.Key) == char.ToUpperInvariant(shortcut.Key))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Edit's own items in their groups (Undo and Redo; Cut, Copy, and Paste; Select All), each left out where the
    /// document has its own shortcut, with a separator between groups that have anything left.
    /// </summary>
    private UIMenuEntry[] EditItems(IReadOnlyList<UIMenu> document)
    {
        List<UIMenuEntry> items = [];
        foreach (var group in (UIMenuEntry[][])[[UndoEntry, RedoEntry], [CutEntry, CopyEntry, PasteEntry], [SelectAllEntry]])
        {
            var started = false;
            foreach (var entry in group)
            {
                if (Takes(document, entry.Shortcut!))
                    continue;
                if (!started && items.Count > 0)
                    items.Add(EditSeparator);
                items.Add(entry);
                started = true;
            }
        }

        return [.. items];
    }

    private UIMenuEntry AboutEntry(string app)
    {
        if (!aboutEntries.TryGetValue(app, out var entry))
        {
            entry = new UIMenuEntry("about", Text.Get("aboutApp", app), 'A', null, 0);
            aboutEntries[app] = entry;
        }

        return entry;
    }

    /// <summary>
    /// The app's menu: New Window when the app said how to open one, and Quit unless it's the fallback. Kept per app
    /// and shape, so the bar sees the same menu from frame to frame.
    /// </summary>
    private BarMenu AppMenu(string app)
    {
        var newWindow = apps?.Windows?.HasNewWindow(app) ?? false;
        var quits = app != FallbackApp;
        var key = (app, newWindow, quits);
        if (!appMenus.TryGetValue(key, out var menu))
        {
            List<UIMenuEntry> entries = [];
            if (newWindow)
                entries.Add(new UIMenuEntry(NewWindowItem, Text.Get("newWindow"), 'N', new UIShortcut(Control: true, Shift: false, Alt: false, 'N'), 0));
            if (newWindow && quits)
                entries.Add(new UIMenuEntry(null, null, null, null, 0));
            if (quits)
                entries.Add(new UIMenuEntry(QuitItem, Text.Get("quitApp", app), 'Q', new UIShortcut(Control: true, Shift: false, Alt: false, 'Q'), 0));

            menu = new BarMenu(new UIMenu("app", app, null, entries, 0), MenuGroup.App, Emphasized: true);
            appMenus[key] = menu;
        }

        return menu;
    }
}
