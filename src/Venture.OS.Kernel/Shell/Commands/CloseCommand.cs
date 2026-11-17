using Venture.OS.Bundles;

namespace Venture.OS.Shell.Commands;

/// <summary>
/// Closes an open app, undoing everything it set up, such as its commands.
/// </summary>
internal sealed class CloseCommand(Func<AppManager?> apps) : IShellCommand
{
    public string Name => "close";
    public string Summary => "Close an open app.";
    public string Usage => "close <app>";
    public int MinArguments => 1;
    public int MaxArguments => 1;

    public void Execute(ShellContext context, IReadOnlyList<string> arguments)
    {
        if (apps() is not { } manager)
        {
            context.Output.WriteLine("close: apps aren't available.");
            return;
        }

        manager.CloseApp(arguments[0], context.Output);
    }
}
