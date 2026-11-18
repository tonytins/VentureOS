using System.Runtime.InteropServices;
using SkiaSharp;
using Venture.OS.Desktop;

namespace Venture.Roundhouse.Drawing;

/// <summary>
/// Reads PNG files into VentureOS's pictures with Skia, as the kernel's loader does with Cosmos's decoder, under the
/// same limits: the size in the header is checked before anything is decoded.
/// </summary>
public static class SkiaImages
{
    /// <summary>Reads a PNG file, or gives <see langword="null"/> when it can't, telling <paramref name="report"/> why.</summary>
    public static DesktopImage? Load(string path, Action<string>? report = null)
    {
        ArgumentNullException.ThrowIfNull(path);
        try
        {
            var bytes = File.ReadAllBytes(path);
            if (DesktopImage.ReadPngSize(bytes, out var problem) is not { } size)
                return Fail(path, problem ?? "it can't be read", report);

            using var decoded = SKBitmap.Decode(bytes);
            if (decoded is null)
                return Fail(path, "it can't be decoded", report);

            // Unpremultiplied BGRA is VentureOS's 0xAARRGGBB with its alpha kept apart, as DesktopImage holds it.
            var info = new SKImageInfo(size.Width, size.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
            using var converted = new SKBitmap(info);
            if (!decoded.CopyTo(converted, SKColorType.Bgra8888))
                return Fail(path, "it can't be converted", report);

            var pixels = new int[size.Width * size.Height];
            Marshal.Copy(converted.GetPixels(), pixels, 0, pixels.Length);
            return new DesktopImage(size.Width, size.Height, pixels);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Fail(path, exception.Message, report);
        }
    }

    private static DesktopImage? Fail(string path, string problem, Action<string>? report)
    {
        report?.Invoke($"The picture {path} couldn't be read: {problem.TrimEnd('.')}.");
        return null;
    }
}
