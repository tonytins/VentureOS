using Venture.OS.Scripting;
using Venture.OS.Tests.Shell;

namespace Venture.OS.Tests.Scripting;

public sealed class LuaRunnerTests
{
    private readonly RecordingOutput output = new();

    [Fact]
    public void SendsPrintAndIoWriteToTheShell()
    {
        var finished = Run("print('hello') io.write('a', 'b') io.write('c\\n') io.stderr:write('oops\\n')");

        Assert.True(finished);
        Assert.Equal(["hello", "abc", "oops"], output.Lines);
    }

    [Fact]
    public void WritesAnUnfinishedLastLine()
    {
        Run("io.write('no line break')");

        Assert.Equal(["no line break"], output.Lines);
    }

    [Fact]
    public void ReportsALuaErrorWithItsTraceback()
    {
        var finished = Run("io.write('before') local x = 1 + nil");

        Assert.False(finished);
        Assert.Equal("before", output.Lines[0]);
        Assert.StartsWith("Lua error: ", output.Lines[1]);
        Assert.Contains("arithmetic", output.Lines[1]);
        Assert.Contains("traceback", string.Join('\n', output.Lines));
    }

    [Fact]
    public void ReportsASyntaxError()
    {
        Assert.False(Run("this is not lua"));
        Assert.StartsWith("Lua error: ", Assert.Single(output.Lines, line => line.StartsWith("Lua error: ")));
    }

    [Fact]
    public void ReportsANonZeroExit()
    {
        Assert.False(Run("os.exit(3)"));
        Assert.Equal(["Lua exited with status 3."], output.Lines);
    }

    [Theory]
    [InlineData("os.exit()")]
    [InlineData("os.exit(0)")]
    [InlineData("os.exit(true)")]
    public void TreatsASuccessfulExitAsFinishing(string code)
    {
        Assert.True(Run(code));
        Assert.Empty(output.Lines);
    }

    [Fact]
    public void StartsRelativePathsInTheWorkingDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"venture-lua-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "six.lua"), "return 6 * 7");

            LuaRunner.Run(output, directory, lua => lua.DoString("print(dofile('six.lua'))"));

            Assert.Equal(["42"], output.Lines);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void GivesEachRunAFreshInterpreter()
    {
        Run("leftover = 1");
        Run("print(leftover)");

        Assert.Equal(["nil"], output.Lines);
    }

    [Fact]
    public void RejectsInvalidArguments()
    {
        Assert.Throws<ArgumentNullException>(() => LuaRunner.Run(null!, "/", _ => { }));
        Assert.Throws<ArgumentException>(() => LuaRunner.Run(output, "", _ => { }));
        Assert.Throws<ArgumentNullException>(() => LuaRunner.Run(output, "/", null!));
    }

    private bool Run(string code) => LuaRunner.Run(output, Path.GetTempPath(), lua => lua.DoString(code));
}
