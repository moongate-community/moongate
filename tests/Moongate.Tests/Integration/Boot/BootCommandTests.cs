using Moongate.Core.Utils;
using Moongate.Server.Data.Config;
using Moongate.Tests.TestSupport.Boot;
using Moongate.Tests.TestSupport.Directories;

namespace Moongate.Tests.Integration.Boot;

public sealed class BootCommandTests
{
    [Theory, InlineData("--help"), InlineData("-h")]
    public async Task Run_Help_DescribesCertificateOptions(string flag)
    {
        var result = await BootProcess.RunAsync(flag);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("--generate-admin-certificate", result.Output);
        Assert.Contains("--admin-certificate-hosts", result.Output);
    }

    [Fact]
    public async Task Run_GenerateCertificate_PreparesRootWithSpacesAndEnablesApi()
    {
        using var directory = new TemporaryDirectory();
        var root = Path.Combine(directory.Path, "server root");
        var result = await BootProcess.RunAsync(
            root,
            "--generate-admin-certificate",
            "--admin-certificate-hosts",
            "login.example.test,192.0.2.10"
        );
        Assert.True(result.ExitCode == 0, result.Output);
        var config = TomlUtils.DeserializeFromFile<MoongateServerConfig>(Path.Combine(root, "config/moongate.toml"))!;
        Assert.True(config.AdminApi.Enabled);
        Assert.True(File.Exists(Path.Combine(root, config.AdminApi.CertificatePath)));
        Assert.True(File.Exists(Path.Combine(root, "certificates/admin.crt")));
        Assert.False(File.Exists(Path.Combine(root, "moongate.pid")));
        Assert.Contains("No database connection or server startup", result.Output);
    }

    [Fact]
    public async Task Run_Default_PreparesRootWithoutEnablingApi()
    {
        using var directory = new TemporaryDirectory();
        var result = await BootProcess.RunAsync(directory.Path);
        Assert.True(result.ExitCode == 0, result.Output);
        var config = TomlUtils.DeserializeFromFile<MoongateServerConfig>(
            Path.Combine(directory.Path, "config/moongate.toml")
        )!;
        Assert.False(config.AdminApi.Enabled);
        Assert.False(Directory.Exists(Path.Combine(directory.Path, "certificates")));
    }

    [Fact]
    public async Task Run_ConflictingMigration_ReturnsServerFailure()
    {
        using var directory = new TemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(directory.Path, "migrations/auth"));
        File.WriteAllText(Path.Combine(directory.Path, "migrations/auth/0001_conflicting.sql"), "SELECT 42;");
        var result = await BootProcess.RunAsync(directory.Path);
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("conflicts with bundled", result.Output);
    }

    [Theory, InlineData("--unknown"), InlineData("--admin-certificate-hosts", "login.example.test"),
     InlineData("extra-root")]
    public async Task Run_InvalidArguments_FailsBeforeCreatingRoot(params string[] arguments)
    {
        using var directory = new TemporaryDirectory();
        var root = Path.Combine(directory.Path, "root");
        var result = await BootProcess.RunAsync([root, .. arguments]);
        Assert.NotEqual(0, result.ExitCode);
        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public async Task Run_MissingRoot_Fails()
    {
        var result = await BootProcess.RunAsync();
        Assert.NotEqual(0, result.ExitCode);
    }
}
