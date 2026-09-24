using System.Diagnostics;

namespace Moongate.Tests.TestSupport.Boot;

internal static class BootProcess
{
    public static async Task<(int ExitCode, string Output)> RunAsync(params string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "mgboot.dll"));

        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
        start.Environment["MOONGATE_SERVER_EXECUTABLE"] = Path.Combine(
            AppContext.BaseDirectory,
            OperatingSystem.IsWindows() ? "Moongate.Server.exe" : "Moongate.Server"
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
