using Cosmos.Executable.Lua;
using Venture.OS.Startup;

namespace Venture.OS.Shell.Commands;

/// <summary>
/// Runs one chunk of Lua code in a fresh interpreter.
/// </summary>
/// <remarks>
/// Each call gets its own interpreter, so nothing carries over between runs. <c>print</c> writes to the console,
/// the same place <see cref="ConsoleShellOutput"/> writes. Relative paths in <c>dofile</c> and <c>io.open</c> start
/// at the RAM disk, so <c>lua "dofile('script.lua')"</c> runs <c>/mnt/script.lua</c>. Lua errors and
/// <c>os.exit</c> are reported here and never reach the shell loop. There's no way to interrupt a chunk yet, so one
/// that never finishes leaves the shell waiting.
/// </remarks>
internal sealed class LuaCommand : IShellCommand
{
    public string Name => "lua";
    public string Summary => "Run a line of Lua code.";
    public string Usage => "lua <code>";
    public int MinArguments => 1;
    public int MaxArguments => 1;

    public void Execute(ShellContext context, IReadOnlyList<string> arguments)
    {
        var output = context.Output;

        using LuaInterpreter lua = new() { WorkingDirectory = Bootstrap.RamMountPoint };

        try
        {
            // "=lua" names the chunk in error messages, such as "lua:1: attempt to call a nil value".
            lua.DoString(arguments[0], "=lua");
        }
        catch (LuaException exception)
        {
            output.WriteLine($"Lua error: {exception.Message}");
            if (!string.IsNullOrEmpty(exception.LuaStackTrace))
                output.WriteLine(exception.LuaStackTrace);
        }
        catch (LuaExitException exit) when (exit.ExitCode != 0)
        {
            output.WriteLine($"Lua exited with status {exit.ExitCode}.");
        }
        catch (LuaExitException)
        {
            // os.exit() or os.exit(0) is a normal way for a chunk to finish.
        }
    }
}
