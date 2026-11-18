using Venture.OS.Vdb;

namespace Venture.OS.Tests.Vdb;

public sealed class VdbProtocolTests
{
    [Fact]
    public void TheLargestDataLineFitsInALine()
    {
        var line = "data " + Convert.ToBase64String(new byte[VdbProtocol.MaximumDataBytes]);

        Assert.True(line.Length <= VdbProtocol.MaximumLineLength, $"A full data line is {line.Length} characters.");
    }

    [Fact]
    public void OneMoreDataByteDoesNotFit()
    {
        // Base64 grows four characters for each three bytes, so the next size that changes the line is three more.
        var line = "data " + Convert.ToBase64String(new byte[VdbProtocol.MaximumDataBytes + 3]);

        Assert.True(line.Length > VdbProtocol.MaximumLineLength);
    }

    [Fact]
    public void AFileFitsInABundle() =>
        Assert.True(VdbProtocol.MaximumFileSize <= VdbProtocol.MaximumBundleSize);
}
