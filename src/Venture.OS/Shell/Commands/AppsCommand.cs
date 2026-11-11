using Venture.OS.Bundles;
using Venture.OS.Startup;

namespace Venture.OS.Shell.Commands;

/// <summary>
/// Lists the system's apps and which are open, the text-mode version of the dock's marks.
/// </summary>
internal sealed class AppsCommand(Func<AppManager?> apps) : IShellCommand
{
    public string Name => "apps";
    public string Summary => "List the apps, and which are open.";
    public string Usage => "apps";
    public int MinArguments => 0;
    public int MaxArguments => 0;

    public void Execute(ShellContext context, IReadOnlyList<string> arguments)
    {
        var output = context.Output;
        if (apps() is not { } manager)
        {
            output.WriteLine("apps: apps aren't available.");
            return;
        }

        var listings = manager.Available();
        if (listings.Count == 0)
        {
            output.WriteLine($"No apps in {Bootstrap.AppsDirectoryPath}.");
        }
        else
        {
            var width = 0;
            foreach (var listing in listings)
                width = Math.Max(width, (listing.Bundle?.Name ?? listing.Folder).Length);

            foreach (var listing in listings)
            {
                if (listing.Bundle is not { } bundle)
                {
                    output.WriteLine($"  {listing.Folder}: {listing.Problem}");
                    continue;
                }

                var state = manager.IsOpen(bundle.Identifier) ? "open" : "";
                output.WriteLine($"  {bundle.Name.PadRight(width)}  {bundle.Version ?? "",-8}  {state}".TrimEnd());
            }
        }

        // Apps opened by path, from outside the system's apps.
        foreach (var running in manager.Running)
        {
            if (!ShellPaths.IsWithin(running.Bundle.Path, Bootstrap.AppsDirectoryPath))
                output.WriteLine($"  {running.Bundle.Name} (from {running.Bundle.Path})  open");
        }
    }
}
