using Cosmos.Executable.Lua;
using Venture.OS.Shell;

namespace Venture.OS.Scripting;

/// <summary>
/// Lua code that lasts, such as <c>init.lua</c> or a <c>lua</c> prompt session: one interpreter that outlives a
/// single run, and the list of ways to undo what the code set up.
/// </summary>
/// <remarks>
/// <para>After the ideas in "A Programming Paradigm for Spatiotemporal Composability": everything the code sets up
/// through the owner, such as a command it registers, comes with its own undo. Disposing the owner runs those undos,
/// newest first, so nothing it set up outlives it, then disposes the interpreter.</para>
/// <para>Code runs in the owner's interpreter both when it's first loaded and later, when something calls back
/// into it, such as a command it registered. Each of those runs gets its own output and stop, so a command
/// <c>init.lua</c> registered prints to whoever ran the command, and isn't held to the limit on <c>init.lua</c>'s
/// own startup.</para>
/// </remarks>
public sealed class LuaOwner : IDisposable
{
    private readonly ShellOutputWriter writer;
    private readonly Action resetStop;
    private readonly List<Undo> undos = [];
    private Func<string?>? currentStop;

    /// <param name="name">Who the owner is, as people see it, such as <c>init.lua</c> or <c>the lua prompt</c>.</param>
    /// <param name="lifetime">When it ends, to finish a sentence, such as <c>shutdown</c> or <c>you exit</c>.</param>
    /// <param name="mayReplaceScripts">
    /// Whether commands it registers may replace system script commands of the same name. Only for code the user
    /// types, so installed code can't quietly take over a system command.
    /// </param>
    /// <param name="output">Where the code's own output goes, outside calls to what it registered.</param>
    /// <param name="workingDirectory">Where relative paths start; see <see cref="LuaRunner.Run"/>.</param>
    /// <param name="system">What the <c>venture</c> table reports, or <see langword="null"/>.</param>
    /// <param name="scopes">What the code may reach through the <c>venture</c> table.</param>
    /// <param name="libraryDirectory">Where <c>require</c> finds the system library, or <see langword="null"/>.</param>
    /// <param name="commands">Where <c>venture.command</c> registers commands, or <see langword="null"/> for none.</param>
    /// <param name="runShell">
    /// Runs a line as if typed at the shell, for <c>venture.shell</c>, or <see langword="null"/> for none.
    /// </param>
    /// <param name="openShell">
    /// Makes a shell of the code's own for <c>venture.openShell</c>, writing to the output given and starting in the
    /// folder given, or <see langword="null"/> for none.
    /// </param>
    public LuaOwner(
        string name,
        string lifetime,
        bool mayReplaceScripts,
        IShellOutput output,
        string workingDirectory,
        ISystemInfo? system = null,
        ScriptScopes scopes = ScriptScopes.None,
        string? libraryDirectory = null,
        RuntimeCommands? commands = null,
        Action<string>? runShell = null,
        Func<IShellOutput, string, ShellContext>? openShell = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(lifetime);
        ArgumentNullException.ThrowIfNull(output);

        Name = name;
        Lifetime = lifetime;
        MayReplaceScripts = mayReplaceScripts;
        Output = output;
        writer = new ShellOutputWriter(output);
        Interpreter = LuaRunner.Create(
            writer, workingDirectory, system, () => currentStop?.Invoke(), scopes, libraryDirectory, out resetStop);

        if (commands is not null)
            ShellLibrary.Open(Interpreter.State, this, commands, scopes, runShell, openShell);
    }

    /// <summary>Who the owner is, such as <c>init.lua</c>.</summary>
    public string Name { get; }

    /// <summary>When it ends, such as <c>shutdown</c>.</summary>
    public string Lifetime { get; }

    /// <summary>Whether commands it registers may replace system script commands.</summary>
    public bool MayReplaceScripts { get; }

    /// <summary>Whether it has ended, its undos run and its interpreter gone.</summary>
    public bool IsDisposed { get; private set; }

    /// <summary>
    /// Whether its code is running right now, such as one of its commands. It mustn't be disposed then: its
    /// interpreter would vanish under the running code.
    /// </summary>
    public bool IsRunning => depth > 0;

    // How many runs are under way, counting calls back into this owner from its own code.
    private int depth;

    /// <summary>The owner's interpreter.</summary>
    public LuaInterpreter Interpreter { get; }

    /// <summary>Where the code's own output goes.</summary>
    internal IShellOutput Output { get; }

    /// <summary>Where the code's own output goes, as the writer Lua writes to.</summary>
    internal ShellOutputWriter Writer => writer;

    /// <summary>
    /// Runs code in the owner's interpreter, such as loading its script, reporting a Lua error or non-zero
    /// <c>os.exit</c> on its output instead of throwing.
    /// </summary>
    /// <param name="stop">What can stop this run; see <see cref="LuaRunner.Run"/>.</param>
    /// <returns><see langword="true"/> if it finished without an error, a stop, or a non-zero exit.</returns>
    public bool Run(Action<LuaInterpreter> run, Func<string?>? stop = null)
    {
        ArgumentNullException.ThrowIfNull(run);
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        return WithStop(stop, () => LuaRunner.Report(Output, writer, () => run(Interpreter)));
    }

