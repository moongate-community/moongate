using FreeSql;
using Moongate.Persistence.Data.Config;
using Moongate.Persistence.Internal;
using Moongate.Persistence.Tests.TestSupport.Persistence;
using Moongate.Persistence.Types.Persistence;

namespace Moongate.Persistence.Tests.Modules;

public sealed class PersistenceModuleRegistryTests
{
    private const string UnitConnectionString = "Host=localhost;Database=unit;Username=postgres;Pooling=false";

    [Fact]
    public void ValidateAndFreeze_RegisteredEntityWithoutOwner_RejectsNamedType()
    {
        var registry = new PersistenceModuleRegistry();
        registry.RegisterEntity(typeof(CharacterEntity));

        var exception = Assert.Throws<InvalidOperationException>(() => Validate(registry));

        Assert.Contains(typeof(CharacterEntity).FullName!, exception.Message, StringComparison.Ordinal);
        Assert.Contains("owner", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateAndFreeze_ModuleEntityWasNotRegistered_RejectsNamedModuleAndType()
    {
        var registry = new PersistenceModuleRegistry();
        registry.RegisterModule(CreateCharacterModule("plugin.characters", "plugin_characters"));

        var exception = Assert.Throws<InvalidOperationException>(() => Validate(registry));

        Assert.Contains("plugin.characters", exception.Message, StringComparison.Ordinal);
        Assert.Contains(typeof(CharacterEntity).FullName!, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateAndFreeze_DuplicateModuleId_RejectsWholeBatch()
    {
        var registry = new PersistenceModuleRegistry();
        registry.RegisterModule(CreateCharacterModule("plugin.same", "plugin_characters"));
        registry.RegisterModule(CreateInventoryModule("plugin.same", "plugin_inventory"));
        registry.RegisterEntity(typeof(CharacterEntity));
        registry.RegisterEntity(typeof(InventoryEntity));

        var exception = Assert.Throws<InvalidOperationException>(() => Validate(registry));

        Assert.Contains("plugin.same", exception.Message, StringComparison.Ordinal);
        Assert.Contains("duplicate", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateAndFreeze_DuplicateSchema_RejectsWholeBatch()
    {
        var registry = new PersistenceModuleRegistry();
        registry.RegisterModule(CreateCharacterModule("plugin.characters", "plugin_shared"));
        registry.RegisterModule(CreateInventoryModule("plugin.inventory", "plugin_shared"));
        registry.RegisterEntity(typeof(CharacterEntity));
        registry.RegisterEntity(typeof(InventoryEntity));

        var exception = Assert.Throws<InvalidOperationException>(() => Validate(registry));

        Assert.Contains("plugin_shared", exception.Message, StringComparison.Ordinal);
        Assert.Contains("duplicate", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateAndFreeze_TypeOwnedByTwoModules_RejectsNamedType()
    {
        var registry = new PersistenceModuleRegistry();
        registry.RegisterModule(CreateCharacterModule("plugin.one", "plugin_one"));
        registry.RegisterModule(new TestPersistenceModule(
            "plugin.two",
            "plugin_two",
            PersistenceDatabaseTarget.Realm,
            [typeof(CharacterEntity)],
            orm => MapCharacter(orm, "plugin_two", "characters")));
        registry.RegisterEntity(typeof(CharacterEntity));

        var exception = Assert.Throws<InvalidOperationException>(() => Validate(registry));

        Assert.Contains(typeof(CharacterEntity).FullName!, exception.Message, StringComparison.Ordinal);
        Assert.Contains("plugin.one", exception.Message, StringComparison.Ordinal);
        Assert.Contains("plugin.two", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateAndFreeze_TwoTypesMappedToSameTable_RejectsCollision()
    {
        var registry = new PersistenceModuleRegistry();
        registry.RegisterModule(new TestPersistenceModule(
            "plugin.colliding",
            "plugin_colliding",
            PersistenceDatabaseTarget.Realm,
            [typeof(CharacterEntity), typeof(InventoryEntity)],
            orm =>
            {
                MapCharacter(orm, "plugin_colliding", "entities");
                MapInventory(orm, "plugin_colliding", "entities");
            }));
        registry.RegisterEntity(typeof(CharacterEntity));
        registry.RegisterEntity(typeof(InventoryEntity));

        var exception = Assert.Throws<InvalidOperationException>(() => Validate(registry));

        Assert.Contains("plugin_colliding.entities", exception.Message, StringComparison.Ordinal);
        Assert.Contains("duplicate", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Plugin_A")]
    [InlineData("plugin-a")]
    [InlineData("1plugin")]
    [InlineData("plugin..a")]
    public void ValidateAndFreeze_InvalidSchema_Rejects(string schema)
    {
        var registry = new PersistenceModuleRegistry();
        registry.RegisterModule(CreateCharacterModule("plugin.characters", schema));
        registry.RegisterEntity(typeof(CharacterEntity));

        var exception = Assert.Throws<InvalidOperationException>(() => Validate(registry));

        Assert.Contains("schema", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateAndFreeze_NonEntityOwnedType_RejectsNamedType()
    {
        var registry = new PersistenceModuleRegistry();
        registry.RegisterModule(new TestPersistenceModule(
            "plugin.invalid",
            "plugin_invalid",
            PersistenceDatabaseTarget.Realm,
            [typeof(NotAnEntity)],
            _ => { }));

        var exception = Assert.Throws<InvalidOperationException>(() => Validate(registry));

        Assert.Contains(typeof(NotAnEntity).FullName!, exception.Message, StringComparison.Ordinal);
        Assert.Contains("IMoongateEntity", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateAndFreeze_ExplicitInterfaceId_RejectsMissingPublicIdentity()
    {
        var registry = new PersistenceModuleRegistry();
        registry.RegisterModule(new TestPersistenceModule(
            "plugin.invalid",
            "plugin_invalid",
            PersistenceDatabaseTarget.Realm,
            [typeof(ExplicitIdEntity)],
            _ => { }));
        registry.RegisterEntity(typeof(ExplicitIdEntity));

        var exception = Assert.Throws<InvalidOperationException>(() => Validate(registry));

        Assert.Contains("public Serial Id", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateAndFreeze_LaterModuleRemapsEarlierEntity_ValidatesFinalSharedOrmOwnership()
    {
        var registry = new PersistenceModuleRegistry();
        registry.RegisterModule(CreateCharacterModule("plugin.characters", "plugin_characters"));
        registry.RegisterModule(new TestPersistenceModule(
            "plugin.inventory",
            "plugin_inventory",
            PersistenceDatabaseTarget.Realm,
            [typeof(InventoryEntity)],
            orm =>
            {
                MapInventory(orm, "plugin_inventory", "inventories");
                MapCharacter(orm, "plugin_inventory", "stolen_characters");
            }));
        registry.RegisterEntity(typeof(CharacterEntity));
        registry.RegisterEntity(typeof(InventoryEntity));

        var exception = Assert.Throws<InvalidOperationException>(() => Validate(registry));

        Assert.Contains("plugin.characters", exception.Message, StringComparison.Ordinal);
        Assert.Contains("plugin_characters", exception.Message, StringComparison.Ordinal);
        Assert.Contains("plugin_inventory", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RegisterEntity_DuplicateRegistration_RejectsImmediately()
    {
        var registry = new PersistenceModuleRegistry();
        registry.RegisterEntity(typeof(CharacterEntity));

        var exception = Assert.Throws<InvalidOperationException>(() => registry.RegisterEntity(typeof(CharacterEntity)));

        Assert.Contains(typeof(CharacterEntity).FullName!, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateAndFreeze_ModulesWithinTarget_PreserveDependencyRegistrationOrder()
    {
        var registry = new PersistenceModuleRegistry();
        registry.RegisterModule(CreateCharacterModule("plugin.z_dependency", "plugin_z"));
        registry.RegisterModule(CreateInventoryModule("plugin.a_dependent", "plugin_a"));
        registry.RegisterEntity(typeof(CharacterEntity));
        registry.RegisterEntity(typeof(InventoryEntity));

        var snapshot = Validate(registry);

        Assert.Equal(
            ["plugin.z_dependency", "plugin.a_dependent"],
            snapshot.Modules.Select(module => module.Module.Id));
    }

    [Fact]
    public void ValidateAndFreeze_SameSchemaAcrossIndependentTargets_IsAllowed()
    {
        var registry = new PersistenceModuleRegistry();
        registry.RegisterModule(new TestPersistenceModule(
            "plugin.accounts",
            "plugin_shared",
            PersistenceDatabaseTarget.Accounts,
            [typeof(CharacterEntity)],
            orm => MapCharacter(orm, "plugin_shared", "characters")));
        registry.RegisterModule(new TestPersistenceModule(
            "plugin.realm",
            "plugin_shared",
            PersistenceDatabaseTarget.Realm,
            [typeof(InventoryEntity)],
            orm => MapInventory(orm, "plugin_shared", "inventories")));
        registry.RegisterEntity(typeof(CharacterEntity));
        registry.RegisterEntity(typeof(InventoryEntity));
        using var accounts = PostgreSqlDatabase.Create(new PersistenceDatabaseOptions(
            PersistenceDatabaseTarget.Accounts,
            UnitConnectionString));
        using var realm = PostgreSqlDatabase.Create(new PersistenceDatabaseOptions(
            PersistenceDatabaseTarget.Realm,
            UnitConnectionString));

        var snapshot = registry.ValidateAndFreeze([accounts, realm]);

        Assert.Equal(2, snapshot.Modules.Count);
    }

    [Fact]
    public void ValidateAndFreeze_AlternatePrimaryKey_RejectsEntityIdentityMismatch()
    {
        var exception = AssertInvalidIdentity(typeof(InvalidKeyEntity), orm =>
            orm.CodeFirst.ConfigEntity<InvalidKeyEntity>(table =>
            {
                table.Name("plugin_invalid.entities");
                table.Property(entity => entity.Id).Name("id");
                table.Property(entity => entity.Code).Name("code").IsPrimary(true);
            }));

        Assert.Contains("sole", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Id", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateAndFreeze_CompositePrimaryKey_RejectsEntityIdentityMismatch()
    {
        var exception = AssertInvalidIdentity(typeof(CompositeKeyEntity), orm =>
            orm.CodeFirst.ConfigEntity<CompositeKeyEntity>(table =>
            {
                table.Name("plugin_invalid.entities");
                table.Property(entity => entity.Id).Name("id").IsPrimary(true);
                table.Property(entity => entity.Partition).Name("partition").IsPrimary(true);
            }));

        Assert.Contains("sole", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Id", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateAndFreeze_IdentityGeneratedSerial_RejectsEntityIdentityMismatch()
    {
        var exception = AssertInvalidIdentity(typeof(InvalidKeyEntity), orm =>
            orm.CodeFirst.ConfigEntity<InvalidKeyEntity>(table =>
            {
                table.Name("plugin_invalid.entities");
                table.Property(entity => entity.Id).Name("id").IsPrimary(true).IsIdentity(true);
                table.Property(entity => entity.Code).Name("code");
            }));

        Assert.Contains("application-assigned", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateAndFreeze_IgnoredSerialId_RejectsEntityIdentityMismatch()
    {
        var exception = AssertInvalidIdentity(typeof(IgnoredIdEntity), orm =>
            orm.CodeFirst.ConfigEntity<IgnoredIdEntity>(table =>
            {
                table.Name("plugin_invalid.entities");
                table.Property(entity => entity.Id).Name("id").IsIgnore(true);
                table.Property(entity => entity.Code).Name("code").IsPrimary(true);
            }));

        Assert.Contains("Id", exception.Message, StringComparison.Ordinal);
        Assert.Contains("map", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateAndFreeze_SerialIdMappedAsInt_RejectsStorageMismatch()
    {
        var exception = AssertInvalidIdentity(typeof(WrongSerialMapEntity), orm =>
            orm.CodeFirst.ConfigEntity<WrongSerialMapEntity>(table =>
            {
                table.Name("plugin_invalid.entities");
                table.Property(entity => entity.Id).Name("id").IsPrimary(true).MapType(typeof(int));
            }));

        Assert.Contains("long", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Id", exception.Message, StringComparison.Ordinal);
    }

    private static PersistenceModuleRegistrySnapshot Validate(PersistenceModuleRegistry registry)
    {
        using var database = PostgreSqlDatabase.Create(new PersistenceDatabaseOptions(
            PersistenceDatabaseTarget.Realm,
            UnitConnectionString));

        return registry.ValidateAndFreeze([database]);
    }

    private static InvalidOperationException AssertInvalidIdentity(Type entityType, Action<IFreeSql> configure)
    {
        var registry = new PersistenceModuleRegistry();
        registry.RegisterModule(new TestPersistenceModule(
            "plugin.invalid",
            "plugin_invalid",
            PersistenceDatabaseTarget.Realm,
            [entityType],
            configure));
        registry.RegisterEntity(entityType);

        return Assert.Throws<InvalidOperationException>(() => Validate(registry));
    }

    private static TestPersistenceModule CreateCharacterModule(string id, string schema)
    {
        return new TestPersistenceModule(
            id,
            schema,
            PersistenceDatabaseTarget.Realm,
            [typeof(CharacterEntity)],
            orm => MapCharacter(orm, schema, "characters"));
    }

    private static TestPersistenceModule CreateInventoryModule(string id, string schema)
    {
        return new TestPersistenceModule(
            id,
            schema,
            PersistenceDatabaseTarget.Realm,
            [typeof(InventoryEntity)],
            orm => MapInventory(orm, schema, "inventories"));
    }

    private static void MapCharacter(IFreeSql orm, string schema, string tableName)
    {
        orm.CodeFirst.ConfigEntity<CharacterEntity>(table =>
        {
            table.Name($"{schema}.{tableName}");
            table.Property(entity => entity.Id).Name("id").IsPrimary(true);
            table.Property(entity => entity.Name).Name("name").StringLength(160);
        });
    }

    private static void MapInventory(IFreeSql orm, string schema, string tableName)
    {
        orm.CodeFirst.ConfigEntity<InventoryEntity>(table =>
        {
            table.Name($"{schema}.{tableName}");
            table.Property(entity => entity.Id).Name("id").IsPrimary(true);
            table.Property(entity => entity.Balance).Name("balance");
        });
    }
}
