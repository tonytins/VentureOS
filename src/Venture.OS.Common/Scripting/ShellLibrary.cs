using Cosmos.Executable.Lua;
using Venture.OS.Shell;

namespace Venture.OS.Scripting;

/// <summary>
/// <c>venture.command</c> and <c>venture.shell</c> for code with a lasting owner: register a command the shell can
/// run, and run a line as if typed at the shell.
/// </summary>
/// <remarks>
/// <code>
/// local greet = venture.command {
///   name = "greet",
///   summary = "Greet someone.",   -- optional
///   usage = "greet &lt;name&gt;",       -- optional
///   run = function(...) print("Hello, " .. (... or "world") .. ".") end,
/// }
/// greet:remove()                  -- optional: undo it before the owner ends
/// </code>
/// <para>The command lasts until its owner ends or it's removed. It needs the <c>shell</c> scope. A name it can't
/// take raises a Lua error with the reason; see <see cref="RuntimeCommands"/>.</para>
/// </remarks>
public static class ShellLibrary
{
    /// <summary>
    /// Puts <c>venture.command</c>, and <c>venture.shell</c> when there's a shell to run, in
    /// <paramref name="state"/>, registering commands into <paramref name="commands"/> for <paramref name="owner"/>.
    /// </summary>
    /// <param name="runShell">
    /// Runs a line as if typed at the shell, for <c>venture.shell</c>, or <see langword="null"/> to leave it out.
    /// </param>
    /// <param name="openShell">
    /// Makes a shell of the code's own, for <c>venture.openShell</c>, or <see langword="null"/> to leave it out.
    /// </param>
    public static void Open(
        ILuaState state,
        LuaOwner owner,
        RuntimeCommands commands,
        ScriptScopes scopes,
        Action<string>? runShell = null,
        Func<IShellOutput, string, ShellContext>? openShell = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(commands);

        if (state.GetGlobal(VentureLibrary.GlobalName) != LuaType.LUA_TTABLE)
        {
            state.Pop(1);
            state.NewTable();
            state.PushValue(-1);
            state.SetGlobal(VentureLibrary.GlobalName);
        }

        if ((scopes & ScriptScopes.Shell) != 0)
            state.PushCSharpFunction(lua => Command(lua, owner, commands));
        else
            state.PushCSharpFunction(lua => Fail(lua, VentureLibrary.UndeclaredMessage("command", ScriptScopes.Shell)));

        state.SetField(-2, "command");

        if (runShell is not null)
        {
            if ((scopes & ScriptScopes.Shell) != 0)
                state.PushCSharpFunction(lua => Shell(lua, runShell));
            else
                state.PushCSharpFunction(lua => Fail(lua, VentureLibrary.UndeclaredMessage("shell", ScriptScopes.Shell)));

            state.SetField(-2, "shell");
        }

        if (openShell is not null)
        {
            if ((scopes & ScriptScopes.Shell) != 0)
                state.PushCSharpFunction(lua => OpenShell(lua, owner, openShell));
            else
                state.PushCSharpFunction(lua => Fail(lua, VentureLibrary.UndeclaredMessage("openShell", ScriptScopes.Shell)));

            state.SetField(-2, "openShell");
        }

        state.Pop(1);
    }

    /// <summary>
    /// <c>venture.openShell()</c>: a shell of the code's own, as Terminal has, with its own current folder. Its
    /// output is caught rather than printed: <c>session:run(line)</c> returns what the command printed, as one string,
    /// and whether it cleared the screen, and <c>session:directory()</c> returns its current folder.
    /// </summary>
    private static int OpenShell(ILuaState lua, LuaOwner owner, Func<IShellOutput, string, ShellContext> openShell)
    {
        CaughtOutput caught = new();
        var context = openShell(caught, owner.Interpreter.WorkingDirectory ?? "/");
        CommandShell shell = new(context);

        lua.NewTable();
        lua.PushCSharpFunction(state =>
        {
            if (state.Type(1) != LuaType.LUA_TTABLE || state.Type(2) != LuaType.LUA_TSTRING)
                return Fail(state, "use session:run(line), with a colon and a command line, such as session:run(\"ls\")");

            if (shellDepth >= MaxShellDepth)
                return Fail(state, $"session:run is nested more than {MaxShellDepth} deep; is a command running itself?");

            var line = LuaText.Decode(state.ToString(2));
            caught.Reset();
            shellDepth++;
            try
            {
                owner.WithoutStop(() =>
                {
                    try
                    {
                        shell.Execute(line);
                    }
                    catch (Exception exception)
                    {
                        // Nothing under this shell catches for it, as the kernel's shell loop does on the console.
                        caught.WriteLine($"{exception.GetType().Name}: {exception.Message}");
                    }
                });
            }
            finally
            {
                shellDepth--;
            }

            state.PushString(LuaText.Encode(caught.Text));
            state.PushBoolean(caught.Cleared);
            return 2;
        });
        lua.SetField(-2, "run");
        lua.PushCSharpFunction(state =>
        {
            state.PushString(LuaText.Encode(context.CurrentDirectory));
            return 1;
        });
        lua.SetField(-2, "directory");
        return 1;
    }

