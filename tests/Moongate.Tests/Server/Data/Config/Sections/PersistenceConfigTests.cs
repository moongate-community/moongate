using Moongate.Core.Utils;
using Moongate.Persistence.Types.Persistence;
using Moongate.Server.Data.Config;
using Moongate.Server.Data.Config.Sections;
using Moongate.Tests.TestSupport.Environment;
using Npgsql;

namespace Moongate.Tests.Server.Data.Config.Sections;

[Collection(EnvironmentTestsCollection.Name)]
public sealed class PersistenceConfigTests
{
    [Fact]
    public void ResolveMigrationsDirectory_ExpandsEnvironmentAndRejectsMissingVariables()
    {
        var name = "MOONGATE_SOURCE_" + Guid.NewGuid().ToString("N");
        var config = new PersistenceConfig { MigrationsDirectory = "${" + name + "}/migrations" };
        Assert.Throws<InvalidOperationException>(() => config.ResolveMigrationsDirectory());
        using var scope = new EnvironmentVariableScope(name, Path.GetTempPath());
        Assert.Equal(Path.Combine(Path.GetTempPath(), "migrations"), config.ResolveMigrationsDirectory());
    }

    [Fact]
    public void Validate_AutomaticPoliciesConflict_RejectsBeforeDatabaseAccess()
    {
        var config = new PersistenceConfig
        {
            AutoSyncSchema = true,
            AutoGenerateMigrations = true,
            MigrationsDirectory = "/tmp/moongate-source/migrations"
        };
        Assert.Throws<InvalidOperationException>(config.Validate);
    }

    [Fact]
    public void Validate_AutomaticGenerationRequiresExplicitSourceDirectory()
    {
        var config = new PersistenceConfig { AutoGenerateMigrations = true };
        Assert.Throws<InvalidOperationException>(config.Validate);
        Assert.False(new PersistenceConfig().AutoGenerateMigrations);
    }

    [Fact]
    public void RoundTrip_DevelopmentMigrations_PreservesSourceDirectory()
    {
        var config = new MoongateServerConfig();
        config.Persistence.AutoGenerateMigrations = true;
        config.Persistence.MigrationsDirectory = "${MOONGATE_SOURCE}/migrations";
        var path = Path.Combine(Path.GetTempPath(), $"moongate-{Guid.NewGuid():N}.toml");
        try
        {
            TomlUtils.SerializeToFile(config, path);
            Assert.Contains("auto_generate_migrations = true", File.ReadAllText(path));
            var restored = TomlUtils.DeserializeFromFile<MoongateServerConfig>(path)!;
            Assert.True(restored.Persistence.AutoGenerateMigrations);
            Assert.Equal(config.Persistence.MigrationsDirectory, restored.Persistence.MigrationsDirectory);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory, InlineData(PersistenceDatabaseTarget.Accounts, "auth"), InlineData(PersistenceDatabaseTarget.Realm, "world")]
    public void Defaults_ResolveLocalDatabaseWithoutEnvironment(PersistenceDatabaseTarget target, string database)
    {
        var config = new MoongateServerConfig();
        Assert.False(config.Persistence.AutoSyncSchema);
        var parsed = new NpgsqlConnectionStringBuilder(
            config.Persistence.ToOptions().GetRequiredDatabase(target).ResolveRuntimeConnectionString()
        );
        Assert.Equal("localhost", parsed.Host);
        Assert.Equal(5432, parsed.Port);
        Assert.Equal(database, parsed.Database);
        Assert.Equal("moongate", parsed.Username);
        Assert.Equal("moongate", parsed.Password);
    }

    [Fact]
    public void RoundTrip_PersistenceSettings_PreservesConnectionTemplateAndPolicy()
    {
        var path = Path.Combine(Path.GetTempPath(), $"moongate-config-{Guid.NewGuid():N}.toml");

        try
        {
            var config = new MoongateServerConfig();
            config.Persistence.AutoSyncSchema = true;
            config.Persistence.Realm.ConnectionString = "${REALM_DATABASE}";
            TomlUtils.SerializeToFile(config, path);
            var text = File.ReadAllText(path);
            Assert.Contains("auto_sync_schema = true", text);
            Assert.Contains("[persistence.realm]", text);
            Assert.Contains("connection_string = \"${REALM_DATABASE}\"", text);
            Assert.DoesNotContain("connection_string_env", text);
            Assert.DoesNotContain("schema_connection", text);
            var restored = TomlUtils.DeserializeFromFile<MoongateServerConfig>(path)!;
            Assert.True(restored.Persistence.AutoSyncSchema);
            Assert.Equal("${REALM_DATABASE}", restored.Persistence.Realm.ConnectionString);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ToOptions_ExpandsEnvironmentLazilyWithoutResolvingAFilePath()
    {
        var key = $"MOONGATE_TEST_URI_{Guid.NewGuid():N}";
        var config = new PersistenceConfig();
        config.Realm.ConnectionString = "${" + key + "}";
        var options = config.ToOptions();
        using var environment = new EnvironmentVariableScope(
            key,
            "postgres://runtime:synthetic$UNEXPANDED@localhost/realm?application_name=Moongate"
        );

        var parsed = new NpgsqlConnectionStringBuilder(
            options.GetRequiredDatabase(PersistenceDatabaseTarget.Realm)
                .ResolveRuntimeConnectionString()
        );

        Assert.Equal("localhost", parsed.Host);
        Assert.Equal("realm", parsed.Database);
        Assert.Equal("synthetic$UNEXPANDED", parsed.Password);
    }

    [Fact]
    public void ToOptions_LiteralUriAndMissingEnvironment_HaveClearBehavior()
    {
        var config = new PersistenceConfig();
        config.Accounts.ConnectionString = "postgres://runtime@localhost/accounts";
        config.Realm.ConnectionString = "$MOONGATE_TEST_MISSING_" + Guid.NewGuid().ToString("N");
        var options = config.ToOptions();
        Assert.Contains(
            "Database=accounts",
            options.GetRequiredDatabase(PersistenceDatabaseTarget.Accounts)
                .ResolveRuntimeConnectionString()
        );
        var error = Assert.Throws<InvalidOperationException>(() =>
            options.GetRequiredDatabase(PersistenceDatabaseTarget.Realm)
                .ResolveRuntimeConnectionString()
        );
        Assert.Contains("is not defined", error.Message);
    }

    [Fact]
    public void Validate_BlankConnectionString_RejectsWithoutReadingEnvironment()
    {
        var config = new PersistenceConfig();
        config.Realm.ConnectionString = " ";
        Assert.Throws<InvalidOperationException>(config.Validate);
    }
}
