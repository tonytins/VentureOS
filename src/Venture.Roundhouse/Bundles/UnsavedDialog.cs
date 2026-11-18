using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Venture.Roundhouse.Bundles;

/// <summary>What to do with unsaved edits when leaving them.</summary>
public enum LeaveChoice
{
    /// <summary>Stay, keeping the edits.</summary>
    Cancel,

    /// <summary>Save every edited file, then go.</summary>
    SaveAll,

    /// <summary>Go, leaving the edits unsaved.</summary>
    DontSave,
}

/// <summary>
/// Asks what to do with unsaved edits before leaving a bundle or closing Roundhouse, naming the files, as editors
/// ask. Closing it any other way cancels.
/// </summary>
public sealed class UnsavedDialog : Window
{
    public UnsavedDialog(IReadOnlyList<string> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        Title = "Unsaved Changes";
        Width = 420;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        Button save = new() { Content = "Save All", IsDefault = true };
        save.Click += (_, _) => Close(LeaveChoice.SaveAll);
        Button discard = new() { Content = "Don't Save" };
        discard.Click += (_, _) => Close(LeaveChoice.DontSave);
        Button cancel = new() { Content = "Cancel", IsCancel = true };
        cancel.Click += (_, _) => Close(LeaveChoice.Cancel);

        Content = new StackPanel
        {
            Margin = new(20),
            Spacing = 12,
            Children =
            {
                new TextBlock
                {
                    Text = files.Count == 1 ? "This file has changes that aren't saved:" : $"These {files.Count} files have changes that aren't saved:",
                    FontWeight = FontWeight.SemiBold,
                    TextWrapping = TextWrapping.Wrap,
                },
                new TextBlock { Text = string.Join("\n", files.Select(Path.GetFileName)), Opacity = 0.8 },
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { discard, cancel, save } },
            },
        };
    }
}