    /// <summary>What one command run in a session printed, and whether it cleared the screen first.</summary>
    private sealed class CaughtOutput : IShellOutput
    {
        private readonly List<string> lines = [];

        public bool Cleared { get; private set; }

        public string Text => string.Join('\n', lines);

        public void Reset()
        {
            lines.Clear();
            Cleared = false;
        }

        public void WriteLine(string text) => lines.Add(text ?? "");

        public void Clear()
        {
            lines.Clear();
            Cleared = true;
        }
    }

    /// <summary>How deeply <c>venture.shell</c> calls may nest, such as a command that runs itself.</summary>
    /// <remarks>
    /// Each level is a C# call into the shell and back into Lua, and a .NET stack overflow can't be caught, so it
    /// would stop the machine. Lua's own recursion is bounded by Lua and reported as an error.
    /// </remarks>
    public const int MaxShellDepth = 8;

    // Per thread: the kernel runs on one, and tests run on several at once.
    [ThreadStatic]
    private static int shellDepth;

    private static int Shell(ILuaState lua, Action<string> runShell)
    {
        if (lua.Type(1) != LuaType.LUA_TSTRING)
            return Fail(lua, "venture.shell needs a command line, such as venture.shell(\"help\")");

        if (shellDepth >= MaxShellDepth)
            return Fail(lua, $"venture.shell is nested more than {MaxShellDepth} deep; is a command running itself?");

        var line = LuaText.Decode(lua.ToString(1));
        shellDepth++;
        try
        {
            runShell(line);
        }
        finally
        {
            shellDepth--;
        }

        return 0;
    }

    private static int Command(ILuaState lua, LuaOwner owner, RuntimeCommands commands)
    {
        if (lua.Type(1) != LuaType.LUA_TTABLE)
            return Fail(lua, "venture.command needs a table, such as venture.command { name = ..., run = ... }");

        var name = ReadString(lua, "name");
        if (string.IsNullOrEmpty(name))
            return Fail(lua, "venture.command needs a name");

        var summary = ReadString(lua, "summary");
        var usage = ReadString(lua, "usage");

        lua.GetField(1, "run");
        if (!lua.IsFunction(-1))
            return Fail(lua, $"venture.command needs a run function for \"{name}\"");

        // Pops the function into the registry, where it stays until the command's undo releases it.
        var reference = lua.L_Ref(LuaDef.LUA_REGISTRYINDEX);
        var error = commands.Register(owner, name, summary, usage, reference, out var command);
        if (error is not null)
        {
            lua.L_Unref(LuaDef.LUA_REGISTRYINDEX, reference);
            return Fail(lua, error);
        }

        if (command!.ReplacesScript)
            owner.Interpreter.Output?.WriteLine($"{name} now replaces the {name} script until {owner.Lifetime}.");

        var undo = command.Undo!;
        lua.NewTable();
        lua.PushString(LuaText.Encode(command.Name));
        lua.SetField(-2, "name");
        lua.PushCSharpFunction(_ =>
        {
            undo.Run();
            return 0;
        });
        lua.SetField(-2, "remove");

        // A readable form, since the lua prompt prints the handle a registration returns.
        lua.NewTable();
        var text = LuaText.Encode($"command \"{command.Name}\"");
        lua.PushCSharpFunction(state =>
        {
            state.PushString(text);
            return 1;
        });
        lua.SetField(-2, "__tostring");
        lua.SetMetaTable(-2);
        return 1;
    }

    /// <summary>Reads a string field of the table at index 1, or <see langword="null"/> when it isn't a string.</summary>
    private static string? ReadString(ILuaState lua, string key)
    {
        lua.GetField(1, key);
        var value = lua.Type(-1) == LuaType.LUA_TSTRING ? LuaText.Decode(lua.ToString(-1)) : null;
        lua.Pop(1);
        return value;
    }

    private static int Fail(ILuaState lua, string message)
    {
        lua.PushString(LuaText.Encode(message));
        return lua.Error();
    }
}
