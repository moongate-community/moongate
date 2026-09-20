using Moongate.Core.Utils;
using Moongate.Persistence.Services;
using Moongate.Server.Data.Config;
using Moongate.Server.Data.Config.Sections;

namespace Moongate.Tests.Server.Data.Config.Sections;

public sealed class PersistenceConfigTests
{
    [Fact]
    public async Task Defaults_NoEntities_DoNotResolveDatabaseEnvironment()
    {
        var config = new MoongateServerConfig();
        Assert.False(config.Persistence.AutoSyncSchema);
        Assert.Equal("MOONGATE_ACCOUNTS_DATABASE", config.Persistence.Accounts.ConnectionStringEnv);
        Assert.Equal("MOONGATE_REALM_DATABASE", config.Persistence.Realm.ConnectionStringEnv);
        config.Persistence.Accounts.ConnectionStringEnv = "MOONGATE_TEST_MISSING_" + Guid.NewGuid().ToString("N");
        config.Persistence.Realm.ConnectionStringEnv = "MOONGATE_TEST_MISSING_" + Guid.NewGuid().ToString("N");
        await using var owner = new MoongatePersistenceService(config.Persistence.ToOptions());
        await owner.InitializeAsync();
    }

    [Fact]
    public void Validate_BlankEnvironmentName_RejectsWithoutReadingEnvironment()
    {
        var config = new PersistenceConfig();
        config.Realm.ConnectionStringEnv = " ";
        Assert.Throws<InvalidOperationException>(config.Validate);
        config.Realm.ConnectionStringEnv = "REALM";
        config.Realm.SchemaConnectionStringEnv = " ";
        Assert.Throws<InvalidOperationException>(config.Validate);
    }

    [Fact]
    public void RoundTrip_PersistenceSettings_PreservesNamesAndPolicy()
    {
        var path = Path.Combine(Path.GetTempPath(), $"moongate-config-{Guid.NewGuid():N}.toml");
        try
        {
            var config = new MoongateServerConfig();
            config.Persistence.AutoSyncSchema = true;
            config.Persistence.Realm.SchemaConnectionStringEnv = "REALM_SCHEMA";
            TomlUtils.SerializeToFile(config, path);
            var text = File.ReadAllText(path);
            Assert.Contains("auto_sync_schema = true", text);
            Assert.Contains("[persistence.realm]", text);
            Assert.DoesNotContain("backup_retention", text);
            var restored = TomlUtils.DeserializeFromFile<MoongateServerConfig>(path)!;
            Assert.True(restored.Persistence.AutoSyncSchema);
            Assert.Equal("REALM_SCHEMA", restored.Persistence.Realm.SchemaConnectionStringEnv);
        }
        finally { File.Delete(path); }
    }
}
