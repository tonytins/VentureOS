using Cosmos.Executable.Lua;
using Venture.OS.Desktop;

namespace Venture.Airo.Testing;

/// <summary>
/// The Lua a project's tests are written with: the <c>test</c> table, which collects cases and checks values, and the
/// <c>app</c> each case is handed, which uses the app's front window as a person would. See
/// <c>docs/airo/testing.md</c>.
/// </summary>
/// <remarks>
/// <para>Checks fail with <c>error(message, 2)</c>, so a failure names the test file's line, not this library's.</para>
/// <para>An <c>app</c> is a table of functions over one <see cref="AppUnderTest"/>, made fresh for each case. Each
/// acts on whichever window is in front when it's called, so a window the app opens is the one the next call
/// uses. A control that isn't there, or that a person couldn't use, such as a dimmed button, is an error rather than
/// <c>nil</c>, since that's a mistake in the test or a bug in the app.</para>
/// </remarks>
public static class TestLibrary
{
    /// <summary>The global the cases are collected in, which <see cref="ProjectTests"/> reads after a file runs.</summary>
    internal const string CasesGlobal = "__airo_cases";

    /// <summary>The global that wraps an <c>app</c>'s functions, so their errors name the test's line too.</summary>
    internal const string WrapGlobal = "__airo_wrap";

    // test.case, test.expect, and test.equal, in Lua so their errors point at the caller's line.
    private static readonly string Prelude = $$"""
        local cases = {}
        {{CasesGlobal}} = cases

        local function show(value)
          if type(value) == "string" then
            return string.format("%q", value)
          end
          return tostring(value)
        end

        test = {}

        function test.case(name, run)
          if type(name) ~= "string" or name == "" or type(run) ~= "function" then
            error('test.case needs a name and a function, such as test.case("opens", function(app) ... end)', 2)
          end
          cases[#cases + 1] = { name = name, run = run }
        end

        function test.expect(value, message)
          if not value then
            error(message or ("expected something true, got " .. show(value)), 2)
          end
        end

        -- The app's functions are C#, whose errors say no line; each is called through pcall and its error raised
        -- again from the test's line, as test.expect's are.
        function {{WrapGlobal}}(raw)
          local app = {}
          for name, run in pairs(raw) do
            app[name] = function(self, ...)
              local results = table.pack(pcall(run, self, ...))
              if not results[1] then
                error(results[2], 2)
              end
              return table.unpack(results, 2, results.n)
            end
          end
          return app
        end

        function test.equal(actual, expected, message)
          if actual ~= expected then
            local said = "expected " .. show(expected) .. ", got " .. show(actual)
            error(message and (message .. ": " .. said) or said, 2)
          end
        end
        """;

    /// <summary>Puts the <c>test</c> table in <paramref name="lua"/>, before a test file runs.</summary>
    internal static void Open(LuaInterpreter lua)
    {
        ArgumentNullException.ThrowIfNull(lua);
        lua.DoString(Prelude, "=airo");
    }

    /// <summary>Pushes the <c>app</c> a case is handed, over <paramref name="app"/>.</summary>
    internal static void PushApp(ILuaState lua, AppUnderTest app)
    {
        ArgumentNullException.ThrowIfNull(lua);
        ArgumentNullException.ThrowIfNull(app);

        lua.GetGlobal(WrapGlobal);
        PushFunctions(lua, app);
        lua.Call(1, 1);
    }

