using System.Diagnostics;

namespace Moongate.Tests.TestSupport.Ctl;

/// <summary>
///     Runs a tool of the machine, such as bash, and gives its exit code and combined output; a tool still running
///     after 30 seconds is stopped.
/// </summary>
internal static class ToolProcess
{
    public static async Task<(int ExitCode, string Output)> RunAsync(string tool, params string[] arguments)
    {
        var start = new ProcessStartInfo(tool)
            { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };

        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();

        try
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));

            return (process.ExitCode, await stdout + await stderr);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(true);
                await process.WaitForExitAsync();
            }
        }
    }
}
