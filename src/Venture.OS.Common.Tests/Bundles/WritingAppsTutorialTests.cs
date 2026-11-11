using System.Text.RegularExpressions;
using Venture.OS.Bundles;
using Venture.OS.Scripting;
using Venture.OS.Shell;
using Venture.OS.Tests.Scripting;
using Venture.OS.Tests.Shell;

namespace Venture.OS.Tests.Bundles;

/// <summary>
/// Builds the Dice app from the code in <c>docs/writing-apps.md</c> and runs it, so the tutorial stays true.
/// </summary>
public sealed partial class WritingAppsTutorialTests : IDisposable
{
    private static readonly string Guide =
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "docs", "writing-apps.md"));

    private readonly string root = Path.Combine(Path.GetTempPath(), $"venture-tutorial-{Guid.NewGuid():N}");
    private readonly string apps;
    private readonly RecordingOutput output = new();
    private readonly CommandRegistry registry = new();
    private readonly CommandShell shell;
    private readonly AppManager manager;

    public WritingAppsTutorialTests()
    {
        apps = Path.Combine(root, "apps");
        Directory.CreateDirectory(apps);
        registry.Register(new TestCommand("help"));
        RuntimeCommands runtime = new(registry.IsBuiltIn, new ScriptCommands(Path.Combine(root, "commands")));
        registry.AddSource(runtime);
        shell = new CommandShell(new ShellContext(output, new RecordingHost(), registry) { CurrentDirectory = root });
        manager = new AppManager(
            apps, runtime, new FakeSystemInfo(), Path.Combine(AppContext.BaseDirectory, "system", "lib"), shell.Execute);
    }

    public void Dispose()
    {
        manager.CloseApp("Dice", new RecordingOutput());
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void TheDiceAppOpensAndRolls()
    {
        Build(Block("xml", 0), Block("lua", 0));

        Assert.True(manager.OpenApp("dice", root, output), string.Join('\n', output.Lines));
        shell.Execute("roll 3");
        shell.Execute("roll 2 -s 20");
        shell.Execute("roll");

        Assert.Matches(@"^[1-6] \+ [1-6] \+ [1-6] = \d+$", output.Lines[0]);
        Assert.Matches(@"^\d+ \+ \d+ = \d+$", output.Lines[1]);
        Assert.Matches(@"^[1-6] = [1-6]$", output.Lines[2]);
        Assert.Equal(3, output.Lines.Count);
    }

    [Theory]
    [InlineData("roll 0", "roll: the count must be a whole number from 1 to 20.")]
    [InlineData("roll 21", "roll: the count must be a whole number from 1 to 20.")]
    [InlineData("roll 1.5", "roll: the count must be a whole number from 1 to 20.")]
    [InlineData("roll many", "roll: the count must be a whole number from 1 to 20.")]
    [InlineData("roll -s 1", "roll: a die needs a whole number of sides, 2 or more.")]
    [InlineData("roll --sides", "roll: --sides needs a value")]
    public void TheDiceAppExplainsMistakes(string line, string message)
    {
        Build(Block("xml", 0), Block("lua", 0));
        manager.OpenApp("dice", root, output);

        shell.Execute(line);

        Assert.Equal(message, output.Lines[0]);
    }

    [Fact]
    public void TheHelpMatchesTheGuide()
    {
        Build(Block("xml", 0), Block("lua", 0));
        manager.OpenApp("dice", root, output);

        Assert.True(registry.TryGet("roll", out var roll));

        Assert.Contains($"\n{roll.Summary}\nUsage: {roll.Usage}\nFrom: {((IRuntimeCommand)roll).From}\n", Guide);
        manager.CloseApp("dice", output);
        Assert.False(registry.TryGet("roll", out _));
    }

    [Fact]
    public void TheFilesStepKeepsEachRoll()
    {
        // The guide's second XML block replaces the Info line; its second Lua block goes at the end of roll.
        var info = Block("xml", 0).Replace(InfoLine(Block("xml", 0)), Block("xml", 1).Trim());
        var main = Block("lua", 0).Replace(
            "  print(table.concat(rolls, \" + \") .. \" = \" .. total)\n",
            "  print(table.concat(rolls, \" + \") .. \" = \" .. total)\n" + Block("lua", 1));
        Build(info, main);

        manager.OpenApp("dice", root, output);
        shell.Execute("roll 2");
        shell.Execute("roll 1");

        var totals = File.ReadAllLines(Path.Combine(root, "rolls.txt"));
        Assert.Equal(2, totals.Length);
        Assert.Equal(output.Lines[0].Split(" = ")[1], totals[0]);
    }

    [Fact]
    public void TheShellScopeIsWhatTheGuideSays()
    {
        Build(Block("xml", 0).Replace("needs=\"shell\"", "needs=\"\""), Block("lua", 0));

        manager.OpenApp("dice", root, output);

        Assert.Contains(output.Lines, line => line.Contains("venture.command needs the shell scope: "));
        Assert.Contains("`venture.command needs the shell scope: ...`", Guide);
    }

    private void Build(string info, string main)
    {
        var contents = Path.Combine(apps, "Dice.app", "Contents");
        Directory.CreateDirectory(Path.Combine(contents, "Lua"));
        File.WriteAllText(Path.Combine(contents, "Info.xml"), info);
        File.WriteAllText(Path.Combine(contents, "Lua", "main.lua"), main);
    }

    /// <summary>The guide's code block in a language, counting from 0 in the order they appear.</summary>
    private static string Block(string language, int index)
    {
        var blocks = CodeBlock().Matches(Guide).Where(match => match.Groups[1].Value == language).ToList();
        Assert.True(index < blocks.Count, $"The guide has no {language} block {index}.");
        return blocks[index].Groups[2].Value;
    }

    private static string InfoLine(string xml) => xml.Split('\n').Single(line => line.TrimStart().StartsWith("<Info")).Trim();

    [GeneratedRegex("```(\\w+)\\n(.*?)```", RegexOptions.Singleline)]
    private static partial Regex CodeBlock();
}
