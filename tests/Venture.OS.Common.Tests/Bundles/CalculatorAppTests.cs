using Venture.OS.Bundles;
using Venture.OS.Desktop;
using Venture.OS.Scripting;
using Venture.OS.Shell;
using Venture.OS.Tests.Scripting;
using Venture.OS.Tests.Shell;

namespace Venture.OS.Tests.Bundles;

/// <summary>
/// Opens the shipped <c>Calculator.app</c> from <c>src/Venture.OS/user/apps</c>, as one of the user's apps, and
/// presses its buttons.
/// </summary>
public sealed class CalculatorAppTests : IDisposable
{
    private static readonly string BuiltIn = AppContext.BaseDirectory;

    private readonly RecordingOutput output = new();
    private readonly RecordingOutput events = new();
    private readonly WindowManager windows = new(1024, 768);
    private readonly AppManager manager;

    public CalculatorAppTests()
    {
        CommandRegistry registry = new();
        manager = new AppManager(
            Path.Combine(BuiltIn, "system", "apps"),
            new RuntimeCommands(registry.IsBuiltIn),
            new FakeSystemInfo(),
            windows: new AppWindows(windows, events),
            userAppsDirectory: Path.Combine(BuiltIn, "user", "apps"));
        Assert.True(manager.OpenApp("calculator", Path.GetTempPath(), output), string.Join('\n', output.Lines));
    }

    public void Dispose() => manager.CloseApp("Calculator", new RecordingOutput());

    private DesktopWindow Window => Assert.Single(windows.Windows);

    [Fact]
    public void IsAUserAppThatOpensQuietly()
    {
        var listing = Assert.Single(manager.Available(), listing => listing.Folder == "Calculator.app");

        Assert.True(listing.IsUser);
        Assert.Equal(ScriptScopes.Ui, listing.Bundle!.Needs);
        Assert.Empty(output.Lines);
        Assert.Equal("0", Window.View.Text("display"));
    }

    [Theory]
    [InlineData("2 + 3 =", "5")]
    [InlineData("2 + 3 × 4 =", "20")]
    [InlineData("7 ÷ 2 =", "3.5")]
    [InlineData("1 ÷ 3 =", "0.3333333333")]
    [InlineData("5 − 8 =", "-3")]
    [InlineData("1 . 5 × 2 =", "3")]
    [InlineData(". 5", "0.5")]
    [InlineData("1 . . 5", "1.5")]
    [InlineData("0 0 7", "7")]
    [InlineData("9 ±", "-9")]
    [InlineData("9 ± ±", "9")]
    [InlineData("5 0 %", "0.5")]
    [InlineData("2 + 3 + 4 =", "9")]
    [InlineData("2 + × 3 =", "6")]
    [InlineData("5 + =", "10")]
    [InlineData("5 ÷ 0 =", "Error")]
    [InlineData("5 ÷ 0 = 4", "4")]
    [InlineData("1 2 3 C", "0")]
    [InlineData("1 2 3 4 5 6 7 8 9 0 1 2 3", "123456789012")]
    public void Calculates(string presses, string display)
    {
        foreach (var key in presses.Split(' '))
            Press(key);

        Assert.Equal(display, Window.View.Text("display"));
        Assert.Empty(events.Lines);
    }

    private void Press(string key)
    {
        var name = key switch
        {
            "+" => "add",
            "−" => "subtract",
            "×" => "multiply",
            "÷" => "divide",
            "=" => "equals",
            "." => "point",
            "±" => "sign",
            "%" => "percent",
            "C" => "clear",
            _ => "d" + key,
        };
        Window.EventHandler!(Window, new UIEvent(UIEventKind.Pressed, name));
    }
}
