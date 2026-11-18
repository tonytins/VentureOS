using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Venture.OS.Desktop;
using Venture.Roundhouse.Drawing;
using Venture.OS.Bundles;
using Venture.OS.Scripting;

namespace Venture.Roundhouse.Bundles;

/// <summary>
/// A form for a bundle's <c>Info.xml</c>: its name and identifier, version, and description; check boxes for the
/// scopes an app needs; and its main script, icon, or picture chosen from the bundle's own files, with the icon or
/// picture shown. Each change goes to <see cref="Commit"/>, which edits the text in place or gives VentureOS's
/// reason for refusing it, shown under the field.
/// </summary>
public sealed class InfoForm : StackPanel
{
    /// <summary>The icon's size in the form: 64, as the app guide asks icons to be made, larger than the dock's 48.</summary>
    public const int IconSize = 64;

    private static readonly ScriptScopes[] Scopes = [ScriptScopes.System, ScriptScopes.Network, ScriptScopes.Shell, ScriptScopes.Files];

    private readonly Dictionary<string, TextBlock> problems = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Control> fields = new(StringComparer.Ordinal);
    private bool updating;

    // The document shown, which every field reads its value from when it's changed, and what the form was built for.
    private InfoDocument? current;
    private (string Root, BundleKind Kind, string Files, string? Icon, string? Image)? builtFor;

    public InfoForm()
    {
        Spacing = 4;
        Margin = new(24, 16);
        MaxWidth = 560;
        HorizontalAlignment = HorizontalAlignment.Left;
    }

    /// <summary>Commits an attribute's new value, or removes it for <see langword="null"/>, giving the reason it's refused, or <see langword="null"/>.</summary>
    public Func<string, string?, string?>? Commit { get; set; }

    /// <summary>The fields, by attribute: text boxes, the needs' check boxes (as <c>needs.shell</c> and so on), and choices.</summary>
    public IReadOnlyDictionary<string, Control> Fields => fields;

    /// <summary>Why an attribute's last change was refused, or <see langword="null"/>.</summary>
    public string? Problem(string attribute) => problems.TryGetValue(attribute, out var block) && block.IsVisible ? block.Text : null;

    /// <summary>
    /// Shows a document, with the bundle's files to choose a main script and icon from, by their paths inside it,
    /// such as <c>Contents/Lua/main.lua</c>. Showing the same bundle again, with the same files, only updates the
    /// values, so the field being typed in keeps its place.
    /// </summary>
    public void Show(InfoDocument document, IReadOnlyList<string> files)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(files);
        current = document;
        // A new icon or picture is drawn anew; anything else only changes the fields' values.
        var shape = (document.Root, document.Kind, string.Join('\n', files), document["icon"], document["image"]);
        if (builtFor == shape)
        {
            Update(document);
            return;
        }

        builtFor = shape;
        Children.Clear();
        fields.Clear();
        problems.Clear();
        updating = true;

        Children.Add(new TextBlock { Text = $"{Path.GetFileName(document.Root)} — Info.xml", FontWeight = FontWeight.SemiBold, FontSize = 15 });

        Heading("Identity");
        TextField(document, "name", "Name", required: true);
        TextField(document, "identifier", "Identifier", required: true);
        TextField(document, "version", "Version");
        TextField(document, "description", "Description");

