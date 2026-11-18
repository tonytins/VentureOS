using Venture.OS.Desktop;
using Venture.OS.Interface;

namespace Venture.OS.Tests.Desktop;

/// <summary>Drawing on a frame of pixels, which the kernel's painter and Roundhouse's share.</summary>
public sealed class FrameBufferTests
{
    private const int White = unchecked((int)0xFFFFFFFF);
    private const int Red = unchecked((int)0xFFFF0000);

    [Fact]
    public void AnOpaqueFillCoversAndIsCutToTheFrame()
    {
        var frame = Enumerable.Repeat(White, 9).ToArray();

        FrameBuffer.Fill(frame, 3, 3, 1, 1, 5, 5, new UIColor(255, 0, 0));

        Assert.Equal([White, White, White, White, Red, Red, White, Red, Red], frame);
    }

    [Fact]
    public void ATranslucentFillBlendsAsTheKernelsPainterDid()
    {
        int[] frame = [White];

        FrameBuffer.Fill(frame, 1, 1, 0, 0, 1, 1, new UIColor(0, 0, 0, 60));

        // (255 * 195) / 255 = 195 for each channel.
        Assert.Equal(DesktopImage.Pack(255, 195, 195, 195), frame[0]);
    }

    [Fact]
    public void AClearFillOrAnEmptyRectangleDoesNothing()
    {
        int[] frame = [White];

        FrameBuffer.Fill(frame, 1, 1, 0, 0, 1, 1, new UIColor(0, 0, 0, 0));
        FrameBuffer.Fill(frame, 1, 1, 0, 0, 0, 1, new UIColor(0, 0, 0));

        Assert.Equal(White, frame[0]);
    }

    [Fact]
    public void CopiesABlockCutToTheFrame()
    {
        var frame = Enumerable.Repeat(White, 4).ToArray();

        FrameBuffer.Copy(frame, 2, 2, [Red, Red, Red, Red], -1, -1, 2, 2);

        Assert.Equal([Red, White, White, White], frame);
    }

    [Fact]
    public void ReadsBackWithPixelsOffTheFrameAsZero()
    {
        int[] frame = [Red, White, White, White];
        var into = new int[4];

        FrameBuffer.Read(frame, 2, 2, into, -1, 0, 2, 2);

        Assert.Equal([0, Red, 0, White], into);
    }

    [Fact]
    public void RefusesAFrameOrBlockSmallerThanItsSize()
    {
        Assert.Throws<ArgumentException>(() => FrameBuffer.Fill(new int[3], 2, 2, 0, 0, 1, 1, new UIColor(0, 0, 0)));
        Assert.Throws<ArgumentException>(() => FrameBuffer.Copy(new int[4], 2, 2, new int[3], 0, 0, 2, 2));
        Assert.Throws<ArgumentException>(() => FrameBuffer.Read(new int[4], 2, 2, new int[3], 0, 0, 2, 2));
    }
}
