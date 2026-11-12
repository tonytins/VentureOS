using Venture.OS.Desktop;
using Venture.OS.Interface;

namespace Venture.OS.Tests.Desktop;

/// <summary>
/// Reads the interfaces in <c>rootfs/system/interfaces</c>, copied next to the tests, since the desktop opens
/// them at runtime, where a mistake would only show up after booting.
/// </summary>
public sealed class SystemInterfacesTests
{
    private static readonly string Interfaces = Path.Combine(AppContext.BaseDirectory, "system", "interfaces");

    [Fact]
    public void EveryShippedInterfaceReads()
    {
        var files = Directory.GetFiles(Interfaces, "*.uix");

        Assert.NotEmpty(files);
        foreach (var file in files)
            _ = UIScript.Load(file);
    }

    [Fact]
    public void AboutHasTheControlsTheDesktopUses()
    {
        UIView view = new(UIScript.Load(Path.Combine(Interfaces, "about.uix")));

        view.SetText("version", "Version 0.3.0 (Build 1)");

        Assert.Equal("Version 0.3.0 (Build 1)", view.Text("version"));
        Assert.Equal(UIControlType.Button, view.Script.Control("ok")?.Type);
        Assert.Empty(view.MissingImages);
        Assert.Empty(view.MissingStrings);
    }
}
