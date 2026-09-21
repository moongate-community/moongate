using Moongate.Persistence.Types.Persistence;

namespace Moongate.Persistence.Tests.TestSupport.Persistence;

internal static class PersistenceTestModules
{
    public static TestPersistenceModule Character(
        string id = "plugin.characters",
        PersistenceDatabaseTarget target = PersistenceDatabaseTarget.Realm
    )
    {
        return new TestPersistenceModule(id, "plugin_characters", target, [typeof(CharacterEntity)]);
    }

    public static TestPersistenceModule Inventory(
        string id = "plugin.inventory",
        PersistenceDatabaseTarget target = PersistenceDatabaseTarget.Realm
    )
    {
        return new TestPersistenceModule(id, "plugin_inventory", target, [typeof(InventoryEntity)]);
    }

    public static TestPersistenceModule CharacterShared()
    {
        return new TestPersistenceModule(
            "plugin.characters",
            "plugin_characters",
            PersistenceDatabaseTarget.Realm,
            [typeof(CharacterSharedEntity)]
        );
    }

    public static TestPersistenceModule InventoryShared()
    {
        return new TestPersistenceModule(
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

    public static TestPersistenceModule FailingSchema()
    {
        return new TestPersistenceModule(
            "plugin.failing",
            "plugin_failing",
            PersistenceDatabaseTarget.Realm,
            [typeof(FailingSchemaEntity)]
        );
    }

    private static TestPersistenceModule Upgrade(Type entityType)
    {
        return new TestPersistenceModule(
            "plugin.upgrade",
            "plugin_upgrade",
            PersistenceDatabaseTarget.Realm,
            [entityType]
        );
    }
}
