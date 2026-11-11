using Cosmos.Executable.Lua;

namespace Venture.OS.Scripting;

/// <summary>
/// The <c>venture</c> table: what Lua scripts can ask the kernel.
/// </summary>
/// <remarks>
/// <para>Each function returns plain values or tables, so scripts decide how to show them:</para>
/// <list type="bullet">
/// <item><description><c>venture.version()</c>: <c>{ name, version, build, commit, cosmos, display }</c>.</description></item>
/// <item><description><c>venture.uptime()</c>: whole seconds since the CPU started counting, or <c>nil</c>.</description></item>
/// <item><description><c>venture.memory()</c>: <c>{ free, total }</c> in bytes, or <c>nil</c>.</description></item>
/// <item><description><c>venture.net()</c>: <c>nil</c> without a network adapter, otherwise
/// <c>{ device, mac, link_up, ready, address, subnet, gateway }</c>, where the last three are <c>nil</c> until
/// IPv4 is configured.</description></item>
/// </list>
/// <para>The functions take no arguments and ignore any they're given. An exception from
/// <see cref="ISystemInfo"/> becomes a Lua error, which <c>pcall</c> can catch, so a failing device can't stop the
/// system.</para>
/// </remarks>
public static class VentureLibrary
{
    /// <summary>The global name scripts use.</summary>
    public const string GlobalName = "venture";

    /// <summary>
    /// Adds the <c>venture</c> table to <paramref name="state"/>'s globals.
    /// </summary>
    /// <remarks>
    /// Every function is in the table, so a script can see what exists. One outside <paramref name="scopes"/> raises
    /// a Lua error that names the <c>-- Needs:</c> line the script is missing.
    /// </remarks>
    /// <param name="scopes">What the code may reach besides <c>venture.version()</c>.</param>
    public static void Open(ILuaState state, ISystemInfo system, ScriptScopes scopes = ScriptScopes.All)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(system);

        state.L_NewLib(
        [
            new("version", lua => PushVersion(lua, system)),
            Gated("uptime", ScriptScopes.System, scopes, lua => PushUptime(lua, system)),
            Gated("memory", ScriptScopes.System, scopes, lua => PushMemory(lua, system)),
            Gated("net", ScriptScopes.Network, scopes, lua => PushNetwork(lua, system)),

            // Replaced by ShellLibrary where the code has a lasting owner; elsewhere it explains why it can't work.
            Gated("command", ScriptScopes.Shell, scopes, lua =>
            {
                lua.PushString(LuaText.Encode(NotOwnedMessage));
                return lua.Error();
            }),
        ]);
        state.SetGlobal(GlobalName);
    }

    /// <summary>The error for <c>venture.command</c> in code that ends when it finishes, such as a script command.</summary>
    public const string NotOwnedMessage =
        "venture.command only works in init.lua and at the lua prompt, where commands can outlive the code that registers them";

    /// <summary>The error a script gets for calling <paramref name="function"/> without declaring its scope.</summary>
    public static string UndeclaredMessage(string function, ScriptScopes scope) =>
        $"venture.{function} needs \"-- Needs: {ScriptScopeNames.Name(scope)}\" in the script's header";

    private static NameFuncPair Gated(
        string name, ScriptScopes scope, ScriptScopes granted, CSharpFunctionDelegate function) =>
        (granted & scope) != 0
            ? new(name, function)
            : new(name, lua =>
            {
                // Pushed rather than passed to L_Error, which would treat the message as a format string.
                lua.PushString(LuaText.Encode(UndeclaredMessage(name, scope)));
                return lua.Error();
            });

    private static int PushVersion(ILuaState lua, ISystemInfo system)
    {
        lua.NewTable();
        SetString(lua, "name", system.Name);
        SetString(lua, "version", system.Version);
        SetInteger(lua, "build", system.BuildNumber);
        SetString(lua, "commit", system.Commit);
        SetString(lua, "cosmos", system.CosmosVersion);
        SetString(lua, "display", $"{system.Name} {system.Version} (Build {system.BuildNumber})");
        return 1;
    }

    private static int PushUptime(ILuaState lua, ISystemInfo system)
    {
        if (system.Uptime is { } uptime && uptime >= TimeSpan.Zero)
            lua.PushInteger((long)uptime.TotalSeconds);
        else
            lua.PushNil();

        return 1;
    }

    private static int PushMemory(ILuaState lua, ISystemInfo system)
    {
        if (system.Memory is not { } memory || memory.FreeBytes > memory.TotalBytes)
        {
            lua.PushNil();
            return 1;
        }

        lua.NewTable();
        SetInteger(lua, "free", memory.FreeBytes);
        SetInteger(lua, "total", memory.TotalBytes);
        return 1;
    }

    private static int PushNetwork(ILuaState lua, ISystemInfo system)
    {
        if (system.Network is not { } network)
        {
            lua.PushNil();
            return 1;
        }

        lua.NewTable();
        SetString(lua, "device", network.Device);
        SetString(lua, "mac", network.MacAddress);
        SetBoolean(lua, "link_up", network.LinkUp);
        SetBoolean(lua, "ready", network.Ready);
        SetString(lua, "address", network.Address);
        SetString(lua, "subnet", network.SubnetMask);
        SetString(lua, "gateway", network.Gateway);
        return 1;
    }

    // Each helper sets one field on the table at the top of the stack. A null string leaves the field nil.

    private static void SetString(ILuaState lua, string key, string? value)
    {
        if (value is null)
            return;

        // Lua strings are bytes; LuaText turns the text into UTF-8 the way the interpreter expects.
        lua.PushString(LuaText.Encode(value));
        lua.SetField(-2, key);
    }

    private static void SetInteger(ILuaState lua, string key, ulong value)
    {
        // Lua integers are signed 64-bit; a larger value (no real memory size) becomes a float rather than wrapping.
        if (value <= long.MaxValue)
            lua.PushInteger((long)value);
        else
            lua.PushNumber(value);

        lua.SetField(-2, key);
    }

    private static void SetBoolean(ILuaState lua, string key, bool value)
    {
        lua.PushBoolean(value);
        lua.SetField(-2, key);
    }
}
