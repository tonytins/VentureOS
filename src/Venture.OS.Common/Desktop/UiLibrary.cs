using Cosmos.Executable.Lua;
using Venture.OS.Bundles;
using Venture.OS.Interface;
using Venture.OS.Scripting;
using Venture.OS.Shell;
using Venture.OS.Interface.Strings;

namespace Venture.OS.Desktop;

/// <summary>Where apps' windows go, how their event handlers run, and what the desktop can ask of apps.</summary>
/// <param name="windows">The desktop's windows, which last while the system runs, so apps can open windows from the
/// shell too; they show the next time the desktop does.</param>
/// <param name="output">Where event handlers write. Events only happen while the desktop shows, and the console
/// would draw over it, so this is the desktop's held output.</param>
/// <param name="stop">Makes the stop for each event handler, such as a time limit; called once per event.</param>
public sealed class AppWindows(WindowManager windows, IShellOutput output, Func<Func<string?>>? stop = null)
{
    /// <summary>The shortest interval a timer may have, so a window can't keep the desktop busy.</summary>
    public static readonly TimeSpan MinimumInterval = TimeSpan.FromSeconds(0.1);

    private readonly Dictionary<string, Action> newWindowHandlers = [];
    private readonly Dictionary<string, Action<string>> openFileHandlers = [];
    private readonly List<WindowTimer> timers = [];

    // The timers as a tick began, reused, since a timer's code may start or stop others while the tick runs.
    private readonly List<WindowTimer> ticking = [];

    /// <summary>The desktop's windows.</summary>
    public WindowManager Windows { get; } = windows ?? throw new ArgumentNullException(nameof(windows));

    /// <summary>Where event handlers write.</summary>
    public IShellOutput Output { get; } = output ?? throw new ArgumentNullException(nameof(output));

    /// <summary>Makes the stop for each event handler, or <see langword="null"/> for none.</summary>
    public Func<Func<string?>>? Stop { get; } = stop;

    /// <summary>
    /// Reads a picture file by its full path, for the <c>&lt;Image&gt;</c>s an app's documents name, or gives
    /// <see langword="null"/> when it can't, having said why; <see langword="null"/> leaves them outlined.
    /// </summary>
    public Func<string, DesktopImage?>? LoadImage { get; init; }

    /// <summary>
    /// The system's own folder, such as <c>/mnt/system</c>, which any app may show pictures from with
    /// <c>window:setImage</c>, since nothing there is private; <see langword="null"/> for none.
    /// </summary>
    public string? SystemFolder { get; init; }

    /// <summary>
    /// The folder that stands for VentureOS's <c>/</c> where it runs inside another system, which scripts' paths to
    /// previews, apps, and pictures are kept within; <see langword="null"/> in the kernel. <see cref="SystemFolder"/>
    /// is a VentureOS path either way.
    /// </summary>
    public FileRoot? Root { get; init; }

    private DesktopText? text;

    /// <summary>
    /// The desktop's own words, such as its menu items and the alerts' OK: by default read from the system's strings
    /// the first time they're asked for, after <see cref="SystemFolder"/> and <see cref="Root"/> are set, with problems
    /// reported to <see cref="Output"/>; or words given in their place, such as a test's own.
    /// </summary>
    public DesktopText Text
    {
        get => text ??= new DesktopText(UiLibrary.SharedStrings(this), problem => Output.WriteLine($"Desktop: {problem}"));
        init => text = value ?? throw new ArgumentNullException(nameof(value));
    }

    // Icons as shelves show them, by host path and size, read once, so a shelf filled again doesn't read them again.
    private readonly Dictionary<(string Path, int Size), DesktopIcon?> icons = [];

    /// <summary>
    /// A picture as an icon at <paramref name="size"/>, as the dock shows icons, read once and kept; or
    /// <see langword="null"/> when it can't be read, or this desktop can't read pictures.
    /// </summary>
    public DesktopIcon? Icon(string path, int size)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (icons.TryGetValue((path, size), out var known))
            return known;

