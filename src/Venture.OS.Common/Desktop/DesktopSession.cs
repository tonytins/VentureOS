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

    private static readonly BarMenu SystemMenu = new(
        new UIMenu("venture", "VentureOS", null,
        [
            new UIMenuEntry(AboutItem, "About VentureOS", 'A', null, 0),
            new UIMenuEntry(null, null, null, null, 0),
            new UIMenuEntry(ReturnToShellItem, "Return to Shell", 'R', new UIShortcut(Control: true, Shift: true, Alt: false, 'Q'), 0),
        ], 0),
        MenuGroup.System);

    private readonly int[] background;
    private readonly AppManager? apps;
    private readonly string workingDirectory;
    private readonly HeldOutput appOutput;
    private readonly Dictionary<string, BarMenu> appMenus = [];
    private DesktopInput last;
    private bool drawn;
    private PointerOwner owner;
    private DesktopWindow? about;

    // The window in front when focus was last checked, so a window opened between frames, such as About when the
    // desktop opens, still makes its app the active one.
    private DesktopWindow? lastFocused;

    // The fallback app's failures to start in a row, when to try next, and the last reason shown.
    private int fallbackFailures;
    private TimeSpan nextFallbackStart;
    private string? lastFallbackProblem;

    // How many apps were open when the dock was last filled, so it's filled again only when that changes.
    private int dockedRunning = -1;

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
        background = DesktopBackground.Render(width, height);
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

    /// <summary>Makes the About VentureOS window, or <see langword="null"/> when it can't.</summary>
    public Func<DesktopWindow?>? AboutWindow { get; init; }

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

    /// <summary>Opens About VentureOS, or brings it to the front if it's open.</summary>
    public void ShowAbout()
    {
        if (about is null || !Windows.Windows.Contains(about))
            about = AboutWindow?.Invoke();
        if (about is not null)
            Windows.Open(about);
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
        List<WindowEvent> happened = [];

        // Apps and windows may have changed since the last frame, such as the fallback app starting.
        RefreshMenus();

        // Keys: the open menu takes them all; otherwise shortcuts and menu hotkeys come before the windows.
        List<KeyPress> forWindows = [];
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
        DesktopInput windowInput = new(
            x,
            y,
            input.PointerDown && owner == PointerOwner.Windows,
            overWindows ? input.Scroll : 0,
            [.. forWindows],
            input.Time);
        happened.AddRange(Windows.Update(windowInput));

        QuitAppsWithoutWindows(happened);
        var restarted = KeepFallbackRunning();
        if (apps is not null && apps.Running.Count != dockedRunning)
            RefreshDock();
        ShowMessages(ActiveApp ?? "VentureOS", ActiveApp);

        if (Windows.Focused != lastFocused)
        {
            lastFocused = Windows.Focused;
            if (lastFocused is not null)
                ActiveApp = lastFocused.App;
        }

        LastEvents = happened;
        RefreshMenus();

        var changed = !drawn || input.HasEvents || happened.Count > 0 || restarted
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

        painter.DrawPixels(background, 0, 0, Width, Height);
        Windows.Draw(painter);
        Dock.Draw(painter);
        MenuBar.Draw(painter);
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
            case MenuGroup.Window:
                // A chosen item reports like a pressed button, by its name, to the window whose menu it's in.
                foreach (var window in Windows.Windows)
                {
                    if (window.View != choice.Menu.View)
                        continue;

                    UIEvent chosen = new(UIEventKind.Pressed, item);
                    happened.Add(new WindowEvent(window, chosen));
                    window.EventHandler?.Invoke(window, chosen);
                    break;
                }

                happened.AddRange(Windows.CloseRequested());
                break;
        }
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

        foreach (var running in apps.Running)
        {
            if (running.Bundle.Name == name)
                return true;
        }

        return false;
    }

    private void OpenFromDock(DockItem item)
    {
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

        if (apps.OpenApp(item.Target, workingDirectory, appOutput))
            ActiveApp = item.Name;

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

    private void RefreshDock()
    {
        if (apps is null)
            return;

        dockedRunning = apps.Running.Count;
        List<DockItem> items = [];
        foreach (var listing in apps.Available())
        {
            // An app that can't be read stays out of the dock; `apps` in the shell says why.
            if (listing.Bundle is not { } bundle)
                continue;

            // A user app opens by its path, so it can't be mistaken for a system app with the same name.
            var extension = Bundle.Extension(BundleKind.App);
            var target = listing.IsUser ? listing.Path
                : listing.Folder.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ? listing.Folder[..^extension.Length]
                : listing.Folder;
            DockItem item = new(bundle.Name, target, apps.IsOpen(bundle.Identifier), listing.IsUser);
            if (bundle.Name == FallbackApp)
                items.Insert(0, item);
            else
                items.Add(item);
        }

        Dock.SetItems(items);
    }

    private void RefreshMenus()
    {
        List<BarMenu> menus = [SystemMenu];

        // With no other app in front, the fallback's menu shows.
        var shown = ActiveApp ?? (FallbackApp is { } fallback && IsAppOpen(fallback) ? fallback : null);
        if (shown is not null)
            menus.Add(AppMenu(shown));

        if (Windows.Focused is { } focused && focused.App == ActiveApp)
        {
            foreach (var menu in focused.View.Script.Menus)
                menus.Add(new BarMenu(menu, MenuGroup.Window, focused.View));
        }

        MenuBar.SetMenus(menus);
    }

    /// <summary>
    /// The app's menu: New Window when the app said how to open one, and Quit unless it's the fallback. Kept per app
    /// and shape, so the bar sees the same menu from frame to frame.
    /// </summary>
    private BarMenu AppMenu(string app)
    {
        var newWindow = apps?.Windows?.HasNewWindow(app) ?? false;
        var quits = app != FallbackApp;
        var key = $"{app}|{newWindow}|{quits}";
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