    private static void PushFunctions(ILuaState lua, AppUnderTest app)
    {
        lua.NewTable();
        Add(lua, "press", state =>
        {
            var (window, name) = Usable(state, app, "press");
            app.Send(window, new UIEvent(UIEventKind.Pressed, name));
            return 0;
        });
        Add(lua, "submit", state =>
        {
            var (window, name) = Usable(state, app, "submit");
            var text = LuaText.Decode(state.L_CheckString(3));
            window.View.SetText(name, text);
            app.Send(window, new UIEvent(UIEventKind.Submitted, name, text));
            return 0;
        });
        Add(lua, "type", state =>
        {
            var (window, name) = Usable(state, app, "type");
            var text = LuaText.Decode(state.L_CheckString(3));
            window.View.SetText(name, text);
            app.Send(window, new UIEvent(UIEventKind.Changed, name, text));
            return 0;
        });
        Add(lua, "choose", state =>
        {
            var (window, name) = Usable(state, app, "choose");
            if (window.View.List(name) is not { } list)
                return Fail(state, $"app:choose: \"{name}\" isn't a list");

            // A row is chosen by its number, from 1, or by its text, as a person reads it.
            long index;
            if (state.Type(3) == LuaType.LUA_TSTRING)
            {
                var text = LuaText.Decode(state.ToString(3));
                index = list.Items.IndexOf(text) + 1;
                if (index == 0)
                    return Fail(state, $"app:choose: \"{name}\" has no row \"{text}\"; its rows are {string.Join(", ", list.Items)}");
            }
            else
            {
                index = state.L_CheckInteger(3);
                if (index < 1 || index > list.Items.Count)
                    return Fail(state, $"app:choose: \"{name}\" has {list.Items.Count} rows, so there's no row {index}");
            }

            // As a click on a row does: the list selects it, then reports it.
            list.SelectedIndex = (int)index - 1;
            app.Send(window, new UIEvent(UIEventKind.Selected, name, Index: (int)index - 1));
            return 0;
        });
        Add(lua, "slide", state =>
        {
            var (window, name) = Usable(state, app, "slide");
            var value = (float)state.L_CheckNumber(3);
            window.View.SetValue(name, value);
            app.Send(window, new UIEvent(UIEventKind.Changed, name, Value: value));
            return 0;
        });

        Add(lua, "has", state =>
        {
            var window = Front(state, app, "has");
            return PushBoolean(state, Has(window, Name(state)));
        });
        Add(lua, "text", state => PushText(state, Named(state, app, "text").View.Text(Name(state))));
        Add(lua, "checked", state => PushBoolean(state, Named(state, app, "checked").View.IsSelected(Name(state))));
        Add(lua, "dimmed", state => PushBoolean(state, Named(state, app, "dimmed").View.IsDisabled(Name(state))));
        Add(lua, "showing", state => PushBoolean(state, Named(state, app, "showing").View.IsShowing(Name(state))));
        Add(lua, "value", state =>
        {
            if (Named(state, app, "value").View.Value(Name(state)) is { } value)
                state.PushNumber(value);
            else
                state.PushNil();
            return 1;
        });
        Add(lua, "rows", state =>
        {
            var list = List(state, app, "rows");
            state.CreateTable(list.Items.Count, 0);
            for (var index = 0; index < list.Items.Count; index++)
            {
                state.PushString(LuaText.Encode(list.Items[index]));
                state.RawSetI(-2, index + 1);
            }

            return 1;
        });
        Add(lua, "chosen", state =>
        {
            var list = List(state, app, "chosen");
            if (list.SelectedIndex >= 0)
                state.PushInteger(list.SelectedIndex + 1);
            else
                state.PushNil();
            return 1;
        });
        Add(lua, "pane", state => PushText(state, Front(state, app, "pane").View.SelectedPane?.Name));
        Add(lua, "title", state => PushText(state, Front(state, app, "title").Title));
        Add(lua, "subtitle", state => PushText(state, Front(state, app, "subtitle").View.Subtitle));
        Add(lua, "focused", state => PushText(state, Front(state, app, "focused").View.FocusedControl));
        Add(lua, "newWindow", state => PushBoolean(state, app.NewWindow()));
        Add(lua, "open", state => PushBoolean(state, app.OpenFile(LuaText.Decode(state.L_CheckString(2)))));
        Add(lua, "wait", state =>
        {
            var seconds = state.L_CheckNumber(2);
            if (!(seconds >= 0 && seconds <= 3600))
                return Fail(state, $"app:wait takes 0 to 3600 seconds, not {seconds}");
            app.Wait(TimeSpan.FromSeconds(seconds));
            return 0;
        });
        Add(lua, "command", state =>
        {
            var lines = app.Command(LuaText.Decode(state.L_CheckString(2)));
            state.CreateTable(lines.Count, 0);
            for (var index = 0; index < lines.Count; index++)
            {
                state.PushString(LuaText.Encode(lines[index]));
                state.RawSetI(-2, index + 1);
            }

            return 1;
        });
        Add(lua, "windows", state =>
        {
            state.PushInteger(app.Windows.Count);
            return 1;
        });
        Add(lua, "printed", state =>
        {
            state.CreateTable(app.PrintedLines.Count, 0);
            for (var index = 0; index < app.PrintedLines.Count; index++)
            {
                state.PushString(LuaText.Encode(app.PrintedLines[index]));
                state.RawSetI(-2, index + 1);
            }

            return 1;
        });
        Add(lua, "write", state =>
        {
            var path = LuaText.Decode(state.L_CheckString(2));
            var text = LuaText.Decode(state.L_CheckString(3));
            var host = HostPath(state, app, path, "write");
            Directory.CreateDirectory(Path.GetDirectoryName(host)!);
            File.WriteAllText(host, text);
            return 0;
        });
        Add(lua, "read", state =>
        {
            var path = LuaText.Decode(state.L_CheckString(2));
            var host = HostPath(state, app, path, "read");

            if (!File.Exists(host))
            {
                state.PushNil();
                state.PushString(LuaText.Encode($"there's no file at {path}"));
                return 2;
            }

            state.PushString(LuaText.Encode(File.ReadAllText(host)));
            return 1;
        });
    }

