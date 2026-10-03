using DryIoc;
using Moongate.Persistence.Extensions;
using Moongate.Persistence.Interfaces;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Persistence;

namespace Moongate.Tests.Integration.Server.Ultima.Entities;

[Collection(PostgresTestCollection.Name)]
public sealed class WorldStateEntityPersistenceTests : IAsyncLifetime
{
    private HostPersistenceFixture _host = null!;
    private IDataAccess<WorldStateEntity> _state = null!;

    public async Task InitializeAsync()
    {
        _host = await HostPersistenceFixture.CreateAsync(false);
        _host.Container.AddPersistenceWorld<MobileEntity>().AddPersistenceWorld<ItemEntity>().AddPersistenceWorld<WorldStateEntity>();
        await CoreMigrationFiles.ApplyAsync(_host.Database, "world");
        await _host.Owner.InitializeAsync();
        _state = _host.Container.Resolve<IDataAccess<WorldStateEntity>>();
    }

    public async Task DisposeAsync()
    {
        await _host.DisposeAsync();
    }

    [Fact]
    public async Task ANewWorld_HasNoRow()
    {
        var service = new WorldPropsService(_state);

        await service.StartAsync();

        Assert.Null(service.State.Props);
        Assert.Empty(await _state.GetAllAsync());
    }

    [Fact]
    public async Task TheProps_ComeBackAsTheyWereSaved_AndTheRowKeepsItsId()
    {
        var service = new WorldPropsService(_state);
        await service.StartAsync();
        service.Set("event.day", 12L);
        service.Set("motto", "hail");
        service.Set("open", true);
        service.Set("rate", 1.5);

        await _state.UpsertAsync(service.State.Snapshot());
        service.Set("event.day", 13L);
        service.Set("motto", null);
        await _state.UpsertAsync(service.State.Snapshot());

        var again = new WorldPropsService(_state);
        await again.StartAsync();
        Assert.Equal((13L, null, true, 1.5), (again.Get("event.day"), again.Get("motto"), again.Get("open"), again.Get("rate")));
        Assert.Equal(WorldStateEntity.RowId, Assert.Single(await _state.GetAllAsync()).Id);
    }
}
