using Venture.OS.Desktop;
using Venture.OS.Startup;

namespace Venture.OS.Shell.Commands;

/// <summary>
/// Opens the desktop: graphics on the whole screen, until the user goes back to the shell.
/// </summary>
/// <remarks>
/// <para>The text console draws on the same canvas, so the desktop draws over it, and leaving clears the console,
/// which redraws the shell. Earlier shell output isn't kept.</para>
/// <para>Whatever happens inside, the console is restored before this returns, so a failure in the desktop never
/// leaves the screen in graphics with no way back. The frame loop redraws only when the input changed.</para>
/// </remarks>
internal sealed class DesktopCommand : IShellCommand
{
    public string Name => "desktop";
    public string Summary => "Open the desktop.";
    public string Usage => "desktop";
    public int MinArguments => 0;
    public int MaxArguments => 0;

    public void Execute(ShellContext context, IReadOnlyList<string> arguments)
    {
        if (!TryLoadFonts(context.Output, out var interfaceFont, out var monospaceFont))
            return;

        Canvas canvas;
        try
        {
            canvas = Canvas.GetFullScreen();
        }
        catch (InvalidOperationException exception)
        {
            context.Output.WriteLine($"desktop: there's no screen to draw on: {exception.Message}");
            return;
        }

        var frames = 0;
        try
        {
            var opening = Stopwatch.GetTimestamp();
            DesktopSession session = new(canvas.Width, canvas.Height);
            CanvasPainter painter = new(canvas, interfaceFont, monospaceFont);
            MouseManager.SetScreenSize(canvas.Width, canvas.Height);
            MouseManager.SetPosition(canvas.Width / 2, canvas.Height / 2);
            KernelLog.WriteSerialLine(
                $"Desktop opened at {canvas.Width}x{canvas.Height} in {Stopwatch.GetElapsedTime(opening).TotalMilliseconds:0} ms.");

            while (!session.IsFinished)
            {
                DesktopInput input = new(MouseManager.X, MouseManager.Y, MouseManager.LeftButton, EscapePressed());
                if (!session.Update(input))
                    continue;

                session.Draw(painter);
                canvas.Display();
                frames++;
            }
        }
        catch (Exception exception)
        {
            // Back to text first, so the report can be read.
            Console.Clear();
            KernelLog.WriteLine($"The desktop stopped ({exception.GetType().Name}): {exception.Message}");
            return;
        }

        Console.Clear();
        KernelLog.WriteSerialLine($"Desktop closed after {frames} frames.");
    }

    /// <summary>Takes every waiting key, and says whether Esc was pressed among them.</summary>
    private static bool EscapePressed()
    {
        var escape = false;
        while (KeyboardManager.TryReadKey(out var key))
        {
            if (key.Type == KeyEvent.KeyEventType.Make && key.Key == Key.Escape)
                escape = true;
        }

        return escape;
    }

    private static bool TryLoadFonts(IShellOutput output, out TrueTypeFont interfaceFont, out TrueTypeFont monospaceFont)
    {
        interfaceFont = null!;
        monospaceFont = null!;
        try
        {
            interfaceFont = new TrueTypeFont(File.ReadAllBytes(Bootstrap.InterfaceFontPath));
            monospaceFont = new TrueTypeFont(File.ReadAllBytes(Bootstrap.MonospaceFontPath));
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            output.WriteLine($"desktop: the fonts in {Bootstrap.FontsDirectoryPath} couldn't be read: {exception.Message}");
            return false;
        }
    }
}
