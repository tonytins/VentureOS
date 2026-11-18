using Venture.OS.Bundles;

namespace Venture.OS.Vdb;

/// <summary>
/// One app being sent: its files written to a staging folder as they arrive, checked as VentureOS checks apps at
/// boot, and only then moved into the user's apps folder. Whatever goes wrong, nothing is left in staging, and the
/// apps folder changes only once the app has passed.
/// </summary>
internal sealed class VdbInstall
{
    private readonly IVdbHost host;
    private readonly HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);
    private long bundleSize;

    // The file being received: where it goes, its bytes, and how many have arrived.
    private string? filePath;
    private byte[]? fileBytes;
    private int received;

    private VdbInstall(IVdbHost host, string app, string staged)
    {
        this.host = host;
        App = app;
        Staged = staged;
    }

    /// <summary>The app's folder name, such as <c>Dice.app</c>.</summary>
    public string App { get; }

    /// <summary>Where its files are written until it's checked.</summary>
    public string Staged { get; }

    /// <summary>Whether a file is being received, between <c>file</c> and <c>end</c>.</summary>
    public bool HasOpenFile => filePath is not null;

    /// <summary>Starts an app, emptying any staging left from before.</summary>
    /// <exception cref="IOException">Staging couldn't be made.</exception>
    /// <exception cref="UnauthorizedAccessException">Staging couldn't be made.</exception>
    public static VdbInstall Begin(IVdbHost host, string app)
    {
        var staged = $"{host.StagingFolder}/{app}";
        if (Directory.Exists(staged))
            Directory.Delete(staged, recursive: true);
        Directory.CreateDirectory(staged);
        return new VdbInstall(host, app, staged);
    }

    /// <summary>Starts a file, or says why it can't be one of this app's.</summary>
    public string? BeginFile(string path, string sizeText)
    {
        if (HasOpenFile)
            return "a file is already open; end it first";
        if (VdbNames.ProblemWithPath(path) is { } problem)
            return problem;
        if (!VdbNames.TryReadSize(sizeText, VdbProtocol.MaximumFileSize, out var size))
            return $"\"{sizeText}\" isn't a size from 0 to {VdbProtocol.MaximumFileSize} bytes";
        if (paths.Count == VdbProtocol.MaximumFiles)
            return $"an app sent over vdb has at most {VdbProtocol.MaximumFiles} files";
        if (!paths.Add(path))
            return $"{path} was already sent";
        if (bundleSize + size > VdbProtocol.MaximumBundleSize)
            return $"an app sent over vdb is at most {VdbProtocol.MaximumBundleSize / (1024 * 1024)} MiB";

        bundleSize += size;
        filePath = path;
        fileBytes = new byte[size];
        received = 0;
        return null;
    }

    /// <summary>Adds a <c>data</c> line's base64 to the open file, or says why it can't.</summary>
    public string? AddData(string base64)
    {
        if (fileBytes is null)
            return "no file is open";
        if (base64.Length == 0 || base64.Length % 4 != 0)
            return "a data line holds whole base64, a multiple of four characters";

        // At most three bytes for every four characters, less padding; more than the file has room for is too many.
        var most = base64.Length / 4 * 3;
        var room = fileBytes.Length - received;
        var span = most <= room ? fileBytes.AsSpan(received, most) : new byte[most];
        if (!Convert.TryFromBase64String(base64, span, out var written))
            return "a data line isn't base64";
        if (written > room)
            return $"{filePath} is longer than the {fileBytes.Length} bytes it was sent as";

        if (most > room)
            span[..written].CopyTo(fileBytes.AsSpan(received));
        received += written;
        return null;
    }

    /// <summary>Writes the open file once all of it has arrived, or says why it can't.</summary>
    public string? EndFile()
    {
        if (filePath is null || fileBytes is null)
            return "no file is open";
        if (received != fileBytes.Length)
            return $"{filePath} is {received} bytes, not the {fileBytes.Length} it was sent as";

        var target = $"{Staged}/{filePath}";
        try
        {
            var folder = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(folder))
                Directory.CreateDirectory(folder);
            File.WriteAllBytes(target, fileBytes);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return $"{filePath} couldn't be written: {exception.Message}";
        }
        finally
        {
            (filePath, fileBytes, received) = (null, null, 0);
        }

        return null;
    }

    /// <summary>
    /// Checks the app and installs it in the user's apps folder, closing it and replacing any copy already there.
    /// </summary>
    /// <returns>Why it was refused, or <see langword="null"/> once it's installed at <paramref name="installed"/>.</returns>
    public string? Commit(out string installed)
    {
        installed = $"{host.AppsFolder}/{App}";
        if (HasOpenFile)
            return $"{filePath} hasn't ended";

        Bundle bundle;
        try
        {
            bundle = Bundle.Read(Staged, BundleKind.App);
        }
        catch (BundleException exception)
        {
            return exception.Message;
        }

        if (host.IsSystemApp(bundle.Identifier))
            return $"{bundle.Identifier} is a system app's, which vdb can't replace";

        try
        {
            host.CloseApp(bundle.Identifier);
            if (Directory.Exists(installed))
            {
                // The copy being replaced may be another app by the same folder name; it closes too.
                if (TryIdentify(installed) is { } old && old != bundle.Identifier)
                    host.CloseApp(old);
                Directory.Delete(installed, recursive: true);
            }

            Directory.CreateDirectory(host.AppsFolder);
            Copy(Staged, installed);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return $"{App} passed, but couldn't be installed: {exception.Message}";
        }

        Abort();
        return null;
    }

    /// <summary>Throws away whatever was staged. Safe to call more than once.</summary>
    public void Abort()
    {
        (filePath, fileBytes, received) = (null, null, 0);
        try
        {
            if (Directory.Exists(Staged))
                Directory.Delete(Staged, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Staging is emptied again when the next app starts, so a leftover costs nothing but memory until then.
        }
    }

    private static string? TryIdentify(string folder)
    {
        try
        {
            return Bundle.Read(folder, BundleKind.App).Identifier;
        }
        catch (BundleException)
        {
            return null;
        }
    }

    // Copied rather than moved: the kernel's filesystem isn't known to move folders.
    private static void Copy(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var file in Directory.GetFiles(from))
            File.Copy(file, $"{to}/{Path.GetFileName(file)}");
        foreach (var folder in Directory.GetDirectories(from))
            Copy(folder, $"{to}/{Path.GetFileName(folder)}");
    }
}
