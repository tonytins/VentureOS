using Cosmos.Executable.Lua;
using Venture.OS.Network;
using Venture.OS.Shell;
using Venture.OS.Time;
using Venture.OS.Vdb;

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
/// <item><description><c>venture.appearance()</c>: the desktop's look, <c>{ mode, dark, accent, corners, wallpaper,
/// darkFrom, darkUntil, dock }</c>, and <c>venture.setAppearance(mode, accent)</c> or <c>venture.setAppearance{ ... }</c>
/// with any of those but <c>dark</c>, which changes them and saves the look, returning <c>true</c> (and why it
/// wasn't saved, if it wasn't), or <c>nil</c> and why.</description></item>
/// <item><description><c>venture.time()</c>: the clock, <c>{ set, offset, zone }</c>, and when the hardware clock is
/// set, also <c>text</c> (as the menu bar shows it), <c>year</c>, <c>month</c>, <c>day</c>, <c>hour</c>, <c>minute</c>,
/// <c>second</c>, <c>weekday</c>, and <c>utc</c>; <c>nil</c> without a clock. <c>venture.setUtcOffset(offset)</c>
/// takes an offset such as <c>"-05:00"</c> and saves it, returning as <c>setAppearance</c> does.</description></item>
/// <item><description><c>venture.developer.enable()</c> and <c>disable()</c> turn developer mode on and off, for vdb;
/// <c>venture.developer.trust(identity)</c> trusts a computer without asking; and <c>venture.developer.status()</c>
/// is <c>{ enabled, port, trusted, connected, problem }</c>, or <c>nil</c> without developer mode. All last until
/// VentureOS restarts.</description></item>
/// <item><description><c>venture.processes()</c>: the running apps, <c>{ id, name, identifier, state, seconds, kept }</c>
/// each, or <c>nil</c>; and <c>venture.kill(target)</c>, which ends one by ID, name, or identifier, returning
/// <c>true</c> and whether it's kept, so starts again, or <c>nil</c> and why.</description></item>
/// </list>
/// <para>The functions other than <c>setAppearance</c> and <c>setUtcOffset</c> take no arguments and ignore any they're given. An exception from
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
    /// <param name="workingDirectory">The script's current folder, for <c>venture.ui</c>'s relative paths.</param>
    /// <param name="output">Where <c>venture.ui.alert</c> prints when the desktop isn't showing.</param>
    public static void Open(
        ILuaState state, ISystemInfo system, ScriptScopes scopes = ScriptScopes.All, Func<string>? workingDirectory = null,
        TextWriter? output = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(system);

        state.L_NewLib(
        [
            new("version", lua => PushVersion(lua, system)),
            Gated("uptime", ScriptScopes.System, scopes, lua => PushUptime(lua, system)),
            Gated("memory", ScriptScopes.System, scopes, lua => PushMemory(lua, system)),
            Gated("net", ScriptScopes.Network, scopes, lua => PushNetwork(lua, system)),
            Gated("resolve", ScriptScopes.Network, scopes, lua => Resolve(lua, system.NetworkTools)),
            Gated("ping", ScriptScopes.Network, scopes, lua => Ping(lua, system.NetworkTools)),
            Gated("appearance", ScriptScopes.System, scopes, lua => PushAppearance(lua, system.Appearance)),
            Gated("setAppearance", ScriptScopes.System, scopes, lua => SetAppearance(lua, system.Appearance)),
            Gated("wallpapers", ScriptScopes.System, scopes, lua => PushWallpapers(lua, system.Appearance)),
            Gated("time", ScriptScopes.System, scopes, lua => PushTime(lua, system.WallClock)),
            Gated("setUtcOffset", ScriptScopes.System, scopes, lua => SetUtcOffset(lua, system.WallClock)),
            Gated("processes", ScriptScopes.System, scopes, lua => PushProcesses(lua, system.Processes)),
            Gated("kill", ScriptScopes.System, scopes, lua => Kill(lua, system.Processes)),

            // Replaced by ShellLibrary where the code has a lasting owner; elsewhere they explain why they can't work.
            Gated("command", ScriptScopes.Shell, scopes, lua => NotOwned(lua, "command")),
            Gated("shell", ScriptScopes.Shell, scopes, lua => NotOwned(lua, "shell")),
        ]);

        // Any script may show a message, Quick Look, or open an app, since none of it calls the script back; only an
        // app, whose code lasts while its windows do, may open a window of its own, so UiLibrary replaces the table
        // for apps.
        var folder = workingDirectory ?? (() => "/");
        state.NewTable();
        state.PushCSharpFunction(lua =>
        {
            lua.PushString(LuaText.Encode(NotAppMessage));
            return lua.Error();
        });
        state.SetField(-2, "open");
        state.PushCSharpFunction(lua => Alert(lua, system.Desktop, output));
        state.SetField(-2, "alert");
        state.PushCSharpFunction(lua =>
        {
            var path = ShellPaths.Resolve(folder(), LuaText.Decode(lua.L_CheckString(1)));
            return Answer(lua, system.Desktop is { } desktop ? desktop.Preview(path) : NoDesktop("preview"));
        });
        state.SetField(-2, "preview");
        state.PushCSharpFunction(lua =>
        {
            var app = LuaText.Decode(lua.L_CheckString(1));
            var file = lua.IsNoneOrNil(2) ? null : LuaText.Decode(lua.L_CheckString(2));
            return Answer(lua, system.Desktop is { } desktop ? desktop.Launch(app, file, folder()) : NoDesktop("launch"));
        });
        state.SetField(-2, "launch");
        state.SetField(-2, "ui");

        state.L_NewLib(
        [
            Gated("enable", ScriptScopes.System, scopes, "developer.", lua => SetDeveloperMode(lua, system.Developer, on: true)),
            Gated("disable", ScriptScopes.System, scopes, "developer.", lua => SetDeveloperMode(lua, system.Developer, on: false)),
            Gated("trust", ScriptScopes.System, scopes, "developer.", lua => TrustComputer(lua, system.Developer)),
            Gated("status", ScriptScopes.System, scopes, "developer.", lua => PushDeveloperStatus(lua, system.Developer)),
        ]);
        state.SetField(-2, "developer");
        state.SetGlobal(GlobalName);
    }

    private const string NoDeveloperMode = "this system has no developer mode";

    /// <summary>
    /// <c>venture.processes()</c>: the running apps, oldest first, each <c>{ id, name, identifier, state, seconds,
    /// kept }</c>, where <c>state</c> is <c>"starting"</c> or <c>"running"</c>, <c>seconds</c> is how long it has run,
    /// and <c>kept</c> whether VentureOS starts it again when it ends; or <c>nil</c> on a system without apps.
    /// </summary>
    private static int PushProcesses(ILuaState lua, Processes.Supervisor? supervisor)
    {
        if (supervisor is null)
        {
            lua.PushNil();
            return 1;
        }

        var now = supervisor.Now;
        lua.NewTable();
        var index = 0;
        foreach (var process in supervisor.Processes)
        {
            lua.NewTable();
            SetInteger(lua, "id", (ulong)process.Id);
            SetString(lua, "name", process.Name);
            SetString(lua, "identifier", process.Bundle.Identifier);
            SetString(lua, "state", process.State == Processes.ProcessState.Starting ? "starting" : "running");
            SetInteger(lua, "seconds", (ulong)Math.Max(0, (now - process.Started).TotalSeconds));
            SetBoolean(lua, "kept", process.Policy == Processes.RestartPolicy.Always);
            lua.RawSetI(-2, ++index);
        }

        return 1;
    }

    /// <summary>
    /// <c>venture.kill(target)</c>: ends a running app's process, named by its ID, its name, or its identifier,
    /// ignoring case. Returns <see langword="true"/>, and whether it'll start again, being kept; or <c>nil</c> and
    /// why, such as when it's the code calling: a process can't end from inside itself.
    /// </summary>
    private static int Kill(ILuaState lua, Processes.Supervisor? supervisor)
    {
        var target = lua.Type(1) == LuaType.LUA_TNUMBER ? lua.ToNumber(1).ToString(System.Globalization.CultureInfo.InvariantCulture)
            : LuaText.Decode(lua.L_CheckString(1));
        if (supervisor is null)
            return Refuse(lua, "this system has no apps to end");

        Processes.AppProcess? found = null;
        if (int.TryParse(target, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var id))
        {
            found = supervisor.Find(id);
        }
        else
        {
            foreach (var process in supervisor.Processes)
            {
                if (process.Name.Equals(target, StringComparison.OrdinalIgnoreCase)
                    || process.Bundle.Identifier.Equals(target, StringComparison.OrdinalIgnoreCase))
                {
                    found = process;
                    break;
                }
            }
        }

        if (found is null)
            return Refuse(lua, $"there's no running app {target}; ps lists them");
        if (found.IsBusy)
            return Refuse(lua, $"{found.Name} is running the code asking, and can't end from inside itself");

        supervisor.End(found, Processes.EndingKind.Killed, $"{found.Name} was killed.");
        lua.PushBoolean(true);
        lua.PushBoolean(found.Policy == Processes.RestartPolicy.Always);
        return 2;
    }

    /// <summary>
    /// <c>venture.developer.enable()</c> and <c>disable()</c>: turn developer mode on or off, so VentureOS listens for
    /// vdb while the desktop runs, until it restarts. Return <see langword="true"/>, or <c>nil</c> and why.
    /// </summary>
    private static int SetDeveloperMode(ILuaState lua, VdbDeveloperMode? developer, bool on)
    {
        if (developer is null)
            return Refuse(lua, NoDeveloperMode);
        if (on)
            developer.Enable();
        else
            developer.Disable();
        return Done(lua);
    }

    /// <summary>
    /// <c>venture.developer.trust(identity)</c>: trusts the computer whose vdb printed this identity, without asking,
    /// until VentureOS restarts. Returns <see langword="true"/>, or <c>nil</c> and why.
    /// </summary>
    private static int TrustComputer(ILuaState lua, VdbDeveloperMode? developer)
    {
        var identity = LuaText.Decode(lua.L_CheckString(1));
        if (developer is null)
            return Refuse(lua, NoDeveloperMode);
        if (!VdbTrust.IsIdentity(identity))
            return Refuse(lua, $"\"{identity}\" isn't a vdb identity, which is 32 lowercase hexadecimal characters; vdb identity prints it");
        developer.Trust.Trust(identity);
        return Done(lua);
    }

    /// <summary>
    /// <c>venture.developer.status()</c>: <c>{ enabled, port, trusted, connected, problem }</c>, where
    /// <c>trusted</c> counts the computers trusted and <c>problem</c> says why it isn't listening, if it isn't; or
    /// <c>nil</c> on a system without developer mode.
    /// </summary>
    private static int PushDeveloperStatus(ILuaState lua, VdbDeveloperMode? developer)
    {
        if (developer is null)
        {
            lua.PushNil();
            return 1;
        }

        lua.NewTable();
        SetBoolean(lua, "enabled", developer.IsEnabled);
        SetInteger(lua, "port", (ulong)developer.Port);
        SetInteger(lua, "trusted", (ulong)developer.Trust.TrustedCount);
        SetBoolean(lua, "connected", developer.IsConnected);
        SetString(lua, "problem", developer.Problem);
        return 1;
    }

    /// <summary>
    /// The error for <c>venture.command</c> or <c>venture.shell</c> in code that ends when it finishes, such as a
    /// script command.
    /// </summary>
    public static string NotOwnedMessage(string function) =>
        $"venture.{function} only works in init.lua and at the lua prompt, which last beyond a single run";

    /// <summary>The error for <c>venture.ui.open</c> in code that isn't an app's.</summary>
    public const string NotAppMessage =
        "venture.ui.open only works in apps, whose windows close when the app quits; other code can use "
        + "venture.ui.alert, preview, and launch; see docs/writing-apps.md";

    /// <summary>
    /// <c>venture.ui.alert(message, title)</c>: shows the message in a window with an OK button, or, without the
    /// desktop on the screen, prints it. Returns <see langword="true"/>.
    /// </summary>
    private static int Alert(ILuaState lua, IScriptDesktop? desktop, TextWriter? output)
    {
        var message = LuaText.Decode(lua.L_CheckString(1));
        var title = lua.IsNoneOrNil(2) ? "Message" : LuaText.Decode(lua.L_CheckString(2));
        if (desktop is { IsShowing: true })
            desktop.Alert(title, message);
        else
            output?.WriteLine(title == "Message" ? message : $"{title}: {message}");

        lua.PushBoolean(true);
        return 1;
    }

    /// <summary>Returns <see langword="true"/>, or <see langword="nil"/> and why.</summary>
    private static int Answer(ILuaState lua, string? problem) =>
        problem is null ? Done(lua) : Refuse(lua, problem);

    private static int Done(ILuaState lua)
    {
        lua.PushBoolean(true);
        return 1;
    }

    private static string NoDesktop(string function) => $"venture.ui.{function} needs the desktop, which this system doesn't have";

    private static int NotOwned(ILuaState lua, string function)
    {
        lua.PushString(LuaText.Encode(NotOwnedMessage(function)));
        return lua.Error();
    }

    /// <summary>The error a script gets for calling <paramref name="function"/> without declaring its scope.</summary>
    public static string UndeclaredMessage(string function, ScriptScopes scope) =>
        $"venture.{function} needs the {ScriptScopeNames.Name(scope)} scope: add it to a script's -- Needs: line, or an app's needs in Info.xml";

    private static NameFuncPair Gated(
        string name, ScriptScopes scope, ScriptScopes granted, CSharpFunctionDelegate function) =>
        Gated(name, scope, granted, "", function);

    /// <param name="table">Where the function sits under <c>venture</c>, such as <c>developer.</c>, for its message.</param>
    private static NameFuncPair Gated(
        string name, ScriptScopes scope, ScriptScopes granted, string table, CSharpFunctionDelegate function) =>
        (granted & scope) != 0
            ? new(name, function)
            : new(name, lua =>
            {
                // Pushed rather than passed to L_Error, which would treat the message as a format string.
                lua.PushString(LuaText.Encode(UndeclaredMessage(table + name, scope)));
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

    /// <summary>
    /// <c>venture.appearance()</c>: the desktop's look as chosen, <c>mode</c> (<c>"light"</c>, <c>"dark"</c>, or
    /// <c>"auto"</c>), <c>accent</c>, <c>corners</c>, <c>wallpaper</c>, <c>darkFrom</c>, and <c>darkUntil</c>, and
    /// whether it's <c>dark</c> now; or <c>nil</c> on a system without one.
    /// </summary>
    private static int PushAppearance(ILuaState lua, IAppearance? appearance)
    {
        if (appearance is null)
        {
            lua.PushNil();
            return 1;
        }

        lua.NewTable();
        SetString(lua, "mode", appearance.Mode);
        SetBoolean(lua, "dark", appearance.IsDark);
        SetString(lua, "accent", appearance.Accent);
        SetString(lua, "corners", appearance.RoundedCorners ? "rounded" : "square");
        SetString(lua, "dock", appearance.Dock);
        SetString(lua, "wallpaper", appearance.Wallpaper);
        SetString(lua, "darkFrom", appearance.DarkFrom);
        SetString(lua, "darkUntil", appearance.DarkUntil);
        return 1;
    }

    /// <summary>
    /// <c>venture.wallpapers()</c>: the wallpapers to choose from, each <c>{ name, title, thumbnail, description }</c>,
    /// sorted by name, without the sky; <c>nil</c> on a system without an appearance.
    /// </summary>
    private static int PushWallpapers(ILuaState lua, IAppearance? appearance)
    {
        if (appearance is null)
        {
            lua.PushNil();
            return 1;
        }

        var wallpapers = appearance.Wallpapers();
        lua.CreateTable(wallpapers.Count, 0);
        for (var index = 0; index < wallpapers.Count; index++)
        {
            var wallpaper = wallpapers[index];
            lua.NewTable();
            SetString(lua, "name", wallpaper.Name);
            SetString(lua, "title", wallpaper.Title);
            SetString(lua, "thumbnail", wallpaper.Thumbnail);
            SetString(lua, "description", wallpaper.Description);
            lua.RawSetI(-2, index + 1);
        }

        return 1;
    }

    /// <summary>
    /// <c>venture.setAppearance(mode, accent)</c>, or <c>venture.setAppearance{ mode, accent, corners, wallpaper,
    /// darkFrom, darkUntil, dock }</c>: changes what's given, keeps the rest, and saves the look. Returns
    /// <see langword="true"/>, with why it couldn't be saved when that failed, or <see langword="nil"/> and why,
    /// changing nothing, when anything is wrong. The desktop shows it on its next frame.
    /// </summary>
    private static int SetAppearance(ILuaState lua, IAppearance? appearance)
    {
        AppearanceRequest request;
        if (lua.Type(1) == LuaType.LUA_TTABLE)
        {
            string? Field(string name)
            {
                lua.GetField(1, name);
                var value = lua.Type(-1) == LuaType.LUA_TSTRING ? LuaText.Decode(lua.ToString(-1)) : null;
                var given = !lua.IsNoneOrNil(-1);
                lua.Pop(1);
                return given && value is null ? throw new ArgumentException($"{name} must be a string") : value;
            }

            try
            {
                request = new AppearanceRequest(
                    Field("mode"), Field("accent"), Field("corners"), Field("wallpaper"), Field("darkFrom"), Field("darkUntil"),
                    Field("dock"));
            }
            catch (ArgumentException exception)
            {
                return Refuse(lua, exception.Message);
            }
        }
        else
        {
            var mode = LuaText.Decode(lua.L_CheckString(1));
            var accent = lua.IsNoneOrNil(2) ? null : LuaText.Decode(lua.L_CheckString(2));
            request = new AppearanceRequest(mode, accent);
        }

        if (appearance is null)
            return Refuse(lua, "this system has no desktop appearance to change");

        var change = appearance.Set(request);
        if (change.Refused is { } refused)
            return Refuse(lua, refused);

        lua.PushBoolean(true);
        if (change.Unsaved is null)
            return 1;

        lua.PushString(LuaText.Encode(change.Unsaved));
        return 2;
    }

    /// <summary>
    /// <c>venture.time()</c>: the offset always, and the time with it when the hardware clock is set, so a script can
    /// tell "not set" from a time; <c>nil</c> without a clock.
    /// </summary>
    private static int PushTime(ILuaState lua, WallClock? clock)
    {
        if (clock is null)
        {
            lua.PushNil();
            return 1;
        }

        var offset = clock.UtcOffset;
        lua.NewTable();
        SetString(lua, "offset", WallClock.FormatOffset(offset));
        SetString(lua, "zone", WallClock.FormatZone(offset));
        if (clock.UtcNow is not { } utc)
        {
            SetBoolean(lua, "set", false);
            return 1;
        }

        var now = utc + offset;
        SetBoolean(lua, "set", true);
        SetString(lua, "text", WallClock.FormatMenuTime(now));
        SetString(lua, "weekday", WallClock.DayName(now));
        SetInteger(lua, "year", (ulong)now.Year);
        SetInteger(lua, "month", (ulong)now.Month);
        SetInteger(lua, "day", (ulong)now.Day);
        SetInteger(lua, "hour", (ulong)now.Hour);
        SetInteger(lua, "minute", (ulong)now.Minute);
        SetInteger(lua, "second", (ulong)now.Second);
        SetString(lua, "utc", string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{utc.Year:0000}-{utc.Month:00}-{utc.Day:00}T{utc.Hour:00}:{utc.Minute:00}:{utc.Second:00}Z"));
        return 1;
    }

    /// <summary>
    /// <c>venture.setUtcOffset(offset)</c>: sets the clock's offset, written like <c>"-05:00"</c>, <c>"+5:30"</c>, or
    /// <c>"UTC"</c>, and saves it. Returns as <c>setAppearance</c> does. The menu bar shows it on its next frame.
    /// </summary>
    private static int SetUtcOffset(ILuaState lua, WallClock? clock)
    {
        var written = LuaText.Decode(lua.L_CheckString(1));
        if (!WallClock.TryParseOffset(written, out var offset, out var problem))
            return Refuse(lua, problem!);
        if (clock is null)
            return Refuse(lua, "this system has no clock to set");

        lua.PushBoolean(true);
        if (clock.SetUtcOffset(offset) is not { } unsaved)
            return 1;

        lua.PushString(LuaText.Encode(unsaved));
        return 2;
    }

    private static int Refuse(ILuaState lua, string reason)
    {
        lua.PushNil();
        lua.PushString(LuaText.Encode(reason));
        return 2;
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
        SetString(lua, "dns", network.DnsServer);
        return 1;
    }

    // How long resolving and pinging wait by default, and the range a script may ask for, in seconds.
    private const int ResolveSeconds = 3;
    private const int PingSeconds = 2;
    private const int LongestWait = 10;

    /// <summary>
    /// <c>venture.resolve(name, seconds)</c>: the address a host name has, as text, or <c>nil</c> and why. An address
    /// passes through. It waits up to <paramref name="lua"/>'s second argument, from 1 to 10 seconds, or 3.
    /// </summary>
    private static int Resolve(ILuaState lua, INetworkTools? tools)
    {
        var name = LuaText.Decode(lua.L_CheckString(1));
        if (HostNames.Problem(name) is { } problem)
            return Refuse(lua, problem);
        if (Wait(lua, ResolveSeconds) is not { } timeout)
            return Refuse(lua, $"a wait is a whole number of seconds from 1 to {LongestWait}");
        if (tools is null)
            return Refuse(lua, "there's no network device");

        var result = tools.Resolve(name, timeout);
        if (result.Address is not { } address)
            return Refuse(lua, result.Problem ?? $"no answer for {name}");

        lua.PushString(LuaText.Encode(address));
        return 1;
    }

    /// <summary>
    /// <c>venture.ping(address, seconds)</c>: how many milliseconds one echo request to an IPv4 address took to be
    /// answered, or <c>nil</c> and why. It waits up to the second argument, from 1 to 10 seconds, or 2.
    /// </summary>
    private static int Ping(ILuaState lua, INetworkTools? tools)
    {
        var address = LuaText.Decode(lua.L_CheckString(1));
        if (HostNames.Problem(address) is { } problem)
            return Refuse(lua, problem);
        if (Wait(lua, PingSeconds) is not { } timeout)
            return Refuse(lua, $"a wait is a whole number of seconds from 1 to {LongestWait}");
        if (tools is null)
            return Refuse(lua, "there's no network device");

        var result = tools.Ping(address, timeout);
        if (result.Time is not { } time)
            return Refuse(lua, result.Problem ?? $"no reply from {address}");

        lua.PushInteger((long)time.TotalMilliseconds);
        return 1;
    }

    // The wait a script gave as its second argument, or the default; null when it's out of range or not whole.
    private static TimeSpan? Wait(ILuaState lua, int seconds)
    {
        if (lua.IsNoneOrNil(2))
            return TimeSpan.FromSeconds(seconds);

        var given = lua.L_CheckNumber(2);
        return given >= 1 && given <= LongestWait && given == Math.Floor(given) ? TimeSpan.FromSeconds(given) : null;
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
