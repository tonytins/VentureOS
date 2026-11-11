using Cosmos.Executable.Lua;
using Venture.OS.Bundles;
using Venture.OS.Interface;
using Venture.OS.Scripting;
using Venture.OS.Shell;

namespace Venture.OS.Desktop;

/// <summary>Where apps' windows go, and how their event handlers run.</summary>
/// <param name="Windows">The desktop's windows, which last while the system runs, so apps can open windows from the
/// shell too; they show the next time the desktop does.</param>
/// <param name="Output">Where event handlers write. Events only happen while the desktop shows, and the console
/// would draw over it, so this is the desktop's held output.</param>
/// <param name="Stop">Makes the stop for each event handler, such as a time limit; called once per event.</param>
public sealed record AppWindows(WindowManager Windows, IShellOutput Output, Func<Func<string?>>? Stop = null);

/// <summary>
/// <c>venture.ui</c>: windows for apps, laid out by <c>.uix</c> documents.
/// </summary>
/// <remarks>
/// <code>
/// local window = venture.ui.open("main.uix", { title = "Notes" })  -- title is optional
/// window:on("add", function(event) ... end)    -- a control's or menu item's events
/// window:setText("count", "3 notes")
/// window:setItems("notes", { "Milk", "Eggs" })
/// window:close()
/// </code>
/// <para>A relative path is in the app's <c>Contents/Resources/</c>. The window belongs to the app: quitting the
/// app closes it, and closing the app's last window quits the app. Event handlers get a table with
/// <c>kind</c> (<c>pressed</c>, <c>submitted</c>, <c>changed</c>, or <c>selected</c>), <c>control</c>, and, when they
/// apply, <c>text</c>, <c>value</c>, and <c>index</c> (counting from 1). A chosen menu item is <c>pressed</c>.</para>
/// <para>Methods take a control by name and raise a Lua error for a name the document doesn't have, so a typo
/// shows at once instead of silently doing nothing. They need the colon: <c>window:setText(...)</c>.</para>
/// <list type="bullet">
/// <item><description><c>text(name)</c>, <c>setText(name, text)</c>: a control's text.</description></item>
/// <item><description><c>value(name)</c>, <c>setValue(name, number)</c>: a slider's or progress bar's value.</description></item>
/// <item><description><c>items(name)</c>, <c>setItems(name, list)</c>, <c>selected(name)</c>,
/// <c>select(name, index)</c>: a list's items and its selection, counting from 1, or <c>nil</c> for none.</description></item>
/// <item><description><c>setHidden</c>, <c>setDisabled</c>, <c>setChecked(name, on)</c>: hide, disable, or check a
/// control or menu item.</description></item>
/// <item><description><c>focus(name)</c>, <c>setTitle(text)</c>, <c>close()</c>.</description></item>
/// </list>
/// </remarks>
public static class UiLibrary
{
    /// <summary>Puts <c>venture.ui</c> in the <c>venture</c> table for an app's code.</summary>
    /// <param name="host">Where windows go, or <see langword="null"/> when there's no desktop.</param>
    public static void Open(ILuaState state, LuaOwner owner, Bundle app, AppWindows? host)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(app);

        if (state.GetGlobal(VentureLibrary.GlobalName) != LuaType.LUA_TTABLE)
        {
            state.Pop(1);
            state.NewTable();
            state.PushValue(-1);
            state.SetGlobal(VentureLibrary.GlobalName);
        }

        state.NewTable();
        if ((app.Needs & ScriptScopes.Ui) == 0)
            state.PushCSharpFunction(lua => Fail(lua, VentureLibrary.UndeclaredMessage("ui.open", ScriptScopes.Ui)));
        else if (host is null)
            state.PushCSharpFunction(lua => Fail(lua, "venture.ui.open needs the desktop, which this system doesn't have"));
        else
            state.PushCSharpFunction(lua => OpenWindow(lua, owner, app, host));
        state.SetField(-2, "open");

