using Venture.OS.Bundles;

namespace Venture.OS.Shell.Commands;

/// <summary>
/// Opens an app: by name from the system's apps, or by the path of an <c>.app</c> bundle anywhere.
/// </summary>
/// <remarks>Built in, since an open app is an owner the kernel keeps until it closes.</remarks>
internal sealed class OpenCommand(Func<AppManager?> apps) : IShellCommand
{
    public string Name => "open";
    public string Summary => "Open an app.";
    public string Usage => "open <app>";
    public int MinArguments => 1;
    public int MaxArguments => 1;

    public void Execute(ShellContext context, IReadOnlyList<string> arguments)
    {
        if (apps() is not { } manager)
        {
            context.Output.WriteLine("open: apps aren't available.");
            return;
        }

        manager.OpenApp(arguments[0], context.CurrentDirectory, context.Output);
    }
}
