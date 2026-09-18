using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Moongate.Server.Bootstrap.Internal;

/// <summary>
/// Last resort that terminates the process when the runtime fails to exit after the graceful
/// shutdown sequence has already completed. The guard runs on background threads, so it never
/// keeps the process alive and never fires when the process exits on its own.
/// </summary>
internal static class ProcessShutdownGuard
{
    private const int GracePeriodMilliseconds = 3000;
    private const int ForcedExitGraceMilliseconds = 2000;
    private const int StandardInputDescriptor = 0;
    private const int SetAttributesNow = 0;
    private const int TerminalStateBufferSize = 128;

    private static byte[]? _terminalState;

    /// <summary>
    /// Snapshots the terminal settings before any console reader switches the terminal to raw mode,
    /// so a forced kill can hand a usable terminal back to the shell.
    /// </summary>
    public static void CaptureTerminalState()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS() && !OperatingSystem.IsFreeBSD())
        {
            return;
        }

        if (System.Console.IsInputRedirected)
        {
            return;
        }

        var state = new byte[TerminalStateBufferSize];

        try
        {
            if (GetTerminalAttributes(StandardInputDescriptor, state) == 0)
            {
                _terminalState = state;
            }
        }
        catch (Exception)
        {
            // No usable terminal: there is nothing to restore later.
        }
    }

    /// <summary>
    /// Arms the guard once the graceful shutdown has finished. A process that exits on its own is
    /// gone long before the grace period elapses.
    /// </summary>
    public static void ArmForcedExit()
    {
        StartGuardThread("Moongate Forced Exit Guard", ForceExitAfterGracePeriod);
    }

    private static void ForceExitAfterGracePeriod()
    {
        Thread.Sleep(GracePeriodMilliseconds);
        RestoreTerminal();
        System.Console.Error.WriteLine(
            "Moongate shutdown completed but the process is still running; forcing termination."
        );
        StartGuardThread("Moongate Forced Kill Guard", KillAfterForcedExitGrace);
        Environment.Exit(Environment.ExitCode);
    }

    private static void KillAfterForcedExitGrace()
    {
        Thread.Sleep(ForcedExitGraceMilliseconds);

        using var current = Process.GetCurrentProcess();

        current.Kill();
    }

    private static void StartGuardThread(string name, ThreadStart work)
    {
        var thread = new Thread(work)
        {
            IsBackground = true,
            Name = name
        };

        thread.Start();
    }

    private static void RestoreTerminal()
    {
        var state = _terminalState;

        if (state is not null)
        {
            try
            {
                SetTerminalAttributes(StandardInputDescriptor, SetAttributesNow, state);
            }
            catch (Exception)
            {
                // The terminal is gone; the forced exit still has to happen.
            }
        }

        try
        {
            System.Console.CursorVisible = true;
        }
        catch (Exception)
        {
            // Cursor visibility is best effort.
        }
    }

    [DllImport("libc", EntryPoint = "tcgetattr", SetLastError = true)]
    private static extern int GetTerminalAttributes(int fileDescriptor, byte[] terminalState);

    [DllImport("libc", EntryPoint = "tcsetattr", SetLastError = true)]
    private static extern int SetTerminalAttributes(int fileDescriptor, int actions, byte[] terminalState);
}
