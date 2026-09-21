using Moongate.Persistence.Data.Config;
using Moongate.Persistence.Migrations.Services;
using Moongate.Persistence.Migrations.Types.Migrations;
using Moongate.Server.Core.Types.Hosting;
using Moongate.Persistence.Types.Persistence;

namespace Moongate.Server.Data.Config.Sections;

/// <summary>Configures lazy PostgreSQL target connections and explicit schema synchronization.</summary>
public sealed class PersistenceConfig
{
    public bool AutoSyncSchema { get; set; }
    public PersistenceDatabaseConfig Accounts { get; set; } = new() { ConnectionString = "$MOONGATE_ACCOUNTS_DATABASE" };
    public PersistenceDatabaseConfig Realm { get; set; } = new() { ConnectionString = "$MOONGATE_REALM_DATABASE" };

    public void Validate()
    {
        if (Accounts is null || Realm is null)
        {
            throw new InvalidOperationException("Persistence accounts and realm configuration sections cannot be null.");
        }

        Accounts.Validate();
        Realm.Validate();
    }

    public PostgreSqlPersistenceOptions ToOptions(string? migrationsDirectory = null, string? pluginsDirectory = null, ServerMode mode = ServerMode.Standalone)
    {
        Validate();
        return new PostgreSqlPersistenceOptions(
            [
                Accounts.ToOptions(PersistenceDatabaseTarget.Accounts),
                Realm.ToOptions(PersistenceDatabaseTarget.Realm)
            ],
            AutoSyncSchema,
            migrationsDirectory is null ? null : target =>
            {
                var selected = target == PersistenceDatabaseTarget.Accounts ? ServerMode.Login : ServerMode.Game;
                return (mode & selected) == 0 ? null : MigrationCatalog.Load(migrationsDirectory, pluginsDirectory,
                    target == PersistenceDatabaseTarget.Accounts ? MigrationTarget.Auth : MigrationTarget.World);
            }
        );
    }
}
