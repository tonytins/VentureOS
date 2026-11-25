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
        Add(lua, "press", state => Done(state, "press", WindowControls.TryPress(Front(state, app, "press"), Name(state), out var problem), problem));
        Add(lua, "submit", state =>
        {
            var window = Front(state, app, "submit");
            var text = LuaText.Decode(state.L_CheckString(3));
            return Done(state, "submit", WindowControls.TrySubmit(window, Name(state), text, out var problem), problem);
        });
        Add(lua, "type", state =>
        {
            var window = Front(state, app, "type");
            var text = LuaText.Decode(state.L_CheckString(3));
            return Done(state, "type", WindowControls.TryType(window, Name(state), text, out var problem), problem);
        });
        Add(lua, "choose", state =>
        {
            var window = Front(state, app, "choose");
            var name = Name(state);

            // A row or item is chosen by its number, from 1, by its text, as a person reads it, or by a grid item's name.
            string? problem;
            var chose = state.Type(3) == LuaType.LUA_TSTRING
                ? WindowControls.TryChoose(window, name, LuaText.Decode(state.ToString(3)), out problem)
                : WindowControls.TryChoose(window, name, Row(state), out problem);
            return Done(state, "choose", chose, problem);
        });

        Add(lua, "slide", state =>
        {
            var window = Front(state, app, "slide");
            var value = (float)state.L_CheckNumber(3);
            return Done(state, "slide", WindowControls.TrySlide(window, Name(state), value, out var problem), problem);
        });

        Add(lua, "has", state =>
        {
            var window = Front(state, app, "has");
            return PushBoolean(state, WindowControls.Has(window, Name(state)));
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
            var rows = ChoicesNamed(state, app, "rows").Rows;
            state.CreateTable(rows.Count, 0);
            for (var index = 0; index < rows.Count; index++)
            {
                state.PushString(LuaText.Encode(rows[index]));
                state.RawSetI(-2, index + 1);
            }

            return 1;
        });
        Add(lua, "chosen", state =>
        {
            if (ChoicesNamed(state, app, "chosen").Chosen is { } chosen)
                state.PushInteger(chosen);
            else
                state.PushNil();
            return 1;
        });
        Add(lua, "pane", state => PushText(state, Front(state, app, "pane").View.SelectedPane?.Name));
        Add(lua, "title", state => PushText(state, Front(state, app, "title") is { ShowsTitle: true } window ? window.Title : null));
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

    // The front window, once the control named by the call's first argument is in it.
    private static DesktopWindow Named(ILuaState state, AppUnderTest app, string function)
    {
        var window = Front(state, app, function);
        if (!WindowControls.TryFind(window, Name(state), out var problem))
            throw Error(state, $"app:{function}: {problem}");
        return window;
    }

    private static WindowChoices ChoicesNamed(ILuaState state, AppUnderTest app, string function) =>
        WindowControls.Choices(Named(state, app, function), Name(state))
            ?? throw Error(state, $"app:{function}: \"{Name(state)}\" isn't a list or a grid");

    // A row's number, which Lua may give as any integer; one past an int's range is no row any list has.
    private static int Row(ILuaState state)
    {
        var row = state.L_CheckInteger(3);
        return row is < int.MinValue or > int.MaxValue ? 0 : (int)row;
    }

    // An action's end: nothing to return when it was done, else Lua's error with the reason.
    private static int Done(ILuaState state, string function, bool done, string? problem) =>
        done ? 0 : Fail(state, $"app:{function}: {problem}");

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
