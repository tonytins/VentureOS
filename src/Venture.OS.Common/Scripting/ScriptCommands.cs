using System.Diagnostics.CodeAnalysis;
using Venture.OS.Shell;

namespace Venture.OS.Scripting;

/// <summary>
/// Commands written in Lua: <c>&lt;name&gt;.lua</c> files in one directory, such as <c>/mnt/system/commands</c>.
/// </summary>
/// <remarks>
/// <para>The directory is read on every lookup, so a script added while the system runs is found at once. When the
/// directory doesn't exist, as when the system files failed to install, there are simply no script commands.</para>
/// <para>A script describes itself in the comment lines at its top. The first is its summary for <c>help</c>, and
/// one starting <c>-- Usage:</c> gives its usage:</para>
/// <code>
/// -- Print the arguments, separated by spaces.
/// -- Usage: echo [text...]
/// print(table.concat({...}, " "))
/// </code>
/// <para>A script gets its arguments as <c>...</c>, and checks them itself.</para>
/// </remarks>
public sealed class ScriptCommands : ICommandSource
{
    private const string Extension = ".lua";

    /// <summary>What <c>help</c> shows for a script without a summary comment.</summary>
    public const string NoSummary = "No description.";

    private readonly string directory;
    private readonly string workingDirectory;
    private readonly ISystemInfo? system;
    private readonly Func<string?>? stop;

    /// <param name="directory">Where the command scripts are.</param>
    /// <param name="workingDirectory">Where relative paths in a script start.</param>
    /// <param name="system">What the scripts' <c>venture</c> table reports, or <see langword="null"/> for none.</param>
    /// <param name="stop">What can stop a running script, such as a key press; see <see cref="LuaRunner.Run"/>.</param>
    public ScriptCommands(
        string directory, string workingDirectory, ISystemInfo? system = null, Func<string?>? stop = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(directory);
        ArgumentException.ThrowIfNullOrEmpty(workingDirectory);

        this.directory = directory.TrimEnd('/');
        this.workingDirectory = workingDirectory;
        this.system = system;
        this.stop = stop;
    }

    public IReadOnlyList<IShellCommand> Commands
    {
        get
        {
            List<IShellCommand> commands = [];
            if (!Directory.Exists(directory))
                return commands;

            foreach (var path in Directory.GetFiles(directory))
            {
                var fileName = Path.GetFileName(path);
                if (!fileName.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
                    continue;

                // FAT ignores case, so a file saved as ECHO.LUA is still the echo command.
                var name = fileName[..^Extension.Length].ToLowerInvariant();
                if (CommandRegistry.IsValidName(name))
                    commands.Add(Load(name, path));
            }

            return commands;
        }
    }

    public bool TryGet(string name, [NotNullWhen(true)] out IShellCommand? command)
    {
        ArgumentNullException.ThrowIfNull(name);

        command = null;

        // The name becomes part of a path, so only names the registry would accept are looked up.
        if (!CommandRegistry.IsValidName(name))
            return false;

        var path = $"{directory}/{name}{Extension}";
        if (!File.Exists(path))
            return false;

        command = Load(name, path);
        return true;
    }

    private ScriptCommand Load(string name, string path)
    {
        var header = ScriptHeader.Read(path);
        return new ScriptCommand(name, header, path, workingDirectory, system, stop);
    }

    /// <summary>
    /// One Lua command script, run by <see cref="LuaRunner"/> with the command's arguments.
    /// </summary>
    private sealed class ScriptCommand(
        string name,
        ScriptHeader header,
        string path,
        string workingDirectory,
        ISystemInfo? system,
        Func<string?>? stop)
        : IScriptCommand
    {
        public string Name { get; } = name;
        public string Summary { get; } = string.IsNullOrEmpty(header.Summary) ? NoSummary : header.Summary;
        public string Usage { get; } = header.Usage ?? $"{name} [arguments]";
        public int MinArguments => 0;
        public int MaxArguments => int.MaxValue;
        public ScriptScopes Needs => header.Needs;
        public string? HeaderError => header.Error;

        public void Execute(ShellContext context, IReadOnlyList<string> arguments)
        {
            if (header.Error is { } error)
            {
                context.Output.WriteLine($"{Name} can't run. Its -- Needs: line: {error}");
                return;
            }

            var values = new string[arguments.Count];
            for (var index = 0; index < values.Length; index++)
                values[index] = arguments[index];

            LuaRunner.Run(context.Output, workingDirectory, lua => lua.DoFile(path, values), system, stop, Needs);
        }
    }
}
