using Moongate.Server.Admin.Data.Config;
using Moongate.Tests.TestSupport.Boot;
using Moongate.Tests.TestSupport.Config;
using Moongate.Tests.TestSupport.Directories;
using Moongate.Tests.TestSupport.Persistence;
using Tomlyn;
using Tomlyn.Model;

namespace Moongate.Tests.Integration.Boot;

public sealed class BootCommandTests
{
    [Theory, InlineData("--help"), InlineData("-h")]
    public async Task Run_Help_ListsEveryCommand(string flag)
    {
        var result = await BootProcess.RunAsync(flag);

        Assert.Equal(0, result.ExitCode);

        foreach (var command in new[]
                 {
                     "init", "migrate status", "migrate apply", "convert uox", "convert modernuo-spawns",
                     "convert modernuo-signs", "convert modernuo-teleporters"
                 })
        {
            Assert.Contains(command, result.Output);
        }
    }

    [Fact]
    public async Task Run_InitWithARoot_PreparesIt_AsTheRootAloneDoes()
    {
        using var directory = new TemporaryDirectory();
        var result = await BootProcess.RunAsync("init", directory.Path);

        Assert.True(result.ExitCode == 0, result.Output);
        Assert.Contains("Root setup", result.Output);
        Assert.True(File.Exists(Path.Combine(directory.Path, "config/moongate.toml")));
    }

    [Fact]
    public async Task Run_ConvertWithoutItsOptions_ShowsTheCommandsHelp()
    {
        var result = await BootProcess.RunAsync("convert", "modernuo-teleporters", "--help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("--source", result.Output);
        Assert.Contains("--destination", result.Output);
    }

    [Fact]
    public async Task Run_ConvertModernUoSigns_WritesTheDecorationFile()
    {
        using var directory = new TemporaryDirectory();
        var source = Path.Combine(directory.Path, "signs.cfg");
        File.WriteAllText(source, "2 2979 3632 2537 0 The Shakin' Bakery\n");
        var destination = Path.Combine(directory.Path, "decorations");

        var result = await BootProcess.RunAsync("convert", "modernuo-signs", "--source", source, "--destination", destination);

        Assert.True(result.ExitCode == 0, result.Output);
        Assert.Contains("The Shakin' Bakery", File.ReadAllText(Path.Combine(destination, "trammel", "signs.toml")));
    }

    [Fact]
    public async Task Run_MigrateStatus_WithoutARoot_UsesTheConfigurationAndPluginsBesideMgboot()
    {
        await using var db = await new PostgreSqlFixture().CreateDatabaseAsync();
        using var directory = new TemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(directory.Path, "config"));
        File.WriteAllText(
            Path.Combine(directory.Path, "config/moongate.toml"),
            "[persistence.realm]\nconnection_string = '" + db.ConnectionString + "'\n"
        );
        // A distribution has its migrations beside mgboot.
        Directory.CreateDirectory(Path.Combine(directory.Path, "migrations"));
        Directory.CreateDirectory(Path.Combine(directory.Path, "plugins/p/migrations/world"));
        File.WriteAllText(Path.Combine(directory.Path, "plugins/p/migrations/manifest.json"), "{\"id\":\"sample\"}");
        File.WriteAllText(Path.Combine(directory.Path, "plugins/p/migrations/world/0001_data.sql"), "SELECT 1;");

        foreach (var file in Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "mgboot")))
        {
            if (Path.GetExtension(file) is ".dll" or ".json")
            {
                File.Copy(file, Path.Combine(directory.Path, Path.GetFileName(file)));
            }
        }

        var result = await BootProcess.RunFromAsync(directory.Path, "migrate", "status", "--target", "world");

        Assert.True(result.ExitCode == 0, result.Output);
        Assert.Contains("sample/0001_data.sql", result.Output);
    }

    [Theory, InlineData("--help"), InlineData("-h")]
    public async Task Run_InitHelp_DescribesCertificateOptions(string flag)
    {
        var result = await BootProcess.RunAsync("init", flag);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("--generate-admin-certificate", result.Output);
        Assert.Contains("--admin-certificate-hosts", result.Output);
        Assert.DoesNotContain(".       .     _.--.", result.Output);
    }

    [Fact]
    public async Task Run_Version_OmitsHeader()
    {
        var result = await BootProcess.RunAsync("--version");

        Assert.Equal(0, result.ExitCode);
        Assert.Matches(@"^\d+\.\d+\.\d+\s*$", result.Output);
        Assert.DoesNotContain(".       .     _.--.", result.Output);
    }

    [Fact]
    public async Task Run_Default_ShowsMoongateHeaderBeforePreparingRoot()
    {
        using var directory = new TemporaryDirectory();
        var result = await BootProcess.RunAsync(directory.Path);

        Assert.Equal(0, result.ExitCode);
        Assert.StartsWith(".       .     _.--.", result.Output);
        Assert.Matches("Version: [0-9]+\\.[0-9]+\\.[0-9]+ Codename: \"[^\"]+\"", result.Output);
        Assert.Contains("Root setup", result.Output);
        Assert.DoesNotContain("{Version}", result.Output);
        Assert.DoesNotContain("{Codename}", result.Output);
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
        var config = TomlSections.Read<AdminApiConfig>(File.ReadAllText(Path.Combine(root, "config/moongate.toml")), "admin_api");
        Assert.True(config.Enabled);
        Assert.True(File.Exists(Path.Combine(root, config.CertificatePath)));
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
        // The Administration plugin appends [admin_api] at the first server start; mgboot writes none by default.
        var document = TomlSerializer.Deserialize<TomlTable>(
            File.ReadAllText(Path.Combine(directory.Path, "config/moongate.toml"))
        )!;
        Assert.False(document.ContainsKey("admin_api"));
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