        var icon = LoadImage?.Invoke(path) is { } picture ? DesktopIcon.From(picture, size) : null;
        icons[(path, size)] = icon;
        return icon;
    }

    /// <summary>Whether an app said how to open a new window, which puts New Window in its app menu.</summary>
    public bool HasNewWindow(string app) => newWindowHandlers.ContainsKey(app);

    /// <summary>Records how an app opens a new window, or forgets it with <see langword="null"/>.</summary>
    public void SetNewWindow(string app, Action? handler)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (handler is null)
            newWindowHandlers.Remove(app);
        else
            newWindowHandlers[app] = handler;
    }

    /// <summary>Asks an app for a new window.</summary>
    /// <returns>Whether the app knows how to open one.</returns>
    public bool NewWindow(string app)
    {
        if (!newWindowHandlers.TryGetValue(app, out var handler))
            return false;

        handler();
        return true;
    }

    /// <summary>Records how an app opens a file, or forgets it with <see langword="null"/>.</summary>
    public void SetOpenFile(string app, Action<string>? handler)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (handler is null)
            openFileHandlers.Remove(app);
        else
            openFileHandlers[app] = handler;
    }

    /// <summary>Hands an app a file to open, such as Files does with a text file for TextEdit.</summary>
    /// <returns>Whether the app opens files.</returns>
    public bool OpenFile(string app, string path)
    {
        if (!openFileHandlers.TryGetValue(app, out var handler))
            return false;

        handler(path);
        return true;
    }

    /// <summary>Whether an app has a window open, shown or minimized.</summary>
    /// <remarks>A panel, such as the menu bar Files holds, isn't one of the windows an app is opened for.</remarks>
    public bool HasWindow(string app)
    {
        for (var index = 0; index < Windows.Windows.Count; index++)
        {
            var window = Windows.Windows[index];
            if (window.OpenedByApp && window.App == app && !window.IsPanel)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Brings an app forward, as clicking it in the dock does: its front window, or failing that its newest minimized
    /// one, or a new window when it has none.
    /// </summary>
    public void Activate(string app)
    {
        ArgumentNullException.ThrowIfNull(app);
        foreach (var minimized in (ReadOnlySpan<bool>)[false, true])
        {
            for (var index = Windows.Windows.Count - 1; index >= 0; index--)
            {
                var window = Windows.Windows[index];
                if (window.OpenedByApp && window.App == app && window.IsMinimized == minimized && !window.IsPanel)
                {
                    Windows.Open(window);
                    return;
                }
            }
        }

        NewWindow(app);
    }

    /// <summary>
    /// The app that is the desktop, as Files is, by name, which <c>venture.desktop.apps()</c> lists first, as the
    /// dock shows it; or <see langword="null"/>.
    /// </summary>
    public string? FallbackApp { get; set; }

    // Who hears that the desktop changed, and whether it has since they last heard.
    private readonly List<Action> desktopListeners = [];
    private bool desktopChanged;

    /// <summary>
    /// Notes that what the dock shows has changed: an app opened, closed, or was installed, or a window was minimized
    /// or restored. Listeners hear at the next tick, once however many changes there were.
    /// </summary>
    public void MarkDesktopChanged() => desktopChanged = true;

    /// <summary>Adds a listener for <see cref="MarkDesktopChanged"/>.</summary>
    /// <returns>What removes it again.</returns>
    public Action ListenToDesktop(Action listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        desktopListeners.Add(listener);
        return () => desktopListeners.Remove(listener);
    }

    /// <summary>
    /// Opens an app as the dock does: brings it forward when it's open, with a new window when it has none; opens it
    /// otherwise, from <paramref name="workingDirectory"/>, with a new window when it opens none of its own.
    /// </summary>
    /// <param name="path">The app's bundle, as scripts name it.</param>
    /// <returns>Why it didn't open, or <see langword="null"/> when it did; what the app said goes to <see cref="Output"/>.</returns>
    public string? OpenApp(AppManager apps, string path, string workingDirectory)
    {
        ArgumentNullException.ThrowIfNull(apps);
        ArgumentNullException.ThrowIfNull(path);
        foreach (var running in apps.Running)
        {
            if (apps.ScriptPath(running.Bundle.Path) == path)
            {
                Activate(running.Bundle.Name);
                ActivatedApp = running.Bundle.Name;
                return null;
            }
        }

        if (!apps.OpenApp(path, workingDirectory, Output, explainNeeds: false))
            return $"{Path.GetFileName(path)} didn't open";

        foreach (var running in apps.Running)
        {
            if (apps.ScriptPath(running.Bundle.Path) == path)
            {
                if (!HasWindow(running.Bundle.Name))
                    NewWindow(running.Bundle.Name);
                ActivatedApp = running.Bundle.Name;
            }
        }

        MarkDesktopChanged();
        return null;
    }

    /// <summary>
    /// The app <see cref="OpenApp"/> last brought forward, which the desktop makes the active one, its menu in the
    /// bar, even when it has no window to put in front; the desktop takes it each frame.
    /// </summary>
    public string? ActivatedApp { get; private set; }

    /// <summary>Takes <see cref="ActivatedApp"/>, leaving none.</summary>
    public string? TakeActivatedApp()
    {
        var app = ActivatedApp;
        ActivatedApp = null;
        return app;
    }

    private bool TellDesktopChanged()
    {
        if (!desktopChanged || desktopListeners.Count == 0)
            return false;

        desktopChanged = false;
        for (var index = desktopListeners.Count - 1; index >= 0; index--)
        {
            if (index < desktopListeners.Count)
                desktopListeners[index]();
        }

        return true;
    }

    /// <summary>The timers running now, such as for tests.</summary>
    public int TimerCount => timers.Count;

    /// <summary>
    /// Runs <paramref name="callback"/> every <paramref name="interval"/> while <paramref name="window"/> is open and
    /// the desktop shows. The first run is one interval after the next tick.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The interval is shorter than <see cref="MinimumInterval"/>.</exception>
    public WindowTimer Every(DesktopWindow window, TimeSpan interval, Action callback)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(callback);
        ArgumentOutOfRangeException.ThrowIfLessThan(interval, MinimumInterval);

        WindowTimer timer = new(window, interval, callback);
        timers.Add(timer);
        return timer;
    }

    /// <summary>Stops a timer. Stopping one twice does nothing.</summary>
    public void StopTimer(WindowTimer timer) => timers.Remove(timer);

    /// <summary>
    /// Runs the timers that are due at <paramref name="now"/>, called once a frame while the desktop shows. A timer
    /// whose window closed stops. Time the desktop wasn't showing isn't made up: a late timer runs once and waits a
    /// whole interval again.
    /// </summary>
    /// <returns>Whether any ran, so the desktop knows to draw again.</returns>
    public bool Tick(TimeSpan now)
    {
        var ran = TellDesktopChanged();
        if (timers.Count == 0)
            return ran;

        ticking.Clear();
        ticking.AddRange(timers);
        foreach (var timer in ticking)
        {
            if (!timers.Contains(timer))
                continue;

            if (!Windows.Contains(timer.Window))
            {
                timers.Remove(timer);
                continue;
            }

            if (timer.Next is not { } next)
            {
                timer.Next = now + timer.Interval;
                continue;
            }

            if (now < next)
                continue;

            timer.Next = now + timer.Interval;
            timer.Callback();
            ran = true;
        }

        return ran;
    }

    /// <summary>Opens a Quick Look preview of a file or folder.</summary>
    /// <returns>Why there's no preview, or <see langword="null"/> when it opened.</returns>
    /// <param name="path">A full VentureOS path, as a script names it.</param>
    /// <summary>
    /// Posts a toast from an app, with its name and icon, or from VentureOS when <paramref name="app"/> is
    /// <see langword="null"/>. The poster supplies only the text, so nothing speaks as another. While the desktop is
    /// hidden, it goes into the history without showing, and is printed, as an alert is.
    /// </summary>
    /// <returns>Why it wasn't posted, or <see langword="null"/>.</returns>
    public string? Toast(string text, Bundles.Bundle? app)
    {
        ArgumentNullException.ThrowIfNull(text);
        var title = app?.Name ?? "VentureOS";
        var showing = Windows.IsShowing;
        if (!Windows.Notifications.TryPost(title, text, app?.Name, app?.Icon, showing, out var posted, out var problem))
            return problem;
        if (!showing)
            Output.WriteLine($"{title}: {posted.Text}");
        return null;
    }

    public string? Preview(string path)
    {
        string host;
        try
        {
            host = FileRoot.Host(Root, path);
        }
        catch (UnauthorizedAccessException exception)
        {
            return exception.Message;
        }

        if (!QuickLook.TryCreate(host, Text, out var window, out var problem))
            return problem;

        Windows.Open(window);
        return null;
    }
}

/// <summary>A callback a window runs on a schedule; see <see cref="AppWindows.Every"/>.</summary>
public sealed class WindowTimer
{
    internal WindowTimer(DesktopWindow window, TimeSpan interval, Action callback)
    {
        Window = window;
        Interval = interval;
        Callback = callback;
    }

    /// <summary>The window it belongs to; it stops when that closes.</summary>
    public DesktopWindow Window { get; }

    /// <summary>How long it waits between runs.</summary>
    public TimeSpan Interval { get; }

    internal Action Callback { get; }

    // When it runs next; unset until the first tick after it starts.
    internal TimeSpan? Next { get; set; }
}

