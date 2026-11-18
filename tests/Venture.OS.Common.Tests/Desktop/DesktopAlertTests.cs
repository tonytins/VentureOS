using Venture.OS.Desktop;
using Venture.OS.Interface;

namespace Venture.OS.Tests.Desktop;

public sealed class DesktopAlertTests
{
    [Fact]
    public void OkClosesAnAlert()
    {
        var alert = DesktopAlert.Create("Note", "Saved.");

        alert.EventHandler!(alert, new UIEvent(UIEventKind.Pressed, "ok"));

        Assert.True(alert.IsCloseRequested);
        Assert.Equal("Saved.", alert.View.Text("message"));
    }

    [Fact]
    public void AQuestionShowsItsButtonsAndHasNoCloseButton()
    {
        var question = DesktopAlert.Ask("Trust This Computer?", "It wants to <install> apps.", "Trust", "Deny", _ => { });

        Assert.False(question.IsClosable);
        Assert.Equal("Trust", question.View.Text("yes"));
        Assert.Equal("Deny", question.View.Text("no"));
        Assert.Equal("It wants to <install> apps.", question.View.Text("message"));
    }

    [Theory]
    [InlineData("yes", true)]
    [InlineData("no", false)]
    public void AQuestionAnswersOnceAndCloses(string button, bool expected)
    {
        List<bool> answers = [];
        var question = DesktopAlert.Ask("Trust This Computer?", "?", "Trust", "Deny", answers.Add);

        question.EventHandler!(question, new UIEvent(UIEventKind.Pressed, button));
        question.EventHandler!(question, new UIEvent(UIEventKind.Pressed, "yes"));

        Assert.Equal([expected], answers);
        Assert.True(question.IsCloseRequested);
    }
}

public sealed class HeldOutputTests
{
    [Fact]
    public void EchoesEveryLineWhetherHeldOrPassedOn()
    {
        List<string> echoed = [];
        Shell.RecordingOutput shell = new();
        HeldOutput output = new() { Echo = echoed.Add };

        output.WriteLine("held");
        output.PassTo = shell;
        output.WriteLine("passed");

        Assert.Equal(["held", "passed"], echoed);
        Assert.Equal(["held"], output.Take());
        Assert.Equal(["passed"], shell.Lines);
    }
}