    private static void Add(ILuaState lua, string name, Func<ILuaState, int> function)
    {
        lua.PushCSharpFunction(state => function(state));
        lua.SetField(-2, name);
    }

    // The window in front, which every call acts on.
    private static DesktopWindow Front(ILuaState state, AppUnderTest app, string function) =>
        app.FrontWindow ?? throw Error(state, $"app:{function}: the app has no window open");

    // Where a VentureOS path is on the test's copy, refusing one that isn't absolute or climbs out.
    private static string HostPath(ILuaState state, AppUnderTest app, string path, string function)
    {
        try
        {
            return app.Disk.HostPath(path);
        }
        catch (ArgumentException)
        {
            throw Error(state, $"app:{function} needs an absolute VentureOS path, such as \"/mnt/user/notes.txt\", not \"{path}\"");
        }
    }

    // Whether the window has a control, list, menu item, toolbar item, or title field of that name.
    private static bool Has(DesktopWindow window, string name) =>
        window.View.Bounds(name) is not null || window.View.List(name) is not null || InBars(window, name);

    // Whether it's in the menu bar or the title bar, which are always there while the window is, rather than among
    // the window's controls.
    private static bool InBars(DesktopWindow window, string name)
    {
        var script = window.View.Script;
        if (script.TitleField?.Name == name)
            return true;
        foreach (var item in script.ToolbarItems)
        {
            if (item.Name == name)
                return true;
        }

        foreach (var menu in script.Menus)
        {
            foreach (var entry in menu.AllEntries())
            {
                if (entry.Name == name)
                    return true;
            }
        }

        return false;
    }

    // The front window, once the control named by the call's first argument is in it.
    private static DesktopWindow Named(ILuaState state, AppUnderTest app, string function)
    {
        var window = Front(state, app, function);
        var name = Name(state);
        if (!Has(window, name))
            throw Error(state, $"app:{function}: there's no \"{name}\" in {window.Title}");
        return window;
    }

    // The front window and the control a person is about to use, which has to be showing and not dimmed.
    private static (DesktopWindow Window, string Name) Usable(ILuaState state, AppUnderTest app, string function)
    {
        var window = Named(state, app, function);
        var name = Name(state);

        // The menu bar and title bar are always there, so only dimming counts for what's in them.
        if (!InBars(window, name) && !window.View.IsShowing(name))
            throw Error(state, $"app:{function}: \"{name}\" isn't showing, so a person couldn't use it");
        if (window.View.IsDisabled(name))
            throw Error(state, $"app:{function}: \"{name}\" is dimmed, so a person couldn't use it");
        return (window, name);
    }

    private static UIListState List(ILuaState state, AppUnderTest app, string function) =>
        Named(state, app, function).View.List(Name(state)) ?? throw Error(state, $"app:{function}: \"{Name(state)}\" isn't a list");

    // The control's name: the argument after the app itself, since these are called as app:press("ok").
    private static string Name(ILuaState state) => LuaText.Decode(state.L_CheckString(2));

    private static int PushText(ILuaState state, string? text)
    {
        if (text is null)
            state.PushNil();
        else
            state.PushString(LuaText.Encode(text));
        return 1;
    }

    private static int PushBoolean(ILuaState state, bool value)
    {
        state.PushBoolean(value);
        return 1;
    }

    private static int Fail(ILuaState state, string message)
    {
        state.PushString(LuaText.Encode(message));
        return state.Error();
    }

    // Lua's error, raised from inside a helper. Error() unwinds at once, as the rest of VentureOS's libraries rely on,
    // so the exception is only there to tell the compiler the helper doesn't return.
    private static LuaException Error(ILuaState state, string message)
    {
        state.PushString(LuaText.Encode(message));
        state.Error();
        return new LuaException(message);
    }
}