/// <summary>
/// <c>venture.ui</c>: windows for apps, laid out by <c>.uix</c> documents.
/// </summary>
/// <remarks>
/// <code>
/// local window = venture.ui.open("main.uix", { title = "Notes" })  -- without a title, the bar is blank
/// window:on("add", function(event) ... end)    -- a control's or menu item's events
/// window:setText("count", "3 notes")
/// window:setItems("notes", { "Milk", "Eggs" })
/// window:close()
/// venture.ui.preview("/mnt/notes.txt")          -- Quick Look; nil and a message when it can't
/// venture.ui.launch("/mnt/system/apps/Notes.app") -- opens an app, or brings an open one forward
/// venture.ui.launch("TextEdit", "/mnt/todo.txt")  -- by name, and hands it a file
/// venture.ui.onOpenFile(function(path) ... end) -- how this app opens a file it's handed
/// venture.ui.onNewWindow(function() ... end)   -- New Window (Ctrl+N), and the dock with no windows open
/// </code>
/// <para>A relative path is in the app's <c>Contents/Resources/</c>. The window belongs to the app: quitting the
/// app closes it, and closing the app's last window quits the app. Event handlers get a table with
/// <c>kind</c> (<c>pressed</c>, <c>submitted</c>, <c>changed</c>, or <c>selected</c>), <c>control</c>, and, when they
/// apply, <c>text</c>, <c>index</c> (counting from 1), and a grid item's <c>item</c>, its name. A chosen menu item is <c>pressed</c>.</para>
/// <para>Methods take a control by name and raise a Lua error for a name the document doesn't have, so a typo
/// shows at once instead of silently doing nothing. They need the colon: <c>window:setText(...)</c>.</para>
/// <list type="bullet">
/// <item><description><c>text(name)</c>, <c>setText(name, text)</c>: a control's text.</description></item>
/// <item><description><c>value(name)</c>, <c>setValue(name, number)</c>: a progress bar's value.</description></item>
/// <item><description><c>items(name)</c>, <c>setItems(name, list)</c>, <c>selected(name)</c>,
/// <c>select(name, index)</c>: a list's or grid's items and its selection, counting from 1, or <c>nil</c> for none.</description></item>
/// <item><description><c>setHidden</c>, <c>setDisabled</c>, <c>setChecked(name, on)</c>: hide, disable, or check a
/// control or menu item.</description></item>
/// <item><description><c>focus(name)</c>, <c>setTitle(text or nil)</c>, where <c>nil</c> blanks the bar, <c>setSubtitle(text or nil)</c>, <c>close()</c>. Focusing the title field starts an
/// edit of it.</description></item>
/// <item><description><c>every(seconds, fn)</c>: runs <c>fn</c> every so often, at least 0.1 seconds apart, while
/// the desktop shows, until the returned timer's <c>stop()</c>, the window closing, or the app quitting. Time the
/// desktop wasn't showing isn't made up.</description></item>
/// <item><description>Toolbar items report <c>pressed</c> like buttons, and take <c>setDisabled</c> and
/// <c>setChecked</c> (shown held down, as a tab). The title field reports <c>submitted</c> on Enter, and takes
/// <c>text</c> and <c>setText</c>.</description></item>
/// </list>
/// </remarks>
public static class UiLibrary
{
    /// <summary>Puts <c>venture.ui</c> in the <c>venture</c> table for an app's code.</summary>
    /// <param name="host">Where windows go, or <see langword="null"/> when there's no desktop.</param>
    /// <param name="apps">Opens and finds apps, for <c>venture.ui.launch</c>, or <see langword="null"/>.</param>
    public static void Open(ILuaState state, LuaOwner owner, Bundle app, AppWindows? host, AppManager? apps = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(app);

        if (state.GetGlobal(VentureLibrary.GlobalName) != LuaType.LUA_TTABLE)
        {
            state.Pop(1);
            state.NewTable();
            state.PushValue(-1);
            state.SetGlobal(VentureLibrary.GlobalName);
        }

        state.NewTable();
        Function(state, "open", app, host, (lua, desktop) => OpenWindow(lua, owner, app, desktop));
        Function(state, "preview", app, host, (lua, desktop) =>
        {
            var path = FullPath(lua, owner, "preview");
            if (desktop.Preview(path) is { } problem)
            {
                lua.PushNil();
                lua.PushString(LuaText.Encode(problem));
                return 2;
            }

            lua.PushBoolean(true);
            return 1;
        });
        Function(state, "launch", app, host, (lua, desktop) => Launch(lua, owner, desktop, apps));
        Function(state, "alert", app, host, (lua, desktop) =>
        {
            var message = LuaText.Decode(lua.L_CheckString(1));
            var title = lua.IsNoneOrNil(2) ? app.Name : LuaText.Decode(lua.L_CheckString(2));

            // Without the desktop on the screen, the window would wait unseen, so the message is printed instead.
            if (desktop.Windows.IsShowing)
                desktop.Windows.Open(DesktopAlert.Create(title, message, desktop.Text.Get("ok"), app.Name));
            else
                desktop.Output.WriteLine($"{title}: {message}");
            lua.PushBoolean(true);
            return 1;
        });
        Function(state, "toast", app, host, (lua, desktop) =>
        {
            if (desktop.Toast(LuaText.Decode(lua.L_CheckString(1)), app) is { } problem)
            {
                lua.PushNil();
                lua.PushString(LuaText.Encode(problem));
                return 2;
            }

            lua.PushBoolean(true);
            return 1;
        });
        Function(state, "onOpenFile", app, host, (lua, desktop) =>
        {
            if (!lua.IsFunction(1))
                return Fail(lua, "venture.ui.onOpenFile needs a function, such as venture.ui.onOpenFile(function(path) ... end)");

            lua.PushValue(1);
            var reference = lua.L_Ref(LuaDef.LUA_REGISTRYINDEX);
            var workingDirectory = owner.Interpreter.WorkingDirectory ?? "/";
            desktop.SetOpenFile(app.Name, path =>
            {
                if (!owner.IsDisposed)
                {
                    owner.Call(reference, state =>
                    {
                        state.PushString(LuaText.Encode(path));
                        return 1;
                    }, desktop.Output, workingDirectory, desktop.Stop?.Invoke());
                }
            });
            owner.AddUndo(() =>
            {
                desktop.SetOpenFile(app.Name, null);
                owner.Interpreter.State.L_Unref(LuaDef.LUA_REGISTRYINDEX, reference);
            });
            return 0;
        });
        Function(state, "onNewWindow", app, host, (lua, desktop) =>
        {
            if (!lua.IsFunction(1))
                return Fail(lua, "venture.ui.onNewWindow needs a function, such as venture.ui.onNewWindow(function() ... end)");

            lua.PushValue(1);
            var reference = lua.L_Ref(LuaDef.LUA_REGISTRYINDEX);
            var workingDirectory = owner.Interpreter.WorkingDirectory ?? "/";
            desktop.SetNewWindow(app.Name, () =>
            {
                if (!owner.IsDisposed)
                    owner.Call(reference, _ => 0, desktop.Output, workingDirectory, desktop.Stop?.Invoke());
            });
            owner.AddUndo(() =>
            {
                desktop.SetNewWindow(app.Name, null);
                owner.Interpreter.State.L_Unref(LuaDef.LUA_REGISTRYINDEX, reference);
            });
            return 0;
        });

        state.SetField(-2, "ui");
        state.Pop(1);
    }

