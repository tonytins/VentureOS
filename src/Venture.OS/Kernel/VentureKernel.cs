using Venture.OS.Bundles;
using Venture.OS.Desktop;
using Venture.OS.Scripting;
using Venture.OS.Shell;
using Venture.OS.Shell.Commands;
using Venture.OS.Startup;

namespace Venture.OS.Kernel;

/// <summary>
/// The VentureOS kernel: runs the boot steps, then the shell loop.
/// </summary>
public class VentureKernel : Sys.Kernel, IShellHost
{
    // Boot finishes faster than its messages can be read, so each step pauses before the screen clears.
    private static readonly TimeSpan BootStepDelay = TimeSpan.FromSeconds(0.5);

    // What Lua scripts read through the venture table, shared by the shell and init.lua.
    private readonly KernelSystemInfo system = new();

    private CommandShell? shell;

    // The shell's state, including its current folder, which the prompt shows.
    private ShellContext? shellContext;

    // Commands registered while the system runs, such as by init.lua; set up with the shell.
    private RuntimeCommands? runtimeCommands;

    // Opens, closes, and lists apps; set up with the shell.
    private AppManager? appManager;

    // The desktop's windows, kept for the whole boot, so apps opened from the shell show theirs in the desktop.
    private readonly WindowManager windows = new(1024, 768);

    // Where apps write while the desktop shows, and their event handlers always; passed to the shell otherwise.
    private readonly HeldOutput desktopOutput = new();

    // init.lua's interpreter and what it set up, kept for the whole boot so its commands keep working.
    private LuaOwner? initOwner;

    protected override void BeforeRun()
    {
        KernelLog.WriteLine($"Starting {HostInfo.DisplayVersion}...");
        KernelLog.WriteLine("Initializing kernel...");
        Thread.Sleep(BootStepDelay);

        RunCriticalStep("filesystem", Bootstrap.BootIntoRam);
        RunOptionalStep("system files", Bootstrap.InstallSystemFiles);
        RunCriticalStep("time manager", Bootstrap.CheckTimeManager);
        RunOptionalStep("network", Bootstrap.ConnectToNetwork);
        RunCriticalStep("shell", CreateShell);

        Console.Clear();

        KernelLog.WriteLine($"{HostInfo.DisplayVersion} booted successfully!");

        // After the banner, so the screen clear above doesn't hide what the script prints.
        RunOptionalStep("init script", () => initOwner = Bootstrap.RunInitScript(system, runtimeCommands, RunShellLine));
    }

    // Early initialization
    protected override void OnBoot()
    {
        base.OnBoot();   // keep the KernelConsole setup; drop this line to boot headless

    }

    /// <summary>
    /// Reads and runs one command. Once boot has finished, a failing command reports its error and
    /// returns to the prompt instead of stopping the system.
    /// </summary>
    protected override void Run()
    {
        try
        {
            Console.Write($"{shellContext?.CurrentDirectory}> ");
            var input = Console.ReadLine();

            if (string.IsNullOrEmpty(input))
                return;

            // The shell step is critical, so boot panics before reaching here without a shell.
            var current = shell ?? throw new InvalidOperationException("The shell wasn't set up during boot.");
            current.Execute(input);
        }
        catch (Exception exception)
        {
            // The shell loop is the post-boot boundary, so it catches everything to keep the system up.
            KernelLog.WriteLine($"Command failed ({exception.GetType().Name}): {exception.Message}");
        }
    }

    /// <summary>
    /// Registers the built-in commands, then the Lua command scripts in <c>/mnt/system/commands</c>. Fails only on a
    /// programming error, such as two commands with one name.
    /// </summary>
    /// <remarks>
    /// Built-in commands win any name clash, and the built-ins alone keep the shell usable if the system files
    /// didn't install.
    /// </remarks>
    private bool CreateShell()
    {
        CommandRegistry commands = new();
        commands.Register(new HelpCommand());
        commands.Register(new ClearCommand());
        commands.Register(new HaltCommand());
        commands.Register(new RebootCommand());
        commands.Register(new ShutdownCommand());
        commands.Register(new LuaCommand(system, () => runtimeCommands, RunShellLine));
        commands.Register(new CdCommand());
        commands.Register(new OpenCommand(() => appManager));
        commands.Register(new CloseCommand(() => appManager));
        commands.Register(new AppsCommand(() => appManager));
        commands.Register(new DesktopCommand(() => appManager, windows, desktopOutput));

        ScriptCommands scripts = new(
            Bootstrap.CommandsDirectoryPath,
            system,
            KeyboardStop.Check,
            Bootstrap.LibraryDirectoryPath);

        // Runtime commands come before the scripts, so the lua prompt can replace one until it exits.
        runtimeCommands = new RuntimeCommands(commands.IsBuiltIn, scripts, KeyboardStop.Check);
        commands.AddSource(runtimeCommands);
        commands.AddSource(scripts);

        appManager = new AppManager(
            Bootstrap.AppsDirectoryPath,
            runtimeCommands,
            system,
            Bootstrap.LibraryDirectoryPath,
            RunShellLine,
            () => ScriptStop.Any(KeyboardStop.Check, ScriptStop.After(Bootstrap.AppStartupLimit)),
            new AppWindows(windows, desktopOutput, () => ScriptStop.After(Bootstrap.UiEventLimit)));

        shellContext = new ShellContext(new ConsoleShellOutput(), this, commands)
        {
            CurrentDirectory = Bootstrap.RamMountPoint,
        };
        shell = new CommandShell(shellContext);
        desktopOutput.PassTo = shellContext.Output;
        return true;
    }

    /// <summary>
    /// Runs a line as if typed at the prompt, for <c>venture.shell</c> and the Lua prompt's <c>!</c>.
    /// </summary>
    /// <remarks>Exceptions reach the caller, which reports them; the shell loop's catch is outside this.</remarks>
    private void RunShellLine(string line)
    {
        var current = shell ?? throw new InvalidOperationException("The shell isn't set up yet.");
        current.Execute(line);
    }

    void IShellHost.Halt()
    {
        KernelLog.WriteLine("Halting system...");
        Stop();
    }

    void IShellHost.Reboot()
    {
        KernelLog.WriteLine("Rebooting...");
        Sys.Power.Reboot();
    }

    void IShellHost.Shutdown()
    {
        KernelLog.WriteLine("Shutting down...");
        Sys.Power.Shutdown();
    }

    /// <summary>
    /// Runs a boot step the system can't work without. A failure or exception panics the kernel.
    /// </summary>
    private static void RunCriticalStep(string name, Func<bool> step)
    {
        try
        {
            if (!step())
                KernelPanic.Halt(name, "The step reported a failure; see the messages above.");
        }
        catch (Exception exception)
        {
            KernelPanic.Halt(name, exception);
        }

        Thread.Sleep(BootStepDelay);
    }

    /// <summary>
    /// Runs a boot step the system can work without. An exception is reported and boot continues.
    /// </summary>
    private static void RunOptionalStep(string name, Action step)
    {
        try
        {
            step();
        }
        catch (Exception exception)
        {
            KernelLog.WriteLine(
                $"Boot step \"{name}\" failed ({exception.GetType().Name}): {exception.Message} Continuing without it.");
        }

        Thread.Sleep(BootStepDelay);
    }
}
