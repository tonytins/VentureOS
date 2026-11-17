namespace Venture.OS.Desktop;

/// <summary>Reads PNG files into pictures the desktop draws, with Cosmos's PNG decoder.</summary>
internal static class PngLoader
{
    /// <summary>
    /// Reads a PNG file, or gives <see langword="null"/> when it can't, having said why on serial, since the screen
    /// is showing the desktop.
    /// </summary>
    public static DesktopImage? Load(string path)
    {
        try
        {
            // The size in the header is checked before decoding, so the decoder never allocates for a picture the
            // desktop would turn away.
            var bytes = File.ReadAllBytes(path);
            if (DesktopImage.ReadPngSize(bytes, out var problem) is not { } size)
                return Fail(path, problem ?? "it can't be read");

            var (width, height) = size;
            Png png = new(bytes);
            if (png.Width != width || png.Height != height || png.RawData is not { } pixels)
                return Fail(path, $"its header says {width}x{height}, but it decodes as {png.Width}x{png.Height}");

            return new DesktopImage(width, height, pixels);
        }
        catch (Exception exception)
        {
            // The decoder throws whatever it meets on a damaged file; a bad picture never stops the desktop.
            return Fail(path, $"{exception.GetType().Name}: {exception.Message}");
        }
    }

    private static DesktopImage? Fail(string path, string problem)
    {
        KernelLog.WriteSerialLine($"The picture {path} couldn't be read: {problem}.");
        return null;
    }
}
