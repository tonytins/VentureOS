using System.Text.RegularExpressions;
using Venture.Airo.Projects;
using Venture.Airo.Simulation;
using Venture.Airo.Testing;
using Venture.OS.Shell;

namespace Venture.Airo.Tests;

/// <summary>
/// Builds the Dice project from the code in <c>docs/writing-apps.md</c>, its window included, and runs the guide's
/// tests against it, so the guide's testing step stays true.
/// </summary>
[Collection(nameof(LookCollection))]
public sealed partial class WritingAppsTestingTests : IDisposable
{
    private static readonly string Guide = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "docs", "writing-apps.md"));

    private readonly string folder = Path.Combine(Path.GetTempPath(), $"airo-guide-{Guid.NewGuid():N}");

    public WritingAppsTestingTests() => Directory.CreateDirectory(folder);

    public void Dispose() => Directory.Delete(folder, recursive: true);

    [Fact]
    public void TheGuidesTestsPassOnTheGuidesDice()
    {
        // As the guide builds it: Info.xml, the roll command with the window step at the end of main.lua, and
        // main.uix; then its tests, the sixth Lua block.
        var project = RoundhouseProject.Create("Dice", folder);
        Write(project.AppFolder, "Info.xml", Block("xml", 0));
        Write(project.AppFolder, Path.Combine("Lua", "main.lua"), Block("lua", 0) + "\n" + Block("lua", 3));
        Write(project.AppFolder, Path.Combine("Resources", "main.uix"), Block("xml", 2));
        Write(project.Root, Path.Combine("Tests", "roll.lua"), Block("lua", 5));

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

    /// <summary>The guide's code block in a language, counting from 0 in the order they appear.</summary>
    private static string Block(string language, int index)
    {
        var blocks = CodeBlock().Matches(Guide).Where(match => match.Groups[1].Value == language).ToList();
        Assert.True(index < blocks.Count, $"The guide has no {language} block {index}.");
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