    /// <summary>A <c>venture.ui</c> function, or one that explains the missing desktop.</summary>
    private static void Function(ILuaState state, string name, Bundle app, AppWindows? host, Func<ILuaState, AppWindows, int> body)
    {
        if (host is null)
            state.PushCSharpFunction(lua => Fail(lua, $"venture.ui.{name} needs the desktop, which this system doesn't have"));
        else
            state.PushCSharpFunction(lua => body(lua, host));
        state.SetField(-2, name);
    }

    /// <summary>The first argument as a full path, from the app's current folder.</summary>
    private static string FullPath(ILuaState lua, LuaOwner owner, string function)
    {
        if (lua.Type(1) != LuaType.LUA_TSTRING)
            Fail(lua, $"venture.ui.{function} needs a path, such as venture.ui.{function}(\"/mnt/notes.txt\")");

        return ShellPaths.Resolve(owner.Interpreter.WorkingDirectory ?? "/", LuaText.Decode(lua.ToString(1)));
    }

    private static int Launch(ILuaState lua, LuaOwner owner, AppWindows host, AppManager? apps)
    {
        if (lua.Type(1) != LuaType.LUA_TSTRING)
            return Fail(lua, "venture.ui.launch needs an app, such as venture.ui.launch(\"TextEdit\")");
        if (apps is null)
            return Fail(lua, "venture.ui.launch needs the system's apps, which aren't available");

        string? file = null;
        if (lua.Type(2) == LuaType.LUA_TSTRING)
            file = LuaText.Decode(lua.ToString(2));
        else if (!lua.IsNoneOrNil(2))
            return Fail(lua, "venture.ui.launch's file must be a path, such as venture.ui.launch(app, \"/mnt/todo.txt\")");

        var problem = LaunchApp(host, apps, LuaText.Decode(lua.ToString(1)), file, owner.Interpreter.WorkingDirectory ?? "/");
        if (problem is not null)
        {
            lua.PushNil();
            lua.PushString(LuaText.Encode(problem));
            return 2;
        }

        lua.PushBoolean(true);
        return 1;
    }

    /// <summary>
    /// Opens an app, by name (in the system's or the user's apps) or by bundle path from a folder, and hands it a
    /// file when one is given; an open app without a file comes forward, as from the dock. What the app says while
    /// it opens goes to the desktop's messages.
    /// </summary>
    /// <returns>Why it couldn't, or <see langword="null"/>.</returns>
    public static string? LaunchApp(AppWindows host, AppManager apps, string given, string? file, string workingDirectory)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(apps);
        ArgumentNullException.ThrowIfNull(given);

        // The app as a script names it (a VentureOS path), and where it is on the host, to read it.
        var path = ShellPaths.Resolve(workingDirectory, given);
        string hostPath;
        if (!given.Contains('/') && !given.EndsWith(Bundle.Extension(BundleKind.App), StringComparison.OrdinalIgnoreCase))
        {
            hostPath = "";
            foreach (var listing in apps.Available())
            {
                if (listing.Bundle is { } named && named.Name.Equals(given, StringComparison.OrdinalIgnoreCase))
                {
                    hostPath = listing.Path;
                    break;
                }
            }

            if (hostPath.Length == 0)
                return $"there's no app called {given}";
            path = apps.ScriptPath(hostPath);
        }
        else
        {
            try
            {
                hostPath = FileRoot.Host(host.Root, path);
            }
            catch (UnauthorizedAccessException exception)
            {
                return exception.Message;
            }
        }

        Bundle bundle;
        try
        {
            bundle = Bundle.Read(hostPath, BundleKind.App);
        }
        catch (BundleException exception)
        {
            return exception.Message;
        }

        if (!apps.IsOpen(bundle.Identifier) && !apps.OpenApp(path, workingDirectory, host.Output, explainNeeds: false))
            return $"{bundle.Name} didn't open";

        // With a file, the app opens it. Without one, the app comes forward, with a new window if it has none.
        if (file is not null)
        {
            if (!host.OpenFile(bundle.Name, ShellPaths.Resolve(workingDirectory, file)))
                return $"{bundle.Name} can't open files";
        }
        else
        {
            host.Activate(bundle.Name);
        }

