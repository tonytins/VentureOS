using Venture.OS.Scripting;
using Venture.OS.Startup;

namespace Venture.OS.Shell.Commands;

/// <summary>
/// Runs one chunk of Lua code, or with no code, opens an interactive Lua prompt.
/// </summary>
/// <remarks>
/// <para><c>lua "code"</c> runs through <see cref="LuaRunner"/>, as script commands do, in a fresh interpreter. Plain
/// <c>lua</c> opens <see cref="LuaRepl"/>, where values carry over between lines until <c>exit</c>.</para>
/// <para>Either way, output goes through the shell, relative paths start at the RAM disk (so
/// <c>dofile('script.lua')</c> runs <c>/mnt/script.lua</c>), Lua errors and <c>os.exit</c> are reported here, and
/// Ctrl+C or Esc stops code that doesn't finish. The code gets every scope of the <c>venture</c> table, since it's
/// the user typing it.</para>
/// <para>The prompt is an owner: commands it registers with <c>venture.command</c> can be tried with <c>!name</c>
/// or <c>venture.shell</c>, may replace a system script, and vanish on <c>exit</c>.</para>
/// </remarks>
/// <param name="system">What the <c>venture</c> table reports.</param>
/// <param name="commands">
/// Where the prompt's <c>venture.command</c> registers; read when the prompt opens, since the shell sets it up after
/// this command.
/// </param>
/// <param name="runShell">Runs a line as if typed at the shell, for <c>!</c> and <c>venture.shell</c>.</param>
internal sealed class LuaCommand(ISystemInfo system, Func<RuntimeCommands?> commands, Action<string> runShell)
    : IShellCommand
{
    private readonly ISystemInfo system = system ?? throw new ArgumentNullException(nameof(system));
    private readonly Func<RuntimeCommands?> commands = commands ?? throw new ArgumentNullException(nameof(commands));
    private readonly Action<string> runShell = runShell ?? throw new ArgumentNullException(nameof(runShell));

    public string Name => "lua";
    public string Summary => "Run a line of Lua code, or open a Lua prompt.";
    public string Usage => "lua [code]";
    public int MinArguments => 0;
    public int MaxArguments => 1;

    public void Execute(ShellContext context, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            LuaRepl.Run(
                context.Output,
                ReadLine,
                Bootstrap.RamMountPoint,
                system,
                KeyboardStop.Check,
                Bootstrap.LibraryDirectoryPath,
                commands(),
                runShell);
            return;
        }

        // "=lua" names the chunk in error messages, such as "lua:1: attempt to call a nil value".
        LuaRunner.Run(
            context.Output,
            Bootstrap.RamMountPoint,
            lua => lua.DoString(arguments[0], "=lua"),
            system,
            KeyboardStop.Check,
            libraryDirectory: Bootstrap.LibraryDirectoryPath);
    }

    private static string? ReadLine(string prompt)
    {
        Console.Write(prompt);
        return Console.ReadLine();
    }
}
