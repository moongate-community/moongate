using Moongate.Core.Extensions.Directories;
using Moongate.Persistence.Data.Config;
using Moongate.Persistence.Migrations.Services;
using Moongate.Persistence.Migrations.Types.Migrations;
using Moongate.Persistence.Types.Persistence;
using Moongate.Server.Core.Types.Hosting;

namespace Moongate.Server.Data.Config.Sections;

/// <summary>Configures lazy PostgreSQL target connections and explicit schema synchronization.</summary>
public sealed class PersistenceConfig
{
    public bool AutoSyncSchema { get; set; }

    public bool AutoGenerateMigrations { get; set; }

    public string? MigrationsDirectory { get; set; }


    public PersistenceDatabaseConfig Accounts { get; set; } =
        new() { ConnectionString = "postgres://moongate:moongate@localhost:5432/auth" };

    public PersistenceDatabaseConfig Realm { get; set; } =
        new() { ConnectionString = "postgres://moongate:moongate@localhost:5432/world" };

    public PostgreSqlPersistenceOptions ToOptions(
        string? migrationsDirectory = null,
        string? pluginsDirectory = null,
        ServerMode mode = ServerMode.Standalone
    )
    {
        Validate();
        migrationsDirectory = ResolveMigrationsDirectory(migrationsDirectory);

        return new(
            [
                Accounts.ToOptions(PersistenceDatabaseTarget.Accounts),
                Realm.ToOptions(PersistenceDatabaseTarget.Realm)
            ],
            AutoSyncSchema,
            migrationsDirectory is null
                ? null
                : target => MigrationCatalog.Load(
                      migrationsDirectory,
                      pluginsDirectory,
                      target == PersistenceDatabaseTarget.Accounts ? MigrationTarget.Auth : MigrationTarget.World
                  ),
            target => (mode & (target == PersistenceDatabaseTarget.Accounts ? ServerMode.Login : ServerMode.Game)) != 0
        );
    }

    public string? ResolveMigrationsDirectory(string? fallback = null)
        => string.IsNullOrWhiteSpace(MigrationsDirectory)
               ? fallback
               : MigrationsDirectory.ResolvePathAndEnvs();

    public void Validate()
    {
        if (AutoGenerateMigrations && AutoSyncSchema)
        {
            throw new InvalidOperationException("auto_generate_migrations and auto_sync_schema are mutually exclusive.");
        }

        if (AutoGenerateMigrations && string.IsNullOrWhiteSpace(MigrationsDirectory))
        {
            throw new InvalidOperationException("auto_generate_migrations requires an explicit source migrations_directory.");
        }

        if (Accounts is null || Realm is null)
        {
            throw new InvalidOperationException("Persistence accounts and realm configuration sections cannot be null.");
        }

        Accounts.Validate();
        Realm.Validate();
    }
}
