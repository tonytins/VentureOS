namespace Venture.OS.Scripting;

/// <summary>
/// The desktop's look as scripts see it, through <c>venture.appearance</c> and <c>venture.setAppearance</c>. The
/// desktop implements it; scripting only knows this shape, so it doesn't depend on the desktop.
/// </summary>
public interface IAppearance
{
    /// <summary>Whether the look showing is dark, which for <c>auto</c> depends on the time.</summary>
    bool IsDark { get; }

    /// <summary>The mode as chosen: <c>light</c>, <c>dark</c>, or <c>auto</c>.</summary>
    string Mode { get; }

    /// <summary>The accent as chosen, written <c>#RRGGBB</c>.</summary>
    string Accent { get; }

    /// <summary>Whether windows have rounded corners.</summary>
    bool RoundedCorners { get; }

    /// <summary>How the dock behaves: <c>always</c>, <c>autohide</c>, or <c>dodge</c>.</summary>
    string Dock { get; }

    /// <summary>How lists and text areas show that they scroll: <c>classic</c> or <c>automatic</c>.</summary>
    string ScrollBars { get; }

    /// <summary>The wallpaper's name, such as <c>Earthset</c>, or <c>sky</c>.</summary>
    string Wallpaper { get; }

    /// <summary>When <c>auto</c> turns dark, written <c>HH:MM</c>.</summary>
    string DarkFrom { get; }

    /// <summary>When <c>auto</c> turns light again, written <c>HH:MM</c>.</summary>
    string DarkUntil { get; }

    /// <summary>The wallpapers there are to choose from, by name, sorted, without the sky.</summary>
    IReadOnlyList<WallpaperChoice> Wallpapers();

    /// <summary>Changes what the request names, keeps the rest, and saves the look.</summary>
    AppearanceChange Set(AppearanceRequest request);
}

/// <summary>A wallpaper to choose from.</summary>
/// <param name="Name">What the look keeps, such as <c>Earthset</c>.</param>
/// <param name="Title">What people see.</param>
/// <param name="Path">The full path of its picture, which a grid scales down to show it.</param>
public sealed record WallpaperChoice(string Name, string Title, string Path);

/// <summary>
/// A change to the look: each part that isn't <see langword="null"/> changes, as scripts write it.
/// </summary>
/// <param name="Mode"><c>light</c>, <c>dark</c>, or <c>auto</c>.</param>
/// <param name="Accent">Written <c>#RRGGBB</c>.</param>
/// <param name="Corners"><c>rounded</c> or <c>square</c>.</param>
/// <param name="Wallpaper">A wallpaper's name, or <c>sky</c>.</param>
/// <param name="DarkFrom">When <c>auto</c> turns dark, written <c>HH:MM</c>.</param>
/// <param name="DarkUntil">When it turns light again, written <c>HH:MM</c>.</param>
/// <param name="Dock"><c>always</c>, <c>autohide</c>, or <c>dodge</c>.</param>
/// <param name="ScrollBars"><c>classic</c> or <c>automatic</c>.</param>
public sealed record AppearanceRequest(
    string? Mode = null, string? Accent = null, string? Corners = null, string? Wallpaper = null,
    string? DarkFrom = null, string? DarkUntil = null, string? Dock = null, string? ScrollBars = null);

/// <summary>
/// What <see cref="IAppearance.Set"/> did: <see cref="Refused"/> says why nothing changed, and otherwise the look
/// changed, with <see cref="Unsaved"/> saying why it couldn't be saved when that failed.
/// </summary>
public readonly record struct AppearanceChange(string? Refused, string? Unsaved);
