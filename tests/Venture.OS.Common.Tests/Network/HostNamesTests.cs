using Venture.OS.Network;

namespace Venture.OS.Tests.Network;

/// <summary>Checking a host name or address someone typed before the kernel sends anything for it.</summary>
public sealed class HostNamesTests
{
    [Theory]
    [InlineData("example.com")]
    [InlineData("example.com.")]
    [InlineData("my-host")]
    [InlineData("a1.b2.c3")]
    [InlineData("10.0.2.2")]
    [InlineData("fe80::1")]
    public void AcceptsNamesAndAddresses(string name)
    {
        Assert.Null(HostNames.Problem(name));
    }

    [Theory]
    [InlineData("", "empty")]
    [InlineData("   ", "empty")]
    [InlineData("exa mple.com", "letters, digits, and hyphens")]
    [InlineData("-start.com", "hyphen")]
    [InlineData("end-.com", "hyphen")]
    [InlineData("two..dots", "empty part")]
    [InlineData("under_score.com", "letters, digits, and hyphens")]
    public void RefusesWhatCantBeAName(string name, string reason)
    {
        Assert.Contains(reason, HostNames.Problem(name), StringComparison.Ordinal);
    }

    [Fact]
    public void RefusesNamesThatAreTooLong()
    {
        Assert.Contains("63", HostNames.Problem(new string('a', 64) + ".com"), StringComparison.Ordinal);
        Assert.Contains("253", HostNames.Problem(string.Join('.', Enumerable.Repeat(new string('a', 60), 5))), StringComparison.Ordinal);
    }
}
