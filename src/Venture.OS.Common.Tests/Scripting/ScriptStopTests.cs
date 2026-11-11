using Venture.OS.Scripting;
using Venture.OS.Tests.Shell;

namespace Venture.OS.Tests.Scripting;

public sealed class ScriptStopTests
{
    private readonly RecordingOutput output = new();

    [Fact]
    public void StopsAnEndlessLoop()
    {
        Assert.False(Run("while true do end", StopAfterChecks(3)));

        Assert.Equal("Lua error: Interrupted.", output.Lines[0]);
    }

    [Fact]
    public void StopsALoopThatCatchesErrorsWithPcall()
    {
        Assert.False(Run("while true do pcall(function() while true do end end) end", StopAfterChecks(3)));

        Assert.Equal("Lua error: Interrupted.", output.Lines[0]);
    }

    [Fact]
    public void StopsALoopThatCatchesErrorsWithCoroutines()
    {
        const string Code = """
            while true do
              coroutine.resume(coroutine.create(function() while true do end end))
            end
            """;

        Assert.False(Run(Code, StopAfterChecks(3)));

        Assert.Equal("Lua error: Interrupted.", output.Lines[0]);
    }

    [Theory]
    [InlineData("while true do end")]
    [InlineData("while true do pcall(function() while true do end end) end")]
    [InlineData("coroutine.wrap(function() while true do end end)()")]
    public void StopsWhenTheCheckSaysSoOnlyOnce(string code)
    {
        Assert.False(Run(code, StopOnceAtCheck(3)));

        Assert.Equal("Lua error: Interrupted.", output.Lines[0]);
    }

    [Fact]
    public void StopsALoopInsideAWrappedCoroutine()
    {
        Assert.False(Run("coroutine.wrap(function() while true do end end)()", StopAfterChecks(3)));

        Assert.Equal("Lua error: Interrupted.", output.Lines[0]);
    }

    [Fact]
    public void KeepsCoroutinesWorking()
    {
        const string Code = """
            local co = coroutine.create(function(a) local b = coroutine.yield(a + 1) return b * 2 end)
            print(coroutine.resume(co, 1))
            print(coroutine.resume(co, 5))
            local gen = coroutine.wrap(function() for i = 1, 3 do coroutine.yield(i) end end)
            print(gen(), gen(), gen())
            print(pcall(coroutine.wrap(function() error("inside", 0) end)))
            """;

        Assert.True(Run(Code, () => null));

        Assert.Equal(["true\t2", "true\t10", "1\t2\t3", "false\tinside"], output.Lines);
    }

    [Theory]
    [InlineData("debug.sethook()")]
    [InlineData("debug.sethook(function() end, '', 1)")]
    public void ScriptsCannotReplaceTheHook(string attempt)
    {
        Assert.False(Run($"local ok, message = pcall(function() {attempt} end) print(ok, message) while true do end",
            StopAfterChecks(3)));

        Assert.StartsWith("false\t", output.Lines[0]);
        Assert.Contains(LuaRunner.SetHookReserved, output.Lines[0]);
        Assert.Equal("Lua error: Interrupted.", output.Lines[1]);
    }

    [Fact]
    public void LetsCodeFinishWhenNothingSaysStop()
    {
        var checks = 0;

        var finished = Run("local n = 0 for i = 1, 100000 do n = n + i end print(n)", () =>
        {
            checks++;
            return null;
        });

        Assert.True(finished);
        Assert.Equal(["5000050000"], output.Lines);
        Assert.InRange(checks, 1, 1000);
    }

    [Fact]
    public void LeavesTheScriptNoTraceOfTheHandoff()
    {
        Run("print(__venture_stop)", () => null);

        Assert.Equal(["nil"], output.Lines);
    }

    [Fact]
    public void LeavesDebugSethookAloneWithoutAStopCheck()
    {
        Assert.True(LuaRunner.Run(output, Path.GetTempPath(), lua => lua.DoString("debug.sethook() print('ok')")));

        Assert.Equal(["ok"], output.Lines);
    }

    [Fact]
    public void AfterStopsOnceTheTimeHasPassed()
    {
        var check = ScriptStop.After(TimeSpan.FromMilliseconds(50));

        Assert.Null(check());
        Thread.Sleep(100);
        Assert.Equal("Stopped after 0.05 seconds.", check());
    }

    [Fact]
    public void AfterNamesWholeSeconds()
    {
        Assert.False(Run("while true do end", ScriptStop.Any(StopAfterChecks(2), () => null)));
        Assert.Equal("Lua error: Interrupted.", output.Lines[0]);

        Assert.Throws<ArgumentOutOfRangeException>(() => ScriptStop.After(TimeSpan.Zero));
    }

    [Fact]
    public void AnyGivesTheFirstReason()
    {
        var check = ScriptStop.Any(() => null, () => "second", () => "third");

        Assert.Equal("second", check());
        Assert.Null(ScriptStop.Any(() => null)());
        Assert.Throws<ArgumentNullException>(() => ScriptStop.Any(null!));
        Assert.Throws<ArgumentNullException>(() => ScriptStop.Any([null!]));
    }

    [Fact]
    public void StopsAnEndlessLoopAfterATimeLimit()
    {
        Assert.False(Run("while true do end", ScriptStop.After(TimeSpan.FromMilliseconds(100))));

        Assert.Equal("Lua error: Stopped after 0.1 seconds.", output.Lines[0]);
    }

    /// <summary>
    /// Runs <paramref name="code"/> on a worker thread, failing the test instead of hanging the suite if the stop
    /// doesn't work. A runaway thread keeps spinning until the test process ends.
    /// </summary>
    private bool Run(string code, Func<string?> stop)
    {
        var run = Task.Run(() => LuaRunner.Run(output, Path.GetTempPath(), lua => lua.DoString(code), stop: stop));

        Assert.True(run.Wait(TimeSpan.FromSeconds(10)), "The Lua code didn't stop within 10 seconds.");
        return run.Result;
    }

    /// <summary>Says to stop at the given check only, as one Ctrl+C press read from the keyboard does.</summary>
    private static Func<string?> StopOnceAtCheck(int check)
    {
        var count = 0;
        return () => ++count == check ? "Interrupted." : null;
    }

    /// <summary>Says to stop from the given check onward, as a time limit does.</summary>
    private static Func<string?> StopAfterChecks(int checks)
    {
        var count = 0;
        return () => ++count >= checks ? "Interrupted." : null;
    }
}
