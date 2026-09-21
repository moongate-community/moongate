using Moongate.Persistence.Data.Config;
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

    public PostgreSqlPersistenceOptions ToOptions()
    {
        Validate();
        return new PostgreSqlPersistenceOptions([
            Accounts.ToOptions(PersistenceDatabaseTarget.Accounts),
            Realm.ToOptions(PersistenceDatabaseTarget.Realm)
        ], AutoSyncSchema);
    }
}
