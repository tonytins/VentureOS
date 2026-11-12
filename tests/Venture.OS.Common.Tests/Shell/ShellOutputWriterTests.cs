using Venture.OS.Shell;

namespace Venture.OS.Tests.Shell;

public sealed class ShellOutputWriterTests
{
    private readonly RecordingOutput output = new();

    [Fact]
    public void WritesEachCompleteLine()
    {
        using (ShellOutputWriter writer = new(output))
            writer.Write("one\ntwo\n");

        Assert.Equal(["one", "two"], output.Lines);
    }

    [Fact]
    public void JoinsPiecesOfOneLine()
    {
        using ShellOutputWriter writer = new(output);
        writer.Write("a");
        writer.Write('b');
        writer.Write("c\n");

        Assert.Equal(["abc"], output.Lines);
    }

    [Fact]
    public void DropsTheCarriageReturnBeforeALineBreak()
    {
        using ShellOutputWriter writer = new(output);
        writer.Write("one\r\n");

        Assert.Equal(["one"], output.Lines);
    }

    [Fact]
    public void KeepsEmptyLines()
    {
        using ShellOutputWriter writer = new(output);
        writer.Write("\n\n");

        Assert.Equal(["", ""], output.Lines);
    }

    [Fact]
    public void WritesAnUnfinishedLineWhenDisposedButNotWhenFlushed()
    {
        ShellOutputWriter writer = new(output);
        writer.Write("partial");
        writer.Flush();

        Assert.Empty(output.Lines);

        writer.Dispose();
        Assert.Equal(["partial"], output.Lines);
    }

    [Fact]
    public void WritesNothingWhenDisposedEmpty()
    {
        new ShellOutputWriter(output).Dispose();

        Assert.Empty(output.Lines);
    }

    [Fact]
    public void RejectsNullOutput() => Assert.Throws<ArgumentNullException>(() => new ShellOutputWriter(null!));
}
