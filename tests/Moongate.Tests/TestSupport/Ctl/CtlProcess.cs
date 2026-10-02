using System.Diagnostics;

namespace Moongate.Tests.TestSupport.Ctl;

internal static class CtlProcess
{
    public static Task<(int ExitCode, string Output)> RunAsync(params string[] arguments)
    {
        return RunAsync(Path.Combine(AppContext.BaseDirectory, "mgctl"), null, arguments);
    }

    /// <summary>
    ///     Runs mgctl with <paramref name="workingDirectory" /> as its current directory, where a relative root lands.
    /// </summary>
    public static Task<(int ExitCode, string Output)> RunInAsync(string workingDirectory, params string[] arguments)
    {
        return RunAsync(Path.Combine(AppContext.BaseDirectory, "mgctl"), workingDirectory, arguments);
    }

    /// <summary>
    ///     Runs the mgctl of <paramref name="directory" />, a copy of the built one, without MOONGATE_ROOT, as a
    ///     distribution unpacked there would.
    /// </summary>
    public static Task<(int ExitCode, string Output)> RunFromAsync(string directory, params string[] arguments)
    {
        return RunAsync(directory, null, arguments);
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(string directory, string? workingDirectory, string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory ?? "",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        start.ArgumentList.Add(Path.Combine(directory, "mgctl.dll"));
        start.Environment.Remove("MOONGATE_ROOT");

        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment["MOONGATE_SERVER_EXECUTABLE"] = Path.Combine(
            AppContext.BaseDirectory,
            OperatingSystem.IsWindows() ? "mgserver.exe" : "mgserver"
        );
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
