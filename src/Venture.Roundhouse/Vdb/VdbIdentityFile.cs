using System.Security.Cryptography;
using Venture.OS.Cryptography;
using Venture.OS.Vdb;

namespace Venture.Roundhouse.Vdb;

/// <summary>
/// This computer's vdb identity, in the same file the <c>vdb</c> tool keeps it in, so VentureOS trusts Roundhouse
/// and the tool as one computer: <c>vdb/identity</c> in <c>%APPDATA%</c> on Windows, and in <c>$XDG_CONFIG_HOME</c>,
/// or <c>~/.config</c>, elsewhere. It's a secret every connection is keyed from, so it's readable only by its owner.
/// </summary>
public static class VdbIdentityFile
{
    /// <summary>Where the identity is kept, for this system and environment.</summary>
    public static string DefaultPath(Func<string, string?>? environment = null)
    {
        environment ??= Environment.GetEnvironmentVariable;
        string folder;
        if (OperatingSystem.IsWindows())
        {
            folder = environment("APPDATA") is { Length: > 0 } appData
                ? appData
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Roaming");
        }
        else
        {
            folder = environment("XDG_CONFIG_HOME") is { Length: > 0 } config
                ? config
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        }

        return Path.Combine(folder, "vdb", "identity");
    }

    /// <summary>Reads the identity from <paramref name="path"/>, making and saving one first if there's none.</summary>
    /// <exception cref="IOException">The file couldn't be read or written, or doesn't hold an identity.</exception>
    public static string Load(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (File.Exists(path))
        {
            var text = File.ReadAllText(path).Trim();
            return VdbTrust.IsIdentity(text)
                ? text
                : throw new IOException($"{path} doesn't hold a vdb identity. Delete it, and a new one is made, which VentureOS will ask to trust.");
        }

        var identity = Hex.Encode(RandomNumberGenerator.GetBytes(16));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (OperatingSystem.IsWindows())
        {
            File.WriteAllText(path, identity + "\n");
        }
        else
        {
            using FileStream file = new(path, new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite,
            });
            file.Write(System.Text.Encoding.ASCII.GetBytes(identity + "\n"));
        }

        return identity;
    }
}
