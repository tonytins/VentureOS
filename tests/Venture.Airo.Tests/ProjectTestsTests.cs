using Venture.Airo.Projects;
using Venture.Airo.Simulation;
using Venture.Airo.Testing;
using Venture.OS.Desktop;
using Venture.OS.Shell;

namespace Venture.Airo.Tests;

/// <summary>
/// <see cref="ProjectTests"/>: a project's Lua tests run against its app in a fresh VentureOS each, using the app as a
/// person would, with every failure saying why and where.
/// </summary>
[Collection(nameof(LookCollection))]
public sealed class ProjectTestsTests : IDisposable
{
    private static readonly SimulatedVersion Version = new("0.8.0", "0123456789abcdef", "tests");

    private readonly string folder = Path.Combine(Path.GetTempPath(), $"airo-tests-{Guid.NewGuid():N}");
    private readonly Printed printed = new();
    private readonly RoundhouseProject project;

    public ProjectTestsTests()
    {
        Directory.CreateDirectory(folder);
        project = SampleProjects.WithApp(folder, "Sample");
    }

    public void Dispose() => Directory.Delete(folder, recursive: true);

    private IReadOnlyList<AppTestResult> Run(string lua, string file = "sample.lua")
    {
        SampleProjects.WithTest(project, file, lua);
        return ProjectTests.Run(project, SimulatedDisk.FindRootfs()!, Version, printed);
    }

    [Fact]
    public void ACaseThatUsesTheAppAsAPersonWouldPasses()
    {
        var result = Assert.Single(Run("""
            test.case("counts and greets", function(app)
              test.equal(app:title(), "Sample")
              test.equal(app:text("result"), "Not pressed")
              app:press("count")
              app:press("count")
              test.equal(app:text("result"), "Pressed 2")
              app:submit("name", "Tony")
              test.equal(app:text("result"), "Hello, Tony")
              test.expect(app:dimmed("off"))
              test.expect(app:showing("count"))
              test.equal(app:windows(), 1)
            end)
            """));

        Assert.Equal(new AppTestResult("Tests/sample.lua", "counts and greets", null), result);
    }

    [Fact]
    public void AFailedCheckSaysWhatItGotAndTheTestsLine()
    {
        var result = Assert.Single(Run("""
            test.case("wrong", function(app)

              test.equal(app:text("result"), "Pressed 9", "the count")
            end)
            """));

        Assert.Equal("Tests/sample.lua:3: the count: expected \"Pressed 9\", got \"Not pressed\"", result.Failure);
    }

    [Fact]
    public void EachCaseStartsOnAFreshApp()
    {
        var results = Run("""
            test.case("first", function(app) app:press("count") test.equal(app:text("result"), "Pressed 1") end)
            test.case("second", function(app) app:press("count") test.equal(app:text("result"), "Pressed 1") end)
            """);

        Assert.Equal(["first", "second"], results.Select(result => result.Name));
        Assert.All(results, result => Assert.True(result.Passed, result.Failure));
    }

    [Theory]
    [InlineData("app:press(\"nothing\")", "Tests/sample.lua:1: app:press: there's no \"nothing\" in Sample")]
    [InlineData("app:press(\"off\")", "Tests/sample.lua:1: app:press: \"off\" is dimmed, so a person couldn't use it")]
    [InlineData("app:choose(\"count\", 1)", "Tests/sample.lua:1: app:choose: \"count\" isn't a list")]
    public void UsingWhatAPersonCouldntFails(string call, string failure)
    {
        var result = Assert.Single(Run($"test.case(\"use\", function(app) {call} end)"));

        Assert.Equal(failure, result.Failure);
    }

    [Fact]
    public void AnErrorInTheAppFailsTheCaseEvenUnchecked()
    {
        var result = Assert.Single(Run("""test.case("boom", function(app) app:press("boom") end)"""));

        Assert.StartsWith("the app hit an error: Lua error:", result.Failure);
        Assert.Contains("boom went off", result.Failure);
    }

    [Fact]
    public void AFileThatDoesntLoadIsItsOwnFailureAndTheRestStillRun()
    {
        SampleProjects.WithTest(project, "a.lua", "test.case(\"unfinished\", function(app)");
        var results = Run("""test.case("fine", function(app) end)""", "b.lua");

        Assert.Equal("Tests/a.lua", results[0].File);
        Assert.Null(results[0].Name);
        Assert.Contains("'end' expected", results[0].Failure);
        Assert.Equal(new AppTestResult("Tests/b.lua", "fine", null), results[1]);
    }

    [Fact]
    public void AFileWithNoCasesSaysSo()
    {
        var result = Assert.Single(Run("local nothing = true"));

        Assert.Equal("it has no test.case in it", result.Failure);
    }

    [Fact]
    public void ReadsWhatTheAppLeftOnItsDisk()
    {
        var result = Assert.Single(Run("""
            test.case("files", function(app)
              test.expect(app:read("/mnt/user/settings/appearance.xml"):find("<Appearance", 1, true))
              local text, why = app:read("/mnt/user/nowhere.txt")
              test.equal(text, nil)
              test.equal(why, "there's no file at /mnt/user/nowhere.txt")
              print("printed by the test")
            end)
            """));

        Assert.True(result.Passed, result.Failure);
        Assert.Equal(["printed by the test"], printed.Lines);
    }

    [Fact]
    public void TheLookIsPutBackAfterwards()
    {
        var before = DesktopAppearance.Current;
        DesktopAppearance.SettingsPath = null;

        Run("""test.case("look", function(app) end)""");

        Assert.Same(before, DesktopAppearance.Current);
        Assert.Null(DesktopAppearance.SettingsPath);
    }

    [Fact]
    public void AProjectWithoutAnAppIsRefused()
    {
        var empty = RoundhouseProject.Create("Empty", folder);

        Assert.Throws<ProjectException>(() => ProjectTests.Run(empty, SimulatedDisk.FindRootfs()!, Version, printed));
    }

    private sealed class Printed : IShellOutput
    {
        public List<string> Lines { get; } = [];

        public void WriteLine(string text) => Lines.Add(text);

        public void Clear() => Lines.Clear();
    }
}

/// <summary>Tests that change the desktop's look, which is the whole process's, run one at a time.</summary>
[CollectionDefinition(nameof(LookCollection), DisableParallelization = true)]
public sealed class LookCollection;
