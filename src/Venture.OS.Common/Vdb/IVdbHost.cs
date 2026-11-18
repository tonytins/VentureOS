namespace Venture.OS.Vdb;

/// <summary>
/// What a vdb session needs from the system it runs on: where apps go, and the apps that are there. The kernel's
/// is backed by its app manager; tests use their own.
/// </summary>
public interface IVdbHost
{
    /// <summary>The folder sent apps are written to before they're checked, such as <c>/mnt/user/.vdb</c>.</summary>
    string StagingFolder { get; }

    /// <summary>The user's apps folder, such as <c>/mnt/user/apps</c>, where a checked app is installed.</summary>
    string AppsFolder { get; }

    /// <summary>Whether a system app, which vdb may not replace, has this identifier.</summary>
    bool IsSystemApp(string identifier);

    /// <summary>Closes the app with this identifier, if it's open, before it's replaced.</summary>
    void CloseApp(string identifier);

    /// <summary>Opens an app by its folder's name, such as <c>Dice.app</c>, as the dock would.</summary>
    /// <returns>Why it didn't open, or <see langword="null"/> when it did.</returns>
    string? Launch(string app);
}
