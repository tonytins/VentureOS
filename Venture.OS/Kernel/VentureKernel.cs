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

    private CommandShell? shell;

    protected override void BeforeRun()
    {
        KernelLog.WriteLine($"Starting {HostInfo.DisplayVersion}...");
        KernelLog.WriteLine("Initializing kernel...");
        Thread.Sleep(BootStepDelay);

        RunCriticalStep("filesystem", Bootstrap.BootIntoRam);
        RunCriticalStep("time manager", Bootstrap.CheckTimeManager);
        RunOptionalStep("network", Bootstrap.ConnectToNetwork);
        RunCriticalStep("shell", CreateShell);

        Console.Clear();

        KernelLog.WriteLine($"{HostInfo.DisplayVersion} booted successfully!");
        Console.WriteLine("Type \"help\" for a list of commands.");
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
            Console.Write("> ");
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
    /// Registers the shell's commands. Fails only on a programming error, such as two commands with one name.
    /// </summary>
    private bool CreateShell()
    {
        CommandRegistry commands = new();
        commands.Register(new HelpCommand());
        commands.Register(new ClearCommand());
        commands.Register(new InfoCommand());
        commands.Register(new NetCommand());
        commands.Register(new HaltCommand());
        commands.Register(new RebootCommand());
        commands.Register(new ShutdownCommand());

        shell = new CommandShell(new ShellContext(new ConsoleShellOutput(), this, commands));
        return true;
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
