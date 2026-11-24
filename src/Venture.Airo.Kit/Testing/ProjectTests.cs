using Cosmos.Executable.Lua;
using Venture.Airo.Projects;
using Venture.OS.Scripting;
using Venture.OS.Shell;

namespace Venture.Airo.Testing;

/// <summary>How a test came out: passed, or failed and why.</summary>
/// <param name="File">The test file, relative to the project, such as <c>Tests/panes.lua</c>.</param>
/// <param name="Name">The case's name, or <see langword="null"/> when the file itself couldn't be run.</param>
/// <param name="Failure">Why it failed, or <see langword="null"/> when it passed.</param>
public sealed record AppTestResult(string File, string? Name, string? Failure)
{
    /// <summary>Whether it passed.</summary>
    public bool Passed => Failure is null;

    /// <summary>
    /// The result as one line, as airo prints it and Roundhouse logs it: <c>pass</c> or <c>fail</c>, the file, the
    /// case, and why it failed.
    /// </summary>
    public string Describe()
    {
        var what = Name is null ? File : $"{File}: {Name}";
        return Failure is null ? $"pass {what}" : $"fail {what}: {Failure.ReplaceLineEndings(" ")}";
    }
}

/// <summary>
/// Runs a project's tests: every <c>.lua</c> file at the top of its <c>Tests/</c> folder, in name order, each case on
/// a fresh VentureOS with the project's app built into it and opened (<see cref="AppUnderTest"/>).
/// </summary>
/// <remarks>
/// <para>A test file is the project's own code and runs on the computer, as a build script would, with Lua's
/// standard libraries; only the app runs inside VentureOS's rules.</para>
/// <para>A case fails when a check fails, when it raises an error, when the app doesn't open, or when the app hits a
/// Lua error while the case runs, even one the case didn't check for, since a person would have seen it. Each case
/// is bounded by <see cref="CaseLimit"/>.</para>
/// <para>It runs a case at a time, through <see cref="Next"/>, so a window can run one between frames and stay
/// responsive; <see cref="Run"/> runs them all. The desktop's look is the whole process's, so cases run on one
/// thread, the one a window draws on.</para>
/// </remarks>
public sealed class ProjectTests : IDisposable
{
    /// <summary>The project's folder its tests are in.</summary>
    public const string FolderName = "Tests";

    /// <summary>How long one case may run before it's stopped and fails.</summary>
    public static readonly TimeSpan CaseLimit = TimeSpan.FromSeconds(30);

    private readonly RoundhouseProject project;
    private readonly string rootfs;
    private readonly SimulatedVersion version;
    private readonly IShellOutput printed;
    private readonly IReadOnlyDictionary<string, string> edits;
    private readonly Queue<string> files;

    // The file whose cases are running: its interpreter, its name, and the cases still to run.
    private LuaOwner? owner;
    private string? file;
    private readonly Queue<(string Name, int Reference)> cases = new();
    private Func<string?>? stop;

    private ProjectTests(
        RoundhouseProject project, string rootfs, SimulatedVersion version, IShellOutput printed, IReadOnlyDictionary<string, string> edits)
    {
        this.project = project;
        this.rootfs = rootfs;
        this.version = version;
        this.printed = printed;
        this.edits = edits;
        files = new(Files(project, edits));
    }

