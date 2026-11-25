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
        quickLookFolder ^Folder^
        quickLookFile ^File^
        quickLookApp ^Application^
        quickLookBrokenApp ^Application that can't open^
        quickLookBrokenInterface ^Interface that can't be shown^
        quickLookNoVersion ^not given^
        quickLookNeeds ^Needs %s^
        quickLookShowingFirst ^(Showing the first %s of %s.)^
        oneItem ^1 item^
        items ^%s items^
        oneByte ^1 byte^
        bytes ^%s bytes^
        kilobytes ^%s KB^
        megabytes ^%s MB^
        trustTitle ^Trust This Computer?^
        trust ^Trust^
        deny ^Deny^
        notifications ^Notifications^
        clearAll ^Clear All^
        noNotifications ^No notifications^
        moreFrom ^%s more from %s^
        justNow ^now^
        minutesAgo ^%s min^
        hoursAgo ^%s hr^
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
