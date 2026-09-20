using FreeSql;
using Moongate.Persistence.Types.Persistence;

namespace Moongate.Persistence.Tests.TestSupport.Persistence;

internal static class PersistenceTestModules
{
    public static TestPersistenceModule Character(
        string id = "plugin.characters",
        string schema = "plugin_characters",
        string tableName = "characters",
        PersistenceDatabaseTarget target = PersistenceDatabaseTarget.Realm
    )
    {
        return new TestPersistenceModule(id, schema, target, [typeof(CharacterEntity)], orm =>
            orm.CodeFirst.ConfigEntity<CharacterEntity>(table =>
            {
                table.Name($"{schema}.{tableName}");
                table.Property(entity => entity.Id).Name("id").IsPrimary(true);
                table.Property(entity => entity.Name).Name("name").StringLength(160);
            }));
    }

    public static TestPersistenceModule Inventory(
        string id = "plugin.inventory",
        string schema = "plugin_inventory",
        string tableName = "inventories",
        PersistenceDatabaseTarget target = PersistenceDatabaseTarget.Realm
    )
    {
        return new TestPersistenceModule(id, schema, target, [typeof(InventoryEntity)], orm =>
            orm.CodeFirst.ConfigEntity<InventoryEntity>(table =>
            {
                table.Name($"{schema}.{tableName}");
                table.Property(entity => entity.Id).Name("id").IsPrimary(true);
                table.Property(entity => entity.Balance).Name("balance");
            }));
    }

    public static TestPersistenceModule UpgradeV1()
    {
        return new TestPersistenceModule(
            "plugin.upgrade",
            "plugin_upgrade",
            PersistenceDatabaseTarget.Realm,
            [typeof(UpgradeV1Entity)],
            orm => orm.CodeFirst.ConfigEntity<UpgradeV1Entity>(table =>
            {
                table.Name("plugin_upgrade.characters");
                table.Property(entity => entity.Id).Name("id").IsPrimary(true);
                table.Property(entity => entity.Name).Name("name").StringLength(80);
            }));
    }

    public static TestPersistenceModule UpgradeV2()
    {
        return new TestPersistenceModule(
            "plugin.upgrade",
            "plugin_upgrade",
            PersistenceDatabaseTarget.Realm,
            [typeof(UpgradeV2Entity)],
            orm => orm.CodeFirst.ConfigEntity<UpgradeV2Entity>(table =>
            {
                table.Name("plugin_upgrade.characters");
                table.Property(entity => entity.Id).Name("id").IsPrimary(true);
                table.Property(entity => entity.DisplayName).Name("display_name").OldName("name").StringLength(160);
                table.Property(entity => entity.Level).Name("level").IsNullable(false);
            }));
    }

    public static TestPersistenceModule UpgradeV3()
    {
        return new TestPersistenceModule(
            "plugin.upgrade",
            "plugin_upgrade",
            PersistenceDatabaseTarget.Realm,
            [typeof(UpgradeV3Entity)],
            orm => orm.CodeFirst.ConfigEntity<UpgradeV3Entity>(table =>
            {
                table.Name("plugin_upgrade.characters");
                table.Property(entity => entity.Id).Name("id").IsPrimary(true);
                table.Property(entity => entity.DisplayName).Name("display_name").StringLength(160);
                table.Property(entity => entity.Level).Name("level").IsNullable(false);
            }));
    }

    public static TestPersistenceModule FailingSchema()
    {
        return new TestPersistenceModule(
            "plugin.failing",
            "plugin_failing",
            PersistenceDatabaseTarget.Realm,
            [typeof(FailingSchemaEntity)],
            orm => orm.CodeFirst.ConfigEntity<FailingSchemaEntity>(table =>
            {
                table.Name("plugin_failing.entities");
                table.Property(entity => entity.Id).Name("id").IsPrimary(true);
                table.Property(entity => entity.Value).Name("value").DbType("type_that_does_not_exist");
            }));
    }
}
