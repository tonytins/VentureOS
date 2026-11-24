using System.Text.RegularExpressions;
using Venture.Airo.Projects;
using Venture.Airo.Simulation;
using Venture.Airo.Testing;
using Venture.OS.Shell;

namespace Venture.Airo.Tests;

/// <summary>
/// Builds the Dice project from the code in <c>docs/tutorial/</c>, its window included, and runs both tutorials' tests
/// against it, so their testing steps stay true.
/// </summary>
[Collection(nameof(LookCollection))]
public sealed partial class WritingAppsTestingTests : IDisposable
{
    private static readonly string CommandLine = Tutorial("command-line.md");
    private static readonly string Desktop = Tutorial("desktop.md");

    private readonly string folder = Path.Combine(Path.GetTempPath(), $"airo-guide-{Guid.NewGuid():N}");

    public WritingAppsTestingTests() => Directory.CreateDirectory(folder);

    public void Dispose() => Directory.Delete(folder, recursive: true);

    [Fact]
    public void TheGuidesTestsPassOnTheGuidesDice()
    {
        // As the tutorials build it: Info.xml, the roll command with the window step at the end of main.lua, and
        // main.uix; then the command-line tutorial's tests, its fourth Lua block, and the desktop one's, its second.
        var project = RoundhouseProject.Create("Dice", folder);
        Write(project.AppFolder, "Info.xml", Block(CommandLine, "xml", 0));
        Write(project.AppFolder, Path.Combine("Lua", "main.lua"), Block(CommandLine, "lua", 0) + "\n" + Block(Desktop, "lua", 0));
        Write(project.AppFolder, Path.Combine("Resources", "main.uix"), Block(Desktop, "xml", 0));
        Write(project.Root, Path.Combine("Tests", "roll.lua"), Block(CommandLine, "lua", 3));
        Write(project.Root, Path.Combine("Tests", "window.lua"), Block(Desktop, "lua", 1));

        var results = ProjectTests.Run(project, SimulatedDisk.FindRootfs()!, new SimulatedVersion("0.8.0", "0123456789abcdef", "tests"), new Ignored());

        Assert.True(results.Count == 3, string.Join("\n", results.Select(result => result.Describe())));
        Assert.All(results, result => Assert.True(result.Passed, result.Describe()));
    }

    private static void Write(string folder, string relative, string text)
    {
        var path = Path.Combine(folder, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private static string Tutorial(string file) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "docs", "tutorial", file));

    /// <summary>A tutorial's code block in a language, counting from 0 in the order they appear.</summary>
    private static string Block(string tutorial, string language, int index)
    {
        var blocks = CodeBlock().Matches(tutorial).Where(match => match.Groups[1].Value == language).ToList();
        Assert.True(index < blocks.Count, $"The tutorial has no {language} block {index}.");
        return blocks[index].Groups[2].Value;
    }

    [GeneratedRegex("```(\\w+)\\n(.*?)```", RegexOptions.Singleline)]
    private static partial Regex CodeBlock();

    private sealed class Ignored : IShellOutput
    {
        public void WriteLine(string text)
        {
        }

        public void Clear()
        {
        }
    }
}
