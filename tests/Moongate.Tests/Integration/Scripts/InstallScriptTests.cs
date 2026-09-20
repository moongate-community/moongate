using Moongate.Tests.TestSupport.Scripts;

namespace Moongate.Tests.Integration.Scripts;

public class InstallScriptTests
{
    [ShellFact]
    public async Task ACleanInstall_PlacesTheArchiveContents_AndLinksTheCommand()
    {
        using var install = new ScriptedInstall();
        install.Publish("0.4.0", "linux-x64", "first payload");

        var result = await install.RunAsync("0.4.0", "linux-x64");

        Assert.True(result.ExitCode == 0, result.Output);
        var binary = Path.Combine(install.InstallDirectory, "Moongate.Server");
        Assert.Equal("first payload", await File.ReadAllTextAsync(binary));
        Assert.True(File.Exists(Path.Combine(install.InstallDirectory, "LICENSE")));
        var command = Path.Combine(install.BinDirectory, "moongate");
        Assert.Equal(binary, File.ResolveLinkTarget(command, true)!.FullName);
        Assert.True(File.GetUnixFileMode(command).HasFlag(UnixFileMode.UserExecute));
    }

    [ShellFact]
    public async Task RunningItAgain_ReplacesTheInstallation_AndLeavesNoStagingDirectories()
    {
        using var install = new ScriptedInstall();
        install.Publish("0.4.0", "linux-x64", "first payload");
        install.Publish("0.5.0", "linux-x64", "second payload");
        Assert.Equal(0, (await install.RunAsync("0.4.0", "linux-x64")).ExitCode);

        var result = await install.RunAsync("0.5.0", "linux-x64");

        Assert.True(result.ExitCode == 0, result.Output);
        Assert.Equal("second payload", await File.ReadAllTextAsync(Path.Combine(install.InstallDirectory, "Moongate.Server")));
        var siblings = Directory.GetDirectories(Path.GetDirectoryName(install.InstallDirectory)!);
        Assert.DoesNotContain(siblings, directory => directory.Contains(".new.") || directory.Contains(".old."));
    }

    [ShellFact]
    public async Task ATamperedArchive_FailsTheChecksum_AndInstallsNothing()
    {
        using var install = new ScriptedInstall();
        install.Publish("0.4.0", "linux-x64", "first payload");
        install.Corrupt("0.4.0", "linux-x64");

        var result = await install.RunAsync("0.4.0", "linux-x64");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("checksum mismatch for moongate-linux-x64-0.4.0.tar.gz", result.Output, StringComparison.Ordinal);
        Assert.False(Directory.Exists(install.InstallDirectory));
        Assert.False(File.Exists(Path.Combine(install.BinDirectory, "moongate")));
    }

    [ShellFact]
    public async Task AnArchitectureTheReleasesDoNotShip_IsRefusedBeforeDownloading()
    {
        using var install = new ScriptedInstall();
        install.Publish("0.4.0", "linux-x64", "first payload");

        var result = await install.RunAsync("0.4.0", "linux-riscv64");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("unsupported architecture 'linux-riscv64'", result.Output, StringComparison.Ordinal);
        Assert.False(Directory.Exists(install.InstallDirectory));
    }

    [ShellFact]
    public async Task AVersionWithNoMatchingAsset_IsReported()
    {
        using var install = new ScriptedInstall();
        install.Publish("0.4.0", "linux-x64", "first payload");

        var result = await install.RunAsync("9.9.9", "linux-x64");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("release v9.9.9 has no asset for linux-x64", result.Output, StringComparison.Ordinal);
        Assert.False(Directory.Exists(install.InstallDirectory));
    }
}