        state.SetField(-2, "ui");
        state.Pop(1);
    }

    private static int OpenWindow(ILuaState lua, LuaOwner owner, Bundle app, AppWindows host)
    {
        if (lua.Type(1) != LuaType.LUA_TSTRING)
            return Fail(lua, "venture.ui.open needs a .uix file, such as venture.ui.open(\"main.uix\")");

        var given = LuaText.Decode(lua.ToString(1));
        string path;
        if (given.StartsWith('/'))
        {
            path = given;
        }
        else if (SystemDirectory.IsValidPath(given))
        {
            path = $"{app.Path}/Contents/Resources/{given}";
        }
        else
        {
            return Fail(lua, $"venture.ui.open needs a path inside Contents/Resources, such as \"main.uix\", not \"{given}\"");
        }

        var title = app.Name;
        if (lua.Type(2) == LuaType.LUA_TTABLE)
        {
            lua.GetField(2, "title");
            if (lua.Type(-1) == LuaType.LUA_TSTRING)
                title = LuaText.Decode(lua.ToString(-1));
            lua.Pop(1);
        }
        else if (!lua.IsNoneOrNil(2))
        {
            return Fail(lua, "venture.ui.open's options must be a table, such as { title = \"Notes\" }");
        }

        UIScript script;
        try
        {
            script = UIScript.Load(path);
        }
        catch (Exception exception) when (exception is UIScriptException or IOException or UnauthorizedAccessException)
        {
            return Fail(lua, $"venture.ui.open: {given}: {exception.Message}");
        }

        Handle handle = new(owner, host, new UIView(script), Path.GetFileName(path));
        handle.Window = new DesktopWindow(title, handle.View)
        {
            App = app.Name,
            OpenedByApp = true,
            EventHandler = (_, uiEvent) => handle.Dispatch(uiEvent),
        };

        // Quitting the app closes the window and lets go of its handlers.
        owner.AddUndo(handle.Release);
        host.Windows.Open(handle.Window);

        PushHandle(lua, handle);
        return 1;
    }

    private static void PushHandle(ILuaState lua, Handle handle)
    {
        lua.NewTable();
        Method(lua, "on", handle, On);
        Method(lua, "text", handle, (state, h) =>
        {
            var name = Name(state, h, "text");
            if (h.View.Text(name) is { } text)
                state.PushString(LuaText.Encode(text));
            else
                state.PushNil();
            return 1;
        });
        Method(lua, "setText", handle, (state, h) =>
        {
            var name = Name(state, h, "setText");
            h.View.SetText(name, LuaText.Decode(state.L_CheckString(3)));
            return 0;
        });
        Method(lua, "value", handle, (state, h) =>
        {
            var name = Name(state, h, "value");
            if (h.View.Value(name) is { } value)
                state.PushNumber(value);
            else
                state.PushNil();
            return 1;
        });
        Method(lua, "setValue", handle, (state, h) =>
        {
            var name = Name(state, h, "setValue");
            h.View.SetValue(name, (float)state.L_CheckNumber(3));
            return 0;
        });
        Method(lua, "items", handle, (state, h) =>
        {
            var list = List(state, h, "items");
            state.CreateTable(list.Items.Count, 0);
            for (var index = 0; index < list.Items.Count; index++)
            {
                state.PushString(LuaText.Encode(list.Items[index]));
                state.RawSetI(-2, index + 1);
            }

            return 1;
        });
        Method(lua, "setItems", handle, SetItems);
        Method(lua, "selected", handle, (state, h) =>
        {
            var list = List(state, h, "selected");
            if (list.SelectedIndex >= 0)
                state.PushInteger(list.SelectedIndex + 1);
            else
                state.PushNil();
            return 1;
        });
        Method(lua, "select", handle, (state, h) =>
        {
            var list = List(state, h, "select");
            if (state.IsNoneOrNil(3))
            {
                list.SelectedIndex = -1;
                return 0;
            }

            var index = state.L_CheckInteger(3);
            if (index < 1 || index > list.Items.Count)
                return Fail(state, $"window:select: there's no item {index} in a list of {list.Items.Count}");

            list.SelectedIndex = (int)index - 1;
            return 0;
        });
        Method(lua, "setHidden", handle, (state, h) =>
        {
            h.View.SetHidden(state.ToBoolean(3), Name(state, h, "setHidden"));
            return 0;
        });
        Method(lua, "setDisabled", handle, (state, h) =>
        {
            h.View.SetDisabled(state.ToBoolean(3), Name(state, h, "setDisabled"));
            return 0;
        });
        Method(lua, "setChecked", handle, (state, h) =>
        {
            h.View.SetSelected(state.ToBoolean(3), Name(state, h, "setChecked"));
            return 0;
        });
        Method(lua, "focus", handle, (state, h) =>
        {
            h.View.Focus(Name(state, h, "focus"));
            return 0;
        });
        Method(lua, "setTitle", handle, (state, h) =>
        {
            h.Window!.Title = LuaText.Decode(state.L_CheckString(2));
            return 0;
        });
        Method(lua, "close", handle, (_, h) =>
        {
            h.Window!.RequestClose();
            return 0;
        });

        // A readable form, since the lua prompt prints what a call returns.
        lua.NewTable();
        var text = LuaText.Encode($"window \"{handle.Window!.Title}\"");
        lua.PushCSharpFunction(state =>
        {
            state.PushString(text);
            return 1;
        });
        lua.SetField(-2, "__tostring");
        lua.SetMetaTable(-2);
    }

    private static int On(ILuaState lua, Handle handle)
    {
        var name = Name(lua, handle, "on");
        if (!lua.IsFunction(3))
            return Fail(lua, $"window:on needs a function for \"{name}\", such as window:on(\"{name}\", function(event) ... end)");

        lua.PushValue(3);
        var reference = lua.L_Ref(LuaDef.LUA_REGISTRYINDEX);
        if (handle.Handlers.TryGetValue(name, out var previous))
            lua.L_Unref(LuaDef.LUA_REGISTRYINDEX, previous);
        handle.Handlers[name] = reference;
        return 0;
    }

    private static int SetItems(ILuaState lua, Handle handle)
    {
        var list = List(lua, handle, "setItems");
        if (lua.Type(3) != LuaType.LUA_TTABLE)
            return Fail(lua, "window:setItems needs a list of items, such as { \"Milk\", \"Eggs\" }");

        List<string> items = [];
        var count = lua.RawLen(3);
        for (var index = 1; index <= count; index++)
        {
            lua.RawGetI(3, index);
            var item = lua.L_ToString(-1);
            lua.Pop(2); // L_ToString pushes its result on top of the value.
            items.Add(LuaText.Decode(item));
        }

        list.Items.Clear();
        list.Items.AddRange(items);
        if (list.SelectedIndex >= items.Count)
            list.SelectedIndex = -1;
        return 0;
    }

    private static void Method(ILuaState lua, string name, Handle handle, Func<ILuaState, Handle, int> body)
    {
        lua.PushCSharpFunction(state =>
        {
            if (state.Type(1) != LuaType.LUA_TTABLE)
                return Fail(state, $"use window:{name}(...), with a colon");
            return body(state, handle);
        });
        lua.SetField(-2, name);
    }

    /// <summary>The control or menu item named by the method's first argument, which must exist.</summary>
    private static string Name(ILuaState lua, Handle handle, string method)
    {
        if (lua.Type(2) != LuaType.LUA_TSTRING)
        {
            Fail(lua, $"window:{method} needs a control's name, such as window:{method}(\"title\", ...)");
            return "";
        }

        var name = LuaText.Decode(lua.ToString(2));
        if (!handle.Names.Contains(name))
            Fail(lua, $"{handle.Document} has no control or menu item called \"{name}\"");
        return name;
    }

    private static UIListState List(ILuaState lua, Handle handle, string method)
    {
        var name = Name(lua, handle, method);
        if (handle.View.List(name) is { } list)
            return list;

        Fail(lua, $"window:{method} needs a list, and \"{name}\" in {handle.Document} isn't one");
        return null!;
    }

    private static int Fail(ILuaState lua, string message)
    {
        lua.PushString(LuaText.Encode(message));
        return lua.Error();
    }

    /// <summary>One window an app opened, and the Lua functions that handle its events.</summary>
    private sealed class Handle
    {
        private readonly LuaOwner owner;
        private readonly AppWindows host;
        private readonly string workingDirectory;

        public Handle(LuaOwner owner, AppWindows host, UIView view, string document)
        {
            this.owner = owner;
            this.host = host;
            View = view;
            Document = document;
            workingDirectory = owner.Interpreter.WorkingDirectory ?? "/";

            foreach (var control in view.Script.Controls)
                Names.Add(control.Name);
            foreach (var visual in view.Script.Visuals)
                Names.Add(visual.Name);
            foreach (var menu in view.Script.Menus)
            {
                foreach (var entry in menu.Entries)
                {
                    if (entry.Name is { } item)
                        Names.Add(item);
                }
            }
        }

        public UIView View { get; }

        public DesktopWindow? Window { get; set; }

        /// <summary>The document's file name, for messages.</summary>
        public string Document { get; }

        public HashSet<string> Names { get; } = [];

        public Dictionary<string, int> Handlers { get; } = [];

        public void Dispatch(UIEvent uiEvent)
        {
            if (owner.IsDisposed || !Handlers.TryGetValue(uiEvent.Control, out var reference))
                return;

            // An error in the handler is reported on the output, and the desktop carries on.
            owner.Call(reference, lua => PushEvent(lua, uiEvent), host.Output, workingDirectory, host.Stop?.Invoke());
        }

        public void Release()
        {
            host.Windows.Close(Window!);
            foreach (var reference in Handlers.Values)
                owner.Interpreter.State.L_Unref(LuaDef.LUA_REGISTRYINDEX, reference);
            Handlers.Clear();
        }

        private static int PushEvent(ILuaState lua, UIEvent uiEvent)
        {
            lua.NewTable();
            lua.PushString(uiEvent.Kind switch
            {
                UIEventKind.Pressed => "pressed",
                UIEventKind.Submitted => "submitted",
                UIEventKind.Changed => "changed",
                _ => "selected",
            });
            lua.SetField(-2, "kind");
            lua.PushString(LuaText.Encode(uiEvent.Control));
            lua.SetField(-2, "control");

            if (uiEvent.Text is { } text)
            {
                lua.PushString(LuaText.Encode(text));
                lua.SetField(-2, "text");
            }

            if (uiEvent.Kind == UIEventKind.Changed)
            {
                lua.PushNumber(uiEvent.Value);
                lua.SetField(-2, "value");
            }

            if (uiEvent.Index >= 0)
            {
                lua.PushInteger(uiEvent.Index + 1);
                lua.SetField(-2, "index");
            }

            return 1;
        }
    }
}
