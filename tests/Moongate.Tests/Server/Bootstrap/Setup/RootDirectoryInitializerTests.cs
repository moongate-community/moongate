using Moongate.Core.Utils;
using Moongate.Server.Bootstrap.Internal.Setup;
using Moongate.Server.Data.Config;
using Moongate.Tests.TestSupport.Directories;

namespace Moongate.Tests.Server.Bootstrap.Setup;

public sealed class RootDirectoryInitializerTests
{
    [Fact]
    public void Initialize_NewRoot_CreatesDefaultConfigDirectoriesAndBaseMigrations()
    {
        using var directory = new TemporaryDirectory();
        var source = CreateMigrations(directory);
        var root = Path.Combine(directory.Path, "new root");
        RootDirectoryInitializer.Initialize(root, source, TextWriter.Null);

        foreach (var folder in new[] { "config", "logs", "plugins", "scripts", "migrations/auth", "migrations/world" })
        {
            Assert.True(Directory.Exists(Path.Combine(root, folder)), folder);
        }

        var config = TomlUtils.DeserializeFromFile<MoongateServerConfig>(Path.Combine(root, "config/moongate.toml"))!;
        config.Validate();
        Assert.Equal(Path.Combine(root, "migrations"), config.Persistence.MigrationsDirectory);
        Assert.False(config.Persistence.AutoGenerateMigrations);
        Assert.NotNull(config.Redis);
        Assert.Equal(
            File.ReadAllBytes(Path.Combine(source, "auth/0001_base.sql")),
            File.ReadAllBytes(Path.Combine(root, "migrations/auth/0001_base.sql"))
        );
        Assert.False(File.Exists(Path.Combine(root, "moongate.pid")));
    }

    [Fact]
    public void Initialize_RepeatedRun_PreservesExistingConfigAndOtherFiles()
    {
        using var directory = new TemporaryDirectory();
        var source = CreateMigrations(directory);
        var root = Path.Combine(directory.Path, "root");
        RootDirectoryInitializer.Initialize(root, source, TextWriter.Null);
        var path = Path.Combine(root, "config/moongate.toml");
        var original = File.ReadAllText(path) + "\n# user customization\n";
        File.WriteAllText(path, original);
        File.WriteAllText(Path.Combine(root, "scripts/custom.lua"), "return 42");
        RootDirectoryInitializer.Initialize(root, source, TextWriter.Null);
        Assert.Equal(original, File.ReadAllText(path));
        Assert.Equal("return 42", File.ReadAllText(Path.Combine(root, "scripts/custom.lua")));
    }

    [Theory, InlineData("0001_base.sql", "SELECT 2;"), InlineData("0001_custom.sql", "SELECT 1;")]
    public void Initialize_ConflictingMigration_FailsWithoutOverwritingOrAddingConfig(string name, string sql)
    {
        using var directory = new TemporaryDirectory();
        var source = CreateMigrations(directory);
        var root = Path.Combine(directory.Path, "root");
        Directory.CreateDirectory(Path.Combine(root, "migrations/auth"));
        var conflicting = Path.Combine(root, "migrations/auth", name);
        File.WriteAllText(conflicting, sql);
        var exception = Assert.Throws<InvalidOperationException>(() =>
            RootDirectoryInitializer.Initialize(root, source, TextWriter.Null)
        );
        Assert.Contains(name, exception.Message);
        Assert.Equal(sql, File.ReadAllText(conflicting));
        Assert.False(File.Exists(Path.Combine(root, "config/moongate.toml")));
    }

    [Fact]
    public void Initialize_MissingDistributionMigrations_FailsBeforeCreatingRoot()
    {
        using var directory = new TemporaryDirectory();
        var root = Path.Combine(directory.Path, "root");
        Assert.ThrowsAny<Exception>(() =>
            RootDirectoryInitializer.Initialize(root, Path.Combine(directory.Path, "missing"), TextWriter.Null)
        );
        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public void Initialize_ExistingConfigWithUnavailableDatabase_DoesNotConnect()
    {
        using var directory = new TemporaryDirectory();
        var source = CreateMigrations(directory);
        var root = Path.Combine(directory.Path, "root");
        Directory.CreateDirectory(Path.Combine(root, "config"));
        var config = new MoongateServerConfig();
        config.Persistence.Accounts.ConnectionString = "postgres://localhost:1/unavailable";
        var path = Path.Combine(root, "config/moongate.toml");
        TomlUtils.SerializeToFile(config, path);
        var original = File.ReadAllBytes(path);
        RootDirectoryInitializer.Initialize(root, source, TextWriter.Null);
        Assert.Equal(original, File.ReadAllBytes(path));
    }

    [Fact]
    public void Initialize_WithCertificate_EnablesAdminApiWithoutStartingServer()
    {
        using var directory = new TemporaryDirectory();
        var source = CreateMigrations(directory);
        var root = Path.Combine(directory.Path, "root");
        RootDirectoryInitializer.Initialize(root, source, TextWriter.Null, ["login.example.test"]);
        var config = TomlUtils.DeserializeFromFile<MoongateServerConfig>(Path.Combine(root, "config/moongate.toml"))!;
        Assert.True(config.AdminApi.Enabled);
        Assert.False(config.AdminApi.AllowInsecureLoopback);
        Assert.True(File.Exists(Path.Combine(root, config.AdminApi.CertificatePath)));
        Assert.False(File.Exists(Path.Combine(root, "moongate.pid")));
    }

    private static string CreateMigrations(TemporaryDirectory directory)
    {
        var source = Path.Combine(directory.Path, "distribution-migrations");
        Directory.CreateDirectory(Path.Combine(source, "auth"));
        File.WriteAllText(Path.Combine(source, "auth/0001_base.sql"), "SELECT 1;\n");

        return source;
    }
}
