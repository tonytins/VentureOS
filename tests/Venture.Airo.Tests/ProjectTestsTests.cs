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
              test.equal(app:title(), nil)
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
    [InlineData("app:choose(\"count\", 1)", "Tests/sample.lua:1: app:choose: \"count\" isn't a list or a grid")]
    [InlineData("app:slide(\"count\", 1)", "Tests/sample.lua:1: app:slide: \"count\" isn't a slider")]
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
    public void ListsMenusTheTitleBarAndTextAreasAreUsedAsAPersonWould()
    {
        var result = Assert.Single(Run("""
            test.case("parts", function(app)
              test.equal(table.concat(app:rows("fruits"), "|"), "apple|pear")
              test.equal(app:chosen("fruits"), nil)
              app:choose("fruits", "pear")
              test.equal(app:chosen("fruits"), 2)
              test.equal(app:text("result"), "Chose row 2")
              app:choose("fruits", 1)
              test.equal(app:text("result"), "Chose row 1")

              app:press("add")
              test.equal(app:text("result"), "Pressed 1")
              app:press("reset")
              test.equal(app:text("result"), "Not pressed")

              app:submit("place", "/mnt/user")
              test.equal(app:text("result"), "Went to /mnt/user")
              app:type("body", "hello")
              test.equal(app:text("result"), "Typed hello")

              test.equal(table.concat(app:rows("looks"), "|"), "Light|Dark")
              app:choose("looks", "dark")
              test.equal(app:chosen("looks"), 2)
              test.equal(app:text("result"), "Chose dark")
              app:choose("looks", "Light")
              test.equal(app:text("result"), "Chose light")

              app:slide("volume", 6.6)
              test.equal(app:value("volume"), 7)
              test.equal(app:text("result"), "committed 7")
              app:slide("volume", 99)
              test.equal(app:value("volume"), 10)

              test.expect(app:has("reset") and app:has("place") and app:has("add") and app:has("fruits"))
              test.expect(not app:has("nothing"))
            end)
            """));

        Assert.True(result.Passed, result.Failure);
    }

    [Fact]
    public void ARowThatIsntThereSaysWhichAre()
    {
        var result = Assert.Single(Run("""test.case("row", function(app) app:choose("fruits", "plum") end)"""));

        Assert.Equal("Tests/sample.lua:1: app:choose: \"fruits\" has no row \"plum\"; its rows are apple, pear", result.Failure);
    }

    [Fact]
    public void NewWindowsFilesTimersAndTheShellReachTheApp()
    {
        var result = Assert.Single(Run("""
            test.case("system", function(app)
              app:wait(0.5)
              test.equal(app:text("ticks"), "0")
              app:wait(1)
              test.equal(app:text("ticks"), "1")

              test.expect(app:newWindow())
              test.equal(app:windows(), 2)

              app:write("/mnt/user/folder/note.txt", "hi")
              test.equal(app:read("/mnt/user/folder/note.txt"), "hi")
              test.expect(app:open("/mnt/user/folder/note.txt"))
              test.equal(app:windows(), 3)
              test.equal(app:text("result"), "Opened /mnt/user/folder/note.txt")

              test.equal(app:command("echo hello")[1], "hello")
              test.expect(app:command("nothing")[1]:find("isn't a command", 1, true))
            end)
            """));

        Assert.True(result.Passed, result.Failure);
    }

    [Theory]
    [InlineData("app:write(\"notes.txt\", \"x\")")]
    [InlineData("app:read(\"/mnt/../../etc/passwd\")")]
    public void PathsStayOnTheTestsVentureOS(string call)
    {
        var result = Assert.Single(Run($"test.case(\"path\", function(app) {call} end)"));

        Assert.Contains("needs an absolute VentureOS path", result.Failure);
    }

    [Fact]
    public void ItRunsACaseAtATimeUntilThereAreNoneLeft()
    {
        SampleProjects.WithTest(project, "a.lua", """
            test.case("one", function(app) end)
            test.case("two", function(app) end)
            """);

        using var tests = ProjectTests.Start(project, SimulatedDisk.FindRootfs()!, Version, printed);

        Assert.Equal("one", tests.Next()?.Name);
        Assert.Equal("two", tests.Next()?.Name);
        Assert.Null(tests.Next());
        Assert.Null(tests.Next());
    }

    [Fact]
    public void EndingPartWayLeavesTheRestUnrunAndTheLookAsItWas()
    {
        var before = DesktopAppearance.Current;
        SampleProjects.WithTest(project, "a.lua", """
            test.case("one", function(app) end)
            test.case("two", function(app) end)
            """);

        var tests = ProjectTests.Start(project, SimulatedDisk.FindRootfs()!, Version, printed);
        Assert.NotNull(tests.Next());
        tests.Dispose();

        Assert.Null(tests.Next());
        Assert.Same(before, DesktopAppearance.Current);
    }

    [Fact]
    public void EditsRunInPlaceOfWhatsSavedInTheAppAndTheTests()
    {
        var saved = Path.Combine(project.Root, "Tests", "saved.lua");
        SampleProjects.WithTest(project, "saved.lua", """test.case("as saved", function(app) end)""");
        var main = Path.Combine(project.AppFolder, "Lua", "main.lua");
        var unsavedTest = Path.Combine(project.Root, "Tests", "unsaved.lua");
        Dictionary<string, string> edits = new()
        {
            [main] = File.ReadAllText(main).Replace("\"Pressed \" .. presses", "\"Clicked \" .. presses", StringComparison.Ordinal),
            [saved] = """test.case("as edited", function(app) app:press("count") test.equal(app:text("result"), "Clicked 1") end)""",
            [unsavedTest] = """test.case("new", function(app) end)""",
        };

        var results = ProjectTests.Run(project, SimulatedDisk.FindRootfs()!, Version, printed, edits: edits);

        Assert.Equal(["as edited", "new"], results.Select(result => result.Name));
        Assert.All(results, result => Assert.True(result.Passed, result.Failure));
        Assert.DoesNotContain("Clicked", File.ReadAllText(main), StringComparison.Ordinal);
        Assert.False(File.Exists(unsavedTest));
    }

    [Fact]
    public void AResultDescribesItselfOnOneLine()
    {
        Assert.Equal("pass Tests/a.lua: one", new AppTestResult("Tests/a.lua", "one", null).Describe());
        Assert.Equal("fail Tests/a.lua: one: why it failed", new AppTestResult("Tests/a.lua", "one", "why it\nfailed").Describe());
        Assert.Equal("fail Tests/a.lua: it has no test.case in it", new AppTestResult("Tests/a.lua", null, "it has no test.case in it").Describe());
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
