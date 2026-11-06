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
    public static void Open(ILuaState state, ISystemInfo system)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(system);

        state.L_NewLib(
        [
            new("version", lua => PushVersion(lua, system)),
            new("uptime", lua => PushUptime(lua, system)),
            new("memory", lua => PushMemory(lua, system)),
            new("net", lua => PushNetwork(lua, system)),
        ]);
        state.SetGlobal(GlobalName);
    }

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