    /// <summary>
    /// Records how to undo something the code set up. It runs when the owner ends, newest first, or earlier
    /// through the returned handle.
    /// </summary>
    /// <returns>A handle that runs the undo now, once.</returns>
    public Undo AddUndo(Action undo)
    {
        ArgumentNullException.ThrowIfNull(undo);
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        Undo entry = new(undo);
        undos.Add(entry);
        return entry;
    }

    /// <summary>
    /// Undoes everything the code set up, newest first, then disposes the interpreter.
    /// </summary>
    /// <exception cref="InvalidOperationException">Its code is running; see <see cref="IsRunning"/>.</exception>
    public void Dispose()
    {
        if (IsDisposed)
            return;
        if (IsRunning)
            throw new InvalidOperationException($"{Name} can't end while its own code is running.");

        for (var index = undos.Count - 1; index >= 0; index--)
            undos[index].Run();

        undos.Clear();
        IsDisposed = true;
        writer.Dispose();
        Interpreter.Dispose();
    }

    /// <summary>
    /// Calls a Lua function the code left in the registry, such as a command's <c>run</c>, with
    /// <paramref name="arguments"/>, its output on <paramref name="output"/>.
    /// </summary>
    /// <param name="workingDirectory">Where relative paths start during the call, such as the shell's folder.</param>
    internal bool Call(
        int reference,
        IReadOnlyList<string> arguments,
        IShellOutput output,
        string workingDirectory,
        Func<string?>? stop) =>
        Call(reference, state =>
        {
            foreach (var argument in arguments)
                state.PushString(LuaText.Encode(argument));
            return arguments.Count;
        }, output, workingDirectory, stop);

    /// <summary>
    /// Calls a Lua function the code left in the registry with whatever <paramref name="pushArguments"/> pushes,
    /// such as an event table; it returns how many values it pushed.
    /// </summary>
    internal bool Call(
        int reference,
        Func<ILuaState, int> pushArguments,
        IShellOutput output,
        string workingDirectory,
        Func<string?>? stop)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        // A call can come while other code in this owner is running, such as a prompt line that runs one of the
        // owner's commands through venture.shell, so the caller's output and stop are put back afterwards.
        var previousOutput = Interpreter.Output;
        var previousError = Interpreter.Error;
        var previousDirectory = Interpreter.WorkingDirectory;
        Interpreter.WorkingDirectory = workingDirectory;
        using ShellOutputWriter callWriter = new(output);
        Interpreter.Output = callWriter;
        Interpreter.Error = callWriter;
        try
        {
            return WithStop(stop, () => LuaRunner.Report(output, callWriter, () => CallReference(reference, pushArguments)));
        }
        finally
        {
            Interpreter.Output = previousOutput;
            Interpreter.Error = previousError;
            Interpreter.WorkingDirectory = previousDirectory;
        }
    }

    /// <summary>
    /// How long code has to finish after a shell command it ran returns, when it was running under a stop.
    /// </summary>
    internal static readonly TimeSpan AfterShellAllowance = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Runs a shell command for the code, such as Terminal's, outside the code's own stop: the command is limited the
    /// way any command is, by Ctrl+C or Esc, and its time doesn't count against the code. The code then has
    /// <see cref="AfterShellAllowance"/> to finish, if it was running under a stop at all.
    /// </summary>
    internal void WithoutStop(Action work)
    {
        var paused = currentStop;
        currentStop = null;
        try
        {
            work();
        }
        finally
        {
            currentStop = paused is null ? null : ScriptStop.After(AfterShellAllowance);
            resetStop();
        }
    }

    private bool WithStop(Func<string?>? stop, Func<bool> run)
    {
        var previousStop = currentStop;
        currentStop = stop;
        resetStop();
        depth++;
        try
        {
            return run();
        }
        finally
        {
            depth--;
            currentStop = previousStop;
        }
    }

    private void CallReference(int reference, Func<ILuaState, int> pushArguments)
    {
        var state = Interpreter.State;
        var top = state.GetTop();
        try
        {
            state.RawGetI(LuaDef.LUA_REGISTRYINDEX, reference);
            var count = pushArguments(state);

            if (state.PCall(count, 0, 0) != ThreadStatus.LUA_OK)
            {
                var message = state.ToString(-1);
                throw new LuaException(message is null ? "(error object is not a string)" : LuaText.Decode(message));
            }
        }
        finally
        {
            state.SetTop(top);
        }
    }

    /// <summary>How to undo one thing the code set up. It runs at most once.</summary>
    public sealed class Undo
    {
        private readonly Action action;

        internal Undo(Action action) => this.action = action;

        /// <summary>Whether it has run.</summary>
        public bool IsDone { get; private set; }

        /// <summary>Runs the undo, unless it already has.</summary>
        public void Run()
        {
            if (IsDone)
                return;

            IsDone = true;
            action();
        }
    }
}
