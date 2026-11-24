using System.Globalization;
using Cosmos.Executable.Lua;
using CSTNet;
using Venture.OS.Interface.Strings;

namespace Venture.OS.Scripting;

/// <summary>
/// <c>venture.cst</c>: an app's text from its CST tables, the ones in its <c>Contents/Resources</c> and the shared
/// ones from 100 up, so Lua code can show text that has to be filled in, such as <c>"3 notes"</c>.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description><c>get(table, key)</c>: the text as written, placeholders and all, or <c>nil</c> and why.
/// <c>key</c> is a number or text, since keys needn't be numbers.</description></item>
/// <item><description><c>format(table, key, ...)</c>: the text with its placeholders (<c>%s</c>, <c>%d</c>, and the
/// rest of the CST specification's) filled in order by the values after the key, or <c>nil</c> and why.</description></item>
/// </list>
/// <para>It needs no scope: an app's text is its own. A missing string isn't an error, since an app may ship
/// without one; it's <c>nil</c> and why, so the app can say so. An argument that isn't text or a number is an
/// error, since that's a mistake in the code. <c>window:string</c> answers the same way for a <c>&lt;String&gt;</c>
/// a window's document names.</para>
/// <para>Only apps have it, since a command has no bundle to keep tables in; other code gets an explanation.</para>
/// </remarks>
public static class CstLibrary
{
    /// <summary>The error for <c>venture.cst</c> in code that isn't an app's.</summary>
    public const string NotAppMessage =
        "venture.cst only works in apps, whose text is in their bundle's Contents/Resources; see docs/tutorial/desktop.md";

    /// <summary>Puts <c>venture.cst</c> in the <c>venture</c> table, for an app whose tables are <paramref name="tables"/>.</summary>
    public static void Open(ILuaState state, StringTables tables)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(tables);

        if (state.GetGlobal(VentureLibrary.GlobalName) != LuaType.LUA_TTABLE)
        {
            state.Pop(1);
            state.NewTable();
            state.PushValue(-1);
            state.SetGlobal(VentureLibrary.GlobalName);
        }

        state.NewTable();
        state.PushCSharpFunction(lua => Get(lua, tables, "get", fill: false));
        state.SetField(-2, "get");
        state.PushCSharpFunction(lua => Get(lua, tables, "format", fill: true));
        state.SetField(-2, "format");
        state.SetField(-2, "cst");
        state.Pop(1);
    }

    /// <summary>Puts a <c>venture.cst</c> in <paramref name="state"/>'s open table that explains it's for apps.</summary>
    internal static void OpenNotApp(ILuaState state)
    {
        state.NewTable();
        foreach (var name in (ReadOnlySpan<string>)["get", "format"])
        {
            state.PushCSharpFunction(lua =>
            {
                lua.PushString(LuaText.Encode(NotAppMessage));
                return lua.Error();
            });
            state.SetField(-2, name);
        }

        state.SetField(-2, "cst");
    }

    /// <summary>
    /// Pushes the text <paramref name="find"/> gives, filled with the Lua values from <paramref name="firstArgument"/>
    /// on when <paramref name="fill"/>, or <c>nil</c> and why: what <paramref name="tables"/> reported while finding
    /// it, or that <paramref name="what"/> has no text.
    /// </summary>
    internal static int Push(
        ILuaState lua, StringTables tables, Func<string?> find, string what, int firstArgument, bool fill, string function)
    {
        object?[] arguments = [];
        if (fill && Arguments(lua, firstArgument, function, out arguments) is { } mistake)
        {
            lua.PushString(LuaText.Encode(mistake));
            return lua.Error();
        }

        var known = tables.Problems.Count;
        if (find() is not { } text)
        {
            List<string> found = [];
            for (var index = known; index < tables.Problems.Count; index++)
                found.Add(tables.Problems[index]);

            lua.PushNil();
            lua.PushString(LuaText.Encode(found.Count > 0 ? string.Join(" ", found) : $"there's no text for {what}"));
            return 2;
        }

        lua.PushString(LuaText.Encode(arguments.Length == 0 ? text : CST.Format(text, arguments)));
        return 1;
    }

    private static int Get(ILuaState lua, StringTables tables, string function, bool fill)
    {
        var table = lua.L_CheckInteger(1);
        if (table < 0 || table > int.MaxValue)
        {
            lua.PushString(LuaText.Encode($"venture.cst.{function}: a table is numbered 0 or more, not {table}"));
            return lua.Error();
        }

        string key;
        switch (lua.Type(2))
        {
            case LuaType.LUA_TNUMBER:
                key = Text(lua, 2);
                break;
            case LuaType.LUA_TSTRING:
                key = LuaText.Decode(lua.ToString(2));
                break;
            default:
                lua.PushString(LuaText.Encode($"venture.cst.{function} needs a key, a number or text, such as venture.cst.{function}(1, 3)"));
                return lua.Error();
        }

        return Push(lua, tables, () => tables.Find((int)table, key), $"table {table}, key {key}", 3, fill, $"venture.cst.{function}");
    }

    // The values that fill placeholders, as text: a whole number without a decimal point, so %d reads it. Anything
    // else is a mistake in the code, which this names.
    private static string? Arguments(ILuaState lua, int first, string function, out object?[] arguments)
    {
        var count = Math.Max(0, lua.GetTop() - first + 1);
        arguments = new object?[count];
        for (var index = 0; index < count; index++)
        {
            var at = first + index;
            var type = lua.Type(at);
            if (type == LuaType.LUA_TSTRING)
                arguments[index] = LuaText.Decode(lua.ToString(at));
            else if (type == LuaType.LUA_TNUMBER)
                arguments[index] = Text(lua, at);
            else
                return $"{function}: argument {at} must be text or a number, not {Describe(type)}";
        }

        return null;
    }

    private static string Describe(LuaType type) => type switch
    {
        LuaType.LUA_TNIL or LuaType.LUA_TNONE => "nil",
        LuaType.LUA_TBOOLEAN => "a boolean",
        LuaType.LUA_TTABLE => "a table",
        LuaType.LUA_TFUNCTION => "a function",
        _ => "that kind of value",
    };

    private static string Text(ILuaState lua, int index)
    {
        var number = lua.ToNumber(index);
        return Math.Abs(number) < 1e15 && number == Math.Floor(number)
            ? ((long)number).ToString(CultureInfo.InvariantCulture)
            : number.ToString(CultureInfo.InvariantCulture);
    }
}
