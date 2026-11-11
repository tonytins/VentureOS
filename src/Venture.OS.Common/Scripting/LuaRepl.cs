using Cosmos.Executable.Lua;
using Venture.OS.Shell;

namespace Venture.OS.Scripting;

/// <summary>
/// An interactive Lua prompt: read a line, run it, print the result, and go again, in one interpreter so values
/// carry over between lines.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description>A line that's an expression prints its value: <c>1 + 1</c> prints <c>2</c>.</description></item>
/// <item><description>A statement left unfinished at the end of a line, such as <c>for i = 1, 3 do</c>, goes on
/// at a <see cref="ContinuationPrompt"/>.</description></item>
/// <item><description>A stop, such as Ctrl+C, ends only the line that's running; the prompt comes back.</description></item>
/// <item><description><c>exit</c>, the end of the input, or <c>os.exit</c> leaves the prompt.</description></item>
/// <item><description>A line starting with <c>!</c> runs the rest as a shell command, as <c>venture.shell</c>
/// does.</description></item>
/// </list>
/// <para>The session is a <see cref="LuaOwner"/> that may replace system script commands, so commands registered
/// with <c>venture.command</c> work until <c>exit</c>, then vanish, restoring anything they replaced.</para>
/// </remarks>
public static class LuaRepl
{
    /// <summary>Shown before each new line.</summary>
    public const string Prompt = "lua> ";

    /// <summary>Shown before each further line of an unfinished statement.</summary>
    public const string ContinuationPrompt = "...> ";

    /// <summary>What the user types to leave.</summary>
    public const string ExitWord = "exit";

    /// <summary>What starts a line that runs as a shell command.</summary>
    public const string ShellPrefix = "!";

    /// <summary>The session's name as an owner, as <c>help</c> shows it.</summary>
    public const string OwnerName = "the lua prompt";

    /// <summary>Shown once, when the prompt opens.</summary>
    /// <remarks>Short enough to fit the console's width.</remarks>
    public const string Greeting = "Lua 5.5 prompt. Type \"exit\" to leave.";

    // The chunk name for errors, such as "lua:1: attempt to call a nil value".
    private const string ChunkName = "=lua";

    // What the compiler's message ends with when a statement is only unfinished, as the reference lua checks.
    private const string UnfinishedMark = "<eof>";

    /// <summary>
    /// Runs the prompt until the user leaves.
    /// </summary>
    /// <param name="output">Where results, output, and errors go.</param>
    /// <param name="readLine">
    /// Shows the prompt it's given and reads one line, or returns <see langword="null"/> when the input ends.
    /// </param>
    /// <param name="workingDirectory">Where relative paths in <c>dofile</c> and <c>io.open</c> start.</param>
    /// <param name="system">What the <c>venture</c> table reports, with every scope, or <see langword="null"/>.</param>
    /// <param name="stop">What can stop a running line, such as a key press; see <see cref="LuaRunner.Run"/>.</param>
    /// <param name="libraryDirectory">Where <c>require</c> finds the system library; see <see cref="LuaRunner.Run"/>.</param>
    /// <param name="commands">Where <c>venture.command</c> registers commands, or <see langword="null"/> for none.</param>
    /// <param name="runShell">
    /// Runs a line as if typed at the shell, for <c>!</c> and <c>venture.shell</c>, or <see langword="null"/> for none.
    /// </param>
    /// <param name="currentDirectory">
    /// The shell's folder, read before each line so relative paths follow a <c>!cd</c>, or <see langword="null"/> to
    /// stay in <paramref name="workingDirectory"/>.
    /// </param>
    public static void Run(
        IShellOutput output,
        Func<string, string?> readLine,
        string workingDirectory,
        ISystemInfo? system = null,
        Func<string?>? stop = null,
        string? libraryDirectory = null,
        RuntimeCommands? commands = null,
        Action<string>? runShell = null,
        Func<string>? currentDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(readLine);
        ArgumentException.ThrowIfNullOrEmpty(workingDirectory);

        using LuaOwner owner = new(
            OwnerName,
            "you exit",
            mayReplaceScripts: true,
            output,
            workingDirectory,
            system,
            ScriptScopes.All,
            libraryDirectory,
            commands,
            runShell);
        var state = owner.Interpreter.State;
        var writer = owner.Writer;

        output.WriteLine(Greeting);
        while (true)
        {
            var line = readLine(Prompt);
            if (line is null || line.Trim() == ExitWord)
                return;

            if (line.Trim().Length == 0)
                continue;

            if (line.TrimStart().StartsWith(ShellPrefix, StringComparison.Ordinal))
            {
                RunShellLine(output, runShell, line.TrimStart()[ShellPrefix.Length..].Trim());
                continue;
            }

            if (currentDirectory is not null)
                owner.Interpreter.WorkingDirectory = currentDirectory();

            var top = state.GetTop();
            if (!TryCompile(state, line, readLine, out var error))
            {
                if (error is null)
                    return;

                output.WriteLine($"Lua error: {error}");
                continue;
            }

            // os.exit leaves the prompt, so it's caught here rather than reported as an ordinary exit.
            int? exitCode = null;
            owner.Run(
                _ =>
                {
                    try
                    {
                        var status = state.PCall(0, -1, 0);
                        if (status == ThreadStatus.LUA_OK)
                            status = PrintResults(state, top);

                        if (status != ThreadStatus.LUA_OK)
                            throw new LuaException(ErrorMessage(state));
                    }
                    catch (LuaExitException exit)
                    {
                        exitCode = exit.ExitCode;
                    }
                    finally
                    {
                        state.SetTop(top);
                    }
                },
                stop);
            writer.FinishLine();

            if (exitCode is { } code)
            {
                if (code != 0)
                    output.WriteLine($"Lua exited with status {code}.");
                return;
            }
        }
    }