        var pictures = files.Where(file => file.EndsWith(".png", StringComparison.OrdinalIgnoreCase)).ToList();
        if (document.Kind == BundleKind.App)
        {
            Heading("Needs");
            Row("needs", "Scopes", Needs(document));
            Heading("Files");
            Choice(document, "main", "Main script", $"({Bundle.DefaultMain})",
                [.. files.Where(file => file.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))]);
        }
        else
        {
            Heading("Files");
        }

        Choice(document, "icon", "Icon", "(none)", pictures);
        if (document.Kind == BundleKind.Wallpaper)
            Choice(document, "image", "Picture", "(none)", pictures);

        Preview(document);
        updating = false;
    }

    /// <summary>
    /// Shows why VentureOS refuses the <c>Info.xml</c> in place of the form, while its text, in the Text tab, is fixed.
    /// </summary>
    public void ShowProblem(string problem)
    {
        ArgumentNullException.ThrowIfNull(problem);
        Children.Clear();
        fields.Clear();
        problems.Clear();
        (current, builtFor) = (null, null);
        Children.Add(new TextBlock { Text = "VentureOS can't read this Info.xml", FontWeight = FontWeight.SemiBold, FontSize = 15 });
        Children.Add(new TextBlock { Text = problem, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.IndianRed, Margin = new(0, 8, 0, 0) });
        Children.Add(new TextBlock
        {
            Text = "Fix it in the Text tab below, and the form comes back once VentureOS reads it.",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.7,
            Margin = new(0, 8, 0, 0),
        });
    }

    /// <summary>The problem shown in place of the form, or <see langword="null"/> while the form shows.</summary>
    public string? Refusal => current is null && Children.Count > 1 && Children[1] is TextBlock { Text: var text } ? text : null;

    /// <summary>Puts a document's values in the fields, leaving a focused text box as it's being typed.</summary>
    private void Update(InfoDocument document)
    {
        updating = true;
        var needs = document.Bundle?.Needs ?? ScriptScopes.None;
        foreach (var (attribute, field) in fields)
        {
            switch (field)
            {
                case TextBox box when !box.IsFocused:
                    box.Text = document[attribute] ?? "";
                    break;
                case CheckBox box when attribute.StartsWith("needs.", StringComparison.Ordinal):
                    box.IsChecked = Scopes.Any(scope => ScriptScopeNames.Name(scope) == attribute["needs.".Length..] && needs.HasFlag(scope));
                    break;
                case ComboBox box:
                    box.SelectedItem = document[attribute] is { } written && ((IList<string>)box.ItemsSource!).Contains(written) ? written : box.Items[0];
                    break;
            }
        }

        updating = false;
    }

    private void Heading(string text) =>
        Children.Add(new TextBlock { Text = text, FontWeight = FontWeight.SemiBold, Opacity = 0.7, Margin = new(0, 12, 0, 2) });

    private void TextField(InfoDocument document, string attribute, string label, bool required = false)
    {
        TextBox box = new() { Text = document[attribute] ?? "", Height = 26, MinHeight = 0, FontSize = 12, Padding = new(6, 0), VerticalContentAlignment = VerticalAlignment.Center };
        void Send()
        {
            var typed = box.Text ?? "";
            if (typed != (current?[attribute] ?? ""))
                Report(attribute, Commit?.Invoke(attribute, typed.Length == 0 ? null : typed));
        }

        box.LostFocus += (_, _) => Send();
        box.KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Enter)
            {
                Send();
                e.Handled = true;
            }
        };
        Row(attribute, required ? $"{label} *" : label, box);
    }

    private WrapPanel Needs(InfoDocument document)
    {
        var needs = document.Bundle?.Needs ?? ScriptScopes.None;
        WrapPanel boxes = new() { Orientation = Orientation.Horizontal };
        foreach (var scope in Scopes)
        {
            var name = ScriptScopeNames.Name(scope);
            CheckBox box = new() { Content = name, IsChecked = needs.HasFlag(scope), Margin = new(0, 0, 12, 0) };
            box.IsCheckedChanged += (_, _) =>
            {
                if (updating)
                    return;

                // Written in the order VentureOS lists the scopes, which reads the same each time.
                var now = current?.Bundle?.Needs ?? ScriptScopes.None;
                var chosen = Scopes.Where(other => other == scope ? box.IsChecked == true : now.HasFlag(other))
                    .Select(ScriptScopeNames.Name).ToList();
                Report("needs", Commit?.Invoke("needs", chosen.Count == 0 ? null : string.Join(' ', chosen)));
            };
            fields[$"needs.{name}"] = box;
            boxes.Children.Add(box);
        }

        return boxes;
    }

    private void Choice(InfoDocument document, string attribute, string label, string unset, IReadOnlyList<string> files)
    {
        var written = document[attribute];
        List<string> items = [unset, .. files];
        if (written is not null && !items.Contains(written))
            items.Add(written);

        ComboBox box = new()
        {
            ItemsSource = items,
            SelectedItem = written ?? unset,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Height = 26,
            MinHeight = 0,
            FontSize = 12,
            Padding = new(6, 0),
        };
        box.SelectionChanged += (_, _) =>
        {
            if (updating)
                return;
            var chosen = box.SelectedIndex <= 0 ? null : (string)box.SelectedItem!;
            if (chosen != current?[attribute])
                Report(attribute, Commit?.Invoke(attribute, chosen));
        };
        Row(attribute, label, box);
    }

    /// <summary>
    /// Shows the bundle's picture, or else its icon as VentureOS's dock makes it, from <see cref="DesktopIcon"/>: the
    /// key colour cleared and a tile's corners rounded.
    /// </summary>
    private void Preview(InfoDocument document)
    {
        var (picture, isIcon) = document.Bundle?.Image is { } image ? (image, false) : (document.Bundle?.Icon, true);
        if (picture is null || !File.Exists(picture))
            return;

        string? problem = null;
        if (SkiaImages.Load(picture, reason => problem = reason) is not { } loaded)
        {
            Children.Add(new TextBlock { Text = problem ?? $"{Path.GetFileName(picture)} couldn't be shown.", Opacity = 0.6, Margin = new(118, 8, 0, 0) });
            return;
        }

        var shown = isIcon ? DesktopIcon.From(loaded, IconSize).Image : loaded;
        WriteableBitmap bitmap = new(new PixelSize(shown.Width, shown.Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
        using (var locked = bitmap.Lock())
        {
            for (var row = 0; row < shown.Height; row++)
                Marshal.Copy(shown.Pixels, row * shown.Width, locked.Address + (row * locked.RowBytes), shown.Width);
        }

        Children.Add(new Image
        {
            Source = bitmap,

            // An icon at its own size; a wallpaper's picture shrunk to fit.
            Stretch = isIcon ? Stretch.None : Stretch.Uniform,
            MaxWidth = 256,
            MaxHeight = 256,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new(118, 8, 0, 0),
        });
    }

    private void Row(string attribute, string label, Control field)
    {
        fields[attribute] = field;
        TextBlock problem = new() { Foreground = Brushes.IndianRed, TextWrapping = TextWrapping.Wrap, FontSize = 11, IsVisible = false };
        problems[attribute] = problem;

        Grid row = new() { ColumnDefinitions = new("110,*"), RowDefinitions = new("Auto,Auto") };
        row.Children.Add(new TextBlock { Text = label, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(field, 1);
        row.Children.Add(field);
        Grid.SetRow(problem, 1);
        Grid.SetColumn(problem, 1);
        row.Children.Add(problem);
        Children.Add(row);
    }

    private void Report(string attribute, string? reason)
    {
        if (!problems.TryGetValue(attribute, out var block))
            return;
        block.Text = reason;
        block.IsVisible = reason is not null;
    }
}
