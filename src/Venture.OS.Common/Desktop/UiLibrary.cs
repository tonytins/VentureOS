using Cosmos.Executable.Lua;
using Venture.OS.Bundles;
using Venture.OS.Interface;
using Venture.OS.Scripting;
using Venture.OS.Shell;

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
    private readonly List<WindowTimer> timers = [];

    // The timers as a tick began, reused, since a timer's code may start or stop others while the tick runs.
    private readonly List<WindowTimer> ticking = [];

    /// <summary>The desktop's windows.</summary>
    public WindowManager Windows { get; } = windows ?? throw new ArgumentNullException(nameof(windows));

    /// <summary>Where event handlers write.</summary>
    public IShellOutput Output { get; } = output ?? throw new ArgumentNullException(nameof(output));

    /// <summary>Makes the stop for each event handler, or <see langword="null"/> for none.</summary>
    public Func<Func<string?>>? Stop { get; } = stop;

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
                if (window.OpenedByApp && window.App == app && window.IsMinimized == minimized)
                {
                    Windows.Open(window);
                    return;
                }
            }
        }

        NewWindow(app);
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
        var ran = false;
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
    public string? Preview(string path)
    {
        if (!QuickLook.TryCreate(path, out var window, out var problem))
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
/// local window = venture.ui.open("main.uix", { title = "Notes" })  -- title is optional
/// window:on("add", function(event) ... end)    -- a control's or menu item's events
/// window:setText("count", "3 notes")
/// window:setItems("notes", { "Milk", "Eggs" })
/// window:close()
/// venture.ui.preview("/mnt/notes.txt")          -- Quick Look; nil and a message when it can't
/// venture.ui.launch("/mnt/system/apps/Notes.app") -- opens an app, or brings an open one forward
/// venture.ui.onNewWindow(function() ... end)   -- New Window (Ctrl+N), and the dock with no windows open
/// </code>
/// <para>A relative path is in the app's <c>Contents/Resources/</c>. The window belongs to the app: quitting the
/// app closes it, and closing the app's last window quits the app. Event handlers get a table with
/// <c>kind</c> (<c>pressed</c>, <c>submitted</c>, <c>changed</c>, or <c>selected</c>), <c>control</c>, and, when they
/// apply, <c>text</c>, <c>value</c>, and <c>index</c> (counting from 1). A chosen menu item is <c>pressed</c>.</para>
/// <para>Methods take a control by name and raise a Lua error for a name the document doesn't have, so a typo
/// shows at once instead of silently doing nothing. They need the colon: <c>window:setText(...)</c>.</para>
/// <list type="bullet">
/// <item><description><c>text(name)</c>, <c>setText(name, text)</c>: a control's text.</description></item>
/// <item><description><c>value(name)</c>, <c>setValue(name, number)</c>: a slider's or progress bar's value.</description></item>
/// <item><description><c>items(name)</c>, <c>setItems(name, list)</c>, <c>selected(name)</c>,
/// <c>select(name, index)</c>: a list's items and its selection, counting from 1, or <c>nil</c> for none.</description></item>
/// <item><description><c>setHidden</c>, <c>setDisabled</c>, <c>setChecked(name, on)</c>: hide, disable, or check a
/// control or menu item.</description></item>
/// <item><description><c>focus(name)</c>, <c>setTitle(text)</c>, <c>close()</c>. Focusing the title field starts an
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

    /// <summary>A <c>venture.ui</c> function, or one that explains the missing scope or desktop.</summary>
    private static void Function(ILuaState state, string name, Bundle app, AppWindows? host, Func<ILuaState, AppWindows, int> body)
    {
        if ((app.Needs & ScriptScopes.Ui) == 0)
            state.PushCSharpFunction(lua => Fail(lua, VentureLibrary.UndeclaredMessage($"ui.{name}", ScriptScopes.Ui)));
        else if (host is null)
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
        var path = FullPath(lua, owner, "launch");
        if (apps is null)
            return Fail(lua, "venture.ui.launch needs the system's apps, which aren't available");

        Bundle bundle;
        try
        {
            bundle = Bundle.Read(path, BundleKind.App);
        }
        catch (BundleException exception)
        {
            lua.PushNil();
            lua.PushString(LuaText.Encode(exception.Message));
            return 2;
        }

        // An open app comes forward instead, as it does from the dock.
        if (apps.IsOpen(bundle.Identifier))
        {
            host.Activate(bundle.Name);
            lua.PushBoolean(true);
            return 1;
        }

        // What the app says while it opens, including why it didn't, goes to the desktop's messages.
        if (apps.OpenApp(path, owner.Interpreter.WorkingDirectory ?? "/", host.Output, explainNeeds: false))
        {
            lua.PushBoolean(true);
            return 1;
        }

        lua.PushNil();
        lua.PushString(LuaText.Encode($"{bundle.Name} didn't open"));
        return 2;
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

        var title = app.Name;
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

        Handle handle = new(owner, host, new UIView(script), Path.GetFileName(path));
        handle.Window = new DesktopWindow(title, handle.View)
        {
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
            var list = List(state, h, "selected");
            if (list.SelectedIndex >= 0)
                state.PushInteger(list.SelectedIndex + 1);
            else
                state.PushNil();
            return 1;
        });
        Method(lua, "select", handle, (state, h) =>
        {
            var list = List(state, h, "select");
            if (state.IsNoneOrNil(3))
            {
                list.SelectedIndex = -1;
                return 0;
            }

            var index = state.L_CheckInteger(3);
            if (index < 1 || index > list.Items.Count)
                return Fail(state, $"window:select: there's no item {index} in a list of {list.Items.Count}");

            list.SelectedIndex = (int)index - 1;
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
            h.Window!.Title = LuaText.Decode(state.L_CheckString(2));
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

    private static int SetItems(ILuaState lua, Handle handle)
    {
        var list = List(lua, handle, "setItems");
        if (lua.Type(3) != LuaType.LUA_TTABLE)
            return Fail(lua, "window:setItems needs a list of items, such as { \"Milk\", \"Eggs\" }");

        List<string> items = [];
        var count = lua.RawLen(3);
        for (var index = 1; index <= count; index++)
        {
            lua.RawGetI(3, index);
            var item = lua.L_ToString(-1);
            lua.Pop(2); // L_ToString pushes its result on top of the value.
            items.Add(LuaText.Decode(item));
        }

        list.Items.Clear();
        list.Items.AddRange(items);
        if (list.SelectedIndex >= items.Count)
            list.SelectedIndex = -1;
        return 0;
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

    private static UIListState List(ILuaState lua, Handle handle, string method)
    {
        var name = Name(lua, handle, method);
        if (handle.View.List(name) is { } list)
            return list;

        Fail(lua, $"window:{method} needs a list, and \"{name}\" in {handle.Document} isn't one");
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
            foreach (var menu in view.Script.Menus)
            {
                foreach (var entry in menu.Entries)
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

            if (uiEvent.Kind == UIEventKind.Changed)
            {
                lua.PushNumber(uiEvent.Value);
                lua.SetField(-2, "value");
            }

            if (uiEvent.Index >= 0)
            {
                lua.PushInteger(uiEvent.Index + 1);
                lua.SetField(-2, "index");
            }

            return 1;
        }
    }
}
