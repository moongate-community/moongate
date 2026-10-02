using System.Runtime.Versioning;
using Moongate.Tests.TestSupport.Scripts;

namespace Moongate.Tests.Integration.Scripts;

// Every fact here is a [ShellFact], which skips unless this is Linux with the tools the script
// needs. Saying so out loud keeps the platform analyser from flagging the Unix-only file checks.
[SupportedOSPlatform("linux")]
public class InstallScriptTests
{
    [ShellFact]
    public async Task Install_WithMgctl_LinksExecutableBesideServer()
    {
        using var install = new ScriptedInstall();
        install.Publish("0.6.0", "linux-x64", "server", true);
        var result = await install.RunAsync("0.6.0", "linux-x64");
        Assert.True(result.ExitCode == 0, result.Output);
        var command = Path.Combine(install.BinDirectory, "mgctl");
        Assert.Equal(Path.Combine(install.InstallDirectory, "mgctl"), File.ResolveLinkTarget(command, true)!.FullName);
        Assert.True(File.GetUnixFileMode(command).HasFlag(UnixFileMode.UserExecute));
        Assert.Equal("boot payload", await File.ReadAllTextAsync(command));
    }

    [ShellFact]
    public async Task Install_DowngradeWithoutMgctl_RemovesOwnedSymlink()
    {
        using var install = new ScriptedInstall();
        install.Publish("0.6.0", "linux-x64", "new server", true);
        install.Publish("0.5.0", "linux-x64", "old server");
        Assert.Equal(0, (await install.RunAsync("0.6.0", "linux-x64")).ExitCode);
        var result = await install.RunAsync("0.5.0", "linux-x64");
        Assert.True(result.ExitCode == 0, result.Output);
        Assert.Null(new FileInfo(Path.Combine(install.BinDirectory, "mgctl")).LinkTarget);
    }

    [ShellFact]
    public async Task Install_AnOlderReleaseWithMgboot_LinksIt()
    {
        using var install = new ScriptedInstall();
        install.Publish("0.11.0", "linux-x64", "server", true, "mgboot");

        var result = await install.RunAsync("0.11.0", "linux-x64");

        Assert.True(result.ExitCode == 0, result.Output);
        var command = Path.Combine(install.BinDirectory, "mgboot");
        Assert.Equal(Path.Combine(install.InstallDirectory, "mgboot"), File.ResolveLinkTarget(command, true)!.FullName);
        Assert.Contains("mgboot /srv/moongate", result.Output);
    }

    [ShellFact]
    public async Task Install_UpgradeFromMgbootToMgctl_ReplacesTheLink()
    {
        using var install = new ScriptedInstall();
        install.Publish("0.11.0", "linux-x64", "old server", true, "mgboot");
        install.Publish("0.12.0", "linux-x64", "new server", true);
        Assert.Equal(0, (await install.RunAsync("0.11.0", "linux-x64")).ExitCode);

        var result = await install.RunAsync("0.12.0", "linux-x64");

        Assert.True(result.ExitCode == 0, result.Output);
        Assert.Null(new FileInfo(Path.Combine(install.BinDirectory, "mgboot")).LinkTarget);
        Assert.Equal(
            Path.Combine(install.InstallDirectory, "mgctl"),
            File.ResolveLinkTarget(Path.Combine(install.BinDirectory, "mgctl"), true)!.FullName
        );
        Assert.Contains("mgctl init /srv/moongate", result.Output);
    }

    [ShellFact]
    public async Task Install_DowngradeFromMgctlToMgboot_ReplacesTheLink()
    {
        using var install = new ScriptedInstall();
        install.Publish("0.12.0", "linux-x64", "new server", true);
        install.Publish("0.11.0", "linux-x64", "old server", true, "mgboot");
        Assert.Equal(0, (await install.RunAsync("0.12.0", "linux-x64")).ExitCode);

        var result = await install.RunAsync("0.11.0", "linux-x64");

        Assert.True(result.ExitCode == 0, result.Output);
        Assert.Null(new FileInfo(Path.Combine(install.BinDirectory, "mgctl")).LinkTarget);
        Assert.Equal(
            Path.Combine(install.InstallDirectory, "mgboot"),
            File.ResolveLinkTarget(Path.Combine(install.BinDirectory, "mgboot"), true)!.FullName
        );
    }

