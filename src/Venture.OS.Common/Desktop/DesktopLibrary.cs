using Cosmos.Executable.Lua;
using Venture.OS.Bundles;
using Venture.OS.Scripting;

namespace Venture.OS.Desktop;

/// <summary>
/// <c>venture.desktop</c>, for apps that act for the desktop, such as the dock, under the <c>system</c> scope since
/// it reaches other apps:
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description><c>apps(all)</c>: every app the dock shows, the desktop's own first, then the system's and the
/// user's, each <c>{ name, path, icon, identifier, user, open, docked }</c>, where <c>path</c> and <c>icon</c> are
/// full paths and <c>icon</c> is <c>nil</c> without one; with <c>all</c> true, the apps that stay out of the dock
/// too, <c>docked</c> saying which, as Settings lists them.</description></item>
/// <item><description><c>open(path)</c>: opens an app as the dock does, bringing it forward when it's open, with a new
/// window when it has none. Returns <c>true</c>, or <c>nil</c> and why.</description></item>
/// <item><description><c>minimized()</c>: minimized windows, oldest first, each <c>{ id, title, app }</c>; and
/// <c>restore(id)</c>, which brings one back, returning <c>true</c>, or <c>nil</c> and why.</description></item>
/// <item><description><c>onChange(fn)</c>: calls <c>fn</c> when apps open, close, or are installed, or a window is
/// minimized or restored, at most once a frame, until the app quits.</description></item>
/// </list>
/// <para>Only apps have it, since <c>onChange</c> lasts as long as its app does.</para>
/// </remarks>
public static class DesktopLibrary
{
    /// <summary>Puts <c>venture.desktop</c> in the <c>venture</c> table for an app's code.</summary>
    /// <param name="host">The desktop's windows, or <see langword="null"/> without a desktop.</param>
    /// <param name="apps">The system's apps, or <see langword="null"/>.</param>
    public static void Open(ILuaState state, LuaOwner owner, Bundle app, AppWindows? host, AppManager? apps)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(app);

        if (state.GetGlobal(VentureLibrary.GlobalName) != LuaType.LUA_TTABLE)
        {
            state.Pop(1);
            return;
        }

        var allowed = app.Needs.HasFlag(ScriptScopes.System);
        state.NewTable();
        Function(state, "apps", allowed, host, apps, (lua, _, manager) => PushApps(lua, host!, manager, lua.ToBoolean(1)));
        Function(state, "open", allowed, host, apps, (lua, desktop, manager) =>
        {
            var path = LuaText.Decode(lua.L_CheckString(1));
            return Answer(lua, desktop.OpenApp(manager, path, owner.Interpreter.WorkingDirectory ?? "/"));
        });
        Function(state, "minimized", allowed, host, apps, (lua, desktop, _) => PushMinimized(lua, desktop));
        Function(state, "restore", allowed, host, apps, (lua, desktop, _) =>
        {
            var id = lua.L_CheckInteger(1);
            if (id < 1 || id > int.MaxValue || desktop.Windows.Find((int)id) is not { IsMinimized: true } window)
                return Answer(lua, $"there's no minimized window {id}; venture.desktop.minimized() lists them");

            desktop.Windows.Open(window);
            desktop.MarkDesktopChanged();
            return Answer(lua, null);
        });
        Function(state, "onChange", allowed, host, apps, (lua, desktop, _) =>
        {
            if (!lua.IsFunction(1))
                return Fail(lua, "venture.desktop.onChange needs a function, such as venture.desktop.onChange(function() ... end)");

            lua.PushValue(1);
            var reference = lua.L_Ref(LuaDef.LUA_REGISTRYINDEX);
            var workingDirectory = owner.Interpreter.WorkingDirectory ?? "/";
            var stopListening = desktop.ListenToDesktop(() =>
            {
                if (!owner.IsDisposed)
                    owner.Call(reference, _ => 0, desktop.Output, workingDirectory, desktop.Stop?.Invoke());
            });
            owner.AddUndo(() =>
            {
                stopListening();
                owner.Interpreter.State.L_Unref(LuaDef.LUA_REGISTRYINDEX, reference);
            });
            return 0;
        });

        state.SetField(-2, "desktop");
        state.Pop(1);
    }

    // The apps, as the dock shows them, or with every app, each saying whether the dock shows it, for Settings.
    private static int PushApps(ILuaState lua, AppWindows host, AppManager apps, bool all)
    {
        lua.NewTable();
        var index = 0;

        // The desktop's own app first, as the dock has always shown it, then the rest in the order they're listed.
        foreach (var first in (ReadOnlySpan<bool>)[true, false])
        {
            foreach (var listing in apps.Available())
            {
                // An app that can't be read stays out; `apps` in the shell says why.
                if (listing.Bundle is not { } bundle || (bundle.Name == host.FallbackApp) != first)
                    continue;

                // An app that stays out of the dock, as a startup item living in the menu bar may.
                var docked = host.IsShownInDock(bundle);
                if (!docked && !all)
                    continue;

                lua.NewTable();
                SetString(lua, "name", bundle.Name);
                SetString(lua, "path", apps.ScriptPath(listing.Path));
                SetString(lua, "icon", bundle.Icon is { } icon ? apps.ScriptPath(icon) : null);
                SetString(lua, "identifier", bundle.Identifier);
                lua.PushBoolean(listing.IsUser);
                lua.SetField(-2, "user");
                lua.PushBoolean(apps.IsOpen(bundle.Identifier));
                lua.SetField(-2, "open");
                lua.PushBoolean(docked);
                lua.SetField(-2, "docked");
                lua.RawSetI(-2, ++index);
            }
        }

        return 1;
    }

    private static int PushMinimized(ILuaState lua, AppWindows host)
    {
        lua.NewTable();
        var index = 0;
        foreach (var window in host.Windows.Windows)
        {
            if (!window.IsMinimized)
                continue;

            lua.NewTable();
            lua.PushInteger(window.Id);
            lua.SetField(-2, "id");
            SetString(lua, "title", window.Title);
            SetString(lua, "app", window.App);
            lua.RawSetI(-2, ++index);
        }

        return 1;
    }

    /// <summary>A <c>venture.desktop</c> function, or one that explains what's missing.</summary>
    private static void Function(
        ILuaState state, string name, bool allowed, AppWindows? host, AppManager? apps, Func<ILuaState, AppWindows, AppManager, int> body)
    {
        state.PushCSharpFunction(lua =>
        {
            if (!allowed)
            {
                lua.PushString(LuaText.Encode(VentureLibrary.UndeclaredMessage($"desktop.{name}", ScriptScopes.System)));
                return lua.Error();
            }

            if (host is null || apps is null)
                return Fail(lua, $"venture.desktop.{name} needs the desktop, which this system doesn't have");

            return body(lua, host, apps);
        });
        state.SetField(-2, name);
    }

    private static void SetString(ILuaState lua, string key, string? value)
    {
        if (value is null)
            return;

        lua.PushString(LuaText.Encode(value));
        lua.SetField(-2, key);
    }

    private static int Answer(ILuaState lua, string? problem)
    {
        if (problem is null)
        {
            lua.PushBoolean(true);
            return 1;
        }

        lua.PushNil();
        lua.PushString(LuaText.Encode(problem));
        return 2;
    }

    private static int Fail(ILuaState lua, string message)
    {
        lua.PushString(LuaText.Encode(message));
        return lua.Error();
    }
}
