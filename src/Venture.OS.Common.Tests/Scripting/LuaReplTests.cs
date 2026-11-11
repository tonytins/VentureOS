using Venture.OS.Scripting;
using Venture.OS.Tests.Shell;

namespace Venture.OS.Tests.Scripting;

public sealed class LuaReplTests
{
    private readonly RecordingOutput output = new();
    private readonly List<string> prompts = [];

    [Fact]
    public void PrintsExpressionsAndKeepsValuesBetweenLines()
    {
        Run("x = 20", "x + 22", "'a', 'b'", "exit");

        Assert.Equal([LuaRepl.Greeting, "42", "a\tb"], output.Lines);
        Assert.Equal([LuaRepl.Prompt, LuaRepl.Prompt, LuaRepl.Prompt, LuaRepl.Prompt], prompts);
    }

    [Fact]
    public void RunsStatementsThatPrint()
    {
        // As in the reference lua, io.write is an expression and would print the file it returns, so "do" makes it
        // a statement.
        Run("for i = 1, 2 do print(i) end", "do io.write('no break') end", "print('next')");

        Assert.Equal([LuaRepl.Greeting, "1", "2", "no break", "next"], output.Lines);
    }

    [Fact]
    public void ContinuesUnfinishedStatements()
    {
        Run("for i = 1, 3 do", "  print(i * 10)", "end", "exit");

        Assert.Equal([LuaRepl.Greeting, "10", "20", "30"], output.Lines);
        Assert.Equal(
            [LuaRepl.Prompt, LuaRepl.ContinuationPrompt, LuaRepl.ContinuationPrompt, LuaRepl.Prompt],
            prompts);
    }

    [Fact]
    public void ReportsErrorsAndCarriesOn()
    {
        Run("nope()", "this is not lua", "print('still here')");

        Assert.Equal(LuaRepl.Greeting, output.Lines[0]);
        Assert.StartsWith("Lua error: ", output.Lines[1]);
        Assert.Contains("nope", output.Lines[1]);
        Assert.StartsWith("Lua error: ", output.Lines[2]);
        Assert.Equal("still here", output.Lines[^1]);
    }

    [Fact]
    public void ReportsAnErrorRaisedWhilePrintingResults()
    {
        Run("setmetatable({}, { __tostring = function() error('bad tostring', 0) end })", "print('after')");

        Assert.Equal([LuaRepl.Greeting, "Lua error: bad tostring", "after"], output.Lines);
    }

    [Theory]
    [InlineData("exit")]
    [InlineData("  exit  ")]
    public void LeavesOnExit(string line)
    {
        Run(line, "print('never')");

        Assert.Equal([LuaRepl.Greeting], output.Lines);
    }

    [Fact]
    public void LeavesWhenTheInputEnds()
    {
        Run("print('one')");

        Assert.Equal([LuaRepl.Greeting, "one"], output.Lines);
    }

    [Fact]
    public void LeavesOnOsExitAndReportsANonZeroStatus()
    {
        Run("do io.write('bye') end", "os.exit(3)", "print('never')");

        Assert.Equal([LuaRepl.Greeting, "bye", "Lua exited with status 3."], output.Lines);
    }

    [Fact]
    public void StopsOnlyTheRunningLine()
    {
        var calls = 0;

        // Says stop once, on its third check, as one Ctrl+C press does.
        Run(() => ++calls == 3 ? "Interrupted." : null, "while true do end", "print('back')", "x = 0",
            "for i = 1, 100000 do x = x + 1 end", "x");

        Assert.Equal([LuaRepl.Greeting, "Lua error: Interrupted.", "back", "100000"], output.Lines);
    }

    [Fact]
    public void GivesEveryScope()
    {
        Run("venture.uptime()", "venture.net().device");

        Assert.Equal([LuaRepl.Greeting, "3725", "e1000e"], output.Lines);
    }

    [Fact]
    public void RejectsNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() => LuaRepl.Run(null!, _ => null, "/"));
        Assert.Throws<ArgumentNullException>(() => LuaRepl.Run(output, null!, "/"));
        Assert.Throws<ArgumentException>(() => LuaRepl.Run(output, _ => null, ""));
    }

    private void Run(params string[] lines) => Run(null, lines);

    /// <summary>Runs the prompt on a worker thread, so a broken stop fails the test instead of hanging the suite.</summary>
    private void Run(Func<string?>? stop, params string[] lines)
    {
        Queue<string> input = new(lines);
        var run = Task.Run(() => LuaRepl.Run(
            output,
            prompt =>
            {
                prompts.Add(prompt);
                return input.TryDequeue(out var line) ? line : null;
            },
            Path.GetTempPath(),
            new FakeSystemInfo(),
            stop));

        Assert.True(run.Wait(TimeSpan.FromSeconds(10)), "The prompt didn't finish within 10 seconds.");
        run.GetAwaiter().GetResult();
    }
}
