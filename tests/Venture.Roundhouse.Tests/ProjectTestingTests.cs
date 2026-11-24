using Avalonia.Controls;
using Avalonia.Threading;
using Venture.OS.Desktop;

namespace Venture.Roundhouse.Tests;

/// <summary>
/// Run ▸ Test: the open project's tests run against its app as it's edited, a result a turn of the window's loop,
/// each in the Log and the counts in the status line, as <c>airo test</c> runs them.
/// </summary>
[Collection(nameof(PaletteCollection))]
public sealed class ProjectTestingTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), $"roundhouse-testing-{Guid.NewGuid():N}");

    public ProjectTestingTests() => Directory.CreateDirectory(folder);

    public void Dispose()
    {
        DesktopAppearance.Use(DesktopLook.Standard);
        Directory.Delete(folder, recursive: true);
    }

    private const string Presses = """
        test.case("counts a press", function(app)
          app:press("press")
          test.equal(app:text("count"), "Pressed once.")
        end)
        test.case("starts unpressed", function(app)
          test.equal(app:text("count"), "Press the button.")
        end)
        """;

    [Fact]
    public void RunsTheProjectsTestsIntoTheLogAndCountsThem() => TestApp.Run(() =>
    {
        var (window, project) = OpenCounter();
        Write(project, "presses.lua", Presses);

        Assert.True(window.Test());
        Assert.NotNull(window.Testing);
        WaitForTests(window);

        Assert.Null(window.Testing);
        Assert.Equal("Counter.rproj: 2 passed, 0 failed", Status(window));
        Assert.Contains(Logging.RoundhouseLog.Buffer.Lines, line => line.Contains("pass Tests/presses.lua: counts a press", StringComparison.Ordinal));
        Assert.Contains(Logging.RoundhouseLog.Buffer.Lines, line => line.Contains("pass Tests/presses.lua: starts unpressed", StringComparison.Ordinal));
    });

    [Fact]
    public void AFailureIsLoggedWithWhyAndCounted() => TestApp.Run(() =>
    {
        var (window, project) = OpenCounter();
        Write(project, "wrong.lua", """test.case("miscounts", function(app) test.equal(app:text("count"), "Pressed twice.") end)""");

        window.Test();
        WaitForTests(window);

        Assert.Equal("Counter.rproj: 0 passed, 1 failed", Status(window));
        Assert.Contains(Logging.RoundhouseLog.Buffer.Lines, line =>
            line.Contains("WARN", StringComparison.Ordinal)
            && line.Contains("fail Tests/wrong.lua: miscounts: Tests/wrong.lua:1: expected \"Pressed twice.\", got \"Press the button.\"", StringComparison.Ordinal));
    });

    [Fact]
    public void TheAppAndTheTestsRunAsTheyreEditedSavedOrNot() => TestApp.Run(() =>
    {
        var (window, project) = OpenCounter();
        var test = Write(project, "presses.lua", """test.case("saved", function(app) end)""");
        var lua = Path.Combine(project, "App", "Lua", "main.lua");
        window.Bundle!.Edit(lua, File.ReadAllText(lua).Replace("Pressed once.", "Pressed one time.", StringComparison.Ordinal));
        window.Bundle.Edit(test, """
            test.case("unsaved", function(app)
              app:press("press")
              test.equal(app:text("count"), "Pressed one time.")
            end)
            """);

        window.Test();
        WaitForTests(window);

        Assert.Equal("Counter.rproj: 1 passed, 0 failed", Status(window));
        Assert.Contains(Logging.RoundhouseLog.Buffer.Lines, line => line.Contains("pass Tests/presses.lua: unsaved", StringComparison.Ordinal));
        Assert.DoesNotContain("one time", File.ReadAllText(lua), StringComparison.Ordinal);
    });

    [Fact]
    public void AProjectWithoutTestsSaysSo() => TestApp.Run(() =>
    {
        var (window, _) = OpenCounter();

        Assert.False(window.Test());
        Assert.Equal("Counter.rproj has no tests: there are no .lua files in its Tests/ folder.", Status(window));
    });

    [Fact]
    public void WhatIsntAProjectSaysWhatTestNeeds() => TestApp.Run(() =>
    {
        var document = Path.Combine(folder, "loose.uix");
        File.WriteAllText(document, """<UIScript version="1"><Window width="100" height="100"/></UIScript>""");
        MainWindow window = new();
        window.Show();
        window.Open(document);

        Assert.False(window.Test());
        Assert.Equal("Test runs a project's tests, and what's open isn't a project.", Status(window));
    });

    [Fact]
    public void ClosingTheProjectStopsTheTests() => TestApp.Run(() =>
    {
        var (window, project) = OpenCounter();
        Write(project, "presses.lua", Presses);

        window.Test();
        _ = window.CloseOpenAsync();
        WaitForTests(window);

        Assert.Null(window.Testing);
        Assert.StartsWith("Counter.rproj's tests stopped, since it was closed", Status(window));
    });

    private (MainWindow Window, string Project) OpenCounter()
    {
        MainWindow window = new() { AskToLeave = _ => Task.FromResult(Bundles.LeaveChoice.DontSave) };
        window.Show();
        var project = window.CreateApp("Counter", "dev.example.counter", folder);
        Dispatcher.UIThread.RunJobs();
        return (window, project);
    }

    private static string Write(string project, string name, string lua)
    {
        var path = Path.Combine(project, "Tests", name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, lua);
        return path;
    }

    private static string? Status(MainWindow window) => window.FindControl<TextBlock>("StatusText")!.Text;

    // Lets the window's loop run the tests, a case a turn, until they finish; ten seconds at most.
    private static void WaitForTests(MainWindow window)
    {
        if (window.Testing is null)
            return;

        DispatcherFrame frame = new();
        void Finished(object? sender, EventArgs e) => frame.Continue = false;
        window.TestingFinished += Finished;
        DispatcherTimer.RunOnce(() => frame.Continue = false, TimeSpan.FromSeconds(10));
        Dispatcher.UIThread.PushFrame(frame);
        window.TestingFinished -= Finished;
        Assert.Null(window.Testing);
    }
}
