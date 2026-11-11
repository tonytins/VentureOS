using Cosmos.Executable.Lua;
using Venture.OS.Scripting;

namespace Venture.OS.Bundles;

/// <summary>
/// <c>venture.app</c>: what an open app's code knows about its own bundle.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description><c>name</c>, <c>identifier</c>, and <c>version</c> (or <c>nil</c>), from <c>Info.xml</c>.</description></item>
/// <item><description><c>path</c>: the bundle folder.</description></item>
/// <item><description><c>resource(path)</c>: the full path of a file in <c>Contents/Resources/</c>, such as
/// <c>venture.app.resource("main.uix")</c>; a path that would leave that folder is an error.</description></item>
/// </list>
/// <para>It's there for every app, with no scope: an app always knows itself. Reading a resource still goes
/// through <c>venture.files</c>, which needs <c>files</c>.</para>
/// </remarks>
public static class AppLibrary
{
    /// <summary>Puts <c>venture.app</c> in the <c>venture</c> table, creating the table if it's missing.</summary>
    public static void Open(ILuaState state, Bundle app)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(app);

        if (state.GetGlobal(VentureLibrary.GlobalName) != LuaType.LUA_TTABLE)
        {
            state.Pop(1);
            state.NewTable();
            state.PushValue(-1);
            state.SetGlobal(VentureLibrary.GlobalName);
        }

        state.NewTable();
        SetString(state, "name", app.Name);
        SetString(state, "identifier", app.Identifier);
        SetString(state, "path", app.Path);
        if (app.Version is { } version)
            SetString(state, "version", version);

        var resources = $"{app.Path}/Contents/Resources";
        state.PushCSharpFunction(lua =>
        {
            var relative = LuaText.Decode(lua.L_CheckString(1));
            if (!SystemDirectory.IsValidPath(relative))
            {
                lua.PushString(LuaText.Encode(
                    $"venture.app.resource needs a path inside Contents/Resources, such as \"main.uix\", not \"{relative}\""));
                return lua.Error();
            }

            lua.PushString(LuaText.Encode($"{resources}/{relative}"));
            return 1;
        });
        state.SetField(-2, "resource");

        state.SetField(-2, "app");
        state.Pop(1);
    }

    private static void SetString(ILuaState state, string key, string value)
    {
        state.PushString(LuaText.Encode(value));
        state.SetField(-2, key);
    }
}
