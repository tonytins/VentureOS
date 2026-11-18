using Venture.OS.Vdb;

namespace Venture.OS.Tests.Vdb;

public sealed class VdbNamesTests
{
    [Theory]
    [InlineData("Dice.app")]
    [InlineData("My Dice.app")]
    [InlineData("dice.APP")]
    public void TakesAnAppsFolderName(string app) => Assert.Null(VdbNames.ProblemWithApp(app));

    [Theory]
    [InlineData("")]
    [InlineData(".app")]
    [InlineData("Dice")]
    [InlineData("Dice.theme")]
    [InlineData(".Dice.app")]
    [InlineData("apps/Dice.app")]
    [InlineData("..\\Dice.app")]
    [InlineData("C:Dice.app")]
    [InlineData("Di\nce.app")]
    public void RefusesWhatIsNotAnAppsFolderName(string app) => Assert.NotNull(VdbNames.ProblemWithApp(app));

    [Fact]
    public void RefusesAnAppsNameTooLong() =>
        Assert.NotNull(VdbNames.ProblemWithApp(new string('a', VdbProtocol.MaximumPathLength) + ".app"));

    [Theory]
    [InlineData("Contents/Info.xml")]
    [InlineData("Contents/Lua/main.lua")]
    [InlineData("Contents/Resources/My Icon.png")]
    [InlineData("README")]
    [InlineData("Contents/..hidden")]
    public void TakesAPathInsideTheBundle(string path) => Assert.Null(VdbNames.ProblemWithPath(path));

    [Theory]
    [InlineData("")]
    [InlineData("/mnt/system/init.lua")]
    [InlineData("../init.lua")]
    [InlineData("Contents/../../init.lua")]
    [InlineData("Contents/./Info.xml")]
    [InlineData("Contents//Info.xml")]
    [InlineData("Contents/")]
    [InlineData("Contents\\Info.xml")]
    [InlineData("C:/Info.xml")]
    [InlineData("Contents/In\u0000fo.xml")]
    public void RefusesAPathThatIsNotInsideTheBundle(string path) => Assert.NotNull(VdbNames.ProblemWithPath(path));

    [Fact]
    public void RefusesAPathTooLong() =>
        Assert.NotNull(VdbNames.ProblemWithPath(new string('a', VdbProtocol.MaximumPathLength + 1)));

    [Theory]
    [InlineData("0", 0)]
    [InlineData("241", 241)]
    [InlineData("4194304", 4194304)]
    public void ReadsASize(string text, long expected)
    {
        Assert.True(VdbNames.TryReadSize(text, VdbProtocol.MaximumFileSize, out var size));
        Assert.Equal(expected, size);
    }

    [Theory]
    [InlineData("")]
    [InlineData("-1")]
    [InlineData("+5")]
    [InlineData("1.5")]
    [InlineData(" 5")]
    [InlineData("4194305")]
    [InlineData("99999999999999999999")]
    [InlineData("٣")]
    public void RefusesWhatIsNotASize(string text) => Assert.False(VdbNames.TryReadSize(text, VdbProtocol.MaximumFileSize, out _));
}
