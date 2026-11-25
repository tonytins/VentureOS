using Venture.OS.Scripting;
using Venture.OS.Tests.Shell;

namespace Venture.OS.Tests.Scripting;

/// <summary>
/// <c>venture.ui</c> for code that isn't an app, such as a shell command: a message, Quick Look, or an app, with no
/// scope to ask for, since none of it calls the script back. Interactive windows stay with apps.
/// </summary>
public sealed class ScriptUiTests
{
    private readonly RecordingOutput output = new();
    private readonly FakeScriptDesktop desktop = new();

    [Fact]
    public void AnAlertShowsInAWindow()
    {
        Run("""
            print(venture.ui.alert("Backup finished."))
            venture.ui.alert("The disk is full.", "Backup")
            """);

        Assert.Equal([("Message", "Backup finished."), ("Backup", "The disk is full.")], desktop.Alerts);
        Assert.Equal(["true"], output.Lines);
    }

    [Fact]
    public void WithoutTheDesktopOnTheScreenAnAlertIsPrinted()
    {
        desktop.IsShowing = false;

        Run("""
            venture.ui.alert("Backup finished.")
            venture.ui.alert("The disk is full.", "Backup")
            """);

        Assert.Empty(desktop.Alerts);
        Assert.Equal(["Backup finished.", "Backup: The disk is full."], output.Lines);
    }

    [Fact]
    public void AToastFromAScriptGoesToTheDesktop()
    {
        Run("""print(venture.ui.toast("Backup finished."))""", scopes: ScriptScopes.None);

        Assert.Equal(["Backup finished."], desktop.Toasts);
        Assert.Equal(["true"], output.Lines);
    }

    [Fact]
    public void WithoutADesktopAToastIsPrinted()
    {
        LuaRunner.Run(output, "/mnt", lua => lua.DoString("""
            print(venture.ui.toast("Backup finished."))
            print(venture.ui.toast(""))
            """), new FakeSystemInfo());

        Assert.Equal(["VentureOS: Backup finished.", "true", "nil\ta notification needs some text"], output.Lines);
    }

    [Fact]
    public void PreviewAndLaunchTakePathsFromTheScriptsFolder()
    {
        Run("""
            print(venture.ui.preview("notes.txt"))
            print(venture.ui.launch("TextEdit", "notes.txt"))
            print(venture.ui.launch("Calculator"))
            """, "/mnt/user");

        Assert.Equal(["/mnt/user/notes.txt"], desktop.Previews);
        Assert.Equal([("TextEdit", "notes.txt", "/mnt/user"), ("Calculator", null, "/mnt/user")], desktop.Launches);
        Assert.Equal(["true", "true", "true"], output.Lines);
    }

    [Fact]
    public void AProblemComesBackAsNilAndWhy()
    {
        desktop.Problem = "there's no app called Nope";

        Run("""print(venture.ui.launch("Nope"))""");

        Assert.Equal(["nil\tthere's no app called Nope"], output.Lines);
    }

    [Fact]
    public void NeedsNoScope()
    {
        Run("""venture.ui.alert("Hi.")""", scopes: ScriptScopes.None);

        Assert.Single(desktop.Alerts);
    }

    [Fact]
    public void WithoutADesktopPreviewAndLaunchSaySo()
    {
        LuaRunner.Run(output, "/mnt", lua => lua.DoString("""
            print(venture.ui.preview("a.txt"))
            print(venture.ui.launch("Files"))
            venture.ui.alert("Still printed.")
            """), new FakeSystemInfo());

        Assert.Equal(
            ["nil\tventure.ui.preview needs the desktop, which this system doesn't have",
             "nil\tventure.ui.launch needs the desktop, which this system doesn't have",
             "Still printed."],
            output.Lines);
    }

    [Fact]
    public void OpeningAWindowIsStillForApps()
    {
        Run("""print(pcall(venture.ui.open, "main.uix"))""");

        Assert.Equal([$"false\t{VentureLibrary.NotAppMessage}"], output.Lines);
    }

    private void Run(string code, string folder = "/mnt", ScriptScopes scopes = ScriptScopes.All) =>
        Assert.True(LuaRunner.Run(output, folder, lua => lua.DoString(code), new FakeSystemInfo { Desktop = desktop }, scopes: scopes));
}
