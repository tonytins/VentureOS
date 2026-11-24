using Venture.OS.Desktop;

namespace Venture.OS.Tests.Desktop;

/// <summary>
/// The desktop's own words, read from the system's table 101 by key: what it shows, how it fills a name in, and how
/// a missing word or table shows and is reported, once.
/// </summary>
public sealed class DesktopTextTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"venture-desktop-text-{Guid.NewGuid():N}");
    private readonly List<string> reports = [];

    public DesktopTextTests() => Directory.CreateDirectory(root);

    public void Dispose() => Directory.Delete(root, recursive: true);

    [Fact]
    public void ReadsAWordByItsKeyAndFillsInNames()
    {
        Table("closeWindow ^Shut Window^\nquitApp ^Leave %s^\n");
        DesktopText text = new(root, reports.Add);

        Assert.Equal("Shut Window", text.Get("closeWindow"));
        Assert.Equal("Leave Notes", text.Get("quitApp", "Notes"));
        Assert.Empty(reports);
    }

    [Fact]
    public void AMissingWordShowsItsKeyAndIsReportedOnce()
    {
        Table("closeWindow ^Close Window^\n");
        DesktopText text = new(root, reports.Add);

        Assert.Equal("101:undo", text.Get("undo"));
        Assert.Equal("101:undo", text.Get("undo"));
        Assert.Equal(["_101_desktop.cst has no \"undo\"."], reports);
    }

    [Fact]
    public void AMissingTableIsReportedOnceNotOnceAWord()
    {
        DesktopText text = new(root, reports.Add);

        Assert.Equal("101:undo", text.Get("undo"));
        Assert.Equal("101:redo", text.Get("redo"));
        Assert.Equal(["The desktop's text, _101_desktop.cst, isn't in the system's strings."], reports);
    }

    [Fact]
    public void WithoutAStringsFolderEveryWordIsItsKey()
    {
        DesktopText text = new(null, reports.Add);

        Assert.Equal("101:file", text.Get("file"));
        Assert.Single(reports);
    }

    [Fact]
    public void ATablesOwnProblemsAreReported()
    {
        Table("closeWindow ^Close Window^\ncloseWindow ^Again^\n");
        DesktopText text = new(root, reports.Add);

        Assert.Equal("Close Window", text.Get("closeWindow"));
        Assert.Contains(reports, report => report.StartsWith("_101_desktop.cst, line 2:", StringComparison.Ordinal));
    }

    [Fact]
    public void TheMenuBarAndAboutSpeakTheTablesWords()
    {
        Table("""
            file ^Document^
            edit ^Change^
            view ^Look^
            help ^Assist^
            closeWindow ^Shut Window^
            undo ^Take Back^
            redo ^Do Again^
            cut ^Snip^
            copy ^Duplicate^
            paste ^Put^
            selectAll ^Choose Everything^
            aboutSystem ^This Computer^
            returnToShell ^Back to the Prompt^
            ok ^Fine^
            """);
        DesktopSession session = new(800, 600, text: new DesktopText(root, reports.Add));

        var titles = session.MenuBar.Menus.Select(menu => menu.Menu.Title).ToList();
        Assert.Contains("Document", titles);
        Assert.Contains("Assist", titles);
        var change = session.MenuBar.Menus.Single(menu => menu.Menu.Title == "Change").Menu;
        Assert.Equal(["Take Back", "Do Again", null, "Snip", "Duplicate", "Put", null, "Choose Everything"],
            change.Entries.Select(entry => entry.Title));
        Assert.Empty(reports);
    }

    [Fact]
    public void TheShippedTableHasEveryWordTheDesktopAsksFor()
    {
        // A rule over what ships, not its wording: every key the desktop uses is in VentureOS's own table.
        DesktopText text = new(Path.Combine(AppContext.BaseDirectory, "system", "strings"), reports.Add);
        foreach (var key in (string[])["file", "edit", "view", "help", "closeWindow", "undo", "redo", "cut", "copy",
            "paste", "selectAll", "aboutSystem", "returnToShell", "newWindow", "ok", "quickLookFolder", "quickLookFile",
            "quickLookApp", "quickLookBrokenApp", "quickLookBrokenInterface", "quickLookNoVersion", "oneItem", "oneByte",
            "trustTitle", "trust", "deny"])
            _ = text.Get(key);
        foreach (var key in (string[])["aboutApp", "version", "quitApp", "quickLookNeeds", "items", "bytes", "kilobytes", "megabytes"])
            Assert.Contains("Name", text.Get(key, "Name"), StringComparison.Ordinal);
        Assert.Contains("Second", text.Get("quickLookShowingFirst", "First", "Second"), StringComparison.Ordinal);

        Assert.Empty(reports);
    }

    private void Table(string content) => File.WriteAllText(Path.Combine(root, "_101_desktop.cst"), content);
}
