using Cosmos.Executable.Lua;
using Venture.OS.Shell;

namespace Venture.OS.Scripting;

/// <summary>
/// Runs Lua for a shell command: a fresh interpreter, output through the shell, and errors reported instead of
/// thrown.
/// </summary>
public static class LuaRunner
{
    /// <summary>
    /// Makes an interpreter, lets <paramref name="run"/> use it, then disposes it.
    /// </summary>
    /// <remarks>
    /// <c>print</c>, <c>io.write</c>, and <c>io.stderr</c> go to <paramref name="output"/>. A Lua error is reported with
    /// its traceback, and <c>os.exit</c> with a non-zero status is reported as that status; neither is thrown.
    /// Other exceptions, which would mean a bug in the interpreter or the caller, are left to the shell loop.
    /// </remarks>
    /// <param name="output">Where the code's output and any error go.</param>
    /// <param name="workingDirectory">Where relative paths in <c>dofile</c>, <c>require</c>, and <c>io.open</c> start.</param>
    /// <param name="run">What to do with the interpreter, such as <c>lua =&gt; lua.DoString(code)</c>.</param>
    /// <param name="system">
    /// What the <c>venture</c> table reports, or <see langword="null"/> to leave the table out.
    /// </param>
    /// <returns><see langword="true"/> if the code finished without an error or a non-zero exit.</returns>
    public static bool Run(
        IShellOutput output, string workingDirectory, Action<LuaInterpreter> run, ISystemInfo? system = null)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentException.ThrowIfNullOrEmpty(workingDirectory);
        ArgumentNullException.ThrowIfNull(run);

        using ShellOutputWriter writer = new(output);
        using LuaInterpreter lua = new() { WorkingDirectory = workingDirectory, Output = writer, Error = writer };
        if (system is not null)
            VentureLibrary.Open(lua.State, system);

        try
        {
            run(lua);
            return true;
        }
        catch (LuaException exception)
        {
            // Finish any partial line the code wrote, so the error starts on a line of its own.
            writer.Dispose();
            output.WriteLine($"Lua error: {exception.Message}");
            if (!string.IsNullOrEmpty(exception.LuaStackTrace))
                output.WriteLine(exception.LuaStackTrace);
            return false;
        }
        catch (LuaExitException exit)
        {
            if (exit.ExitCode == 0)
                return true;

            writer.Dispose();
            output.WriteLine($"Lua exited with status {exit.ExitCode}.");
            return false;
        }
    }
}
