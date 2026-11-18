using SkiaSharp;

namespace Venture.Roundhouse.Drawing;

/// <summary>VentureOS's two faces, read from the font files it ships, which Roundhouse carries beside itself.</summary>
public sealed class SkiaFonts : IDisposable
{
    private SkiaFonts(SKTypeface interfaceFace, SKTypeface monospaceFace)
    {
        Interface = interfaceFace;
        Monospace = monospaceFace;
    }

    /// <summary>Open Sans, for menus, titles, and controls.</summary>
    public SKTypeface Interface { get; }

    /// <summary>Fira Code, for Terminal and columns.</summary>
    public SKTypeface Monospace { get; }

    /// <summary>Reads the fonts from a folder holding <c>OpenSans-Regular.ttf</c> and <c>FiraCode-Regular.ttf</c>.</summary>
    /// <exception cref="FileNotFoundException">A font isn't there, or can't be read as one.</exception>
    public static SkiaFonts Load(string folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        return new SkiaFonts(Face(folder, "OpenSans-Regular.ttf"), Face(folder, "FiraCode-Regular.ttf"));
    }

    /// <summary>The fonts Roundhouse carries, in the <c>fonts</c> folder beside it.</summary>
    public static SkiaFonts LoadShipped() => Load(Path.Combine(AppContext.BaseDirectory, "fonts"));

    public void Dispose()
    {
        Interface.Dispose();
        Monospace.Dispose();
    }

    private static SKTypeface Face(string folder, string file)
    {
        var path = Path.Combine(folder, file);
        return File.Exists(path) && SKTypeface.FromFile(path) is { } face
            ? face
            : throw new FileNotFoundException($"VentureOS's font {file} isn't in {folder}, or can't be read.", path);
    }
}
