using Moongate.Persistence.Types.Persistence;

namespace Moongate.Persistence.Tests.TestSupport.Persistence;

internal static class PersistenceTestModules
{
    public static TestPersistenceModule Character(
        string id = "plugin.characters",
        PersistenceDatabaseTarget target = PersistenceDatabaseTarget.Realm
    )
    {
        return new(id, "plugin_characters", target, [typeof(CharacterEntity)]);
    }

    public static TestPersistenceModule CharacterShared()
    {
        return new(
            "plugin.characters",
            "plugin_characters",
            PersistenceDatabaseTarget.Realm,
            [typeof(CharacterSharedEntity)]
        );
    }

    public static TestPersistenceModule FailingSchema()
    {
        return new(
            "plugin.failing",
            "plugin_failing",
            PersistenceDatabaseTarget.Realm,
            [typeof(FailingSchemaEntity)]
        );
    }

    public static TestPersistenceModule Inventory(
        string id = "plugin.inventory",
        PersistenceDatabaseTarget target = PersistenceDatabaseTarget.Realm
    )
    {
        return new(id, "plugin_inventory", target, [typeof(InventoryEntity)]);
    }

    public static TestPersistenceModule InventoryShared()
    {
        return new(
            "plugin.inventory",
            "plugin_inventory",
            PersistenceDatabaseTarget.Realm,
            [typeof(InventorySharedEntity)]
        );
    }

    public static TestPersistenceModule UpgradeV1()
    {
        return Upgrade(typeof(UpgradeV1Entity));
    }

    public static TestPersistenceModule UpgradeV2()
    {
        return Upgrade(typeof(UpgradeV2Entity));
    }

    public static TestPersistenceModule UpgradeV3()
    {
        return Upgrade(typeof(UpgradeV3Entity));
    }

    private static TestPersistenceModule Upgrade(Type entityType)
    {
        return new(
            "plugin.upgrade",
            "plugin_upgrade",
            PersistenceDatabaseTarget.Realm,
            [entityType]
        );
    }
}
