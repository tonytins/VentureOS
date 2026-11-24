using System.CommandLine;
using System.CommandLine.Invocation;
using Venture.Airo.Projects;
using Venture.Airo.Simulation;
using Venture.Airo.Testing;
using Venture.OS.Shell;

namespace Venture.Airo;

/// <summary>
/// airo's command line. It never asks anything, so scripts and Roundhouse can run it: what it made goes to the
/// output, one path a line, for the next program to read, and what went wrong goes to the error output with exit code 1.
/// </summary>
public static class AiroCommand
{
    /// <summary>Runs airo with <paramref name="args"/>, writing to <paramref name="output"/> and <paramref name="error"/>.</summary>
    /// <returns>The exit code: 0 when it did what was asked, 1 when it didn't.</returns>
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        var root = new RootCommand("airo builds Roundhouse Projects into apps VentureOS runs.");
        root.Subcommands.Add(BuildCommand());
        root.Subcommands.Add(TestCommand());

        var result = root.Parse(args);

        // A mistake on the command line is said on the error output alone; help on the output would read as
        // something airo made.
        if (result.Action is ParseErrorAction parseError)
            parseError.ShowHelp = false;

        return result.Invoke(new InvocationConfiguration { Output = output, Error = error });
    }

    private static Command BuildCommand()
    {
        Argument<string> project = new("project") { Description = "The project to build: its .rproj folder, or its Project.xml." };
        Option<string> outputFolder = new("--output", "-o")
        {
            Description = "The folder to write the .app into, made if it isn't there, such as rootfs/user/apps.",
            Required = true,
            HelpName = "folder",
        };
        Option<bool> replace = new("--replace") { Description = "Replace an .app of the same name that's already there." };

        var command = new Command("build", "Build a project's app into an .app, checked as VentureOS reads apps.")
        {
            project,
            outputFolder,
            replace,
        };
        command.SetAction(parse =>
        {
            var output = parse.InvocationConfiguration.Output;
            var error = parse.InvocationConfiguration.Error;
            try
            {
                var opened = RoundhouseProject.Open(ProjectFolder(parse.GetValue(project)!));
                var app = opened.Assemble(parse.GetValue(outputFolder)!, replace: parse.GetValue(replace));
                output.WriteLine(app);
                return 0;
            }
            catch (ProjectException exception)
            {
                error.WriteLine($"airo: {exception.Message}");
                return 1;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                error.WriteLine($"airo: the app couldn't be written: {exception.Message}");
                return 1;
            }
        });
        return command;
    }

    private static Command TestCommand()
    {
        Argument<string> project = new("project") { Description = "The project to test: its .rproj folder, or its Project.xml." };
        var command = new Command("test", "Run a project's tests, in its Tests/ folder, against its app in a simulated VentureOS.")
        {
            project,
        };
        command.SetAction(parse =>
        {
            var output = parse.InvocationConfiguration.Output;
            var error = parse.InvocationConfiguration.Error;
            try
            {
                var opened = RoundhouseProject.Open(ProjectFolder(parse.GetValue(project)!));
                if (ProjectTests.Files(opened).Count == 0)
                {
                    error.WriteLine($"airo: {Path.GetFileName(opened.Root)} has no tests: there are no .lua files in its {ProjectTests.FolderName}/ folder.");
                    return 1;
                }

                if (SimulatedDisk.FindRootfs() is not { } rootfs)
                {
                    error.WriteLine("airo: VentureOS's files (rootfs/) aren't beside airo or in a repository above it, so there's nothing to run the app in.");
                    return 1;
                }

                var results = ProjectTests.Run(opened, rootfs, Version, new WriterOutput(error), result => output.WriteLine(result.Describe()));
                var failed = results.Count(result => !result.Passed);
                output.WriteLine($"{results.Count - failed} passed, {failed} failed");
                return failed == 0 ? 0 : 1;
            }
            catch (ProjectException exception)
            {
                error.WriteLine($"airo: {exception.Message}");
                return 1;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                error.WriteLine($"airo: the tests couldn't be run: {exception.Message}");
                return 1;
            }
        });
        return command;
    }

    // What the apps under test report as VentureOS's version: airo's own, as the kernel built from this commit would.
    private static readonly SimulatedVersion Version = new(
        $"{ThisAssembly.Git.SemVer.Major}.{ThisAssembly.Git.SemVer.Minor}.{ThisAssembly.Git.SemVer.Patch}", ThisAssembly.Git.Commit, "airo");

    // The tests' own print, to the error output, so the output stays a line a result.
    private sealed class WriterOutput(TextWriter writer) : IShellOutput
    {
        public void WriteLine(string text) => writer.WriteLine(text);

        public void Clear()
        {
        }
    }

    // A project is named by its folder, or by its Project.xml, which a file picker or a shell's completion may give.
    private static string ProjectFolder(string path) =>
        string.Equals(Path.GetFileName(path), RoundhouseProject.FileName, StringComparison.OrdinalIgnoreCase) && File.Exists(path)
            ? Path.GetDirectoryName(Path.GetFullPath(path))!
            : path;
}
