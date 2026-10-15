using Venture.OS.Shell;

namespace Venture.OS.Tests.Shell;

public sealed class CommandRegistryTests
{
    [Fact]
    public void FindsRegisteredCommandsIgnoringCase()
    {
        CommandRegistry registry = new();
        TestCommand help = new("help");
        registry.Register(help);

        Assert.True(registry.TryGet("help", out var lower));
        Assert.True(registry.TryGet("HELP", out var upper));
        Assert.Same(help, lower);
        Assert.Same(help, upper);
    }

    [Fact]
    public void ReportsUnknownCommands()
    {
        CommandRegistry registry = new();
        registry.Register(new TestCommand("help"));

        Assert.False(registry.TryGet("nope", out var command));
        Assert.Null(command);
    }

    [Fact]
    public void ListsCommandsInNameOrder()
    {
        CommandRegistry registry = new();
        registry.Register(new TestCommand("shutdown"));
        registry.Register(new TestCommand("clear"));
        registry.Register(new TestCommand("help"));

        Assert.Equal(["clear", "help", "shutdown"], registry.Commands.Select(command => command.Name));
    }

    [Theory]
    [InlineData("info")]
    [InlineData("net2")]
    [InlineData("disk-free")]
    [InlineData("a")]
    public void AcceptsLowercaseLettersDigitsAndHyphens(string name)
    {
        CommandRegistry registry = new();
        registry.Register(new TestCommand(name));

        Assert.True(registry.TryGet(name, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Help")]
    [InlineData("two words")]
    [InlineData("under_score")]
    [InlineData("café")]
    public void RejectsInvalidNames(string name)
    {
        CommandRegistry registry = new();

        Assert.Throws<ArgumentException>(() => registry.Register(new TestCommand(name)));
    }

    [Fact]
    public void RejectsDuplicateNames()
    {
        CommandRegistry registry = new();
        registry.Register(new TestCommand("help"));

        var exception = Assert.Throws<ArgumentException>(() => registry.Register(new TestCommand("help")));
        Assert.Contains("already registered", exception.Message);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(2, 1)]
    public void RejectsInvalidArgumentRanges(int minArguments, int maxArguments)
    {
        CommandRegistry registry = new();

        Assert.Throws<ArgumentException>(() => registry.Register(new TestCommand("bad", minArguments, maxArguments)));
    }

    [Fact]
    public void RejectsNullArguments()
    {
        CommandRegistry registry = new();

        Assert.Throws<ArgumentNullException>(() => registry.Register(null!));
        Assert.Throws<ArgumentNullException>(() => registry.TryGet(null!, out _));
    }
}
