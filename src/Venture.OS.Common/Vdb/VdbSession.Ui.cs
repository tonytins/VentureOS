using System.Globalization;
using Venture.OS.Desktop;

namespace Venture.OS.Vdb;

// The ui feature: app windows seen and used by name, keys, and screenshots. See docs/vdb/README.md#ui.
public sealed partial class VdbSession
{
    /// <summary>The most rows a snapshot lists of one list, sidebar, or grid; <c>rows=</c> still says how many there are.</summary>
    public const int MaximumSnapshotRows = 100;

    /// <summary>How many of a screenshot's <c>data</c> lines go out each frame, so the frame loop keeps its pace.</summary>
    public const int ScreenLinesPerTick = 16;

    private readonly IVdbDesktop? desktop;

    // A screenshot still going out, and how far.
    private VdbScreenshot? screen;
    private int screenSent;

    // A heads-up on the desktop, from VentureOS, when there's a desktop to show it.
    private void Notify(string text)
    {
        if (desktop is { } shown)
            _ = shown.Windows.Notifications.TryPost("VentureOS", text, null, null, shown.Windows.IsShowing, out _, out _);
    }

    private void HandleUi(string command, string rest, IVdbDesktop desktop)
    {
        if (!VdbWords.TrySplit(rest, out var words, out var problem))
        {
            Error(problem);
            return;
        }

        switch (command)
        {
            case "windows":
                Windows(desktop, words);
                break;
            case "snapshot":
                Snapshot(desktop, words);
                break;
            case "act":
                Act(desktop, words);
                break;
            case "key":
                Key(desktop, words);
                break;
            case "screenshot":
                Screenshot(desktop, words);
                break;
            case "notifications":
                Notifications(desktop, words);
                break;
        }
    }

    // Only windows an app opened, its alerts among them; never a panel, or one of the system's own, such as Trust.
    private static bool IsAppWindow(DesktopWindow window) => window.App is not null && !window.IsPanel;

    private void Windows(IVdbDesktop desktop, List<VdbWord> words)
    {
        if (words.Count != 0)
        {
            Error("windows takes nothing after it");
            return;
        }

        var all = desktop.Windows.Windows;
        var focused = desktop.Windows.Focused;
        for (var index = all.Count - 1; index >= 0; index--)
        {
            var window = all[index];
            if (!IsAppWindow(window))
                continue;
            var cut = false;
            var line = $"window id={window.Id}{VdbWords.Pair("app", window.App, ref cut)}{VdbWords.Pair("title", window.Title, ref cut)}"
                + $"{VdbWords.Flag("front", window == focused)}{VdbWords.Flag("minimized", window.IsMinimized)}";
            Send(line + VdbWords.Flag("cut", cut));
        }

        Send("ok");
    }

    private void Notifications(IVdbDesktop desktop, List<VdbWord> words)
    {
        if (words.Count != 0)
        {
            Error("notifications takes nothing after it");
            return;
        }

        var notifications = desktop.Windows.Notifications;
        var now = notifications.Now;
        foreach (var notification in notifications.History)
        {
            var cut = false;
            var line = $"notification{VdbWords.Pair("app", notification.App, ref cut)}{VdbWords.Pair("title", notification.Title, ref cut)}"
                + $"{VdbWords.Pair("text", notification.Text, ref cut)}{VdbWords.Pair("seconds", Math.Max(0, (int)(now - notification.Posted).TotalSeconds))}";
            Send(line + VdbWords.Flag("cut", cut));
        }

        Send("ok");
    }

    private void Snapshot(IVdbDesktop desktop, List<VdbWord> words)
    {
        if (words.Count != 1)
        {
            Error("snapshot names a window by its number, such as snapshot 3");
            return;
        }

        if (!TryFindWindow(desktop, words[0], out var window))
            return;

        var snapshot = WindowControls.Snapshot(window);
        var cut = false;
        var head = $"window id={window.Id}{VdbWords.Pair("title", snapshot.Title, ref cut)}{VdbWords.Pair("subtitle", snapshot.Subtitle, ref cut)}"
            + $"{VdbWords.Pair("pane", snapshot.Pane, ref cut)}{VdbWords.Pair("focused", snapshot.Focused, ref cut)}";
        Send(head + VdbWords.Flag("cut", cut));

        foreach (var control in snapshot.Controls)
        {
            cut = false;
            var line = $"control{VdbWords.Pair("name", control.Name, ref cut)} kind={control.Kind}{VdbWords.Pair("text", control.Text, ref cut)}"
                + $"{VdbWords.Pair("value", control.Value)}{VdbWords.Flag("hidden", !control.Showing)}{VdbWords.Flag("dimmed", control.Dimmed)}"
                + $"{VdbWords.Flag("checked", control.Checked)}{VdbWords.Flag("focused", control.Focused)}"
                + $"{VdbWords.Pair("chosen", control.Chosen)}{VdbWords.Pair("rows", control.Rows?.Count)}";
            List<string> rows = [];
            for (var index = 0; index < Math.Min(control.Rows?.Count ?? 0, MaximumSnapshotRows); index++)
                rows.Add($"row {VdbWords.Quote(control.Rows![index], ref cut)}");

            Send(line + VdbWords.Flag("cut", cut));
            foreach (var row in rows)
                Send(row);
        }

        Send("ok");
    }

