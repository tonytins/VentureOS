using Venture.OS.Bundles;
using Venture.OS.Interface;

namespace Venture.OS.Desktop;

/// <summary>
/// Quick Look: a window that shows what a file is without opening it in an app.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description>A <c>.uix</c> file shows as its interface, live, so a document can be checked while it's
/// written. One that can't be read shows why.</description></item>
/// <item><description>Text shows as its lines, from the top, up to <see cref="MaximumBytes"/>.</description></item>
/// <item><description>An app shows its name, version, identifier, and what it needs; a folder, how many items it
/// holds; anything else, its size.</description></item>
/// </list>
/// <para>A file is text unless its start holds a zero byte, as binary files nearly always do. Previews belong to the
/// desktop, not the app that asked for them, so they close when the desktop does.</para>
/// </remarks>
public static class QuickLook
{
    /// <summary>The most of a file a preview reads, so a huge file can't use up memory.</summary>
    public const int MaximumBytes = 64 * 1024;

    // How much of the start is checked for a zero byte.
    private const int SniffBytes = 1024;

    /// <summary>Makes a preview of a file or folder.</summary>
    /// <param name="path">A full path.</param>
    /// <param name="window">The preview, when it returns <see langword="true"/>.</param>
    /// <param name="problem">Why there's no preview, such as a missing file, when it returns <see langword="false"/>.</param>
    public static bool TryCreate(string path, out DesktopWindow window, out string problem)
    {
        ArgumentNullException.ThrowIfNull(path);
        window = null!;
        problem = "";
        var name = Path.GetFileName(path.TrimEnd('/'));
        if (name.Length == 0)
            name = path;

        try
        {
            if (Directory.Exists(path))
            {
                window = path.EndsWith(Bundle.Extension(BundleKind.App), StringComparison.OrdinalIgnoreCase)
                    ? AppSummary(path, name)
                    : Summary(name, "Folder", Items(Directory.GetFileSystemEntries(path).Length));
                return true;
            }

            if (!File.Exists(path))
            {
                problem = $"there's no file or folder at {path}";
                return false;
            }

            if (path.EndsWith(".uix", StringComparison.OrdinalIgnoreCase))
            {
                window = Interface(path, name);
                return true;
            }

            window = Contents(path, name);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            problem = $"{name} couldn't be read: {exception.Message}";
            return false;
        }
    }

    private static DesktopWindow Interface(string path, string name)
    {
        try
        {
            return new DesktopWindow(name, new UIView(UIScript.Load(path)));
        }
        catch (UIScriptException exception)
        {
            return Summary(name, "Interface that can't be shown", exception.Message);
        }
    }

    private static DesktopWindow Contents(string path, string name)
    {
        long length;
        byte[] bytes;
        using (var stream = File.OpenRead(path))
        {
            length = stream.Length;
            bytes = new byte[(int)Math.Min(length, MaximumBytes)];
            var read = 0;
            while (read < bytes.Length)
            {
                var got = stream.Read(bytes, read, bytes.Length - read);
                if (got == 0)
                    break;
                read += got;
            }

            if (read < bytes.Length)
                Array.Resize(ref bytes, read);
        }

        if (Array.IndexOf(bytes, (byte)0, 0, Math.Min(bytes.Length, SniffBytes)) >= 0)
            return Summary(name, "File", Size(length));

        var text = System.Text.Encoding.UTF8.GetString(bytes);
        List<string> lines = [.. text.Replace("\r\n", "\n").Split('\n')];
        if (lines.Count > 1 && lines[^1].Length == 0)
            lines.RemoveAt(lines.Count - 1);
        if (length > MaximumBytes)
            lines.Add($"(Showing the first {MaximumBytes / 1024} KB of {Size(length)}.)");

        var view = new UIView(UIScript.Parse("""
            <UIScript version="1">
              <Window width="520" height="360"/>
              <List name="lines" x="8" y="8" width="504" height="344" selectable="true"/>
            </UIScript>
            """));
        view.List("lines")!.Items.AddRange(lines);
        return new DesktopWindow(name, view);
    }

    private static DesktopWindow AppSummary(string path, string name)
    {
        try
        {
            var app = Bundle.Read(path, BundleKind.App);
            var details = $"{app.Identifier}\nVersion {app.Version ?? "not given"}\nNeeds {AppManager.DescribeNeeds(app.Needs)}";
            return Summary(app.Name, "Application", details);
        }
        catch (BundleException exception)
        {
            return Summary(name, "Application that can't open", exception.Message);
        }
    }

    /// <summary>A small window with a kind in large text and details under it.</summary>
    private static DesktopWindow Summary(string title, string kind, string details)
    {
        var view = new UIView(UIScript.Parse("""
            <UIScript version="1">
              <Window width="340" height="150"/>
              <Style name="kind" fontSize="18"/>
              <Style name="plain" backgroundColor="#F0F3F7"/>
              <Text name="kind" x="16" y="14" width="308" height="26" style="kind"/>
              <FormattedText name="details" x="16" y="46" width="308" height="90" style="plain"/>
            </UIScript>
            """));
        view.SetText("kind", kind);
        view.SetText("details", details);
        return new DesktopWindow(title, view);
    }

    private static string Items(int count) => count == 1 ? "1 item" : $"{count} items";

    private static string Size(long bytes) => bytes switch
    {
        1 => "1 byte",
        < 1024 => $"{bytes} bytes",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
        _ => $"{bytes / (1024.0 * 1024.0):0.#} MB",
    };
}