    /// <summary>Runs what follows <c>!</c> as a shell command, reporting a failure rather than leaving the prompt.</summary>
    private static void RunShellLine(IShellOutput output, Action<string>? runShell, string line)
    {
        if (runShell is null)
        {
            output.WriteLine("Shell commands aren't available here.");
            return;
        }

        if (line.Length == 0)
            return;

        try
        {
            runShell(line);
        }
        catch (Exception exception)
        {
            // The shell loop would catch this, but only by leaving the prompt and its commands behind.
            output.WriteLine($"Command failed ({exception.GetType().Name}): {exception.Message}");
        }
    }

    /// <summary>
    /// Compiles what the user typed, as an expression to print if it is one, otherwise as statements, reading more
    /// lines while a statement is unfinished. Leaves the compiled chunk on the stack.
    /// </summary>
    /// <param name="error">The syntax error, or <see langword="null"/> when the input ended first.</param>
    private static bool TryCompile(ILuaState state, string line, Func<string, string?> readLine, out string? error)
    {
        var top = state.GetTop();
        if (state.L_LoadBufferX(LuaText.Encode($"return {line}"), ChunkName, "t") == ThreadStatus.LUA_OK)
        {
            error = null;
            return true;
        }

        state.SetTop(top);
        var code = line;
        while (true)
        {
            if (state.L_LoadBufferX(LuaText.Encode(code), ChunkName, "t") == ThreadStatus.LUA_OK)
            {
                error = null;
                return true;
            }

            var message = ErrorMessage(state);
            state.SetTop(top);
            if (!message.EndsWith(UnfinishedMark, StringComparison.Ordinal))
            {
                error = message;
                return false;
            }

            var more = readLine(ContinuationPrompt);
            if (more is null)
            {
                error = null;
                return false;
            }

            code = $"{code}\n{more}";
        }
    }

    /// <summary>Prints whatever the line returned, through Lua's own <c>print</c>, then drops it.</summary>
    private static ThreadStatus PrintResults(ILuaState state, int top)
    {
        var count = state.GetTop() - top;
        if (count == 0)
            return ThreadStatus.LUA_OK;

        state.GetGlobal("print");
        state.Insert(top + 1);
        return state.PCall(count, 0, 0);
    }

    private static string ErrorMessage(ILuaState state) =>
        state.ToString(-1) is { } message ? LuaText.Decode(message) : "(error object is not a string)";
}
