using Moongate.Persistence.Types.Persistence;

namespace Moongate.Persistence.Tests.TestSupport.Persistence;

internal static class PersistenceTestModules
{
    public static TestPersistenceModule Character(
        string id = "plugin.characters",
        PersistenceDatabaseTarget target = PersistenceDatabaseTarget.Realm
    )
        => new(id, "plugin_characters", target, [typeof(CharacterEntity)]);

    public static TestPersistenceModule CharacterShared()
        => new(
            "plugin.characters",
            "plugin_characters",
            PersistenceDatabaseTarget.Realm,
            [typeof(CharacterSharedEntity)]
        );

    public static TestPersistenceModule FailingSchema()
        => new(
            "plugin.failing",
            "plugin_failing",
            PersistenceDatabaseTarget.Realm,
            [typeof(FailingSchemaEntity)]
        );

    public static TestPersistenceModule Inventory(
        string id = "plugin.inventory",
        PersistenceDatabaseTarget target = PersistenceDatabaseTarget.Realm
    )
        => new(id, "plugin_inventory", target, [typeof(InventoryEntity)]);

    public static TestPersistenceModule InventoryShared()
        => new(
            "plugin.inventory",
            "plugin_inventory",
            PersistenceDatabaseTarget.Realm,
            [typeof(InventorySharedEntity)]
        );

    public static TestPersistenceModule UpgradeV1()
        => Upgrade(typeof(UpgradeV1Entity));

    public static TestPersistenceModule UpgradeV2()
        => Upgrade(typeof(UpgradeV2Entity));

    public static TestPersistenceModule UpgradeV3()
        => Upgrade(typeof(UpgradeV3Entity));

    private static TestPersistenceModule Upgrade(Type entityType)
        => new(
            "plugin.upgrade",
            "plugin_upgrade",
            PersistenceDatabaseTarget.Realm,
            [entityType]
        );
}
