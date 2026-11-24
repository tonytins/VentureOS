using Venture.OS.Desktop;

namespace Venture.OS.Tests.Desktop;

/// <summary>
/// The desktop's words for tests: a table of the tests' own, written once beside the test assembly, so the tests
/// that check the standard menus never read or pin the table VentureOS ships.
/// </summary>
internal static class TestDesktopText
{
    private const string Table = """
        file ^File^
        edit ^Edit^
        view ^View^
        help ^Help^
        closeWindow ^Close Window^
        undo ^Undo^
        redo ^Redo^
        cut ^Cut^
        copy ^Copy^
        paste ^Paste^
        selectAll ^Select All^
        aboutSystem ^About VentureOS^
        returnToShell ^Return to Shell^
        aboutApp ^About %s^
        version ^Version %s^
        newWindow ^New Window^
        quitApp ^Quit %s^
        ok ^OK^
        """;

    private static readonly Lazy<string> Folder = new(() =>
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "test-desktop-text");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "_101_desktop.cst"), Table);
        return folder;
    });

    /// <summary>The tests' words, with anything missing reported to <paramref name="report"/>.</summary>
    public static DesktopText Create(Action<string>? report = null) => new(Folder.Value, report);
}
