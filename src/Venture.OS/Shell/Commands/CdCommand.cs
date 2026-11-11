using Venture.OS.Startup;

namespace Venture.OS.Shell.Commands;

/// <summary>
/// Changes the shell's current folder, which relative paths and scripts start from.
/// </summary>
/// <remarks>
/// Built in rather than a script, since it changes the shell itself. With no folder, it goes back to the RAM disk.
/// </remarks>
internal sealed class CdCommand : IShellCommand
{
    public string Name => "cd";
    public string Summary => "Change the current folder.";
    public string Usage => "cd [folder]";
    public int MinArguments => 0;
    public int MaxArguments => 1;

    public void Execute(ShellContext context, IReadOnlyList<string> arguments)
    {
        var target = arguments.Count == 0
            ? Bootstrap.RamMountPoint
            : ShellPaths.Resolve(context.CurrentDirectory, arguments[0]);

        if (File.Exists(target))
        {
            context.Output.WriteLine($"cd: {target} is a file, not a folder.");
            return;
        }

        if (!Directory.Exists(target))
        {
            context.Output.WriteLine($"cd: there's no folder at {target}.");
            return;
        }

        context.CurrentDirectory = target;
    }
}
