using Venture.OS.Shell;

namespace Venture.OS.Tests.Shell;

public sealed class CommandShellTests
{
    private readonly RecordingOutput output = new();
    private readonly RecordingHost host = new();
    private readonly CommandRegistry registry = new();

    private CommandShell CreateShell() => new(new ShellContext(output, host, registry));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void IgnoresBlankLines(string input)
    {
        TestCommand command = new("help");
        registry.Register(command);

        CreateShell().Execute(input);

        Assert.Empty(command.Calls);
        Assert.Empty(output.Lines);
    }

    [Fact]
    public void RunsTheCommandWithItsArguments()
    {
        TestCommand command = new("write", minArguments: 1, maxArguments: 2);
        registry.Register(command);

        CreateShell().Execute(@"write ""hello world"" again");

        var call = Assert.Single(command.Calls);
        Assert.Equal(["hello world", "again"], call);
        Assert.Empty(output.Lines);
    }

    [Fact]
    public void MatchesCommandNamesIgnoringCase()
    {
        TestCommand command = new("help");
        registry.Register(command);

        CreateShell().Execute("HeLp");

        Assert.Single(command.Calls);
    }

    [Fact]
    public void ReportsUnknownCommands()
    {
        CreateShell().Execute("nope");

        var line = Assert.Single(output.Lines);
        Assert.Contains("\"nope\" is not a command", line);
    }

    [Fact]
    public void ReportsParseErrorsWithoutRunningAnything()
    {
        TestCommand command = new("help", maxArguments: 1);
        registry.Register(command);

        CreateShell().Execute(@"help ""open");

        Assert.Empty(command.Calls);
        var line = Assert.Single(output.Lines);
        Assert.Contains("double quote", line);
    }

    [Theory]
    [InlineData("copy")]
    [InlineData("copy one")]
    [InlineData("copy one two three")]
    public void PrintsUsageWhenTheArgumentCountIsWrong(string input)
    {
        TestCommand command = new("copy", minArguments: 2, maxArguments: 2);
        registry.Register(command);

        CreateShell().Execute(input);

        Assert.Empty(command.Calls);
        Assert.Equal([$"Usage: {command.Usage}"], output.Lines);
    }

    [Fact]
    public void LetsCommandExceptionsReachTheCaller()
    {
        InvalidOperationException failure = new("disk on fire");
        registry.Register(new TestCommand("fail") { ExceptionToThrow = failure });

        var thrown = Assert.Throws<InvalidOperationException>(() => CreateShell().Execute("fail"));
        Assert.Same(failure, thrown);
    }

    [Fact]
    public void GivesCommandsTheContextTheShellWasBuiltWith()
    {
        ShellContext? seen = null;
        registry.Register(new ContextCapturingCommand(context => seen = context));
        ShellContext context = new(output, host, registry);

        new CommandShell(context).Execute("capture");

        Assert.Same(context, seen);
    }

    [Fact]
    public void RejectsNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() => new CommandShell(null!));
        Assert.Throws<ArgumentNullException>(() => CreateShell().Execute(null!));
        Assert.Throws<ArgumentNullException>(() => new ShellContext(null!, host, registry));
        Assert.Throws<ArgumentNullException>(() => new ShellContext(output, null!, registry));
        Assert.Throws<ArgumentNullException>(() => new ShellContext(output, host, null!));
    }

    private sealed class ContextCapturingCommand(Action<ShellContext> capture) : IShellCommand
    {
        public string Name => "capture";
        public string Summary => "Captures its context.";
        public string Usage => "capture";
        public int MinArguments => 0;
        public int MaxArguments => 0;

        public void Execute(ShellContext context, IReadOnlyList<string> arguments) => capture(context);
    }
}
