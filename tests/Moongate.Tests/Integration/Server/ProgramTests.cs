using System.Diagnostics;
using System.Globalization;
using Moongate.Server.Bootstrap;
using Moongate.Server.Bootstrap.Internal;
using Moongate.Tests.TestSupport.Directories;

namespace Moongate.Tests.Integration.Server;

public sealed class ProgramTests
{
    [Fact]
    public async Task Startup_AnotherProcessHoldsLock_ExitsEvenWithMissingPid()
    {
        using var directory = new TemporaryDirectory();
        using var guard = PidFileGuard.Acquire(directory.Path);
        var pidPath = Path.Combine(directory.Path, "moongate.pid");
        File.Delete(pidPath);

        var (exitCode, output) = await RunServerAsync(directory.Path);

        Assert.Equal(1, exitCode);
        Assert.Contains("already be running or starting", output, StringComparison.Ordinal);
        Assert.False(File.Exists(pidPath));
        Assert.False(Directory.Exists(Path.Combine(directory.Path, "config")));
        Assert.False(Directory.Exists(Path.Combine(directory.Path, "save")));
    }

    [Fact]
    public async Task Startup_InvalidConfiguration_RemovesPidAndReleasesLock()
    {
        using var directory = new TemporaryDirectory();
        directory.CreateFile("config/moongate.toml", "[invalid");

        var (exitCode, output) = await RunServerAsync(directory.Path);

        Assert.NotEqual(0, exitCode);
        Assert.Contains("Moongate Server starting", output, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(directory.Path, "moongate.pid.lock")));
        Assert.False(File.Exists(Path.Combine(directory.Path, "moongate.pid")));
        using var guard = PidFileGuard.Acquire(directory.Path);
    }

    [Fact]
    public async Task Startup_LivePid_ExitsBeforeCreatingConfigurationOrServices()
    {
        using var directory = new TemporaryDirectory();
        var content = Environment.ProcessId.ToString(CultureInfo.InvariantCulture);
        var pidPath = directory.CreateFile("moongate.pid", content);

        var (exitCode, output) = await RunServerAsync(directory.Path);

        Assert.Equal(1, exitCode);
        Assert.Contains("already running", output, StringComparison.Ordinal);
        Assert.Contains(content, output, StringComparison.Ordinal);
        Assert.Equal(content, File.ReadAllText(pidPath));
        Assert.False(Directory.Exists(Path.Combine(directory.Path, "config")));
        Assert.False(Directory.Exists(Path.Combine(directory.Path, "save")));
    }

    private static async Task<(int ExitCode, string Output)> RunServerAsync(string root)
    {
        using var process = Process.Start(
            new ProcessStartInfo
            {
                FileName = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
                ArgumentList =
                {
                    typeof(MoongateServerBootstrap).Assembly.Location,
                    "--root-directory", root
                },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        )!;
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
