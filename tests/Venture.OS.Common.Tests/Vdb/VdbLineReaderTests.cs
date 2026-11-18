using System.Text;
using Venture.OS.Vdb;

namespace Venture.OS.Tests.Vdb;

public sealed class VdbLineReaderTests
{
    [Fact]
    public void JoinsALineCutAcrossReads()
    {
        VdbLineReader reader = new();

        reader.Feed("hel"u8);
        Assert.Null(reader.Take());
        reader.Feed("lo\nbye\n"u8);

        Assert.Equal("hello", reader.Take());
        Assert.Equal("bye", reader.Take());
        Assert.Null(reader.Take());
    }

    [Fact]
    public void DropsTheCarriageReturnBeforeALineFeed()
    {
        VdbLineReader reader = new();

        reader.Feed("ok\r\n"u8);

        Assert.Equal("ok", reader.Take());
    }

    [Fact]
    public void TakesALineOfTheLongestLength()
    {
        VdbLineReader reader = new();

        reader.Feed(Encoding.ASCII.GetBytes(new string('a', VdbProtocol.MaximumLineLength) + "\r\n"));

        Assert.Null(reader.Problem);
        Assert.Equal(VdbProtocol.MaximumLineLength, reader.Take()!.Length);
    }

    [Fact]
    public void StopsAtALineTooLong()
    {
        VdbLineReader reader = new();

        reader.Feed(Encoding.ASCII.GetBytes(new string('a', VdbProtocol.MaximumLineLength + 2) + "\nok\n"));

        Assert.Equal("a line is longer than 8192 bytes", reader.Problem);
        Assert.Null(reader.Take());
    }

    [Fact]
    public void StopsAtALineTooLongEvenWithACarriageReturnInIt()
    {
        VdbLineReader reader = new();

        reader.Feed(Encoding.ASCII.GetBytes(new string('a', VdbProtocol.MaximumLineLength) + "a\n"));

        Assert.NotNull(reader.Problem);
    }

    [Fact]
    public void StopsAtTextThatIsNotUtf8()
    {
        VdbLineReader reader = new();

        reader.Feed([(byte)'o', 0xFF, (byte)'\n']);

        Assert.Equal("a line isn't UTF-8 text", reader.Problem);
        Assert.Null(reader.Take());
    }

    [Fact]
    public void ReadsUtf8()
    {
        VdbLineReader reader = new();

        reader.Feed("out Café ☕\n"u8);

        Assert.Equal("out Café ☕", reader.Take());
    }
}
