namespace Venture.OS.Shell;

/// <summary>
/// What a running command can reach: its output, the kernel, and the other commands.
/// </summary>
public sealed class ShellContext(IShellOutput output, IShellHost host, CommandRegistry commands)
{
    public IShellOutput Output { get; } = output ?? throw new ArgumentNullException(nameof(output));

    public IShellHost Host { get; } = host ?? throw new ArgumentNullException(nameof(host));

    public CommandRegistry Commands { get; } = commands ?? throw new ArgumentNullException(nameof(commands));

    /// <summary>
    /// The folder relative paths start from, such as <c>/mnt</c>, which <c>cd</c> changes. Scripts run from it.
    /// </summary>
    /// <exception cref="ArgumentException">The value isn't a full path.</exception>
    public string CurrentDirectory
    {
        get;
        set => field = ShellPaths.IsFull(value)
            ? value
            : throw new ArgumentException($"\"{value}\" isn't a full path.", nameof(value));
    } = "/";
}
