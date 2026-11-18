using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Venture.OS.Interface;
using Venture.OS.Interface.Xml;

namespace Venture.Roundhouse.Designer;

/// <summary>
/// The inspector: every attribute the selected element takes, grouped as <see cref="UIScriptSchema"/> groups them,
/// and any it has that VentureOS doesn't read, under Other, so nothing in the file is hidden. A change is committed
/// when the field is left or Enter is pressed; an empty field removes the attribute.
/// </summary>
public sealed class InspectorPanel : StackPanel
{
    private readonly Dictionary<string, Field> fields = new(StringComparer.Ordinal);
    private string? shownElement;
    private string[] shownOthers = [];
    private bool updating;

    public InspectorPanel() => Spacing = 2;

    /// <summary>
    /// Commits an attribute's new value, or removes it for <see langword="null"/>, and gives the reason when it's
    /// refused, or <see langword="null"/> when it's taken.
    /// </summary>
    public Func<string, string?, string?>? Commit { get; set; }

    /// <summary>The attributes shown, by name, each with its editor and the reason its last change was refused.</summary>
    public IReadOnlyDictionary<string, Field> Fields => fields;

    /// <summary>
    /// Shows an element's attributes, or nothing. Showing the same kind of element again only updates the values, so
    /// the field being typed in keeps its place.
    /// </summary>
    public void Show(FlatXmlElement? element)
    {
        if (element is null)
        {
            Children.Clear();
            fields.Clear();
            (shownElement, shownOthers) = (null, []);
            return;
        }

        var listed = UIScriptSchema.For(element.Name);
        string[] others = [.. element.Attributes.Select(pair => pair.Key).Where(name => !listed.Any(info => info.Name == name))];
        if (element.Name != shownElement || !others.SequenceEqual(shownOthers))
            Build(element.Name, listed, others);

        updating = true;
        foreach (var (name, field) in fields)
            field.Show(element[name]);
        updating = false;
    }

    private void Build(string element, IReadOnlyList<UIAttributeInfo> listed, string[] others)
    {
        Children.Clear();
        fields.Clear();
        (shownElement, shownOthers) = (element, others);

        Children.Add(new TextBlock { Text = $"<{element}>", FontWeight = FontWeight.SemiBold, Margin = new(0, 0, 0, 4) });
        foreach (var group in Enum.GetValues<UIAttributeGroup>())
        {
            var members = listed.Where(info => info.Group == group).ToList();
            if (members.Count == 0)
                continue;
            AddHeading(group.ToString());
            foreach (var info in members)
                AddField(info);
        }

        if (others.Length > 0)
        {
            AddHeading("Other");
            Children.Add(new TextBlock
            {
                Text = "VentureOS doesn't read these; they're kept as written.",
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.6,
                FontSize = 11,
            });
            foreach (var name in others)
                AddField(new UIAttributeInfo(name, UIAttributeGroup.Behaviour, UIAttributeKind.Text));
        }
    }

    private void AddHeading(string text) =>
        Children.Add(new TextBlock { Text = text, FontWeight = FontWeight.SemiBold, Opacity = 0.7, Margin = new(0, 10, 0, 2) });

    private void AddField(UIAttributeInfo info)
    {
        Field field = new(info, this);
        fields[info.Name] = field;
        Children.Add(field.Row);
    }

    private void Send(Field field, string? value)
    {
        if (updating || Commit is null)
            return;
        field.ShowProblem(Commit(field.Info.Name, value));
    }

    /// <summary>One attribute's row: its label, its editor, and why its last change was refused.</summary>
    public sealed class Field
    {
        private readonly InspectorPanel panel;
        private readonly TextBlock problem = new() { Foreground = Brushes.IndianRed, TextWrapping = TextWrapping.Wrap, FontSize = 11, IsVisible = false };
        private string? value;

        internal Field(UIAttributeInfo info, InspectorPanel panel)
        {
            Info = info;
            this.panel = panel;
            var label = new TextBlock
            {
                Text = info.IsRequired ? $"{info.Name} *" : info.Name,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            ToolTip.SetTip(label, info.IsRequired ? $"{info.Name}: required" : info.Name);

            Editor = info.Kind switch
            {
                UIAttributeKind.Boolean => MakeCheckBox(),
                UIAttributeKind.Choice => MakeChoices(info.Choices ?? []),
                _ => MakeTextBox(),
            };

            Grid row = new() { ColumnDefinitions = new("110,*"), RowDefinitions = new("Auto,Auto") };
            row.Children.Add(label);
            Grid.SetColumn(Editor, 1);
            row.Children.Add(Editor);
            Grid.SetRow(problem, 1);
            Grid.SetColumn(problem, 1);
            row.Children.Add(problem);
            Row = row;
        }

        /// <summary>What the attribute is.</summary>
        public UIAttributeInfo Info { get; }

        /// <summary>The control that edits it: a text box, a check box for true or false, or a list of its choices.</summary>
        public Control Editor { get; }

        /// <summary>Why the last change was refused, or <see langword="null"/>.</summary>
        public string? Problem => problem.IsVisible ? problem.Text : null;

        internal Control Row { get; }

        internal void Show(string? written)
        {
            value = written;
            switch (Editor)
            {
                case TextBox box when !box.IsFocused || Problem is null:
                    box.Text = written ?? "";
                    break;
                case CheckBox check:
                    check.IsChecked = written == "true";
                    break;
                case ComboBox choices:
                    choices.SelectedIndex = written is null ? 0 : Math.Max(0, ((IList<string>)choices.ItemsSource!).IndexOf(written));
                    break;
            }
        }

        internal void ShowProblem(string? reason)
        {
            problem.Text = reason;
            problem.IsVisible = reason is not null;
        }

        private TextBox MakeTextBox()
        {
            TextBox box = new() { MinHeight = 26, Padding = new(6, 2) };
            box.LostFocus += (_, _) => CommitText(box);
            box.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    CommitText(box);
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    box.Text = value ?? "";
                    ShowProblem(null);
                    e.Handled = true;
                }
            };
            return box;
        }

        private void CommitText(TextBox box)
        {
            var typed = box.Text ?? "";
            if (typed == (value ?? ""))
                return;
            panel.Send(this, typed.Length == 0 ? null : typed);
        }

        private CheckBox MakeCheckBox()
        {
            // Every flag defaults to false, so unchecking removes it rather than writing false.
            CheckBox check = new();
            check.IsCheckedChanged += (_, _) => panel.Send(this, check.IsChecked == true ? "true" : null);
            return check;
        }

        private ComboBox MakeChoices(IReadOnlyList<string> choices)
        {
            ComboBox box = new() { ItemsSource = (List<string>)["(default)", .. choices], HorizontalAlignment = HorizontalAlignment.Stretch };
            box.SelectionChanged += (_, _) =>
            {
                var chosen = box.SelectedIndex <= 0 ? null : (string)box.SelectedItem!;
                if (chosen != value)
                    panel.Send(this, chosen);
            };
            return box;
        }
    }
}
