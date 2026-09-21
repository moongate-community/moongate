using Moongate.Core.Primitives;
using Moongate.Persistence.Data.Config;
using Moongate.Persistence.Internal;
using Moongate.Persistence.Tests.TestSupport.Persistence;
using Moongate.Persistence.Types.Persistence;

namespace Moongate.Persistence.Tests.Integration.Mapping;

[Collection(PostgreSqlCollection.Name)]
public sealed class SerialTypeHandlerTests
{
    private readonly PostgreSqlFixture _fixture;

    public SerialTypeHandlerTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData(1L)]
    [InlineData(1073741824L)]
    [InlineData(2147483647L)]
    [InlineData(2147483648L)]
    [InlineData(4294967295L)]
    public async Task Serial_FullUnsignedRange_RoundTripsAsBigint(long value)
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var coordinator = CreateCoordinator(database);
        await coordinator.SynchronizeAsync();
        var orm = coordinator.GetDatabase(PersistenceDatabaseTarget.Realm).Orm;
        var id = new Serial((uint)value);

        await orm.Insert(new CharacterEntity { Id = id, Name = "round trip" }).ExecuteAffrowsAsync();
        var loaded = await orm.Select<CharacterEntity>().Where(entity => entity.Id == id).FirstAsync();

        Assert.NotNull(loaded);
        Assert.Equal(id, loaded.Id);
        Assert.Equal(
            value,
            await database.ScalarAsync<long>(
                "SELECT id FROM plugin_characters.characters"
            )
        );
        Assert.Equal(
            "bigint",
            await database.ScalarAsync<string>(
                "SELECT data_type FROM information_schema.columns " +
                "WHERE table_schema = 'plugin_characters' AND table_name = 'characters' AND column_name = 'id'"
            )
        );
    }

    [Fact]
    public async Task Serial_DatabaseValueAboveUIntRange_RejectsCheckedConversion()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        await using var coordinator = CreateCoordinator(database);
        await coordinator.SynchronizeAsync();
        await database.ExecuteAsync(
            "INSERT INTO plugin_characters.characters (id, name) VALUES (4294967296, 'invalid')"
        );

        var exception = await Record.ExceptionAsync(() =>
            coordinator.GetDatabase(PersistenceDatabaseTarget.Realm).Orm.Select<CharacterEntity>().ToListAsync()
        );

        Assert.NotNull(exception);
        Assert.IsType<OverflowException>(exception.GetBaseException());
    }

    private static PersistenceSchemaCoordinator CreateCoordinator(PostgreSqlTestDatabase database)
    {
        var module = PersistenceTestModules.Character();
        var registry = new PersistenceModuleRegistry();
        registry.RegisterModule(module);
        registry.RegisterEntity(typeof(CharacterEntity));
        var options = new PostgreSqlPersistenceOptions(
            [
                new PersistenceDatabaseOptions(PersistenceDatabaseTarget.Realm, database.ConnectionString)
            ]
        );

        return new PersistenceSchemaCoordinator(options, registry);
    }
}
