using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Venture.Roundhouse.Bundles;

/// <summary>
/// Asks a yes-or-no question before doing something Roundhouse shouldn't do unasked, such as replacing an app that's
/// there, with buttons that say what each answer does. Closing it any other way says no.
/// </summary>
public sealed class ConfirmDialog : Window
{
    /// <param name="question">The question, in bold.</param>
    /// <param name="detail">What follows from it, or <see langword="null"/>.</param>
    /// <param name="yes">The button that goes ahead, such as "Replace".</param>
    /// <param name="no">The button that doesn't, such as "Cancel".</param>
    public ConfirmDialog(string title, string question, string? detail, string yes, string no = "Cancel")
    {
        ArgumentNullException.ThrowIfNull(question);
        Title = title;
        Width = 420;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        Button go = new() { Content = yes, IsDefault = true };
        go.Click += (_, _) => Close(true);
        Button stay = new() { Content = no, IsCancel = true };
        stay.Click += (_, _) => Close(false);

        StackPanel body = new() { Margin = new(20), Spacing = 12 };
        body.Children.Add(new TextBlock { Text = question, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
        if (detail is not null)
            body.Children.Add(new TextBlock { Text = detail, Opacity = 0.8, TextWrapping = TextWrapping.Wrap });
        body.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { stay, go } });
        Content = body;
    }

    /// <summary>Shows the question over <paramref name="owner"/>, and answers whether to go ahead.</summary>
    public static async Task<bool> AskAsync(Window owner, string title, string question, string? detail, string yes, string no = "Cancel") =>
        await new ConfirmDialog(title, question, detail, yes, no).ShowDialog<bool?>(owner) == true;
}
