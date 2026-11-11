using Cosmos.Executable.Lua;
using Venture.OS.Shell;

namespace Venture.OS.Scripting;

/// <summary>
/// <c>venture.files</c>: listing, reading, and changing files and folders, under the <c>files</c> scope.
/// </summary>
/// <remarks>
/// <para>Paths start from the code's current folder, as typed paths do in the shell; see <see cref="ShellPaths"/>.
/// Failures a script should expect, such as a missing file, return <c>nil</c> and a message, as Lua's <c>io</c>
/// does, so a command can print the reason and carry on. A wrong argument type raises an error, since it's the
/// script's bug.</para>
/// <list type="bullet">
/// <item><description><c>list([path])</c>: <c>{ {name, type, size}, ... }</c>, sorted by name, where <c>type</c> is
/// <c>"file"</c> or <c>"folder"</c>, and <c>size</c> is in bytes, 0 for folders.</description></item>
/// <item><description><c>info(path)</c>: <c>{name, path, type, size}</c>.</description></item>
/// <item><description><c>read(path)</c>: the whole file, byte for byte.</description></item>
/// <item><description><c>write(path, data, [append])</c>: writes or adds to a file, byte for byte.</description></item>
/// <item><description><c>mkdir(path, [parents])</c>: makes a folder, and with <c>parents</c>, any folders above it
/// that are missing, without complaining if it's already there.</description></item>
/// <item><description><c>remove(path, [recursive])</c>: removes a file or an empty folder, or with
/// <c>recursive</c>, a folder and everything in it.</description></item>
/// <item><description><c>space([path])</c>: <c>{free, total}</c> in bytes, for where the path is stored.</description></item>
/// <item><description><c>resolve([path])</c>: the full path, such as <c>/mnt/notes</c>.</description></item>
/// </list>
/// <para>Nothing that changes files works inside the system's own folder (<see cref="ISystemInfo.SystemDirectory"/>),
/// which is rebuilt at every boot, so a stray <c>rm -r</c> can't break the running system.</para>
/// </remarks>
public static class FilesLibrary
{
    /// <summary>The field the functions live under, as <c>venture.files</c>.</summary>
    public const string FieldName = "files";

    /// <summary>
    /// Puts <c>venture.files</c> in the <c>venture</c> table of <paramref name="state"/>, which must exist.
    /// </summary>
    /// <param name="currentDirectory">The code's current folder, read on every call.</param>
    /// <param name="system">The system folder to protect, and free space, or <see langword="null"/>.</param>
    /// <param name="scopes">Without <see cref="ScriptScopes.Files"/>, every function explains the missing scope.</param>
    public static void Open(ILuaState state, Func<string> currentDirectory, ISystemInfo? system, ScriptScopes scopes)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(currentDirectory);

        Files files = new(currentDirectory, system);
        NameFuncPair[] functions =
        [
            new("list", files.List),
            new("info", files.Info),
            new("read", files.Read),
            new("write", files.Write),
            new("mkdir", files.MakeFolder),
            new("remove", files.Remove),
            new("space", files.Space),
            new("resolve", files.Resolve),
        ];

        if ((scopes & ScriptScopes.Files) == 0)
        {
            for (var index = 0; index < functions.Length; index++)
            {
                var message = VentureLibrary.UndeclaredMessage($"{FieldName}.{functions[index].Name}", ScriptScopes.Files);
                functions[index] = new(functions[index].Name, lua =>
                {
                    lua.PushString(LuaText.Encode(message));
                    return lua.Error();
                });
            }
        }

        state.GetGlobal(VentureLibrary.GlobalName);
        state.NewTable();
        foreach (var function in functions)
        {
            state.PushCSharpFunction(function.Func);
            state.SetField(-2, function.Name);
        }

