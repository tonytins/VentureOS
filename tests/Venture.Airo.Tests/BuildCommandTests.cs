namespace Venture.Airo.Tests;

/// <summary>
/// <c>airo build</c>: a project built into an <c>.app</c> where it's told, its path the only output, and every refusal
/// said on the error output with exit code 1, so a script or Roundhouse can tell.
/// </summary>
[Collection(nameof(LookCollection))]
public sealed class BuildCommandTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), $"airo-build-{Guid.NewGuid():N}");
    private readonly StringWriter output = new();
    private readonly StringWriter error = new();

    public BuildCommandTests() => Directory.CreateDirectory(folder);

    public void Dispose() => Directory.Delete(folder, recursive: true);

    private int Airo(params string[] args) => AiroCommand.Run(args, output, error);

    [Fact]
    public void BuildsTheAppIntoTheFolderNamedAndPrintsItsPath()
    {
        var project = SampleProjects.WithApp(folder, "Dice");
        var target = Path.Combine(folder, "apps");

        Assert.Equal(0, Airo("build", project.Root, "--output", target));

        var app = Path.Combine(target, "Dice.app");
        Assert.Equal(app + Environment.NewLine, output.ToString());
        Assert.Empty(error.ToString());
        Assert.True(File.Exists(Path.Combine(app, "Contents", "Resources", "main.uix")));
    }

    [Fact]
    public void TakesTheProjectsXmlInPlaceOfItsFolder()
    {
        var project = SampleProjects.WithApp(folder, "Dice");

        Assert.Equal(0, Airo("build", Path.Combine(project.Root, "Project.xml"), "-o", folder));
        Assert.True(Directory.Exists(Path.Combine(folder, "Dice.app")));
    }

    [Fact]
    public void WithoutAnOutputItSaysSoAndBuildsNothing()
    {
        var project = SampleProjects.WithApp(folder, "Dice");

        Assert.Equal(1, Airo("build", project.Root));
        Assert.Contains("--output", error.ToString());
        Assert.Empty(output.ToString());
        Assert.False(Directory.Exists(Path.Combine(folder, "Dice.app")));
    }

    [Fact]
    public void LeavesAnAppThatsThereUnlessToldToReplaceIt()
    {
        var project = SampleProjects.WithApp(folder, "Dice");
        Assert.Equal(0, Airo("build", project.Root, "-o", folder));
        File.WriteAllText(Path.Combine(folder, "Dice.app", "stale.txt"), "old");

        Assert.Equal(1, Airo("build", project.Root, "-o", folder));
        Assert.Contains("airo: There's already Dice.app", error.ToString());
        Assert.True(File.Exists(Path.Combine(folder, "Dice.app", "stale.txt")));

        Assert.Equal(0, Airo("build", project.Root, "-o", folder, "--replace"));
        Assert.False(File.Exists(Path.Combine(folder, "Dice.app", "stale.txt")));
    }

    [Fact]
    public void AFolderThatIsntAProjectIsRefused()
    {
        Assert.Equal(1, Airo("build", folder, "-o", folder));
        Assert.Contains("has no Project.xml", error.ToString());
        Assert.Empty(output.ToString());
    }

    [Fact]
    public void TestPrintsALineATestAndTheCountsThenFailsWhenOneDid()
    {
        var project = SampleProjects.WithApp(folder, "Dice");
        SampleProjects.WithTest(project, "count.lua", """
            test.case("counts", function(app) app:press("count") test.equal(app:text("result"), "Pressed 1") end)
            test.case("miscounts", function(app) test.equal(app:text("result"), "Pressed 1") end)
            """);

        Assert.Equal(1, Airo("test", project.Root));

        Assert.Equal(
            [
                "pass Tests/count.lua: counts",
                "fail Tests/count.lua: miscounts: Tests/count.lua:2: expected \"Pressed 1\", got \"Not pressed\"",
                "1 passed, 1 failed",
            ],
            output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public void TestPassesWhenEveryTestDoes()
    {
        var project = SampleProjects.WithApp(folder, "Dice");
        SampleProjects.WithTest(project, "count.lua", """test.case("counts", function(app) app:press("count") end)""");

        Assert.Equal(0, Airo("test", project.Root));
        Assert.EndsWith("1 passed, 0 failed" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void TestWithNoTestsSaysSo()
    {
        var project = SampleProjects.WithApp(folder, "Dice");

        Assert.Equal(1, Airo("test", project.Root));
        Assert.Contains("Dice.rproj has no tests", error.ToString());
        Assert.Empty(output.ToString());
    }
}