    private void Act(IVdbDesktop desktop, List<VdbWord> words)
    {
        if (words.Count < 3)
        {
            Error("act names a window, an action, and a control, such as act 3 press ok");
            return;
        }

        var action = words[1].Text;
        var name = words[2].Text;
        var expected = action switch
        {
            "press" => 3,
            "submit" or "type" or "choose" => 4,
            _ => 0,
        };
        if (expected == 0)
        {
            Error($"\"{Shorten(action)}\" isn't an action; act can press, submit, type, or choose");
            return;
        }

        if (words.Count != expected)
        {
            Error(action switch
            {
                "press" => "act press takes a window and a control, such as act 3 press ok",
                "choose" => "act choose takes a window, a control, and a row's number or quoted text, such as act 3 choose notes 2",
                _ => $"act {action} takes a window, a control, and quoted text, such as act 3 {action} name \"Ada\"",
            });
            return;
        }

        if (!TryFindWindow(desktop, words[0], out var window))
            return;

        // As a click would, it comes to the front first.
        desktop.Windows.Open(window);

        string? problem;
        var done = action switch
        {
            "press" => WindowControls.TryPress(window, name, out problem),
            "submit" => WindowControls.TrySubmit(window, name, words[3].Text, out problem),
            "type" => WindowControls.TryType(window, name, words[3].Text, out problem),
            _ => Choose(window, name, words[3], out problem),
        };
        if (done)
            Send("ok");
        else
            Error(problem!);
    }

    // A quoted row is its text, and a bare one its number, counting from 1.
    private static bool Choose(DesktopWindow window, string name, VdbWord row, out string? problem)
    {
        if (row.IsQuoted)
            return WindowControls.TryChoose(window, name, row.Text, out problem);
        if (int.TryParse(row.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
            return WindowControls.TryChoose(window, name, number, out problem);
        problem = $"a row is its number, such as 2, or its text, quoted, such as \"{Shorten(row.Text)}\"";
        return false;
    }

    private void Key(IVdbDesktop desktop, List<VdbWord> words)
    {
        if (words.Count < 2 || words.Count > VdbKeys.MaximumKeys + 1)
        {
            Error($"key names a window and 1 to {VdbKeys.MaximumKeys} keys, such as key 3 Ctrl+Z");
            return;
        }

        List<KeyPress> keys = [];
        foreach (var word in words.Skip(1))
        {
            if (!VdbKeys.TryParse(word.Text, out var key, out var problem))
            {
                Error(problem);
                return;
            }

            keys.Add(key);
        }

        if (!TryFindWindow(desktop, words[0], out var window))
            return;

        desktop.Windows.Open(window);
        if (desktop.PressKeys(window, keys) is { } refused)
            Error(refused);
        else
            Send("ok");
    }

    private void Screenshot(IVdbDesktop desktop, List<VdbWord> words)
    {
        if (words.Count != 0)
        {
            Error("screenshot takes nothing after it");
            return;
        }

        VdbScreenshot? shot;
        try
        {
            shot = desktop.Screenshot();
        }
        catch (OutOfMemoryException)
        {
            shot = null;
        }

        if (shot is null)
        {
            Error("the screen couldn't be read");
            return;
        }

        if (shot.Problem() is { } problem)
        {
            Error(problem);
            return;
        }

        Send($"screen width={shot.Width} height={shot.Height}");
        screen = shot;
        screenSent = 0;
        SendScreen();
    }

    // Sends the next few of a screenshot's data lines, and ok after the last.
    private void SendScreen()
    {
        var shot = screen!;
        for (var lines = 0; lines < ScreenLinesPerTick && screenSent < shot.Rgb.Length && !IsClosed; lines++)
        {
            var count = Math.Min(VdbProtocol.MaximumDataBytes, shot.Rgb.Length - screenSent);
            Send($"data {Convert.ToBase64String(shot.Rgb, screenSent, count)}");
            screenSent += count;
        }

        if (screenSent >= shot.Rgb.Length)
        {
            screen = null;
            Send("ok");
        }
    }

    // The app window a word names by its number, answering an error when there's none.
    private bool TryFindWindow(IVdbDesktop desktop, VdbWord word, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out DesktopWindow? window)
    {
        window = null;
        if (word.IsQuoted || !int.TryParse(word.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
        {
            Error($"a window is named by its number, as windows lists it, not \"{Shorten(word.Text)}\"");
            return false;
        }

        window = desktop.Windows.Find(id);
        if (window is not null && IsAppWindow(window))
            return true;
        Error($"there's no app window {id}; windows lists them");
        window = null;
        return false;
    }

    // An error, cut to fit in a line, since its reason may carry what the computer sent.
    private void Error(string problem) => Send($"error {Fit(problem, VdbProtocol.MaximumLineLength - 6)}");
}
