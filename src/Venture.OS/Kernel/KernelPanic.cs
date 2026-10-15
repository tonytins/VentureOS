using System.Diagnostics.CodeAnalysis;

namespace Venture.OS.Kernel;

/// <summary>
/// Stops the system deliberately when it can't continue, after reporting why.
/// </summary>
internal static class KernelPanic
{
    /// <summary>
    /// Writes a panic report to the serial port and the console, then halts the CPU for good.
    /// </summary>
    /// <param name="step">The boot step or subsystem that failed.</param>
    /// <param name="reason">What went wrong, in a form the user can report.</param>
    [DoesNotReturn]
    public static void Halt(string step, string reason)
    {
        var report = $"""

                      ========================================
                      KERNEL PANIC
                      ========================================
                      {HostInfo.DisplayVersion}
                      Failed step: {step}
                      Reason:      {reason}
                      ========================================
                      System halted.
                      """;

        // KernelLog writes serial first, so the report survives even if the console is what broke.
        KernelLog.WriteLine(report);

        // Power.Halt returns when an interrupt wakes the CPU, so keep halting.
        while (true)
            Sys.Power.Halt();
    }

    /// <inheritdoc cref="Halt(string, string)"/>
    /// <param name="exception">The exception that stopped the step.</param>
    [DoesNotReturn]
    public static void Halt(string step, Exception exception) =>
        Halt(step, $"{exception.GetType().Name}: {exception.Message}");
}
