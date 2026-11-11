using Cosmos.Executable.Lua;
using Venture.OS.Shell;

namespace Venture.OS.Scripting;

/// <summary>
/// Runs Lua for a shell command: a fresh interpreter, output through the shell, and errors reported instead of
/// thrown.
/// </summary>
public static class LuaRunner
{
    /// <summary>How many Lua instructions run between stop checks.</summary>
    /// <remarks>Often enough to stop within moments, rarely enough not to slow scripts down.</remarks>
    internal const int StopCheckInterval = 1000;

    /// <summary>The error a script gets if it tries to replace or remove the stop hook.</summary>
    public const string SetHookReserved = "debug.sethook is reserved by VentureOS";

    // The global that hands the stop check to the hook chunk; the chunk clears it before the script runs.
    private const string StopHandoff = "__venture_stop";

    // Installs the stop hook with the standard debug.sethook, then takes that function away from the script.
    //
    // - Once a check has said stop, the hook runs on every instruction, so the error escapes a pcall or
    //   coroutine.resume that catches it almost at once.
    // - debug.sethook keeps the hook function per thread, and a new coroutine inherits only the count, so the
    //   hook would do nothing there. coroutine.create and coroutine.wrap are replaced to hook each new
    //   coroutine too; wrap is rebuilt on create, propagating errors as the standard one does.
    private static readonly string StopHookChunk = $$"""
        local stop = {{StopHandoff}}
        {{StopHandoff}} = nil
        local sethook, create, resume = debug.sethook, coroutine.create, coroutine.resume
        local interval = {{StopCheckInterval}}
        local hook
        hook = function()
          local reason = stop()
          if reason then
            interval = 1
            sethook(hook, "", 1)
            error(reason, 0)
          end
        end
        sethook(hook, "", interval)

        coroutine.create = function(f)
          local co = create(f)
          sethook(co, hook, "", interval)
          return co
        end
        coroutine.wrap = function(f)
          local co = coroutine.create(f)
          return function(...)
            local results = table.pack(resume(co, ...))
            if not results[1] then
              error(results[2], 0)
            end
            return table.unpack(results, 2, results.n)
          end
        end
        debug.sethook = function() error("{{SetHookReserved}}", 2) end
        """;

    /// <summary>
    /// Makes an interpreter, lets <paramref name="run"/> use it, then disposes it.
    /// </summary>
    /// <remarks>
    /// <c>print</c>, <c>io.write</c>, and <c>io.stderr</c> go to <paramref name="output"/>. A Lua error, including a
    /// stop, is reported with its traceback, and <c>os.exit</c> with a non-zero status is reported as that status;
    /// none of them is thrown. Other exceptions, which would mean a bug in the interpreter or the caller, are left to
    /// the shell loop.
    /// </remarks>
    /// <param name="output">Where the code's output and any error go.</param>
    /// <param name="workingDirectory">Where relative paths in <c>dofile</c>, <c>require</c>, and <c>io.open</c> start.</param>
    /// <param name="run">What to do with the interpreter, such as <c>lua =&gt; lua.DoString(code)</c>.</param>
    /// <param name="system">
    /// What the <c>venture</c> table reports, or <see langword="null"/> to leave the table out.
    /// </param>
    /// <param name="stop">
    /// Checked every <see cref="StopCheckInterval"/> instructions. A reason it returns, such as <c>Interrupted.</c>,
    /// ends the code with that message. Without it, code runs until it finishes. See <see cref="ScriptStop"/>.
    /// </param>
    /// <returns><see langword="true"/> if the code finished without an error, a stop, or a non-zero exit.</returns>
    public static bool Run(
        IShellOutput output,
        string workingDirectory,
        Action<LuaInterpreter> run,
        ISystemInfo? system = null,
        Func<string?>? stop = null)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentException.ThrowIfNullOrEmpty(workingDirectory);
        ArgumentNullException.ThrowIfNull(run);

        using ShellOutputWriter writer = new(output);
        using LuaInterpreter lua = new() { WorkingDirectory = workingDirectory, Output = writer, Error = writer };
        if (system is not null)
            VentureLibrary.Open(lua.State, system);
        if (stop is not null)
            InstallStopHook(lua, stop);

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

    private static void InstallStopHook(LuaInterpreter lua, Func<string?> stop)
    {
        // Once a check says stop, keep saying it: a key press is read only once, and the script may catch the
        // first error with pcall.
        string? stopped = null;
        lua.State.PushCSharpFunction(state =>
        {
            if ((stopped ??= stop()) is { } reason)
                state.PushString(LuaText.Encode(reason));
            else
                state.PushNil();

            return 1;
        });
        lua.State.SetGlobal(StopHandoff);
        lua.DoString(StopHookChunk, "=stop-hook");
    }
}
