using Venture.OS.Desktop;
using Venture.OS.Scripting;
using Venture.OS.Shell;

namespace Venture.OS.Bundles;

/// <summary>
/// Opens, closes, and lists apps: <c>.app</c> bundles, each running as a <see cref="LuaOwner"/> while it's open.
/// </summary>
/// <remarks>
/// <para>Opening an app reads its bundle, makes an owner with the app's <c>needs</c>, and runs its <c>main</c>
/// script, which sets the app up, such as registering commands. Closing it ends the owner, which undoes all of
/// that. An app is open at most once, by identifier.</para>
/// <para>Apps live in one folder, such as <c>/mnt/system/apps</c>, and open by name, ignoring case. An app
/// elsewhere opens by its path, and says what it needs first, since nothing reviewed it.</para>
/// </remarks>
/// <param name="appsDirectory">Where the system's apps are, such as <c>/mnt/system/apps</c>.</param>
/// <param name="commands">Where apps register commands.</param>
/// <param name="system">What apps' <c>venture</c> table reports, or <see langword="null"/>.</param>
/// <param name="libraryDirectory">The system library, searched after an app's own <c>Contents/Lua</c>.</param>
/// <param name="runShell">Runs a line as if typed at the shell, for <c>venture.shell</c>, or <see langword="null"/>.</param>
/// <param name="startupStop">
/// Makes the stop for an app's <c>main</c> script when it opens, such as Ctrl+C or a time limit; called once per open.
/// </param>
/// <param name="windows">Where apps' windows go, for <c>venture.ui</c>, or <see langword="null"/> without a desktop.</param>
public sealed class AppManager(
    string appsDirectory,
    RuntimeCommands commands,
    ISystemInfo? system = null,
    string? libraryDirectory = null,
    Action<string>? runShell = null,
    Func<Func<string?>>? startupStop = null,
    AppWindows? windows = null)
{
    private readonly string appsDirectory = appsDirectory?.TrimEnd('/') ?? throw new ArgumentNullException(nameof(appsDirectory));
    private readonly RuntimeCommands commands = commands ?? throw new ArgumentNullException(nameof(commands));
    private readonly List<RunningApp> running = [];

    /// <summary>An app that's open: its bundle and the owner running it.</summary>
    public sealed record RunningApp(Bundle Bundle, LuaOwner Owner);

    /// <summary>An entry in the apps folder: a bundle that read, or the reason it didn't.</summary>
    public sealed record Listing(string Folder, Bundle? Bundle, string? Problem);

    /// <summary>The apps that are open, in the order they opened.</summary>
    public IReadOnlyList<RunningApp> Running => running;

    /// <summary>Every <c>.app</c> in the apps folder, sorted by folder name, with any that can't be read.</summary>
    public IReadOnlyList<Listing> Available()
    {
        List<Listing> listings = [];
        if (!Directory.Exists(appsDirectory))
            return listings;

        foreach (var folder in Directory.GetDirectories(appsDirectory))
        {
            var name = Path.GetFileName(folder);
            if (!name.EndsWith(Bundle.Extension(BundleKind.App), StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                listings.Add(new Listing(name, Bundle.Read($"{appsDirectory}/{name}", BundleKind.App), null));
            }
            catch (BundleException exception)
            {
                listings.Add(new Listing(name, null, exception.Message));
            }
        }

        listings.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.Folder, right.Folder));
        return listings;
    }

    /// <summary>
    /// Opens the app <paramref name="target"/> names: an app in the apps folder, by name, or a bundle path.
    /// </summary>
    /// <param name="currentDirectory">Where a relative path starts, and where the app's code runs from.</param>
    /// <param name="output">Where the app's messages, and any problem, go.</param>
    /// <returns><see langword="true"/> if it opened.</returns>
    public bool OpenApp(string target, string currentDirectory, IShellOutput output)
    {
        ArgumentException.ThrowIfNullOrEmpty(target);
        ArgumentNullException.ThrowIfNull(currentDirectory);
        ArgumentNullException.ThrowIfNull(output);

        if (Find(target, currentDirectory) is not { } path)
        {
            output.WriteLine($"open: there's no app called {target}. Type \"apps\" to see them.");
            return false;
        }

        Bundle bundle;
        try
        {
            bundle = Bundle.Read(path, BundleKind.App);
        }
        catch (BundleException exception)
        {
            output.WriteLine($"open: {exception.Message}");
            return false;
        }

        if (FindOpen(bundle.Identifier) is not null)
        {
            output.WriteLine($"{bundle.Name} is already open.");
            return false;
        }

        if (!ShellPaths.IsWithin(bundle.Path, appsDirectory))
            output.WriteLine($"{bundle.Name} needs {DescribeNeeds(bundle.Needs)}.");

        var library = libraryDirectory is null
            ? $"{bundle.Path}/Contents/Lua"
            : $"{bundle.Path}/Contents/Lua;{libraryDirectory}";
        LuaOwner owner = new(
            bundle.Name,
            $"{bundle.Name} closes",
            mayReplaceScripts: false,
            output,
            currentDirectory,
            system,
            bundle.Needs,
            library,
            commands,
            runShell);
        AppLibrary.Open(owner.Interpreter.State, bundle);
        UiLibrary.Open(owner.Interpreter.State, owner, bundle, windows);

        if (!owner.Run(lua => lua.DoFile(bundle.Main!), startupStop?.Invoke()))
        {
            // A main script that failed partway undoes what it set up, so nothing half-open is left.
            owner.Dispose();
            output.WriteLine($"open: {bundle.Name} didn't open; see the error above.");
            return false;
        }

        running.Add(new RunningApp(bundle, owner));
        return true;
    }

    /// <summary>Closes an open app, named by its name, folder name, or identifier, ignoring case.</summary>
    /// <returns><see langword="true"/> if it closed.</returns>
    public bool CloseApp(string target, IShellOutput output)
    {
        ArgumentException.ThrowIfNullOrEmpty(target);
        ArgumentNullException.ThrowIfNull(output);

        var app = FindOpen(target);
        if (app is null)
        {
            output.WriteLine($"close: {target} isn't open.");
            return false;
        }

        if (app.Owner.IsRunning)
        {
            output.WriteLine($"close: {app.Bundle.Name} can't close while its own code is running.");
            return false;
        }

        app.Owner.Dispose();
        running.Remove(app);
        return true;
    }

    /// <summary>Whether an app with this identifier is open.</summary>
    public bool IsOpen(string identifier) => FindOpen(identifier) is not null;

    /// <summary>The scopes an app needs, as words, such as <c>files and shell</c>, or <c>nothing extra</c>.</summary>
    public static string DescribeNeeds(ScriptScopes needs)
    {
        var names = ScriptScopeNames.Describe(needs);
        if (names == "none")
            return "nothing extra";

        var parts = names.Split(", ");
        return parts.Length == 1 ? parts[0] : $"{string.Join(", ", parts[..^1])} and {parts[^1]}";
    }

    /// <summary>The bundle path a target names, or <see langword="null"/>.</summary>
    private string? Find(string target, string currentDirectory)
    {
        var extension = Bundle.Extension(BundleKind.App);

        // A path, such as ./Notes.app or /mnt/Notes.app.
        if (target.Contains('/') || target.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
        {
            var path = ShellPaths.Resolve(currentDirectory, target);
            if (Directory.Exists(path))
                return path;
            if (target.Contains('/'))
                return null;
        }

        // A name in the apps folder, by folder name, then by the name in its Info.xml.
        var bare = target.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ? target[..^extension.Length] : target;
        foreach (var listing in Available())
        {
            if (listing.Folder.Equals(bare + extension, StringComparison.OrdinalIgnoreCase))
                return $"{appsDirectory}/{listing.Folder}";
        }

        foreach (var listing in Available())
        {
            if (listing.Bundle is { } bundle && bundle.Name.Equals(bare, StringComparison.OrdinalIgnoreCase))
                return bundle.Path;
        }

        return null;
    }

    private RunningApp? FindOpen(string target)
    {
        var extension = Bundle.Extension(BundleKind.App);
        foreach (var app in running)
        {
            var folder = Path.GetFileName(app.Bundle.Path);
            if (app.Bundle.Identifier.Equals(target, StringComparison.OrdinalIgnoreCase)
                || app.Bundle.Name.Equals(target, StringComparison.OrdinalIgnoreCase)
                || folder.Equals(target, StringComparison.OrdinalIgnoreCase)
                || folder.Equals(target + extension, StringComparison.OrdinalIgnoreCase))
                return app;
        }

        return null;
    }
}
