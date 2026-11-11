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
/// </remarks>
internal sealed class LuaCommand(ISystemInfo system) : IShellCommand
{
    private readonly ISystemInfo system = system ?? throw new ArgumentNullException(nameof(system));

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
                context.Output, ReadLine, Bootstrap.RamMountPoint, system, KeyboardStop.Check, Bootstrap.LibraryDirectoryPath);
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
