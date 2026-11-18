using Venture.OS.Desktop;
using Venture.OS.Interface;
using Venture.Roundhouse.Designer;

namespace Venture.Roundhouse.Tests;

/// <summary><see cref="FrameDrag"/> and <see cref="FrameEdit"/>: what a drag does to a frame, and how it's written.</summary>
public sealed class FrameDragTests
{
    private static readonly UIRect Frame = new(100, 50, 40, 20);

    [Theory]
    [InlineData(100, 50, Handle.TopLeft)]
    [InlineData(120, 50, Handle.Top)]
    [InlineData(142, 72, Handle.BottomRight)]
    [InlineData(98, 60, Handle.Left)]
    [InlineData(120, 60, Handle.None)]
    [InlineData(200, 200, Handle.None)]
    public void AHandleIsFoundNearItsCentre(float x, float y, Handle expected)
    {
        Assert.Equal(expected, FrameDrag.HandleAt(Frame, x, y));
    }

    [Fact]
    public void DraggingTheFrameMovesIt()
    {
        Assert.Equal(new FrameChange(5, -3, 0, 0), FrameDrag.Change(Frame, Handle.None, 5, -3));
    }

    [Fact]
    public void ARightOrBottomHandleGrowsAndALeftOrTopOneMovesTheEdge()
    {
        Assert.Equal(new FrameChange(0, 0, 6, 4), FrameDrag.Change(Frame, Handle.BottomRight, 6, 4));
        Assert.Equal(new FrameChange(6, 4, -6, -4), FrameDrag.Change(Frame, Handle.TopLeft, 6, 4));
        Assert.Equal(new FrameChange(0, 0, 6, 0), FrameDrag.Change(Frame, Handle.Right, 6, 4));
        Assert.Equal(new FrameChange(0, 4, 0, -4), FrameDrag.Change(Frame, Handle.Top, 6, 4));
    }

    [Fact]
    public void AFrameNeverGetsSmallerThanAPixel()
    {
        Assert.Equal(new UIRect(100, 50, 1, 1), FrameDrag.Change(Frame, Handle.BottomRight, -100, -100).ApplyTo(Frame));
        Assert.Equal(new UIRect(139, 69, 1, 1), FrameDrag.Change(Frame, Handle.TopLeft, 100, 100).ApplyTo(Frame));
    }

    [Fact]
    public void AChangeWritesOnlyTheAttributesItChanges()
    {
        var document = UIDocument.Read("""<UIScript><Window width="200" height="100"/><Region name="r" x="10" y="12.5" width="40" height="20"/></UIScript>""");

        var moved = FrameEdit.Apply(document, 1, Frame, new FrameChange(3, 1, 0, 0));
        var grown = FrameEdit.Apply(document, 1, Frame, new FrameChange(0, 0, 5, 0));

        Assert.Equal("""<UIScript><Window width="200" height="100"/><Region name="r" x="13" y="13.5" width="40" height="20"/></UIScript>""", moved.Text);
        Assert.Equal("""<UIScript><Window width="200" height="100"/><Region name="r" x="10" y="12.5" width="45" height="20"/></UIScript>""", grown.Text);
        Assert.Same(document, FrameEdit.Apply(document, 1, Frame, default));
    }

    [Fact]
    public void ASizeTheElementDoesntWriteStartsFromHowItWasDrawn()
    {
        var document = UIDocument.Read("""<UIScript><Window width="200" height="100"/><Image name="i" path="i.png"/><Button name="b" x="0" y="0" image="i"/></UIScript>""");

        var grown = FrameEdit.Apply(document, 2, new UIRect(0, 0, 24, 24), new FrameChange(0, 0, 8, 0));

        Assert.Contains("""<Button name="b" x="0" y="0" image="i" width="32"/>""", grown.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void OnlyControlsAndVisualsHaveFrames()
    {
        var document = UIDocument.Read("""<UIScript><Window width="200" height="100"/><Style name="s"/></UIScript>""");

        // The window grows and shrinks but doesn't move; a style has no frame at all.
        Assert.Same(document, FrameEdit.Apply(document, 0, Frame, new FrameChange(1, 0, 0, 0)));
        Assert.Throws<ArgumentException>(() => FrameEdit.Apply(document, 1, Frame, new FrameChange(1, 0, 0, 0)));
        Assert.True(FrameEdit.IsFramed("Visual"));
        Assert.True(FrameEdit.IsFramed("Slider"));
        Assert.False(FrameEdit.IsFramed("Window"));
    }

    [Theory]
    [InlineData(12f, "12")]
    [InlineData(-3f, "-3")]
    [InlineData(12.5f, "12.5")]
    [InlineData(0.125f, "0.125")]
    public void NumbersAreWrittenAsADocumentWouldWriteThem(float value, string expected)
    {
        Assert.Equal(expected, FrameEdit.Write(value));
    }
}
