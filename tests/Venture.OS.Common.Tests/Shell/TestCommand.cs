using Venture.OS.Shell;

namespace Venture.OS.Tests.Shell;

/// <summary>
/// A command that records the arguments of every call, and can be told to throw.
/// </summary>
internal sealed class TestCommand(string name, int minArguments = 0, int maxArguments = 0) : IShellCommand
{
    public string Name { get; } = name;
    public string Summary => $"The {Name} test command.";
    public string Usage => $"{Name} [arguments]";
    public int MinArguments { get; } = minArguments;
    public int MaxArguments { get; } = maxArguments;

    public List<IReadOnlyList<string>> Calls { get; } = [];

    /// <summary>When set, <see cref="Execute"/> throws it after recording the call.</summary>
    public Exception? ExceptionToThrow { get; init; }

    public void Execute(ShellContext context, IReadOnlyList<string> arguments)
    {
        Calls.Add(arguments);

        if (ExceptionToThrow is not null)
            throw ExceptionToThrow;
    }
}

/// <summary>A command that takes the rest of the line as written, as <c>lua</c> does, and records it.</summary>
internal sealed class TestTextCommand(string name) : ITextCommand
{
    public string Name { get; } = name;
    public string Summary => $"The {Name} text command.";
    public string Usage => $"{Name} [text]";
    public int MinArguments => 0;
    public int MaxArguments => 1;

    public List<IReadOnlyList<string>> Calls { get; } = [];

    public void Execute(ShellContext context, IReadOnlyList<string> arguments) => Calls.Add(arguments);
}
