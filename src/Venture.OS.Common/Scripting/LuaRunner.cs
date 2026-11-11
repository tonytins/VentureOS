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
          elseif interval ~= {{StopCheckInterval}} then
            -- The stop was cleared, as the REPL does between lines, so go back to the usual pace.
            interval = {{StopCheckInterval}}
            sethook(hook, "", interval)
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
    /// <param name="scopes">
    /// What the code may reach through the <c>venture</c> table besides the version; see <see cref="ScriptScopes"/>.
    /// </param>
    /// <param name="libraryDirectory">
    /// Where <c>require</c> finds the system library, such as <c>/mnt/system/lib</c>, or <see langword="null"/> to
    /// leave Lua's default search path.
    /// </param>
    /// <returns><see langword="true"/> if the code finished without an error, a stop, or a non-zero exit.</returns>
    public static bool Run(
        IShellOutput output,
        string workingDirectory,
        Action<LuaInterpreter> run,
        ISystemInfo? system = null,
        Func<string?>? stop = null,
        ScriptScopes scopes = ScriptScopes.All,
        string? libraryDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentException.ThrowIfNullOrEmpty(workingDirectory);
        ArgumentNullException.ThrowIfNull(run);

        using ShellOutputWriter writer = new(output);
        using var lua = Create(writer, workingDirectory, system, stop, scopes, libraryDirectory, out _);
        return Report(output, writer, () => run(lua));
    }

    /// <summary>
    /// Runs <paramref name="action"/>, reporting a Lua error or a non-zero <c>os.exit</c> on
    /// <paramref name="output"/> instead of throwing.
    /// </summary>
    /// <param name="writer">The interpreter's writer, finished before an error so the error starts its own line.</param>
    /// <returns><see langword="true"/> if it finished without an error, a stop, or a non-zero exit.</returns>
    internal static bool Report(IShellOutput output, ShellOutputWriter writer, Action action)
    {
        try
        {
            action();
            return true;
        }
        catch (LuaException exception)
        {
            // Finish any partial line the code wrote, so the error starts on a line of its own.
            writer.FinishLine();
            output.WriteLine($"Lua error: {exception.Message}");
            if (!string.IsNullOrEmpty(exception.LuaStackTrace))
                output.WriteLine(exception.LuaStackTrace);
            return false;
        }
        catch (LuaExitException exit)
        {
            if (exit.ExitCode == 0)
                return true;

            writer.FinishLine();
            output.WriteLine($"Lua exited with status {exit.ExitCode}.");
            return false;
        }
    }

    /// <summary>
    /// Makes an interpreter with its output on <paramref name="writer"/>, the <c>venture</c> table, and the stop hook.
    /// </summary>
    /// <param name="resetStop">
    /// Clears a stop that has happened, so the next code can run, as the REPL does between lines. Does nothing
    /// without <paramref name="stop"/>.
    /// </param>
    internal static LuaInterpreter Create(
        TextWriter writer,
        string workingDirectory,
        ISystemInfo? system,
        Func<string?>? stop,
        ScriptScopes scopes,
        string? libraryDirectory,
        out Action resetStop)
    {
        LuaInterpreter lua = new() { WorkingDirectory = workingDirectory, Output = writer, Error = writer };
        if (system is not null)
        {
            VentureLibrary.Open(lua.State, system, scopes);
            FilesLibrary.Open(lua.State, () => lua.WorkingDirectory ?? "/", system, scopes);
        }

        if (libraryDirectory is not null)
            SetLibraryDirectory(lua.State, libraryDirectory);

        resetStop = stop is null ? () => { } : InstallStopHook(lua, stop);
        return lua;
    }

    /// <summary>
    /// Points <c>require</c> at <paramref name="directory"/> only, so <c>require("venture.args")</c> loads
    /// <c>venture/args.lua</c> there, and no native modules load.
    /// </summary>
    private static void SetLibraryDirectory(ILuaState state, string directory)
    {
        var root = directory.TrimEnd('/');
        state.GetGlobal("package");
        state.PushString(LuaText.Encode($"{root}/?.lua;{root}/?/init.lua"));
        state.SetField(-2, "path");
        state.PushString("");
        state.SetField(-2, "cpath");
        state.Pop(1);
    }

    /// <returns>An action that clears the latched stop.</returns>
    private static Action InstallStopHook(LuaInterpreter lua, Func<string?> stop)
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
        return () => stopped = null;
    }
}