        return null;
    }

    private static int OpenWindow(ILuaState lua, LuaOwner owner, Bundle app, AppWindows host)
    {
        if (lua.Type(1) != LuaType.LUA_TSTRING)
            return Fail(lua, "venture.ui.open needs a .uix file, such as venture.ui.open(\"main.uix\")");

        var given = LuaText.Decode(lua.ToString(1));
        string path;
        if (given.StartsWith('/'))
        {
            path = given;
        }
        else if (SystemDirectory.IsValidPath(given))
        {
            path = $"{app.Path}/Contents/Resources/{given}";
        }
        else
        {
            return Fail(lua, $"venture.ui.open needs a path inside Contents/Resources, such as \"main.uix\", not \"{given}\"");
        }

        // Without a title the bar is blank, as the menu bar already names the app, and the window goes by the app's
        // name wherever it's listed.
        string? title = null;
        if (lua.Type(2) == LuaType.LUA_TTABLE)
        {
            lua.GetField(2, "title");
            if (lua.Type(-1) == LuaType.LUA_TSTRING)
                title = LuaText.Decode(lua.ToString(-1));
            lua.Pop(1);
        }
        else if (!lua.IsNoneOrNil(2))
        {
            return Fail(lua, "venture.ui.open's options must be a table, such as { title = \"Notes\" }");
        }

        UIScript script;
        try
        {
            script = UIScript.Load(path);
        }
        catch (Exception exception) when (exception is UIScriptException or IOException or UnauthorizedAccessException)
        {
            return Fail(lua, $"venture.ui.open: {given}: {exception.Message}");
        }

        // The top edge holds the menu bar, and there's one.
        if (script.Window.Edge == UIEdge.Top && host.Windows.TopPanel is { } top)
            return Fail(lua, $"venture.ui.open: {given} is a top panel, and the top edge holds the menu bar, which {top.App ?? top.Title} shows");

        // A document's <String>s and <Image>s come from beside it, such as an app's Contents/Resources, and its
        // <String>s from the system's shared tables too.
        var folder = Path.GetDirectoryName(path) ?? "/";
        StringTables strings = new(folder, SharedStrings(host));
        var view = DocumentView.Open(
            script, folder, strings, host.LoadImage, problem => host.Output.WriteLine($"{app.Name}: {given}: {problem}"));

        Handle handle = new(owner, host, view, Path.GetFileName(path)) { Folder = folder, App = app, Strings = strings };
        handle.Window = new DesktopWindow(title is { Length: > 0 } ? title : app.Name, handle.View)
        {
            ShowsTitle = title is { Length: > 0 },
            App = app.Name,
            OpenedByApp = true,
            EventHandler = (_, uiEvent) => handle.Dispatch(uiEvent),
        };

        // Quitting the app closes the window and lets go of its handlers.
        owner.AddUndo(handle.Release);
        host.Windows.Open(handle.Window);

        PushHandle(lua, handle);
        return 1;
    }

    /// <summary>The system's shared string tables, as the host sees them, or <see langword="null"/> without a system folder.</summary>
    internal static string? SharedStrings(AppWindows? host) =>
        host?.SystemFolder is { } system ? FileRoot.Host(host.Root, system + "/strings") : null;

    private static void PushHandle(ILuaState lua, Handle handle)
    {
        lua.NewTable();
        Method(lua, "on", handle, On);
        Method(lua, "text", handle, (state, h) =>
        {
            var name = Name(state, h, "text");
            if (h.View.Text(name) is { } text)
                state.PushString(LuaText.Encode(text));
            else
                state.PushNil();
            return 1;
        });
        Method(lua, "string", handle, (state, h) =>
        {
            var name = LuaText.Decode(state.L_CheckString(2));
            if (!h.View.Script.Strings.TryGetValue(name, out var resource))
                return Fail(state, $"window:string: {h.Document} has no <String name=\"{name}\">");

            return CstLibrary.Push(
                state, h.Strings, () => h.Strings.Find(resource), $"the string \"{name}\"", 3, fill: true, "window:string");
        });
        Method(lua, "setText", handle, (state, h) =>
        {
            var name = Name(state, h, "setText");
            h.View.SetText(name, LuaText.Decode(state.L_CheckString(3)));
            return 0;
        });
        Method(lua, "value", handle, (state, h) =>
        {
            var name = Name(state, h, "value");
            if (h.View.Value(name) is { } value)
                state.PushNumber(value);
            else
                state.PushNil();
            return 1;
        });
        Method(lua, "setValue", handle, (state, h) =>
        {
            var name = Name(state, h, "setValue");
            h.View.SetValue(name, (float)state.L_CheckNumber(3));
            return 0;
        });
        Method(lua, "items", handle, (state, h) =>
        {
            if (GridNamed(state, h) is { } grid)
            {
                state.CreateTable(grid.Items.Count, 0);
                for (var index = 0; index < grid.Items.Count; index++)
                {
                    state.PushString(LuaText.Encode(grid.Items[index].Title));
                    state.RawSetI(-2, index + 1);
                }

                return 1;
            }

            var list = List(state, h, "items");
            state.CreateTable(list.Items.Count, 0);
            for (var index = 0; index < list.Items.Count; index++)
            {
                state.PushString(LuaText.Encode(list.Items[index]));
                state.RawSetI(-2, index + 1);
            }

            return 1;
        });
        Method(lua, "setItems", handle, SetItems);
        Method(lua, "selected", handle, (state, h) =>
        {
            var selected = GridNamed(state, h) is { } grid ? grid.SelectedIndex : List(state, h, "selected").SelectedIndex;
            if (selected >= 0)
                state.PushInteger(selected + 1);
            else
                state.PushNil();
            return 1;
        });
        Method(lua, "select", handle, (state, h) =>
        {
            var grid = GridNamed(state, h);
            var list = grid is null ? List(state, h, "select") : null;
            var count = grid?.Items.Count ?? list!.Items.Count;
            var chosen = -1;
            if (!state.IsNoneOrNil(3))
            {
                var index = state.L_CheckInteger(3);
                if (index < 1 || index > count)
                    return Fail(state, $"window:select: there's no item {index} in a {(grid is null ? "list" : "grid")} of {count}");
                chosen = (int)index - 1;
            }

            if (grid is not null)
                grid.SelectedIndex = chosen;
            else
                list!.SelectedIndex = chosen;
            return 0;
        });
        Method(lua, "setHidden", handle, (state, h) =>
        {
            h.View.SetHidden(state.ToBoolean(3), Name(state, h, "setHidden"));
            return 0;
        });
        Method(lua, "setDisabled", handle, (state, h) =>
        {
            h.View.SetDisabled(state.ToBoolean(3), Name(state, h, "setDisabled"));
            return 0;
        });
        Method(lua, "setChecked", handle, (state, h) =>
        {
            h.View.SetSelected(state.ToBoolean(3), Name(state, h, "setChecked"));
            return 0;
        });
        Method(lua, "focus", handle, (state, h) =>
        {
            var name = Name(state, h, "focus");
            if (h.View.Script.TitleField?.Name == name)
                h.Window!.BeginTitleEdit();
            else
                h.View.Focus(name);
            return 0;
        });
        Method(lua, "setTitle", handle, (state, h) =>
        {
            // nil or an empty string blanks the bar again, and the window goes back to the app's name.
            var title = state.IsNoneOrNil(2) ? "" : LuaText.Decode(state.L_CheckString(2));
            h.Window!.Title = title.Length > 0 ? title : h.Window.App ?? h.Window.Title;
            h.Window.ShowsTitle = title.Length > 0;
            return 0;
        });
        Method(lua, "setImage", handle, SetImage);
        Method(lua, "setSubtitle", handle, (state, h) =>
        {
            // nil or an empty string takes the line away, and the bar goes back to its usual height.
            h.View.Subtitle = state.IsNoneOrNil(2) ? null : LuaText.Decode(state.L_CheckString(2));
            return 0;
        });
        Method(lua, "close", handle, (_, h) =>
        {
            h.Window!.RequestClose();
            return 0;
        });
        Method(lua, "every", handle, (state, h) => h.Every(state));

        // A readable form, since the lua prompt prints what a call returns.
        lua.NewTable();
        var text = LuaText.Encode($"window \"{handle.Window!.Title}\"");
        lua.PushCSharpFunction(state =>
        {
            state.PushString(text);
            return 1;
        });
        lua.SetField(-2, "__tostring");
        lua.SetMetaTable(-2);
    }

    private static int On(ILuaState lua, Handle handle)
    {
        var name = Name(lua, handle, "on");
        if (!lua.IsFunction(3))
            return Fail(lua, $"window:on needs a function for \"{name}\", such as window:on(\"{name}\", function(event) ... end)");

        lua.PushValue(3);
        var reference = lua.L_Ref(LuaDef.LUA_REGISTRYINDEX);
        if (handle.Handlers.TryGetValue(name, out var previous))
            lua.L_Unref(LuaDef.LUA_REGISTRYINDEX, previous);
        handle.Handlers[name] = reference;
        return 0;
    }

    /// <summary>
    /// <c>window:setItems(name, items)</c>: a list's items, each a string, or, for the sidebar, a table with
    /// <c>text</c> and an optional <c>icon</c> naming an <c>&lt;Image&gt;</c>, or <c>{ separator = true }</c> between
    /// groups. Separators aren't items, so indexes count items only. Nothing changes when an entry is wrong. A
    /// sidebar of panes takes its rows from the document, so setting them is an error.
    /// </summary>
    private static int SetItems(ILuaState lua, Handle handle)
    {
        var name = Name(lua, handle, "setItems");
        if (handle.View.Shelf(name) is { } shelf)
            return SetShelfItems(lua, handle, name, shelf);
        if (handle.View.Grid(name) is { } grid)
            return SetGridItems(lua, handle, grid);

        var list = List(lua, handle, "setItems");
        if (lua.Type(3) != LuaType.LUA_TTABLE)
            return Fail(lua, "window:setItems needs a list of items, such as { \"Milk\", \"Eggs\" }");

        var isSidebar = name == handle.View.SidebarName;
        if (isSidebar && handle.View.HasPanes)
            return Fail(lua, $"window:setItems can't change \"{name}\" in {handle.Document}: its rows are the document's panes");

        List<string> items = [];
        List<string?> icons = [];
        List<int> groupStarts = [];
        var count = lua.RawLen(3);
        for (var index = 1; index <= count; index++)
        {
            lua.RawGetI(3, index);
            if (lua.Type(-1) != LuaType.LUA_TTABLE)
            {
                var item = lua.L_ToString(-1);
                lua.Pop(2); // L_ToString pushes its result on top of the value.
                items.Add(LuaText.Decode(item));
                icons.Add(null);
                continue;
            }

            if (!isSidebar)
            {
                lua.Pop(1);
                return Fail(lua, $"window:setItems: item {index} is a table, but only a sidebar's items can have icons or separators; \"{name}\" takes strings");
            }

            lua.GetField(-1, "separator");
            var separator = lua.ToBoolean(-1);
            lua.Pop(1);
            if (separator)
            {
                // A separator only splits rows: one at either end, or beside another, splits nothing.
                if (items.Count > 0 && (groupStarts.Count == 0 || groupStarts[^1] != items.Count))
                    groupStarts.Add(items.Count);
                lua.Pop(1);
                continue;
            }

            lua.GetField(-1, "text");
            var text = lua.Type(-1) == LuaType.LUA_TSTRING ? LuaText.Decode(lua.ToString(-1)) : null;
            lua.Pop(1);
            lua.GetField(-1, "icon");
            var icon = lua.Type(-1) == LuaType.LUA_TSTRING ? LuaText.Decode(lua.ToString(-1)) : null;
            var iconGiven = !lua.IsNoneOrNil(-1);
            lua.Pop(2);

            if (text is null)
                return Fail(lua, $"window:setItems: item {index} needs text, such as {{ text = \"Home\", icon = \"homeIcon\" }}, or separator = true");
            if (iconGiven && icon is null)
                return Fail(lua, $"window:setItems: item {index}'s icon must be the name of an <Image>");
            if (icon is not null && !handle.View.Script.Images.ContainsKey(icon))
                return Fail(lua, $"window:setItems: item {index}'s icon \"{icon}\" isn't an <Image> in {handle.Document}");

            items.Add(text);
            icons.Add(icon);
        }

        // A separator after the last item splits nothing.
        if (groupStarts.Count > 0 && groupStarts[^1] >= items.Count)
            groupStarts.RemoveAt(groupStarts.Count - 1);

        list.Items.Clear();
        list.Items.AddRange(items);
        list.Icons.Clear();
        list.Icons.AddRange(icons);
        list.GroupStarts.Clear();
        list.GroupStarts.AddRange(groupStarts);
        if (list.SelectedIndex >= items.Count)
            list.SelectedIndex = -1;
        return 0;
    }

    /// <summary>
    /// <c>window:setItems(name, items)</c> on a grid: its items in place of the document's, each a string, its title,
    /// or a table with <c>title</c>, and an optional <c>name</c>, reported when it's chosen; <c>image</c>, an
    /// <c>&lt;Image&gt;</c>'s name or a picture's path, as <c>setImage</c> takes; <c>color</c>, <c>#RRGGBB</c>; and
    /// <c>tooltip</c>. An item shows a picture or a color, not both. Nothing changes when an entry is wrong, or a
    /// picture can't be read, which says so.
    /// </summary>
    private static int SetGridItems(ILuaState lua, Handle handle, UIGridState grid)
    {
        if (lua.Type(3) != LuaType.LUA_TTABLE)
            return Fail(lua, "window:setItems needs a list of items, such as { { title = \"Sky\", image = \"sky.png\" } }");

        List<UIGridEntry> items = [];
        var count = lua.RawLen(3);
        for (var index = 1; index <= count; index++)
        {
            lua.RawGetI(3, index);
            if (lua.Type(-1) != LuaType.LUA_TTABLE)
            {
                var title = lua.L_ToString(-1);
                lua.Pop(2); // L_ToString pushes its result on top of the value.
                items.Add(new UIGridEntry(LuaText.Decode(title)));
                continue;
            }

            string? Field(string field)
            {
                lua.GetField(-1, field);
                var value = lua.Type(-1) == LuaType.LUA_TSTRING ? LuaText.Decode(lua.ToString(-1)) : null;
                var given = !lua.IsNoneOrNil(-1);
                lua.Pop(1);
                return given && value is null ? throw new ArgumentException($"item {index}'s {field} must be a string") : value;
            }

            string? itemTitle, name, image, written, tooltip;
            try
            {
                (itemTitle, name, image, written, tooltip) = (Field("title"), Field("name"), Field("image"), Field("color"), Field("tooltip"));
            }
            catch (ArgumentException exception)
            {
                lua.Pop(1);
                return Fail(lua, $"window:setItems: {exception.Message}");
            }

            lua.Pop(1);
            if (image is not null && written is not null)
                return Fail(lua, $"window:setItems: item {index} has an image and a color; an item shows one");
            if (itemTitle is null && image is null && written is null)
                return Fail(lua, $"window:setItems: item {index} needs a title, an image, or a color, such as {{ title = \"Sky\", image = \"sky.png\" }}");

            UIColor? color = null;
            if (written is not null)
            {
                if (!DesktopPalette.TryParseColor(written, out var parsed))
                    return Fail(lua, $"window:setItems: item {index}'s color is written #RRGGBB, not \"{written}\"");
                color = parsed;
            }

            // An <Image> in the document by its name; anything else is a picture's path, read now.
            DesktopImage? picture = null;
            if (image is not null && !handle.View.Script.Images.ContainsKey(image))
            {
                if (PicturePath(handle, image, fromAnyApp: false, out var path) is { } problem)
                    return Fail(lua, $"window:setItems: item {index}'s image: {problem}");
                if (handle.Host.LoadImage is not { } load)
                    return Fail(lua, "window:setItems: this desktop can't show pictures");
                picture = load(path);
                if (picture is null)
                    return Fail(lua, $"window:setItems: item {index}'s image, {image}, couldn't be read as a picture");
                image = null;
            }

            items.Add(new UIGridEntry(itemTitle ?? "", name, image, picture, color, tooltip));
        }

        grid.Items.Clear();
        grid.Items.AddRange(items);
        if (grid.SelectedIndex >= items.Count)
            grid.SelectedIndex = -1;
        return 0;
    }

    /// <summary>
    /// <c>window:setImage(name, path)</c>: shows a picture for one of the document's <c>&lt;Image&gt;</c>s, from a
    /// PNG beside the document, in the app's bundle, or in the system's folder, or anywhere with the <c>files</c>
    /// scope, since a picture shows what a file holds; <c>nil</c> takes it away. Returns <see langword="true"/>, or
    /// <see langword="nil"/> and why, so a picture that can't be read doesn't stop the app.
    /// </summary>
    private static int SetImage(ILuaState lua, Handle handle)
    {
        if (lua.Type(2) != LuaType.LUA_TSTRING)
            return Fail(lua, "window:setImage needs an <Image>'s name, such as window:setImage(\"thumbnail\", \"pictures/beach.png\")");

        var name = LuaText.Decode(lua.ToString(2));
        if (!handle.View.Script.Images.ContainsKey(name))
            return Fail(lua, $"window:setImage: \"{name}\" isn't an <Image> in {handle.Document}");

        if (lua.IsNoneOrNil(3))
        {
            handle.View.SetImage(name, null);
            lua.PushBoolean(true);
            return 1;
        }

        var given = LuaText.Decode(lua.L_CheckString(3));
        if (PicturePath(handle, given, fromAnyApp: false, out var path) is { } problem)
            return Refuse(lua, problem);

        if (handle.Host.LoadImage is not { } load)
            return Refuse(lua, "this desktop can't show pictures");
        if (load(path) is not { } picture)
            return Refuse(lua, $"{given} couldn't be read as a picture");

        handle.View.SetImage(name, picture);
        lua.PushBoolean(true);
        return 1;
    }

    /// <summary>
    /// Where a picture an app names is on the host: a path beside its document, or a full path in the app, the system's
    /// folder, or, with <paramref name="fromAnyApp"/>, any app bundle, since apps' icons are theirs to show; anywhere
    /// with the <c>files</c> scope, since a picture shows what a file holds.
    /// </summary>
    /// <returns>Why it can't be shown, or <see langword="null"/>.</returns>
    private static string? PicturePath(Handle handle, string given, bool fromAnyApp, out string path)
    {
        path = "";
        if (!given.StartsWith('/'))
        {
            if (!SystemDirectory.IsValidPath(given))
                return $"\"{given}\" isn't a path beside the document";
            path = $"{handle.Folder}/{given}";
            return null;
        }

        // A script's full path is a VentureOS path; the app's folder is the host's, so it's compared as scripts see it.
        var root = handle.Host.Root;
        var venture = ShellPaths.Resolve("/", given);
        var allowed = (handle.App is { } app && (ShellPaths.IsWithin(venture, FileRoot.Venture(root, app.Path)) || app.Needs.HasFlag(ScriptScopes.Files)))
            || (handle.Host.SystemFolder is { } system && ShellPaths.IsWithin(venture, system))
            || (fromAnyApp && venture.Contains(".app/Contents/", StringComparison.OrdinalIgnoreCase));
        if (!allowed)
            return $"{given} isn't in the app or the system's folder; showing it needs the files scope";

        try
        {
            path = FileRoot.Host(root, venture);
            return null;
        }
        catch (UnauthorizedAccessException exception)
        {
            return exception.Message;
        }
    }

    /// <summary>
    /// <c>window:setItems(name, items)</c> for a shelf: each item a table with a <c>title</c>, and optionally an
    /// <c>icon</c> (a PNG, as <c>setImage</c> takes, or one in any app's bundle), <c>open</c> for a dot under it,
    /// <c>group</c>, a number, with a divider between groups, and <c>window</c> for a window's pale tile. An icon that
    /// can't be read shows the title's initial. Nothing changes when an entry is wrong.
    /// </summary>
    private static int SetShelfItems(ILuaState lua, Handle handle, string name, Shelf shelf)
    {
        if (lua.Type(3) != LuaType.LUA_TTABLE)
            return Fail(lua, "window:setItems needs a list of items, such as { { title = \"Files\", icon = \"...\" } }");

        List<ShelfItem> items = [];
        var count = lua.RawLen(3);
        for (var index = 1; index <= count; index++)
        {
            lua.RawGetI(3, index);
            if (lua.Type(-1) != LuaType.LUA_TTABLE)
            {
                lua.Pop(1);
                return Fail(lua, $"window:setItems: item {index} of the shelf \"{name}\" needs to be a table, such as {{ title = \"Files\" }}");
            }

            lua.GetField(-1, "title");
            var title = lua.Type(-1) == LuaType.LUA_TSTRING ? LuaText.Decode(lua.ToString(-1)) : null;
            lua.Pop(1);
            lua.GetField(-1, "icon");
            var iconGiven = !lua.IsNoneOrNil(-1);
            var icon = lua.Type(-1) == LuaType.LUA_TSTRING ? LuaText.Decode(lua.ToString(-1)) : null;
            lua.Pop(1);
            lua.GetField(-1, "group");
            var groupGiven = !lua.IsNoneOrNil(-1);
            var group = lua.Type(-1) == LuaType.LUA_TNUMBER ? lua.ToNumber(-1) : double.NaN;
            lua.Pop(1);
            lua.GetField(-1, "open");
            var open = lua.ToBoolean(-1);
            lua.Pop(1);
            lua.GetField(-1, "window");
            var window = lua.ToBoolean(-1);
            lua.Pop(2);

            if (title is null)
                return Fail(lua, $"window:setItems: item {index} needs a title, such as {{ title = \"Files\" }}");
            if (iconGiven && icon is null)
                return Fail(lua, $"window:setItems: item {index}'s icon must be a path to a PNG");
            if (groupGiven && (double.IsNaN(group) || group != Math.Floor(group) || group < int.MinValue || group > int.MaxValue))
                return Fail(lua, $"window:setItems: item {index}'s group must be a whole number");

            DesktopIcon? picture = null;
            if (icon is not null)
            {
                if (PicturePath(handle, icon, fromAnyApp: true, out var path) is { } problem)
                    return Fail(lua, $"window:setItems: item {index}'s icon: {problem}");
                picture = handle.Host.Icon(path, Shelf.IconSize);
            }

            items.Add(new ShelfItem(title, picture, open, groupGiven ? (int)group : 0, window));
        }

        shelf.SetItems(items);
        return 0;
    }

    private static int Refuse(ILuaState lua, string reason)
    {
        lua.PushNil();
        lua.PushString(LuaText.Encode(reason));
        return 2;
    }

    private static void Method(ILuaState lua, string name, Handle handle, Func<ILuaState, Handle, int> body)
    {
        lua.PushCSharpFunction(state =>
        {
            if (state.Type(1) != LuaType.LUA_TTABLE)
                return Fail(state, $"use window:{name}(...), with a colon");
            return body(state, handle);
        });
        lua.SetField(-2, name);
    }

    /// <summary>The control or menu item named by the method's first argument, which must exist.</summary>
    private static string Name(ILuaState lua, Handle handle, string method)
    {
        if (lua.Type(2) != LuaType.LUA_TSTRING)
        {
            Fail(lua, $"window:{method} needs a control's name, such as window:{method}(\"title\", ...)");
            return "";
        }

        var name = LuaText.Decode(lua.ToString(2));
        if (!handle.Names.Contains(name))
            Fail(lua, $"{handle.Document} has no control or menu item called \"{name}\"");
        return name;
    }

    /// <summary>The grid the call's name gives, or <see langword="null"/> when it names something else.</summary>
    private static UIGridState? GridNamed(ILuaState lua, Handle handle) =>
        lua.Type(2) == LuaType.LUA_TSTRING ? handle.View.Grid(LuaText.Decode(lua.ToString(2))) : null;

    private static UIListState List(ILuaState lua, Handle handle, string method)
    {
        var name = Name(lua, handle, method);
        if (handle.View.List(name) is { } list)
            return list;

        Fail(lua, name == handle.View.SidebarName && handle.View.SidebarHoldsContent
            ? $"window:{method} needs a list, and \"{name}\" in {handle.Document} is a sidebar that holds controls, not rows"
            : $"window:{method} needs a list, and \"{name}\" in {handle.Document} isn't one");
        return null!;
    }

    private static int Fail(ILuaState lua, string message)
    {
        lua.PushString(LuaText.Encode(message));
        return lua.Error();
    }

    /// <summary>One window an app opened, and the Lua functions that handle its events.</summary>
    private sealed class Handle
    {
        private readonly LuaOwner owner;
        private readonly AppWindows host;
        private readonly string workingDirectory;

        /// <summary>The tables the window's document found its strings in, for <c>window:string</c>.</summary>
        public required StringTables Strings { get; init; }

        public Handle(LuaOwner owner, AppWindows host, UIView view, string document)
        {
            this.owner = owner;
            this.host = host;
            View = view;
            Document = document;
            workingDirectory = owner.Interpreter.WorkingDirectory ?? "/";

            foreach (var control in view.Script.Controls)
                Names.Add(control.Name);
            foreach (var visual in view.Script.Visuals)
                Names.Add(visual.Name);
            foreach (var item in view.Script.ToolbarItems)
                Names.Add(item.Name);
            if (view.Script.TitleField is { } title)
                Names.Add(title.Name);
            if (view.Script.Sidebar is { } sidebar)
                Names.Add(sidebar.Name);
            foreach (var menu in view.Script.Menus)
            {
                foreach (var entry in menu.AllEntries())
                {
                    if (entry.Name is { } item)
                        Names.Add(item);
                }
            }
        }

        public UIView View { get; }

        public DesktopWindow? Window { get; set; }

        /// <summary>The document's file name, for messages.</summary>
        public string Document { get; }

        /// <summary>The folder the document is in, which relative image paths start from.</summary>
        public string Folder { get; init; } = "/";

        /// <summary>The app that opened the window.</summary>
        public Bundle? App { get; init; }

        public AppWindows Host => host;

        public HashSet<string> Names { get; } = [];

        public Dictionary<string, int> Handlers { get; } = [];

        /// <summary><c>window:every(seconds, fn)</c>: a timer that lasts until it's stopped, the window closes, or the app quits.</summary>
        public int Every(ILuaState lua)
        {
            if (lua.Type(2) != LuaType.LUA_TNUMBER)
                return Fail(lua, "window:every needs a number of seconds, such as window:every(1, function() ... end)");

            var seconds = lua.ToNumber(2);
            if (double.IsNaN(seconds) || seconds < AppWindows.MinimumInterval.TotalSeconds || seconds > TimeSpan.FromDays(1).TotalSeconds)
                return Fail(lua, $"window:every needs from {AppWindows.MinimumInterval.TotalSeconds} seconds to a day, not {seconds}");
            if (!lua.IsFunction(3))
                return Fail(lua, "window:every needs a function to run, such as window:every(1, function() ... end)");

            lua.PushValue(3);
            var reference = lua.L_Ref(LuaDef.LUA_REGISTRYINDEX);
            var timer = host.Every(Window!, TimeSpan.FromSeconds(seconds), () =>
            {
                // An error in it is reported, as for an event, and it keeps running.
                if (!owner.IsDisposed)
                    owner.Call(reference, _ => 0, host.Output, workingDirectory, host.Stop?.Invoke());
            });
            var undo = owner.AddUndo(() =>
            {
                host.StopTimer(timer);
                owner.Interpreter.State.L_Unref(LuaDef.LUA_REGISTRYINDEX, reference);
            });

            lua.NewTable();
            lua.PushCSharpFunction(_ =>
            {
                undo.Run();
                return 0;
            });
            lua.SetField(-2, "stop");
            return 1;
        }

        public void Dispatch(UIEvent uiEvent)
        {
            if (owner.IsDisposed || !Handlers.TryGetValue(uiEvent.Control, out var reference))
                return;

            // An error in the handler is reported on the output, and the desktop carries on.
            owner.Call(reference, lua => PushEvent(lua, uiEvent), host.Output, workingDirectory, host.Stop?.Invoke());
        }

        public void Release()
        {
            host.Windows.Close(Window!);
            foreach (var reference in Handlers.Values)
                owner.Interpreter.State.L_Unref(LuaDef.LUA_REGISTRYINDEX, reference);
            Handlers.Clear();
        }

        private static int PushEvent(ILuaState lua, UIEvent uiEvent)
        {
            lua.NewTable();
            lua.PushString(uiEvent.Kind switch
            {
                UIEventKind.Pressed => "pressed",
                UIEventKind.Submitted => "submitted",
                UIEventKind.Changed => "changed",
                UIEventKind.Committed => "committed",
                _ => "selected",
            });
            lua.SetField(-2, "kind");
            lua.PushString(LuaText.Encode(uiEvent.Control));
            lua.SetField(-2, "control");

            if (uiEvent.Text is { } text)
            {
                lua.PushString(LuaText.Encode(text));
                lua.SetField(-2, "text");
            }

            if (uiEvent.Index >= 0)
            {
                lua.PushInteger(uiEvent.Index + 1);
                lua.SetField(-2, "index");
            }

            if (uiEvent.Item is { } item)
            {
                lua.PushString(LuaText.Encode(item));
                lua.SetField(-2, "item");
            }

            if (uiEvent.Value is { } value)
            {
                lua.PushNumber(value);
                lua.SetField(-2, "value");
            }

            return 1;
        }
    }
}
