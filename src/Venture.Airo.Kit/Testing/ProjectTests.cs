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
/// </remarks>
public static class ProjectTests
{
    /// <summary>The project's folder its tests are in.</summary>
    public const string FolderName = "Tests";

    /// <summary>How long one case may run before it's stopped and fails.</summary>
    public static readonly TimeSpan CaseLimit = TimeSpan.FromSeconds(30);

    /// <summary>The test files in <paramref name="project"/>, in the order they run; empty when it has none.</summary>
    public static IReadOnlyList<string> Files(RoundhouseProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var folder = Path.Combine(project.Root, FolderName);
        return Directory.Exists(folder)
            ? [.. Directory.EnumerateFiles(folder, "*.lua").Order(StringComparer.Ordinal)]
            : [];
    }

    /// <summary>
    /// Runs every test in <paramref name="project"/> against VentureOS's files in <paramref name="rootfs"/>, telling
    /// <paramref name="report"/> each result as it comes.
    /// </summary>
    /// <param name="printed">Where the tests' own <c>print</c> goes.</param>
    /// <returns>Every result, in the order they ran.</returns>
    /// <exception cref="ProjectException">The project has no app, so there's nothing to test.</exception>
    public static IReadOnlyList<AppTestResult> Run(
        RoundhouseProject project, string rootfs, SimulatedVersion version, IShellOutput printed, Action<AppTestResult>? report = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrEmpty(rootfs);
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(printed);
        if (!project.HasApp)
            throw new ProjectException($"{Path.GetFileName(project.Root)} has no app yet, so there's nothing to test.");

        List<AppTestResult> results = [];
        void Add(AppTestResult result)
        {
            results.Add(result);
            report?.Invoke(result);
        }

        foreach (var path in Files(project))
            RunFile(project, rootfs, version, printed, path, Add);
        return results;
    }

    private static void RunFile(
        RoundhouseProject project, string rootfs, SimulatedVersion version, IShellOutput printed, string path, Action<AppTestResult> add)
    {
        var file = Path.GetRelativePath(project.Root, path).Replace('\\', '/');
        Func<string?>? stop = null;

        // The file's own errors, such as a typo that stops it loading, come out of LuaRunner on printed; they're
        // caught here instead, so they become the file's result.
        string? loadFailure = null;
        LuaRunner.Run(printed, Path.GetDirectoryName(path)!, lua =>
        {
            TestLibrary.Open(lua);
            var state = lua.State;
            stop = ScriptStop.After(CaseLimit);
            if (state.L_LoadBufferX(LuaText.Encode(File.ReadAllText(path)), "@" + file, "t") != ThreadStatus.LUA_OK
                || state.PCall(0, 0, 0) != ThreadStatus.LUA_OK)
            {
                loadFailure = Message(state);
                state.Pop(1);
                return;
            }

            var cases = Cases(state);
            if (cases.Count == 0)
            {
                loadFailure = "it has no test.case in it";
                return;
            }

            foreach (var (name, reference) in cases)
            {
                stop = ScriptStop.After(CaseLimit);
                add(new AppTestResult(file, name, RunCase(state, reference, project, rootfs, version)));
                state.L_Unref(LuaDef.LUA_REGISTRYINDEX, reference);
            }
        }, stop: () => stop?.Invoke());

        if (loadFailure is not null)
            add(new AppTestResult(file, null, loadFailure));
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
    private static string? RunCase(ILuaState state, int reference, RoundhouseProject project, string rootfs, SimulatedVersion version)
    {
        AppUnderTest app;
        try
        {
            app = AppUnderTest.Start(project, rootfs, version);
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
