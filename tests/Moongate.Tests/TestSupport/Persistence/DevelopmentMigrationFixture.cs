using Moongate.Core.Utils;
using Moongate.Persistence.Internal;
using Moongate.Persistence.Types.Persistence;
using Moongate.Server.Data.Config;

namespace Moongate.Tests.TestSupport.Persistence;

public sealed class DevelopmentMigrationFixture : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), $"moongate development {Guid.NewGuid():N}");
    public string Migrations => Path.Combine(Root, "source migrations");
    public string Plugins => Path.Combine(Root, "plugins");
    public MoongateServerConfig Config { get; }

    public DevelopmentMigrationFixture(string connectionString)
    {
        Directory.CreateDirectory(Migrations);
        Directory.CreateDirectory(Plugins);
        Directory.CreateDirectory(Path.Combine(Root, "config"));
        Config = new();
        Config.Persistence.Accounts.ConnectionString = connectionString;
        Config.Persistence.Realm.ConnectionString = connectionString;
        Config.Persistence.AutoGenerateMigrations = true;
        Config.Persistence.MigrationsDirectory = Migrations;
        TomlUtils.SerializeToFile(Config, Path.Combine(Root, "config/moongate.toml"));
    }

    internal PersistenceSchemaCoordinator Create(
        Type entity, PersistenceDatabaseTarget target = PersistenceDatabaseTarget.Realm
    )
    {
        var registry = new PersistenceModuleRegistry();
        registry.RegisterEntity(entity, target);
        return new(Config.Persistence.ToOptions(Migrations, Plugins, rootDirectory: Root), registry);
    }

    public void Dispose() => Directory.Delete(Root, true);
}
