using Venture.OS.Desktop;
using Venture.OS.Interface;
using Venture.OS.Startup;

namespace Venture.OS.Shell.Commands;

/// <summary>
/// Opens the desktop: graphics on the whole screen, until the user goes back to the shell.
/// </summary>
/// <remarks>
/// <para>It opens with the About VentureOS window. Any <c>.uix</c> files named on the command line open as windows
/// too, which previews an interface while it's being written.</para>
/// <para>The text console draws on the same canvas, so the desktop draws over it, and leaving clears the console,
/// which redraws the shell. Earlier shell output isn't kept. Whatever happens inside, the console is restored before
/// this returns, so a failure in the desktop never leaves the screen in graphics with no way back. The frame loop
/// redraws only when the input changed.</para>
/// </remarks>
internal sealed class DesktopCommand : IShellCommand
{
    public string Name => "desktop";
    public string Summary => "Open the desktop, with any .uix files as windows.";
    public string Usage => "desktop [interface.uix...]";
    public int MinArguments => 0;
    public int MaxArguments => int.MaxValue;

    public void Execute(ShellContext context, IReadOnlyList<string> arguments)
    {
        // Read every document first, so a mistake is reported in the shell, where it can be read.
        List<(string Title, UIScript Script)> previews = [];
        foreach (var argument in arguments)
        {
            var path = ShellPaths.Resolve(context.CurrentDirectory, argument);
            try
            {
                previews.Add((Path.GetFileName(path), UIScript.Load(path)));
            }
            catch (Exception exception) when (exception is UIScriptException or IOException or UnauthorizedAccessException)
            {
                context.Output.WriteLine($"desktop: {argument}: {exception.Message}");
                return;
            }
        }

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
            OpenAbout(session);
            foreach (var (title, script) in previews)
                session.Windows.Open(new DesktopWindow(title, new UIView(script)));

            MouseManager.SetScreenSize(canvas.Width, canvas.Height);
            MouseManager.SetPosition(canvas.Width / 2, canvas.Height / 2);
            MouseManager.ResetScrollDelta();
            KernelLog.WriteSerialLine(
                $"Desktop opened at {canvas.Width}x{canvas.Height} in {Stopwatch.GetElapsedTime(opening).TotalMilliseconds:0} ms.");

            while (!session.IsFinished)
            {
                var scroll = MouseManager.ScrollDelta;
                if (scroll != 0)
                    MouseManager.ResetScrollDelta();

                DesktopInput input = new(MouseManager.X, MouseManager.Y, MouseManager.LeftButton, scroll, ReadKeys());
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

    /// <summary>Opens About VentureOS, with the version filled in; its OK button closes it.</summary>
    private static void OpenAbout(DesktopSession session)
    {
        UIScript script;
        try
        {
            script = UIScript.Load(Bootstrap.AboutInterfacePath);
        }
        catch (Exception exception) when (exception is UIScriptException or IOException)
        {
            // The desktop works without it; say why it's missing on serial, since the screen is in graphics.
            KernelLog.WriteSerialLine($"About VentureOS couldn't open: {exception.Message}");
            return;
        }

        UIView view = new(script);
        view.SetText("version", $"Version {HostInfo.Version} (Build {HostInfo.BuildNumber})");
        DesktopWindow window = new("About VentureOS", view)
        {
            EventHandler = (about, uiEvent) =>
            {
                if (uiEvent is { Kind: UIEventKind.Pressed, Control: "ok" })
                    session.Windows.Close(about);
            },
        };
        session.Windows.Open(window);
    }

    /// <summary>Takes every waiting key, as the desktop sees them.</summary>
    private static KeyPress[] ReadKeys()
    {
        List<KeyPress> keys = [];
        while (KeyboardManager.TryReadKey(out var key))
        {
            if (key.Type != KeyEvent.KeyEventType.Make)
                continue;

            var control = key.Modifiers.HasFlag(ConsoleModifiers.Control);
            var shift = key.Modifiers.HasFlag(ConsoleModifiers.Shift);
            var alt = key.Modifiers.HasFlag(ConsoleModifiers.Alt);
            var special = key.Key switch
            {
                Key.Backspace => DesktopKey.Backspace,
                Key.Delete => DesktopKey.Delete,
                Key.Enter => DesktopKey.Enter,
                Key.Escape => DesktopKey.Escape,
                Key.Tab => DesktopKey.Tab,
                Key.LeftArrow => DesktopKey.Left,
                Key.RightArrow => DesktopKey.Right,
                Key.UpArrow => DesktopKey.Up,
                Key.DownArrow => DesktopKey.Down,
                Key.Home => DesktopKey.Home,
                Key.End => DesktopKey.End,
                _ => DesktopKey.Character,
            };

            if (special != DesktopKey.Character)
                keys.Add(new KeyPress(special, '\0', control, shift, alt));
            else if (key.KeyChar != '\0')
                keys.Add(new KeyPress(DesktopKey.Character, key.KeyChar, control, shift, alt));
        }

        return [.. keys];
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
