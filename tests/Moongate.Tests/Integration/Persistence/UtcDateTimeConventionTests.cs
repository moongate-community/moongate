using Moongate.Persistence.Types.Persistence;
using Moongate.Tests.TestSupport.Persistence;

namespace Moongate.Tests.Integration.Persistence;

[Collection(PostgresTestCollection.Name)]
public sealed class UtcDateTimeConventionTests
{
    [Fact]
    public async Task UtcValue_IsStoredAsIsAndReadBackAsUtc()
    {
        await using var database = await new PostgreSqlFixture().CreateDatabaseAsync();
        using var fixture = new DevelopmentMigrationFixture(database.ConnectionString);
        await using var coordinator = fixture.Create(typeof(DevelopmentTimestampEntity));
        await coordinator.InitializeAsync();
        var orm = coordinator.GetDatabase(PersistenceDatabaseTarget.Realm).Orm;
        var createdAt = new DateTime(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);

        await orm.Insert(new DevelopmentTimestampEntity { Id = new(1), CreatedAt = createdAt }).ExecuteAffrowsAsync();
        var loaded = await orm.Select<DevelopmentTimestampEntity>().FirstAsync();

        Assert.Equal(createdAt, loaded.CreatedAt);
        Assert.Equal(DateTimeKind.Utc, loaded.CreatedAt.Kind);
        Assert.Null(loaded.LastSeenAt);
        Assert.Equal(
            "2026-09-25 12:00:00",
            await database.ScalarAsync<string>("SELECT to_char(created_at, 'YYYY-MM-DD HH24:MI:SS') FROM host_test.timestamps")
        );
    }

    [Fact]
    public async Task LocalValue_IsConvertedToUtcBeforeItIsWritten()
    {
        await using var database = await new PostgreSqlFixture().CreateDatabaseAsync();
        using var fixture = new DevelopmentMigrationFixture(database.ConnectionString);
        await using var coordinator = fixture.Create(typeof(DevelopmentTimestampEntity));
        await coordinator.InitializeAsync();
        var orm = coordinator.GetDatabase(PersistenceDatabaseTarget.Realm).Orm;
        var utc = new DateTime(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);

        await orm.Insert(
                new DevelopmentTimestampEntity { Id = new(1), CreatedAt = utc.ToLocalTime(), LastSeenAt = utc.ToLocalTime() }
            )
            .ExecuteAffrowsAsync();
        var loaded = await orm.Select<DevelopmentTimestampEntity>().FirstAsync();

        Assert.Equal(utc, loaded.CreatedAt);
        Assert.Equal(utc, loaded.LastSeenAt);
        Assert.Equal(DateTimeKind.Utc, loaded.LastSeenAt!.Value.Kind);
    }

    [Fact]
    public async Task Update_OfALocalValue_StoresUtc()
    {
        await using var database = await new PostgreSqlFixture().CreateDatabaseAsync();
        using var fixture = new DevelopmentMigrationFixture(database.ConnectionString);
        await using var coordinator = fixture.Create(typeof(DevelopmentTimestampEntity));
        await coordinator.InitializeAsync();
        var orm = coordinator.GetDatabase(PersistenceDatabaseTarget.Realm).Orm;
        var entity = new DevelopmentTimestampEntity { Id = new(1), CreatedAt = DateTime.UnixEpoch };
        await orm.Insert(entity).ExecuteAffrowsAsync();
        var utc = new DateTime(2026, 9, 25, 18, 30, 0, DateTimeKind.Utc);

        entity.LastSeenAt = utc.ToLocalTime();
        await orm.Update<DevelopmentTimestampEntity>().SetSource(entity).ExecuteAffrowsAsync();

        Assert.Equal(utc, (await orm.Select<DevelopmentTimestampEntity>().FirstAsync()).LastSeenAt);
    }
}
