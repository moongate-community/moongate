using Moongate.Persistence.Services;
using Moongate.Persistence.Types.Persistence;

namespace Moongate.Persistence.Tests.TestSupport.Persistence;

internal static class FacadeFixture
{
    public static MoongatePersistenceService Create(PostgreSqlTestDatabase database)
    {
        var owner = new MoongatePersistenceService(
            new(
                [new(PersistenceDatabaseTarget.Realm, database.ConnectionString)],
                true
            )
        );
        owner.RegisterModule(
            new TestPersistenceModule(
                "characters",
                "plugin_characters",
                PersistenceDatabaseTarget.Realm,
                [typeof(CharacterEntity)]
            )
        );
        owner.RegisterModule(
            new TestPersistenceModule(
                "inventory",
                "plugin_inventory",
                PersistenceDatabaseTarget.Realm,
                [typeof(InventoryEntity)]
            )
        );

        return owner;
    }
}