    /// <summary>The test files in <paramref name="project"/>, in the order they run; empty when it has none.</summary>
    /// <param name="edits">Files' text by full path, such as unsaved ones; a new test file among them counts.</param>
    public static IReadOnlyList<string> Files(RoundhouseProject project, IReadOnlyDictionary<string, string>? edits = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        var folder = Path.GetFullPath(Path.Combine(project.Root, FolderName));
        SortedSet<string> found = new(StringComparer.Ordinal);
        if (Directory.Exists(folder))
            found.UnionWith(Directory.EnumerateFiles(folder, "*.lua").Select(Path.GetFullPath));
        foreach (var path in edits?.Keys ?? [])
        {
            var full = Path.GetFullPath(path);
            if (string.Equals(Path.GetDirectoryName(full), folder, StringComparison.Ordinal)
                && full.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
                found.Add(full);
        }

        return [.. found];
    }

    /// <summary>
    /// Gets ready to run the tests in <paramref name="project"/> against VentureOS's files in <paramref name="rootfs"/>,
    /// with <paramref name="edits"/> (text by full path, such as unsaved files) in place of what's saved, in the app
    /// and the tests alike.
    /// </summary>
    /// <param name="printed">Where the tests' own <c>print</c> goes.</param>
    /// <exception cref="ProjectException">The project has no app, so there's nothing to test.</exception>
    public static ProjectTests Start(
        RoundhouseProject project, string rootfs, SimulatedVersion version, IShellOutput printed, IReadOnlyDictionary<string, string>? edits = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrEmpty(rootfs);
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(printed);
        if (!project.HasApp)
            throw new ProjectException($"{Path.GetFileName(project.Root)} has no app yet, so there's nothing to test.");

        return new ProjectTests(project, rootfs, version, printed, edits ?? new Dictionary<string, string>());
    }

    /// <summary>Runs every test, telling <paramref name="report"/> each result as it comes.</summary>
    /// <returns>Every result, in the order they ran.</returns>
    /// <exception cref="ProjectException">The project has no app, so there's nothing to test.</exception>
    public static IReadOnlyList<AppTestResult> Run(
        RoundhouseProject project, string rootfs, SimulatedVersion version, IShellOutput printed, Action<AppTestResult>? report = null,
        IReadOnlyDictionary<string, string>? edits = null)
    {
        using var tests = Start(project, rootfs, version, printed, edits);
        List<AppTestResult> results = [];
        while (tests.Next() is { } result)
        {
            results.Add(result);
            report?.Invoke(result);
        }

        return results;
    }

    /// <summary>Runs the next case, or reports the next file that couldn't be run.</summary>
    /// <returns>Its result, or <see langword="null"/> when every test has run.</returns>
    public AppTestResult? Next()
    {
        while (cases.Count == 0)
        {
            EndFile();
            if (!files.TryDequeue(out var path))
                return null;
            if (Load(path) is { } failure)
                return failure;
        }

        var (name, reference) = cases.Dequeue();
        stop = ScriptStop.After(CaseLimit);
        string? why = null;
        owner!.Run(lua =>
        {
            why = RunCase(lua.State, reference);
            lua.State.L_Unref(LuaDef.LUA_REGISTRYINDEX, reference);
        }, () => stop?.Invoke());
        return new AppTestResult(file!, name, why);
    }

    /// <summary>Ends the run, with whatever cases were left not run.</summary>
    public void Dispose()
    {
        cases.Clear();
        files.Clear();
        EndFile();
    }

    // Runs a file's own code, which declares its cases: a result when it can't be run, or null when its cases are
    // queued.
    private AppTestResult? Load(string path)
    {
        file = Path.GetRelativePath(project.Root, path).Replace('\\', '/');
        string text;
        try
        {
            text = edits.TryGetValue(path, out var edited) ? edited : File.ReadAllText(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new AppTestResult(file, null, $"it couldn't be read: {exception.Message}");
        }

        owner = new LuaOwner(file, "the tests end", mayReplaceScripts: false, printed, Path.GetDirectoryName(path)!);
        stop = ScriptStop.After(CaseLimit);
        string? failure = null;
        owner.Run(lua =>
        {
            TestLibrary.Open(lua);
            var state = lua.State;
            if (state.L_LoadBufferX(LuaText.Encode(text), "@" + file, "t") != ThreadStatus.LUA_OK
                || state.PCall(0, 0, 0) != ThreadStatus.LUA_OK)
            {
                failure = Message(state);
                state.Pop(1);
                return;
            }

            foreach (var found in Cases(state))
                cases.Enqueue(found);
            if (cases.Count == 0)
                failure = "it has no test.case in it";
        }, () => stop?.Invoke());

        return failure is null ? null : new AppTestResult(file, null, failure);
    }

    private void EndFile()
    {
        owner?.Dispose();
        owner = null;
    }

    // The cases a file declared, in order, each its name and its function kept in the registry.
    private static List<(string Name, int Reference)> Cases(ILuaState state)
    {
        List<(string, int)> found = [];
        state.GetGlobal(TestLibrary.CasesGlobal);
        var count = (int)state.RawLen(-1);
        for (var index = 1; index <= count; index++)
        {
            state.RawGetI(-1, index);
            state.GetField(-1, "name");
            var name = LuaText.Decode(state.ToString(-1));
            state.Pop(1);
            state.GetField(-1, "run");
            found.Add((name, state.L_Ref(LuaDef.LUA_REGISTRYINDEX)));
            state.Pop(1);
        }

        state.Pop(1);
        return found;
    }

    // Runs one case on a fresh VentureOS: why it failed, or null when it passed.
    private string? RunCase(ILuaState state, int reference)
    {
        AppUnderTest app;
        try
        {
            app = AppUnderTest.Start(project, rootfs, version, edits);
        }
        catch (Exception exception) when (exception is ProjectException or IOException or UnauthorizedAccessException)
        {
            return $"the app couldn't be built: {exception.Message}";
        }

        using (app)
        {
            if (!app.Opened)
                return "the app didn't open: " + string.Join(" ", app.PrintedLines);

            var known = app.PrintedLines.Count;
            state.RawGetI(LuaDef.LUA_REGISTRYINDEX, reference);
            TestLibrary.PushApp(state, app);
            if (state.PCall(1, 0, 0) != ThreadStatus.LUA_OK)
            {
                var message = Message(state);
                state.Pop(1);
                return message;
            }

            for (var index = known; index < app.PrintedLines.Count; index++)
            {
                if (app.PrintedLines[index].StartsWith("Lua error", StringComparison.Ordinal))
                    return $"the app hit an error: {app.PrintedLines[index]}";
            }

            return null;
        }
    }

    private static string Message(ILuaState state) =>
        state.ToString(-1) is { } message ? LuaText.Decode(message) : "(the error isn't text)";
}
