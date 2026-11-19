using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Venture.Roundhouse.Vdb;

namespace Venture.Roundhouse.Tests;

/// <summary>The window a run on VentureOS shows: what Roundhouse says along the top, what the app prints below.</summary>
[Collection(nameof(PaletteCollection))]
public sealed class VentureOSRunWindowTests
{
    [Fact]
    public void ShowsStatusInTheThemesColorProblemsInRedAndOutputBelow() => TestApp.Run(() =>
    {
        VentureOSRunWindow run = new("Dice.app", (report, _) => Task.Run(() =>
        {
            report(new VentureOSRunLine("Nothing answered.", IsProblem: true));
            report(new VentureOSRunLine("Opened it."));
            report(new VentureOSRunLine("Rolled a 4", IsOutput: true));
            report(new VentureOSRunLine("Rolled a 6", IsOutput: true));
        }));
        run.Show();
        Assert.True(run.Running.Wait(TimeSpan.FromSeconds(10)));
        Dispatcher.UIThread.RunJobs();

        var status = run.GetLogicalDescendants().OfType<TextBlock>().Single(block => block.Text == "Opened it.");
        Assert.Equal("Opened it.", run.Status);
        Assert.NotNull(status.Foreground);
        Assert.NotEqual(Brushes.IndianRed, status.Foreground);
        Assert.Equal("Rolled a 4\nRolled a 6", run.Output);
        Assert.False(run.GetLogicalDescendants().OfType<Button>().Single().IsEnabled);
        run.Close();
    });
}
