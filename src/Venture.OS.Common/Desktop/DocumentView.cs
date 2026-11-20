using Venture.OS.Interface;
using Venture.OS.Interface.Strings;
using Venture.OS.Scripting;

namespace Venture.OS.Desktop;

/// <summary>
/// A <c>.uix</c> document ready to draw: read, with the text for its <c>&lt;String&gt;</c>s from the CST tables beside
/// it and the pictures for its <c>&lt;Image&gt;</c>s from the files their paths name, beside it. An app's windows
/// open this way, and so does Roundhouse's preview, so both show a document the same.
/// </summary>
public static class DocumentView
{
    /// <summary>Reads a document and what it names, saying what's missing through <paramref name="report"/>.</summary>
    /// <param name="loadImage">Reads a picture file, or gives <see langword="null"/>; without it, images stay outlined.</param>
    /// <param name="report">Hears each problem: a string table, a string, or an image that couldn't be had.</param>
    /// <param name="sharedStrings">The folder of tables every app shares, such as <c>/mnt/system/strings</c>.</param>
    /// <exception cref="UIScriptException">The document isn't one VentureOS reads.</exception>
    /// <exception cref="IOException">The file couldn't be read.</exception>
    public static UIView Open(
        string path, Func<string, DesktopImage?>? loadImage, Action<string> report, string? sharedStrings = null)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(report);
        return Open(UIScript.Load(path), Path.GetDirectoryName(path) ?? "/", loadImage, report, sharedStrings);
    }

    /// <summary>
    /// Makes a view of a document already read, such as text being edited, with its strings and images from
    /// <paramref name="folder"/>, and strings from the shared tables in <paramref name="sharedStrings"/> too.
    /// </summary>
    public static UIView Open(
        UIScript script, string folder, Func<string, DesktopImage?>? loadImage, Action<string> report, string? sharedStrings = null)
    {
        ArgumentNullException.ThrowIfNull(folder);
        return Open(script, folder, new StringTables(folder, sharedStrings), loadImage, report);
    }

    /// <summary>
    /// Makes a view of a document already read with its strings from <paramref name="tables"/>, which the caller keeps,
    /// such as for an app's <c>window:string</c>, and its images from <paramref name="folder"/>.
    /// </summary>
    public static UIView Open(
        UIScript script, string folder, StringTables tables, Func<string, DesktopImage?>? loadImage, Action<string> report)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(report);

        UIView view = new(script, tables.Find);
        foreach (var problem in tables.Problems)
            report(problem);
        foreach (var missing in view.MissingStrings)
            report($"no text for the string \"{missing}\" in its tables.");

        if (loadImage is null)
            return view;

        foreach (var image in script.Images.Values)
        {
            // An image named only by asset ID is TSO's, which VentureOS can't look up.
            if (image.Path is not { } relative)
                continue;

            if (!SystemDirectory.IsValidPath(relative))
                report($"the image \"{image.Name}\" has path=\"{relative}\", which isn't a path beside the document.");
            else if (loadImage($"{folder.TrimEnd('/')}/{relative}") is { } picture)
                view.SetImage(image.Name, picture);
            else
                report($"the image \"{image.Name}\" couldn't be read from {relative}.");
        }

        return view;
    }
}
