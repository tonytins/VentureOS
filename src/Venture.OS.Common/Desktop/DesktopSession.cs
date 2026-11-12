using System.Diagnostics;
using Venture.OS.Bundles;
using Venture.OS.Interface;

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

    /// <summary>The item that closes the desktop.</summary>
    public const string ReturnToShellItem = "returnToShell";

    /// <summary>The app menu's item that closes the app.</summary>
    public const string QuitItem = "quit";

    /// <summary>The app menu's item that asks the app for a new window, for apps that said how.</summary>
    public const string NewWindowItem = "newWindow";

    // How long to wait before starting the fallback app again after each failure in a row; the last one repeats.
    private static readonly TimeSpan[] RestartDelays =
        [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)];

    private static readonly long Started = Stopwatch.GetTimestamp();

    // The standard File menu's own item, which closes the window in front, whoever's it is.
    private static readonly UIShortcut CloseShortcut = new(Control: true, Shift: false, Alt: false, 'W');
    private static readonly UIMenuEntry CloseWindowEntry = new("closeWindow", "Close Window", 'C', CloseShortcut, 0);

    private static readonly BarMenu SystemMenu = new(
        new UIMenu("venture", "VentureOS", null,
        [
            new UIMenuEntry(AboutItem, "About VentureOS", 'A', null, 0),
            new UIMenuEntry(null, null, null, null, 0),
            new UIMenuEntry(ReturnToShellItem, "Return to Shell", 'R', new UIShortcut(Control: true, Shift: true, Alt: false, 'Q'), 0),
        ], 0),
        MenuGroup.System);

    private int[] background;

    // The hover text last drawn, so its showing or going redraws the desktop.
    private string? drawnTooltip;

    // The palette the sky was worked out in; a different one in use means working it out again.
    private DesktopPalette backgroundPalette;
    private readonly AppManager? apps;
    private readonly string workingDirectory;
    private readonly HeldOutput appOutput;
    private readonly Dictionary<(string App, bool NewWindow, bool Quits), BarMenu> appMenus = [];

    // Help's About item for each app, kept so the bar sees the same item, and tells it apart, from frame to frame.
    private readonly Dictionary<string, UIMenuEntry> aboutEntries = [];

    // The open About windows: VentureOS's under "", and each app's under its name.
    private readonly Dictionary<string, DesktopWindow> aboutWindows = [];

    // Reused every frame: the frame loop runs without pause, so a frame where nothing changes allocates nothing.
    private readonly List<WindowEvent> happened = [];
    private readonly List<KeyPress> forWindows = [];

    // What the bar's menus were built from, so they're rebuilt only when one of these changes: the app shown, its
    // window in front, the window in front whoever's it is, and whether the app opens new windows.
    private (string? App, DesktopWindow? Window, DesktopWindow? Front, bool NewWindow, bool Built) menuSource;
    private DesktopInput last;
    private bool drawn;
    private PointerOwner owner;

    // The window in front when focus was last checked, so a window opened between frames, such as About when the
    // desktop opens, still makes its app the active one.
    private DesktopWindow? lastFocused;

    // The fallback app's failures to start in a row, when to try next, and the last reason shown.
    private int fallbackFailures;
    private TimeSpan nextFallbackStart;
    private string? lastFallbackProblem;

    // How many apps were open, and which windows were minimized, when the dock was last filled, so it's filled again
    // only when one of those changes.
    private int dockedRunning = -1;
    private readonly List<DesktopWindow> dockedWindows = [];

    private enum PointerOwner
    {
        None,
        Bar,
        Dock,
        Windows,
    }

    /// <summary>Opens the desktop on a screen of this size.</summary>
    /// <param name="width">The screen's width.</param>
    /// <param name="height">The screen's height.</param>
    /// <param name="apps">The system's apps, for the dock, or <see langword="null"/> for a desktop without one.</param>
    /// <param name="workingDirectory">Where apps opened from the dock run from.</param>
    /// <param name="appOutput">Where apps opened from the dock write; what they write shows in a window.</param>
    /// <param name="windows">The system's windows, which last between visits, or <see langword="null"/> for new ones.</param>
    public DesktopSession(
        int width,
        int height,
        AppManager? apps = null,
        string workingDirectory = "/",
        HeldOutput? appOutput = null,
        WindowManager? windows = null)
    {
        ArgumentNullException.ThrowIfNull(workingDirectory);
        Width = width;
        Height = height;
        this.apps = apps;
        this.workingDirectory = workingDirectory;
        this.appOutput = appOutput ?? new HeldOutput();
        backgroundPalette = DesktopTheme.Palette;
        background = DesktopBackground.Render(width, height, backgroundPalette);
        last = new DesktopInput(width / 2, height / 2, false);
        Windows = windows ?? new WindowManager(width, height);
        Windows.SetScreenSize(width, height);
        Windows.TopInset = MenuBar.Height + 4;
        ActiveApp = Windows.Focused?.App;
        lastFocused = Windows.Focused;
        Dock = new Dock(width, height);
        RefreshDock();
        RefreshMenus();
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>The desktop's windows.</summary>
    public WindowManager Windows { get; }

    /// <summary>The menu bar along the top.</summary>
    public MenuBar MenuBar { get; } = new();

    /// <summary>The dock along the bottom.</summary>
    public Dock Dock { get; }

    /// <summary>
    /// Reads the About window's document, which About VentureOS and every app's About share, or gives
    /// <see langword="null"/> when it can't, having said why. It has a <c>name</c>, a <c>version</c>, a <c>blurb</c>,
    /// and an <c>ok</c> button.
    /// </summary>
    public Func<UIScript?>? AboutDocument { get; init; }

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

    private DesktopWindow? MakeAbout(string? app)
    {
        if (AboutDocument?.Invoke() is not { } script)
            return null;

        UIView view = new(script);
        if (app is null)
        {
            view.SetText("version", SystemVersion ?? "");
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
            view.SetText("version", bundle?.Version is { } version ? $"Version {version}" : "");
            if (bundle?.Description is { } description)
                view.SetText("blurb", description);
            else
                view.SetHidden(true, "blurb");
        }

        return new DesktopWindow(app is null ? "About VentureOS" : $"About {app}", view)
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
    /// Starts the fallback app now rather than on the first frame, so windows opened after it, such as previews,
    /// land in front of its window.
    /// </summary>
    public void StartFallbackApp() => KeepFallbackRunning();

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
            else if (!(key is { Key: DesktopKey.Character, Alt: true, Control: false } && MenuBar.OpenByHotkey(key.Character)))
            {
                forWindows.Add(key);
            }
        }

        if (IsFinished)
            return false;

        // The pointer: a press belongs to what it lands on until it's released.
        if (pressed)
        {
            owner = MenuBar.IsOpen || MenuBar.Contains(x, y) ? PointerOwner.Bar
                : Dock.Contains(x, y) ? PointerOwner.Dock
                : PointerOwner.Windows;
        }

        if (owner == PointerOwner.Bar || MenuBar.IsOpen)
        {
            var mine = owner == PointerOwner.Bar;
            if (MenuBar.UpdatePointer(x, y, pressed && mine, released && mine) is { } chosen)
                Choose(chosen, happened);
        }

        var dockOwned = !MenuBar.IsOpen && (input.PointerDown || released ? owner == PointerOwner.Dock : true);
        if (Dock.Update(x, y, dockOwned, pressed, released && owner == PointerOwner.Dock) is { } clicked)
            OpenFromDock(clicked);

        var overWindows = !MenuBar.IsOpen && !MenuBar.Contains(x, y) && !Dock.Contains(x, y);
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
        if ((apps is not null && apps.Running.Count != dockedRunning) || MinimizedChanged())
            RefreshDock();
        ShowMessages(ActiveApp ?? "VentureOS", ActiveApp);

        if (Windows.Focused != lastFocused)
        {
            lastFocused = Windows.Focused;
            if (lastFocused is not null)
                ActiveApp = lastFocused.App;
        }

        LastEvents = happened.Count == 0 ? [] : [.. happened];
        RefreshMenus();

        var changed = !drawn || input.HasEvents || happened.Count > 0 || restarted || ticked
            || !ReferenceEquals(DesktopTheme.Palette, backgroundPalette)
            || !ReferenceEquals(Windows.Tooltip, drawnTooltip)
            || x != last.PointerX || y != last.PointerY || input.PointerDown != last.PointerDown;
        last = new DesktopInput(x, y, input.PointerDown);
        if (released)
            owner = PointerOwner.None;
        return changed;
    }

    /// <summary>Draws the whole desktop: the sky, the windows, the dock, the menu bar, then the pointer on top.</summary>
    public void Draw(IPainter painter)
    {
        ArgumentNullException.ThrowIfNull(painter);

        // A new palette needs a new sky; working it out allocates, so only then.
        if (!ReferenceEquals(DesktopTheme.Palette, backgroundPalette))
        {
            backgroundPalette = DesktopTheme.Palette;
            background = DesktopBackground.Render(Width, Height, backgroundPalette);
        }

        painter.DrawPixels(background, 0, 0, Width, Height);
        Windows.Draw(painter);
        Dock.Draw(painter);
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
            case MenuGroup.Standard when choice.Menu.IsOwn(choice.Item):
                // Help's About item, for the app the bar shows.
                ShowAbout(menuSource.App);
                break;
            case MenuGroup.Window or MenuGroup.Standard:
                ReportToWindow(choice.Menu.View, item, happened);
                break;
        }
    }

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

    private void OpenFromDock(DockItem item)
    {
        // A minimized window's tile restores it.
        if (item.Window is { } window)
        {
            Windows.Open(window);
            return;
        }

        if (apps is null)
            return;

        if (item.IsOpen)
        {
            // Bring the app forward: its menu, and its front window, or a new one when it has none.
            ActiveApp = item.Name;
            if (apps.Windows is { } host)
            {
                host.Activate(item.Name);
            }
            else
            {
                for (var index = Windows.Windows.Count - 1; index >= 0; index--)
                {
                    if (Windows.Windows[index].App == item.Name)
                    {
                        Windows.Open(Windows.Windows[index]);
                        break;
                    }
                }
            }

            return;
        }

        if (apps.OpenApp(item.Target, workingDirectory, appOutput, explainNeeds: false))
        {
            ActiveApp = item.Name;

            // An app that opens no window of its own, such as TextEdit waiting for a file, gets a new one.
            if (apps.Windows is { } host && !host.HasWindow(item.Name))
                host.NewWindow(item.Name);
        }

        ShowMessages(item.Name, ActiveApp);
        RefreshDock();
    }

    private void Quit(string app)
    {
        if (app == FallbackApp)
            return;

        apps?.CloseApp(app, appOutput);

        // Its windows go with it.
        foreach (var window in (DesktopWindow[])[.. Windows.Windows])
        {
            if (window.App == app)
                Windows.Close(window);
        }

        ActiveApp = null;
        ShowMessages(app, null);
        RefreshDock();
    }

    /// <summary>Shows what apps wrote, in a window titled for the app, so nothing they said is lost.</summary>
    private void ShowMessages(string title, string? app)
    {
        if (appOutput.HasLines)
            Windows.Open(DesktopAlert.Create(title, string.Join('\n', appOutput.Take()), app));
    }

    /// <summary>
    /// Starts the fallback app when it isn't running, waiting longer after each failure in a row, so a broken app
    /// can't keep the desktop busy. A failure is shown once, and again only when the reason changes.
    /// </summary>
    /// <returns>Whether it started.</returns>
    private bool KeepFallbackRunning()
    {
        if (FallbackApp is not { } fallback || apps is null || IsAppOpen(fallback))
            return false;

        var now = Clock();
        if (fallbackFailures > 0 && now < nextFallbackStart)
            return false;

        if (apps.OpenApp(fallback, workingDirectory, appOutput))
        {
            fallbackFailures = 0;
            lastFallbackProblem = null;
            ShowMessages(fallback, fallback);
            RefreshDock();
            return true;
        }

        var delay = RestartDelays[Math.Min(fallbackFailures, RestartDelays.Length - 1)];
        fallbackFailures++;
        nextFallbackStart = now + delay;

        var problem = string.Join('\n', appOutput.Take());
        if (problem != lastFallbackProblem)
        {
            lastFallbackProblem = problem;
            Windows.Open(DesktopAlert.Create(
                fallback,
                $"{fallback} didn't start. VentureOS will keep trying, next in {delay.TotalSeconds:0} seconds.\n{problem}".TrimEnd()));
        }

        return false;
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

    private void RefreshDock()
    {
        List<DockItem> items = [];
        dockedRunning = apps?.Running.Count ?? 0;
        foreach (var listing in apps?.Available() ?? [])
        {
            // An app that can't be read stays out of the dock; `apps` in the shell says why.
            if (listing.Bundle is not { } bundle)
                continue;

            // A user app opens by its path, so it can't be mistaken for a system app with the same name.
            var extension = Bundle.Extension(BundleKind.App);
            var target = listing.IsUser ? listing.Path
                : listing.Folder.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ? listing.Folder[..^extension.Length]
                : listing.Folder;
            DockItem item = new(bundle.Name, target, apps!.IsOpen(bundle.Identifier), listing.IsUser);
            if (bundle.Name == FallbackApp)
                items.Insert(0, item);
            else
                items.Add(item);
        }

        // Minimized windows, oldest first, after the apps.
        dockedWindows.Clear();
        foreach (var window in Windows.Windows)
        {
            if (window.IsMinimized)
            {
                dockedWindows.Add(window);
                items.Add(new DockItem(window.Title, "", IsOpen: false, Window: window));
            }
        }

        Dock.SetItems(items);
    }

    private void RefreshMenus()
    {
        // With no other app in front, the fallback's menu shows.
        var shown = ActiveApp ?? (FallbackApp is { } fallback && IsAppOpen(fallback) ? fallback : null);
        var front = Windows.Focused;
        var focused = front is not null && front.App == ActiveApp ? front : null;
        var newWindow = shown is not null && (apps?.Windows?.HasNewWindow(shown) ?? false);
        if (menuSource == (shown, focused, front, newWindow, true))
            return;

        menuSource = (shown, focused, front, newWindow, true);
        List<BarMenu> menus = [SystemMenu];
        if (shown is not null)
            menus.Add(AppMenu(shown));

        // The standard menus, with the window's menus of the same role merged in, then its others, with Help last.
        var view = focused?.View;
        IReadOnlyList<UIMenu> document = view?.Script.Menus ?? [];
        UIMenuEntry[] closing = front is not null && !Takes(document, CloseShortcut) ? [CloseWindowEntry] : [];
        UIMenuEntry[] helping = shown is not null ? [AboutEntry(shown)] : [];

        AddStandard(menus, document, view, UIMenuRole.File, "File", 'F', closing);
        AddStandard(menus, document, view, UIMenuRole.Edit, "Edit", 'E', []);
        AddStandard(menus, document, view, UIMenuRole.View, "View", 'V', []);
        foreach (var menu in document)
        {
            if (RoleOf(menu) is null)
                menus.Add(new BarMenu(menu, MenuGroup.Window, view));
        }

        AddStandard(menus, document, view, UIMenuRole.Help, "Help", 'H', helping);
        MenuBar.SetMenus(menus);
    }

    /// <summary>
    /// Adds a standard menu: the document's items of that role, in order, then the desktop's own after a separator.
    /// An empty one is left out, so a menu shows only when there's something in it.
    /// </summary>
    private static void AddStandard(
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
            menus.Add(new BarMenu(new UIMenu(title.ToLowerInvariant(), title, hotkey, entries, 0, role), MenuGroup.Standard, view, Own: own));
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
            foreach (var entry in menu.Entries)
            {
                if (entry.Shortcut is { } own && own.Control == shortcut.Control && own.Shift == shortcut.Shift
                    && own.Alt == shortcut.Alt && char.ToUpperInvariant(own.Key) == char.ToUpperInvariant(shortcut.Key))
                    return true;
            }
        }

        return false;
    }

    private UIMenuEntry AboutEntry(string app)
    {
        if (!aboutEntries.TryGetValue(app, out var entry))
        {
            entry = new UIMenuEntry("about", $"About {app}", 'A', null, 0);
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
                entries.Add(new UIMenuEntry(NewWindowItem, "New Window", 'N', new UIShortcut(Control: true, Shift: false, Alt: false, 'N'), 0));
            if (newWindow && quits)
                entries.Add(new UIMenuEntry(null, null, null, null, 0));
            if (quits)
                entries.Add(new UIMenuEntry(QuitItem, $"Quit {app}", 'Q', new UIShortcut(Control: true, Shift: false, Alt: false, 'Q'), 0));

            menu = new BarMenu(new UIMenu("app", app, null, entries, 0), MenuGroup.App, Emphasized: true);
            appMenus[key] = menu;
        }

        return menu;
    }
}
