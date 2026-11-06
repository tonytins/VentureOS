namespace Venture.OS.Scripting;

/// <summary>
/// A file built into the kernel image, to be written out to the disk at boot.
/// </summary>
/// <param name="Path">
/// Where the file goes, relative to the directory it's installed in, with <c>/</c> between folders, such as
/// <c>commands/echo.lua</c>.
/// </param>
/// <param name="Contents">The file's bytes, written as they are.</param>
public sealed record EmbeddedFile(string Path, byte[] Contents);
