using Cosmos.Executable.Lua;

namespace Venture.OS.Scripting;

/// <summary>
/// <c>venture.command</c> for code with a lasting owner: registers a command the shell can run.
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
    /// Puts <c>venture.command</c> in <paramref name="state"/>, registering into <paramref name="commands"/> for
    /// <paramref name="owner"/>.
    /// </summary>
    public static void Open(ILuaState state, LuaOwner owner, RuntimeCommands commands, ScriptScopes scopes)
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
        state.Pop(1);
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
