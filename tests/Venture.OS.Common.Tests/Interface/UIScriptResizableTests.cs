using Venture.OS.Interface;

namespace Venture.OS.Tests.Interface;

/// <summary><c>resizable</c> on <c>&lt;Window&gt;</c>, which lets the desktop resize it.</summary>
public sealed class UIScriptResizableTests
{
    [Fact]
    public void ReadsResizable()
    {
        var script = UIScript.Parse("""
            <UIScript><Window width="300" height="200" minWidth="200" minHeight="150" resizable="true"/></UIScript>
            """);

        Assert.Equal(new UIWindow(new UISize(300, 200), new UISize(200, 150), IsResizable: true), script.Window);
        Assert.Empty(script.IgnoredAttributes);
    }

    [Fact]
    public void IsFixedUnlessItSaysSo()
    {
        Assert.False(UIScript.Parse("""<UIScript><Window width="300" height="200"/></UIScript>""").Window.IsResizable);
        Assert.False(UIScript.Parse("""<UIScript><Window width="300" height="200" resizable="false"/></UIScript>""").Window.IsResizable);
    }

    [Fact]
    public void RefusesAValueThatIsntTrueOrFalse()
    {
        Assert.Throws<UIScriptException>(() => UIScript.Parse("""<UIScript><Window width="300" height="200" resizable="yes"/></UIScript>"""));
    }
}
