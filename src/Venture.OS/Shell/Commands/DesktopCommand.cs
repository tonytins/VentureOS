using Venture.OS.Bundles;
using Venture.OS.Desktop;
using Venture.OS.Interface;
using Venture.OS.Startup;

namespace Venture.OS.Shell.Commands;

/// <summary>
/// Opens the desktop: graphics on the whole screen, until the user goes back to the shell.
/// </summary>
/// <remarks>
/// <para>Files is the desktop's own app: the desktop starts it, which opens a Files window, and keeps it running.
/// Any <c>.uix</c> files named on the command line open as windows too, which previews an interface while it's
/// being written. The dock opens the system's apps; what they write while
/// the desktop is showing is held and shown in a window, since the console would draw over the desktop, and goes to
/// the shell again once the desktop closes.</para>
/// <para>The text console draws on the same canvas, so the desktop draws over it, and leaving clears the console,
/// which redraws the shell. Earlier shell output isn't kept. Whatever happens inside, the console is restored before
/// this returns, so a failure in the desktop never leaves the screen in graphics with no way back. The frame loop
/// redraws only when the input changed.</para>
/// </remarks>
/// <param name="apps">The system's apps, for the dock.</param>
/// <param name="windows">The system's windows, which apps' windows stay in between visits.</param>
/// <param name="appOutput">Where apps write while the desktop shows; held then, and passed to the shell after.</param>
internal sealed class DesktopCommand(Func<AppManager?> apps, WindowManager windows, HeldOutput appOutput) : IShellCommand
{
    /// <summary>The app that is the desktop, as Finder is on a Mac.</summary>
    private const string FilesApp = "Files";

    public string Name => "desktop";
    public string Summary => "Open the desktop, with any .uix files as windows. Ctrl+Shift+Q comes back.";
    public string Usage => "desktop [interface.uix...]";
    public int MinArguments => 0;
    public int MaxArguments => int.MaxValue;

    public void Execute(ShellContext context, IReadOnlyList<string> arguments)
    {
        // A shell in a window, such as Terminal's, is already inside the desktop.
        if (!context.HasConsole)
        {
            context.Output.WriteLine("desktop: you're already in the desktop.");
            return;
        }

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
        appOutput.PassTo = null;
        DesktopSession? session = null;
        try
        {
            var opening = Stopwatch.GetTimestamp();
            session = new(canvas.Width, canvas.Height, apps(), context.CurrentDirectory, appOutput, windows)
            {
                AboutDocument = LoadAbout,
                SystemVersion = $"Version {HostInfo.Version} (Build {HostInfo.BuildNumber})",
                FallbackApp = FilesApp,
            };
            CanvasPainter painter = new(canvas, interfaceFont, monospaceFont);
            session.StartFallbackApp();
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

                DesktopInput input = new(
                    MouseManager.X, MouseManager.Y, MouseManager.LeftButton, scroll, ReadKeys(), Stopwatch.GetElapsedTime(opening));
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
            Leave(session, context.Output);
            return;
        }

        Console.Clear();
        KernelLog.WriteSerialLine($"Desktop closed after {frames} frames.");
        Leave(session, context.Output);
    }

    /// <summary>
    /// Closes the desktop's own windows, keeping apps' for next time, shows anything apps said that the desktop
    /// didn't, and sends what they say next to the shell.
    /// </summary>
    private void Leave(DesktopSession? session, IShellOutput output)
    {
        session?.CloseDesktopWindows();
        foreach (var line in appOutput.Take())
            output.WriteLine(line);
        appOutput.PassTo = output;
    }

    /// <summary>Makes About VentureOS, with the version filled in; its OK button closes it.</summary>
    /// <summary>Reads the About window's document, which About VentureOS and every app's About share.</summary>
    private static UIScript? LoadAbout()
    {
        try
        {
            return UIScript.Load(Bootstrap.AboutInterfacePath);
        }
        catch (Exception exception) when (exception is UIScriptException or IOException)
        {
            // The desktop works without it; say why it's missing on serial, since the screen is in graphics.
            KernelLog.WriteSerialLine($"The About window couldn't open: {exception.Message}");
            return null;
        }
    }

    /// <summary>Takes every waiting key, as the desktop sees them.</summary>
    /// <remarks>
    /// The frame loop calls this on every pass, so with no key waiting it allocates nothing; the list exists only once
    /// a key arrives.
    /// </remarks>
    private static KeyPress[] ReadKeys()
    {
        List<KeyPress>? keys = null;
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

            keys ??= [];
            if (special != DesktopKey.Character)
                keys.Add(new KeyPress(special, '\0', control, shift, alt));
            else if ((control || alt) && Letter(key.Key) is { } letter)
                keys.Add(new KeyPress(DesktopKey.Character, letter, control, shift, alt)); // For shortcuts and hotkeys.
            else if (key.KeyChar != '\0')
                keys.Add(new KeyPress(DesktopKey.Character, key.KeyChar, control, shift, alt));
        }

        return keys is null || keys.Count == 0 ? [] : [.. keys];
    }

    /// <summary>
    /// The letter or digit a key is labeled with. With Ctrl or Alt held, the character a key types may be a control
    /// code or a symbol, so shortcuts and menu hotkeys go by the key itself.
    /// </summary>
    private static char? Letter(Key key) => key switch
    {
        Key.A => 'a', Key.B => 'b', Key.C => 'c', Key.D => 'd', Key.E => 'e', Key.F => 'f', Key.G => 'g',
        Key.H => 'h', Key.I => 'i', Key.J => 'j', Key.K => 'k', Key.L => 'l', Key.M => 'm', Key.N => 'n',
        Key.O => 'o', Key.P => 'p', Key.Q => 'q', Key.R => 'r', Key.S => 's', Key.T => 't', Key.U => 'u',
        Key.V => 'v', Key.W => 'w', Key.X => 'x', Key.Y => 'y', Key.Z => 'z',
        Key.D0 => '0', Key.D1 => '1', Key.D2 => '2', Key.D3 => '3', Key.D4 => '4',
        Key.D5 => '5', Key.D6 => '6', Key.D7 => '7', Key.D8 => '8', Key.D9 => '9',
        _ => null,
    };

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
