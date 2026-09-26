using Moongate.Persistence.Extensions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Tests.TestSupport.Persistence;

namespace Moongate.Tests.Integration.Server.Ultima.Entities;

[Collection(PostgresTestCollection.Name)]
public sealed class WorldMigrationsTests
{
    [Fact]
    public async Task ShippedWorldSql_MatchesMobileAndItemEntities()
    {
        await using var host = await HostPersistenceFixture.CreateAsync(false);
        host.Container.AddPersistenceWorld<MobileEntity>().AddPersistenceWorld<ItemEntity>();
        await CoreMigrationFiles.ApplyAsync(host.Database, "world");

        // Without development migrations, startup compares the model with the database and fails on any difference.
        await host.Owner.InitializeAsync();
    }
}
