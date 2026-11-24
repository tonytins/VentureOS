using System.CommandLine;
using System.CommandLine.Invocation;
using Venture.Airo.Projects;

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

    // A project is named by its folder, or by its Project.xml, which a file picker or a shell's completion may give.
    private static string ProjectFolder(string path) =>
        string.Equals(Path.GetFileName(path), RoundhouseProject.FileName, StringComparison.OrdinalIgnoreCase) && File.Exists(path)
            ? Path.GetDirectoryName(Path.GetFullPath(path))!
            : path;
}
