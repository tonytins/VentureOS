using Venture.OS.Interface;

namespace Venture.OS.Desktop;

/// <summary>The mouse pointer: a black-edged white arrow, with its tip at the pointer's position.</summary>
public static class Pointer
{
    // '#' is the black edge and '.' the white fill, row by row from the tip.
    private static readonly string[] Shape =
    [
        "#",
        "##",
        "#.#",
        "#..#",
        "#...#",
        "#....#",
        "#.....#",
        "#......#",
        "#.......#",
        "#........#",
        "#.....#####",
        "#..#..#",
        "#.# #..#",
        "##  #..#",
        "#    #..#",
        "     #..#",
        "      ##",
    ];

    /// <summary>The arrow's width in pixels.</summary>
    public static int Width { get; } = Widest(Shape);

    /// <summary>The arrow's height in pixels.</summary>
    public static int Height => Shape.Length;

    /// <summary>Draws the arrow with its tip at (<paramref name="x"/>, <paramref name="y"/>).</summary>
    public static void Draw(IPainter painter, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(painter);

        for (var row = 0; row < Shape.Length; row++)
        {
            var line = Shape[row];
            var start = 0;
            while (start < line.Length)
            {
                // One rectangle per run of the same mark, rather than one per pixel.
                var mark = line[start];
                var end = start;
                while (end < line.Length && line[end] == mark)
                    end++;

                if (mark != ' ')
                    painter.FillRectangle(x + start, y + row, end - start, 1, mark == '#' ? Black : White);
                start = end;
            }
        }
    }

    private static int Widest(string[] rows)
    {
        var widest = 0;
        foreach (var row in rows)
            widest = Math.Max(widest, row.Length);
        return widest;
    }

    private static readonly UIColor Black = new(0, 0, 0);
    private static readonly UIColor White = new(255, 255, 255);
}
