using Moongate.Persistence.Interfaces;
using Moongate.Persistence.Internal;
using Moongate.Persistence.Tests.TestSupport.Persistence;
using Moongate.Persistence.Types.Persistence;

namespace Moongate.Persistence.Tests.Modules;

public sealed class PersistenceModuleRegistryTests
{
    private const string UnitConnectionString = "Host=localhost;Database=unit;Username=postgres;Pooling=false";

    [Theory, InlineData(PersistenceDatabaseTarget.Accounts), InlineData(PersistenceDatabaseTarget.Realm)]
    public void ValidateAndFreeze_DefaultColumnNames_UseSnakeCaseAndPreserveExplicitMappings(
        PersistenceDatabaseTarget target
    )
    {
        using var database = CreateDatabase(target);
        var registry = new PersistenceModuleRegistry();
        registry.RegisterEntity(typeof(ConventionNamedEntity), target);

        registry.ValidateAndFreeze([database]);

        var table = database.Orm.CodeFirst.GetTableByEntity(typeof(ConventionNamedEntity));
        Assert.Equal("auth.convention_entities", table.DbName);
        Assert.Equal(
            ["created_at", "display_label", "hash_password", "id", "username"],
            table.ColumnsByCs.Values.Select(column => column.Attribute.Name).Order(StringComparer.Ordinal)
        );
        var sql = database.Orm
            .Select<ConventionNamedEntity>()
            .Where(entity => entity.HashPassword == "test")
            .ToSql();
        Assert.Contains("\"hash_password\"", sql, StringComparison.Ordinal);
        Assert.Contains("\"display_label\"", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void PersistenceModuleContract_DoesNotExposeMappingMutationCallback()
    {
        Assert.DoesNotContain(
            typeof(IPersistenceModule).GetMethods(),
            method => string.Equals(method.Name, "Configure", StringComparison.Ordinal)
        );
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
    public void ValidateAndFreeze_AlternatePrimaryKey_RejectsEntityIdentityMismatch()
    {
        var exception = AssertInvalidIdentity(typeof(InvalidKeyEntity));

        Assert.Contains("sole", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Id", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateAndFreeze_AutomaticModules_AcceptValidUnderscoreSchemasWithStableDistinctIds()
    {
        Type[] entities =
        [
            typeof(CharacterEntity), typeof(ConsecutiveUnderscoreSchemaEntity),
            typeof(TrailingUnderscoreSchemaEntity), typeof(MaximumLengthSchemaEntity)
        ];
        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var target in Enum.GetValues<PersistenceDatabaseTarget>())
        {
            using var database = CreateDatabase(target);
            var first = new PersistenceModuleRegistry();
            var second = new PersistenceModuleRegistry();

            foreach (var entity in entities)
            {
                first.RegisterEntity(entity, target);
                second.RegisterEntity(entity, target);
            }

            var modules = first.ValidateAndFreeze([database]).Modules;
            var repeatedModules = second.ValidateAndFreeze([database]).Modules;

            Assert.Equal(entities.Length, modules.Count);
            Assert.Equal(modules.Select(item => item.Module.Id), repeatedModules.Select(item => item.Module.Id));

            foreach (var item in modules)
            {
                Assert.True(ids.Add(item.Module.Id));
            }
        }
    }

    [Fact]
    public void ValidateAndFreeze_CompositePrimaryKey_RejectsEntityIdentityMismatch()
    {
        var exception = AssertInvalidIdentity(typeof(CompositeKeyEntity));

        Assert.Contains("sole", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Id", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateAndFreeze_ConflictingSchemaDeclaration_DoesNotChangePreparedOwnerSql()
    {
        using var firstDatabase = CreateDatabase();
        using var invalidDatabase = CreateDatabase();
        var firstRegistry = Registry(PersistenceTestModules.Character());
        firstRegistry.ValidateAndFreeze([firstDatabase]);
        var before = firstDatabase.Orm.Select<CharacterEntity>().ToSql();
        var invalidRegistry = Registry(Module("plugin.characters", "plugin_wrong", typeof(CharacterEntity)));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            invalidRegistry.ValidateAndFreeze([invalidDatabase])
        );

        Assert.Contains("plugin_wrong", exception.Message, StringComparison.Ordinal);
        Assert.Contains("plugin_characters", exception.Message, StringComparison.Ordinal);
        Assert.Equal(before, firstDatabase.Orm.Select<CharacterEntity>().ToSql());
    }

    [Fact]
    public void ValidateAndFreeze_DuplicateModuleId_RejectsWholeBatch()
    {
        var registry = new PersistenceModuleRegistry();
        registry.RegisterModule(PersistenceTestModules.Character("plugin.same"));
        registry.RegisterModule(PersistenceTestModules.Inventory("plugin.same"));
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
        registry.RegisterModule(Module("plugin.characters", "plugin_shared", typeof(CharacterEntity)));
        registry.RegisterModule(Module("plugin.inventory", "plugin_shared", typeof(InventoryEntity)));
        registry.RegisterEntity(typeof(CharacterEntity));
        registry.RegisterEntity(typeof(InventoryEntity));

        var exception = Assert.Throws<InvalidOperationException>(() => Validate(registry));

        Assert.Contains("plugin_shared", exception.Message, StringComparison.Ordinal);
        Assert.Contains("duplicate", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateAndFreeze_ExplicitInterfaceId_RejectsMissingPublicIdentity()
    {
        var exception = AssertInvalidIdentity(typeof(ExplicitIdEntity));

        Assert.Contains("public Serial Id", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateAndFreeze_IdentityGeneratedSerial_RejectsEntityIdentityMismatch()
    {
        var exception = AssertInvalidIdentity(typeof(IdentitySerialEntity));

        Assert.Contains("application-assigned", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateAndFreeze_IgnoredSerialId_RejectsEntityIdentityMismatch()
    {
        var exception = AssertInvalidIdentity(typeof(IgnoredIdEntity));

        Assert.Contains("Id", exception.Message, StringComparison.Ordinal);
        Assert.Contains("map", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateAndFreeze_InvalidBatch_DoesNotChangePreparedOwnerSql()
    {
        using var firstDatabase = CreateDatabase();
        using var invalidDatabase = CreateDatabase();
        var firstRegistry = Registry(PersistenceTestModules.Character());
        firstRegistry.ValidateAndFreeze([firstDatabase]);
        var before = firstDatabase.Orm.Select<CharacterEntity>().ToSql();
        var invalidRegistry = Registry(
            PersistenceTestModules.Character(),
            Module("plugin.invalid", "plugin_invalid", typeof(NarrowSqlIdEntity))
        );

        Assert.Throws<InvalidOperationException>(() => invalidRegistry.ValidateAndFreeze([invalidDatabase]));

        Assert.Equal(before, firstDatabase.Orm.Select<CharacterEntity>().ToSql());
    }

    [Theory, InlineData(""), InlineData("Plugin_A"), InlineData("plugin-a"), InlineData("1plugin"), InlineData("plugin..a")]
    public void ValidateAndFreeze_InvalidSchema_Rejects(string schema)
    {
        var registry = new PersistenceModuleRegistry();
        registry.RegisterModule(Module("plugin.characters", schema, typeof(CharacterEntity)));
        registry.RegisterEntity(typeof(CharacterEntity));

        var exception = Assert.Throws<InvalidOperationException>(() => Validate(registry));

        Assert.Contains("schema", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateAndFreeze_ModuleEntityWasNotRegistered_RejectsNamedModuleAndType()
    {
        var registry = new PersistenceModuleRegistry();
        registry.RegisterModule(PersistenceTestModules.Character());

        var exception = Assert.Throws<InvalidOperationException>(() => Validate(registry));

        Assert.Contains("plugin.characters", exception.Message, StringComparison.Ordinal);
        Assert.Contains(typeof(CharacterEntity).FullName!, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateAndFreeze_ModulesWithinTarget_PreserveDependencyRegistrationOrder()
    {
        var registry = new PersistenceModuleRegistry();
        registry.RegisterModule(PersistenceTestModules.Character("plugin.z_dependency"));
        registry.RegisterModule(PersistenceTestModules.Inventory("plugin.a_dependent"));
        registry.RegisterEntity(typeof(CharacterEntity));
        registry.RegisterEntity(typeof(InventoryEntity));

        var snapshot = Validate(registry);

        Assert.Equal(
            ["plugin.z_dependency", "plugin.a_dependent"],
            snapshot.Modules.Select(module => module.Module.Id)
        );
    }

    [Fact]
    public void ValidateAndFreeze_NonEntityOwnedType_RejectsNamedType()
    {
        var registry = new PersistenceModuleRegistry();
        registry.RegisterModule(Module("plugin.invalid", "plugin_invalid", typeof(NotAnEntity)));

        var exception = Assert.Throws<InvalidOperationException>(() => Validate(registry));

        Assert.Contains(typeof(NotAnEntity).FullName!, exception.Message, StringComparison.Ordinal);
        Assert.Contains("IMoongateEntity", exception.Message, StringComparison.Ordinal);
    }

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
    public void ValidateAndFreeze_SameSchemaAcrossIndependentTargets_IsAllowed()
    {
        var registry = new PersistenceModuleRegistry();
        registry.RegisterModule(
            Module(
                "plugin.accounts",
                "plugin_shared",
                typeof(AccountsSharedEntity),
                PersistenceDatabaseTarget.Accounts
            )
        );
        registry.RegisterModule(
            Module(
                "plugin.realm",
                "plugin_shared",
                typeof(RealmSharedEntity)
            )
        );
        registry.RegisterEntity(typeof(AccountsSharedEntity));
        registry.RegisterEntity(typeof(RealmSharedEntity));
        using var accounts = CreateDatabase(PersistenceDatabaseTarget.Accounts);
        using var realm = CreateDatabase();

        var snapshot = registry.ValidateAndFreeze([accounts, realm]);

        Assert.Equal(2, snapshot.Modules.Count);
    }

    [Fact]
    public void ValidateAndFreeze_SerialIdMappedAsInt_RejectsClrStorageMismatch()
    {
        var exception = AssertInvalidIdentity(typeof(WrongSerialMapEntity));

        Assert.Contains("long", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Id", exception.Message, StringComparison.Ordinal);
    }

    [Theory, InlineData(typeof(NarrowSqlIdEntity), "integer"), InlineData(typeof(IncompatibleSqlIdEntity), "numeric")]
    public void ValidateAndFreeze_SerialIdWithIncompatibleDbType_RejectsEffectiveSqlStorage(
        Type entityType,
        string databaseType
    )
    {
        var exception = AssertInvalidIdentity(entityType);

        Assert.Contains("PostgreSQL bigint", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(databaseType, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateAndFreeze_TwoLiveOwnersOfAttributedType_KeepIdenticalSql()
    {
        using var firstDatabase = CreateDatabase();
        using var secondDatabase = CreateDatabase();
        var firstRegistry = Registry(PersistenceTestModules.Character());
        var secondRegistry = Registry(PersistenceTestModules.Character());

        firstRegistry.ValidateAndFreeze([firstDatabase]);
        var before = firstDatabase.Orm.Select<CharacterEntity>().ToSql();
        secondRegistry.ValidateAndFreeze([secondDatabase]);
        var after = firstDatabase.Orm.Select<CharacterEntity>().ToSql();
        var second = secondDatabase.Orm.Select<CharacterEntity>().ToSql();

        Assert.Equal(before, after);
        Assert.Equal(before, second);
        Assert.Contains("\"plugin_characters\".\"characters\"", before, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateAndFreeze_TwoTypesMappedToSameTable_RejectsCollision()
    {
        var registry = new PersistenceModuleRegistry();
        registry.RegisterModule(
            new TestPersistenceModule(
                "plugin.colliding",
                "plugin_colliding",
                PersistenceDatabaseTarget.Realm,
                [typeof(FirstCollidingEntity), typeof(SecondCollidingEntity)]
            )
        );
        registry.RegisterEntity(typeof(FirstCollidingEntity));
        registry.RegisterEntity(typeof(SecondCollidingEntity));

        var exception = Assert.Throws<InvalidOperationException>(() => Validate(registry));

        Assert.Contains("plugin_colliding.entities", exception.Message, StringComparison.Ordinal);
        Assert.Contains("duplicate", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateAndFreeze_TypeOwnedByTwoModules_RejectsNamedType()
    {
        var registry = new PersistenceModuleRegistry();
        registry.RegisterModule(Module("plugin.one", "plugin_characters", typeof(CharacterEntity)));
        registry.RegisterModule(Module("plugin.two", "plugin_two", typeof(CharacterEntity)));
        registry.RegisterEntity(typeof(CharacterEntity));

        var exception = Assert.Throws<InvalidOperationException>(() => Validate(registry));

        Assert.Contains(typeof(CharacterEntity).FullName!, exception.Message, StringComparison.Ordinal);
        Assert.Contains("plugin.one", exception.Message, StringComparison.Ordinal);
        Assert.Contains("plugin.two", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateAndFreeze_UnmappedWritableProperty_RejectsNamedModuleEntityAndProperty()
    {
        var registry = Registry(Module("plugin.mapping", "plugin_mapping", typeof(UnmappedPropertyEntity)));

        var exception = Assert.Throws<InvalidOperationException>(() => Validate(registry));

        Assert.Contains("plugin.mapping", exception.Message, StringComparison.Ordinal);
        Assert.Contains(typeof(UnmappedPropertyEntity).FullName!, exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(UnmappedPropertyEntity.Position), exception.Message, StringComparison.Ordinal);
    }

    private static InvalidOperationException AssertInvalidIdentity(Type entityType)
    {
        var registry = Registry(Module("plugin.invalid", "plugin_invalid", entityType));

        return Assert.Throws<InvalidOperationException>(() => Validate(registry));
    }

    private static PostgreSqlDatabase CreateDatabase(PersistenceDatabaseTarget target = PersistenceDatabaseTarget.Realm)
    {
        return PostgreSqlDatabase.Create(new(target, UnitConnectionString));
    }

    private static TestPersistenceModule Module(
        string id,
        string schema,
        Type entityType,
        PersistenceDatabaseTarget target = PersistenceDatabaseTarget.Realm
    )
    {
        return new(id, schema, target, [entityType]);
    }

    private static PersistenceModuleRegistry Registry(params TestPersistenceModule[] modules)
    {
        var registry = new PersistenceModuleRegistry();

        foreach (var module in modules)
        {
            registry.RegisterModule(module);

            foreach (var entityType in module.EntityTypes)
            {
                registry.RegisterEntity(entityType);
            }
        }

        return registry;
    }

    private static PersistenceModuleRegistrySnapshot Validate(PersistenceModuleRegistry registry)
    {
        using var database = CreateDatabase();

        return registry.ValidateAndFreeze([database]);
    }
}
