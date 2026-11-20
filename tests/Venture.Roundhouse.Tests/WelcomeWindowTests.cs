using Avalonia.Threading;
using Venture.Roundhouse.Welcome;

namespace Venture.Roundhouse.Tests;

/// <summary>
/// The welcome window: its ways to start, the recent items with what's gone dimmed and taken off the list, "Show at
/// startup" kept, and, with the main window, starting from it, coming back when nothing opened, and after Close.
/// </summary>
public sealed class WelcomeWindowTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), $"roundhouse-welcome-{Guid.NewGuid():N}");

    public WelcomeWindowTests() => Directory.CreateDirectory(folder);

    public void Dispose() => Directory.Delete(folder, recursive: true);

    [Fact]
    public void SharesTheMainWindowsTitleBar() => TestApp.Run(() =>
    {
        WelcomeWindow welcome = new("0.7.700", [], [], true, _ => { }, _ => Task.CompletedTask, _ => { });
        welcome.Show();
        MainWindow main = new();
        main.Show();

        // The same bar, merged or not as the main window's is, with the window's name centred in it.
        var bar = welcome.TitleBar;
        Assert.Equal(main.IsTitleBarMerged, bar.IsMerged);
        Assert.Equal(main.DrawsWindowButtons, bar.DrawsWindowButtons);
        Assert.Equal(bar.IsMerged, welcome.ExtendClientAreaToDecorationsHint);
        Assert.Equal(WindowTitleBar.BarHeight, bar.Height);
        Assert.Equal("Welcome to Roundhouse", ((Avalonia.Controls.TextBlock)bar.Title!).Text);
        Assert.Same(bar.WindowButtons, bar.StartItems.Children[0]);

        // It can't be resized, so it can't be zoomed, by its button or by double-clicking its bar.
        Assert.False(bar.MaximizeButton.IsEnabled);
        bar.MaximizeButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Assert.Equal(Avalonia.Controls.WindowState.Normal, welcome.WindowState);

        main.Close();
        welcome.Close();
    });

    [Fact]
    public void ABarBelongsToOneWindow() => TestApp.Run(() =>
    {
        WelcomeWindow welcome = new("0.7.700", [], [], true, _ => { }, _ => Task.CompletedTask, _ => { });

        Assert.Throws<InvalidOperationException>(() => welcome.TitleBar.Attach(welcome, merged: false));
    });

    [Fact]
    public void ListsTheRecentItemsAndSaysWhatsGone() => TestApp.Run(() =>
    {
        var project = Projects.RoundhouseProject.Create("Dice", folder).Root;
        var gone = Path.Combine(folder, "Gone.rproj");
        List<string> opened = [];
        List<string> forgotten = [];
        WelcomeWindow welcome = new("0.7.604", [], [new(project, RecentKind.Project), new(gone, RecentKind.Project)], true, _ => { },
            file =>
            {
                opened.Add(file);
                return Task.CompletedTask;
            },
            forgotten.Add);
        welcome.Show();

        Assert.Equal([true, false], welcome.Recent.Select(row => row.Exists));
        Assert.Equal(WelcomeWindow.Prompt, welcome.Hint);

        welcome.OpenAsync(welcome.Recent[1]).GetAwaiter().GetResult();
        Assert.Empty(opened);
        Assert.Contains("isn't there any more", welcome.Hint);

        welcome.Forget(welcome.Recent[1]);
        Assert.Equal([gone], forgotten);
        Assert.Single(welcome.Recent);

        welcome.OpenAsync(welcome.Recent[0]).GetAwaiter().GetResult();
        Assert.Equal([project], opened);
        welcome.Close();
    });

    [Fact]
    public void HintsSayWhatEachRecentItemIs()
    {
        Assert.Contains("double-click to open", WelcomeWindow.HintFor(new(new("/a/Dice.rproj", RecentKind.Project), true)));
        Assert.Contains("as VentureOS installs it", WelcomeWindow.HintFor(new(new("/a/Settings.app", RecentKind.App), true)));
        Assert.Contains("an interface of its own", WelcomeWindow.HintFor(new(new("/a/open.uix", RecentKind.Interface), true)));
        Assert.Contains("isn't there any more", WelcomeWindow.HintFor(new(new("/a/Gone.rproj", RecentKind.Project), false)));
    }

    [Fact]
    public void ItOffersTheFourWaysToStartAndKeepsShowAtStartup() => TestApp.Run(() =>
    {
        MainWindow window = new() { AskToLeave = _ => Task.FromResult(Bundles.LeaveChoice.DontSave) };
        var welcome = window.ShowWelcome();

        Assert.Equal(["New Interface…", "New App…", "New Project…", "Open…"], welcome.Actions.Select(action => action.Title));
        Assert.All(welcome.Actions, action => Assert.False(string.IsNullOrEmpty(action.Icon)));
        Assert.Same(welcome, window.ShowWelcome());

        var was = welcome.ShowAtStartup;
        welcome.ShowAtStartup = !was;
        Assert.Equal(!was, RoundhouseSettings.Load(RoundhouseSettings.Location).Settings.ShowAtStartup);
        Assert.Equal(!was, window.ShowsWelcomeAtStartup);
        welcome.ShowAtStartup = was;
        window.Close();
    });

    [Fact]
    public void StartingSomethingShowsTheMainWindowAndTheWelcomeGoes() => TestApp.Run(() =>
    {
        MainWindow window = new() { AskToLeave = _ => Task.FromResult(Bundles.LeaveChoice.DontSave) };
        var welcome = window.ShowWelcome();
        Assert.False(window.IsVisible);

        welcome.Actions[0].Run().GetAwaiter().GetResult();
        Dispatcher.UIThread.RunJobs();

        Assert.True(window.IsVisible);
        Assert.True(window.IsUntitled);
        Assert.Null(window.Welcome);
        window.Close();
    });

    [Fact]
    public void OpeningARecentItemOpensItAndCloseBringsTheWelcomeBack() => TestApp.Run(() =>
    {
        var project = Projects.RoundhouseProject.Create("Dialogs", folder).Root;
        MainWindow window = new() { AskToLeave = _ => Task.FromResult(Bundles.LeaveChoice.DontSave) };
        window.Show();
        window.Open(project);
        window.CloseOpenAsync().GetAwaiter().GetResult();

        // Closed: nothing's open, this window's hidden, and the welcome window lists what was.
        Assert.True(window.NothingOpen);
        Assert.False(window.IsVisible);
        var welcome = window.Welcome!;
        var row = Assert.Single(welcome.Recent, row => row.Item.Path == project);

        welcome.OpenAsync(row).GetAwaiter().GetResult();
        Dispatcher.UIThread.RunJobs();

        Assert.True(window.IsVisible);
        Assert.Equal(project, window.Bundle!.Root);
        Assert.Null(window.Welcome);
        window.Close();
    });

    [Fact]
    public void ClosingTheWelcomeWithNothingElseShowingQuits() => TestApp.Run(() =>
    {
        MainWindow window = new();
        var closed = false;
        window.Closed += (_, _) => closed = true;
        var welcome = window.ShowWelcome();

        welcome.Close();
        Dispatcher.UIThread.RunJobs();

        Assert.True(closed);
    });

    [Theory]
    [InlineData("Dice.rproj/Project.xml", "Dice.rproj")]
    [InlineData("Dice.app/Contents/Info.xml", "Dice.app")]
    [InlineData("Dice.rproj/App/Info.xml", "Dice.rproj/App/Info.xml")]
    [InlineData("open.uix", "open.uix")]
    public void OpenTakesAProjectOrAppByItsFile(string chosen, string opened) =>
        Assert.Equal(Path.Combine(folder, opened), MainWindow.Openable(Path.Combine(folder, chosen)));
}