        state.SetField(-2, FieldName);
        state.Pop(1);
    }

    /// <summary>The functions, sharing the current folder and the system folder.</summary>
    private sealed class Files(Func<string> currentDirectory, ISystemInfo? system)
    {
        public int List(ILuaState lua)
        {
            var path = PathArgument(lua, 1, optional: true);
            return Guard(lua, () =>
            {
                if (File.Exists(path))
                    return Missing(lua, $"{path} is a file, not a folder");
                if (!Directory.Exists(path))
                    return Missing(lua, $"there's no folder at {path}");

                List<(string Name, bool IsFolder, long Size)> entries = [];
                foreach (var folder in Directory.GetDirectories(path))
                    entries.Add((Path.GetFileName(folder), true, 0));
                foreach (var file in Directory.GetFiles(path))
                    entries.Add((Path.GetFileName(file), false, new FileInfo(file).Length));

                entries.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name));

                lua.CreateTable(entries.Count, 0);
                for (var index = 0; index < entries.Count; index++)
                {
                    var entry = entries[index];
                    lua.CreateTable(0, 3);
                    SetString(lua, "name", entry.Name);
                    SetString(lua, "type", entry.IsFolder ? "folder" : "file");
                    lua.PushInteger(entry.Size);
                    lua.SetField(-2, "size");
                    lua.RawSetI(-2, index + 1);
                }

                return 1;
            });
        }

        public int Info(ILuaState lua)
        {
            var path = PathArgument(lua, 1, optional: false);
            return Guard(lua, () =>
            {
                bool isFolder;
                long size;
                if (Directory.Exists(path))
                    (isFolder, size) = (true, 0);
                else if (File.Exists(path))
                    (isFolder, size) = (false, new FileInfo(path).Length);
                else
                    return Missing(lua, $"there's no file or folder at {path}");

                lua.CreateTable(0, 4);
                SetString(lua, "name", path == "/" ? "/" : Path.GetFileName(path));
                SetString(lua, "path", path);
                SetString(lua, "type", isFolder ? "folder" : "file");
                lua.PushInteger(size);
                lua.SetField(-2, "size");
                return 1;
            });
        }

        public int Read(ILuaState lua)
        {
            var path = PathArgument(lua, 1, optional: false);
            return Guard(lua, () =>
            {
                if (Directory.Exists(path))
                    return Missing(lua, $"{path} is a folder, not a file");
                if (!File.Exists(path))
                    return Missing(lua, $"there's no file at {path}");

                lua.PushString(FromBytes(File.ReadAllBytes(path)));
                return 1;
            });
        }

        public int Write(ILuaState lua)
        {
            var path = PathArgument(lua, 1, optional: false);
            var data = lua.L_CheckString(2);
            var append = lua.ToBoolean(3);
            return Guard(lua, () =>
            {
                if (Protected(lua, path) is { } refused)
                    return refused;
                if (Directory.Exists(path))
                    return Missing(lua, $"{path} is a folder, not a file");
                if (!Directory.Exists(Parent(path)))
                    return Missing(lua, $"there's no folder at {Parent(path)}");

                var bytes = ToBytes(data);
                if (append)
                {
                    using var stream = new FileStream(path, FileMode.Append, FileAccess.Write);
                    stream.Write(bytes);
                }
                else
                {
                    File.WriteAllBytes(path, bytes);
                }

                lua.PushBoolean(true);
                return 1;
            });
        }

        public int MakeFolder(ILuaState lua)
        {
            var path = PathArgument(lua, 1, optional: false);
            var parents = lua.ToBoolean(2);
            return Guard(lua, () =>
            {
                if (Protected(lua, path) is { } refused)
                    return refused;
                if (File.Exists(path))
                    return Missing(lua, $"{path} is a file");
                if (Directory.Exists(path))
                {
                    if (parents)
                    {
                        lua.PushBoolean(true);
                        return 1;
                    }

                    return Missing(lua, $"{path} already exists");
                }

                if (!parents && !Directory.Exists(Parent(path)))
                    return Missing(lua, $"there's no folder at {Parent(path)}");

                Directory.CreateDirectory(path);
                lua.PushBoolean(true);
                return 1;
            });
        }

        public int Remove(ILuaState lua)
        {
            var path = PathArgument(lua, 1, optional: false);
            var recursive = lua.ToBoolean(2);
            return Guard(lua, () =>
            {
                if (path == "/")
                    return Missing(lua, "/ can't be removed");
                if (Protected(lua, path) is { } refused)
                    return refused;
                if (system?.SystemDirectory is { } systemFolder && ShellPaths.IsWithin(systemFolder, path))
                    return Missing(lua, $"{path} holds the system's own folder, {systemFolder}");

                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                else if (Directory.Exists(path))
                {
                    if (!recursive && (Directory.GetFiles(path).Length > 0 || Directory.GetDirectories(path).Length > 0))
                        return Missing(lua, $"{path} is a folder with things in it");

                    Directory.Delete(path, recursive);
                }
                else
                {
                    return Missing(lua, $"there's no file or folder at {path}");
                }

                lua.PushBoolean(true);
                return 1;
            });
        }

        public int Space(ILuaState lua)
        {
            var path = PathArgument(lua, 1, optional: true);
            return Guard(lua, () =>
            {
                if (system?.Space(path) is not { } space || space.FreeBytes > space.TotalBytes)
                    return Missing(lua, $"the space at {path} is unknown");

                lua.CreateTable(0, 2);
                PushBytes(lua, space.FreeBytes);
                lua.SetField(-2, "free");
                PushBytes(lua, space.TotalBytes);
                lua.SetField(-2, "total");
                return 1;
            });
        }

        public int Resolve(ILuaState lua)
        {
            lua.PushString(LuaText.Encode(PathArgument(lua, 1, optional: true)));
            return 1;
        }

        /// <summary>The full path an argument names; a missing optional one is the current folder.</summary>
        private string PathArgument(ILuaState lua, int index, bool optional)
        {
            var text = optional && lua.IsNoneOrNil(index) ? "." : LuaText.Decode(lua.L_CheckString(index));
            return ShellPaths.Resolve(currentDirectory(), text);
        }

        /// <summary>Refuses a change inside the system folder, or returns <see langword="null"/> to go on.</summary>
        private int? Protected(ILuaState lua, string path) =>
            system?.SystemDirectory is { } folder && ShellPaths.IsWithin(path, folder)
                ? Missing(lua, $"{folder} is the system's own folder, rebuilt at every boot, and can't be changed")
                : null;

        private static string Parent(string path)
        {
            var end = path.LastIndexOf('/');
            return end <= 0 ? "/" : path[..end];
        }

        /// <summary>Turns a file system failure into nil and a message, as an expected failure.</summary>
        private static int Guard(ILuaState lua, Func<int> body)
        {
            try
            {
                return body();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                                  or ArgumentException or NotSupportedException)
            {
                return Missing(lua, exception.Message);
            }
        }

        private static int Missing(ILuaState lua, string message)
        {
            lua.PushNil();
            lua.PushString(LuaText.Encode(message));
            return 2;
        }

        private static void SetString(ILuaState lua, string key, string value)
        {
            lua.PushString(LuaText.Encode(value));
            lua.SetField(-2, key);
        }

        private static void PushBytes(ILuaState lua, ulong value)
        {
            if (value <= long.MaxValue)
                lua.PushInteger((long)value);
            else
                lua.PushNumber(value);
        }

        // A Lua string here holds one character per byte, so files keep their bytes exactly, whatever they hold.
        private static string FromBytes(byte[] bytes) =>
            string.Create(bytes.Length, bytes, (characters, source) =>
            {
                for (var index = 0; index < source.Length; index++)
                    characters[index] = (char)source[index];
            });

        private static byte[] ToBytes(string text)
        {
            var bytes = new byte[text.Length];
            for (var index = 0; index < text.Length; index++)
                bytes[index] = (byte)text[index];
            return bytes;
        }
    }
}
