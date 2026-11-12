using Venture.OS.Interface;

namespace Venture.OS.Tests.Interface;

/// <summary>
/// <c>role</c> on <c>&lt;Menu&gt;</c>, which says which of the desktop's standard menus an app's menu adds to.
/// </summary>
public sealed class UIScriptMenuRoleTests
{
    private const string Head = "<UIScript requires=\"menus\"><Window width=\"400\" height=\"300\"/>";

    [Theory]
    [InlineData("file", UIMenuRole.File)]
    [InlineData("edit", UIMenuRole.Edit)]
    [InlineData("view", UIMenuRole.View)]
    [InlineData("help", UIMenuRole.Help)]
    public void ReadsARole(string role, UIMenuRole expected)
    {
        var script = UIScript.Parse(Head + $"""<Menu name="m" title="Things" role="{role}"/></UIScript>""");

        Assert.Equal(expected, script.Menus[0].Role);
        Assert.Empty(script.IgnoredAttributes);
    }

    [Fact]
    public void HasNoRoleUnlessItSaysSo()
    {
        Assert.Null(UIScript.Parse(Head + """<Menu name="m" title="File"/></UIScript>""").Menus[0].Role);
    }

    [Fact]
    public void SkipsARoleItDoesntKnow()
    {
        // A newer VentureOS may add roles; this one shows such a menu as the app's own rather than refusing it.
        var script = UIScript.Parse(Head + """<Menu name="m" title="Window" role="window"/></UIScript>""");

        Assert.Null(script.Menus[0].Role);
        Assert.Equal([new UIIgnoredAttribute("Menu", "role", 1)], script.IgnoredAttributes);
    }
}
