using Venture.OS.Scripting;
using Venture.OS.Startup;

namespace Venture.OS.Shell.Commands;

/// <summary>
/// Runs one chunk of Lua code in a fresh interpreter.
/// </summary>
/// <remarks>
/// <see cref="LuaRunner"/> does the work, as it does for script commands: output goes through the shell, relative
/// paths start at the RAM disk (so <c>lua "dofile('script.lua')"</c> runs <c>/mnt/script.lua</c>), and Lua errors
/// and <c>os.exit</c> are reported here. There's no way to interrupt a chunk yet, so one that never finishes leaves
/// the shell waiting.
/// </remarks>
internal sealed class LuaCommand : IShellCommand
{
    public string Name => "lua";
    public string Summary => "Run a line of Lua code.";
    public string Usage => "lua <code>";
    public int MinArguments => 1;
    public int MaxArguments => 1;

    // "=lua" names the chunk in error messages, such as "lua:1: attempt to call a nil value".
    public void Execute(ShellContext context, IReadOnlyList<string> arguments) =>
        LuaRunner.Run(context.Output, Bootstrap.RamMountPoint, lua => lua.DoString(arguments[0], "=lua"));
}