    [ShellFact]
    public async Task Install_WithMgctl_InstallsItsCompletionsWhereTheShellsLook()
    {
        using var install = new ScriptedInstall();
        // A stand-in mgctl that prints what it was asked for.
        install.Publish("0.12.0", "linux-x64", "server", true, toolContent: "#!/bin/sh\necho \"script of $1 $2\"\n");

        var result = await install.RunAsync("0.12.0", "linux-x64");

        Assert.True(result.ExitCode == 0, result.Output);
        Assert.Equal("script of completion bash\n", File.ReadAllText(Path.Combine(install.CompletionDirectory, "bash", "mgctl")));
        Assert.Equal("script of completion zsh\n", File.ReadAllText(Path.Combine(install.CompletionDirectory, "zsh", "_mgctl")));
        Assert.Equal("script of completion fish\n", File.ReadAllText(Path.Combine(install.CompletionDirectory, "fish", "mgctl.fish")));
        Assert.Contains("  completion ", result.Output);
    }

    [ShellFact]
    public async Task Install_DowngradeToAReleaseWithoutCompletions_RemovesTheScripts()
    {
        using var install = new ScriptedInstall();
        install.Publish("0.12.0", "linux-x64", "new server", true, toolContent: "#!/bin/sh\necho \"script of $1 $2\"\n");
        install.Publish("0.11.0", "linux-x64", "old server", true, "mgboot");
        Assert.Equal(0, (await install.RunAsync("0.12.0", "linux-x64")).ExitCode);
        Assert.NotEmpty(Directory.EnumerateFiles(install.CompletionDirectory, "*", SearchOption.AllDirectories));

        var result = await install.RunAsync("0.11.0", "linux-x64");

        Assert.True(result.ExitCode == 0, result.Output);
        Assert.Empty(Directory.EnumerateFiles(install.CompletionDirectory, "*", SearchOption.AllDirectories));
    }

    [ShellFact]
    public async Task Install_AMgctlWithoutCompletions_StillInstalls_AndWritesNoScript()
    {
        using var install = new ScriptedInstall();
        // Not a program: asking it for a script fails.
        install.Publish("0.12.0", "linux-x64", "server", true);

        var result = await install.RunAsync("0.12.0", "linux-x64");

        Assert.True(result.ExitCode == 0, result.Output);
        Assert.Empty(Directory.EnumerateFiles(install.CompletionDirectory, "*", SearchOption.AllDirectories));
    }

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
    public async Task AVersionWithNoMatchingAsset_IsReported()
    {
        using var install = new ScriptedInstall();
        install.Publish("0.4.0", "linux-x64", "first payload");

        var result = await install.RunAsync("9.9.9", "linux-x64");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("release v9.9.9 has no asset for linux-x64", result.Output, StringComparison.Ordinal);
        Assert.False(Directory.Exists(install.InstallDirectory));
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
    public async Task RunningItAgain_ReplacesTheInstallation_AndLeavesNoStagingDirectories()
    {
        using var install = new ScriptedInstall();
        install.Publish("0.4.0", "linux-x64", "first payload");
        install.Publish("0.5.0", "linux-x64", "second payload");
        Assert.Equal(0, (await install.RunAsync("0.4.0", "linux-x64")).ExitCode);

        var result = await install.RunAsync("0.5.0", "linux-x64");

        Assert.True(result.ExitCode == 0, result.Output);
        Assert.Equal(
            "second payload",
            await File.ReadAllTextAsync(Path.Combine(install.InstallDirectory, "Moongate.Server"))
        );
        var siblings = Directory.GetDirectories(Path.GetDirectoryName(install.InstallDirectory)!);
        Assert.DoesNotContain(siblings, directory => directory.Contains(".new.") || directory.Contains(".old."));
    }
}
