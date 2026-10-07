using DryIoc;
using Moongate.Persistence.Extensions;
using Moongate.Persistence.Interfaces;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces;
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
        _host.Container.AddPersistenceWorld<MobileEntity>()
            .AddPersistenceWorld<ItemEntity>()
            .AddPersistenceWorld<WorldStateEntity>();
        await CoreMigrationFiles.ApplyAsync(_host.Database, "world");
        await _host.Owner.InitializeAsync();
        _state = _host.Container.Resolve<IDataAccess<WorldStateEntity>>();
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
        Assert.Equal(
            (13L, null, true, 1.5),
            (again.Get("event.day"), again.Get("motto"), again.Get("open"), again.Get("rate"))
        );
        Assert.Equal(WorldStateEntity.RowId, Assert.Single(await _state.GetAllAsync()).Id);
    }

    [Fact]
    public async Task WorldSave_WritesTheLiveProps_TheFirstTimeAndWhenTheyChange()
    {
        await using var host = await HostPersistenceFixture.CreateAsync(false);
        host.Container.Register<IWorldPropsService, WorldPropsService>(Reuse.Singleton);
        host.Container.AddPersistenceWorld<MobileEntity>().AddPersistenceWorld<ItemEntity>().AddLiveWorldState();
        await CoreMigrationFiles.ApplyAsync(host.Database, "world");
        await host.Owner.InitializeAsync();
        var data = host.Container.Resolve<IDataAccess<WorldStateEntity>>();
        var service = (WorldPropsService)host.Container.Resolve<IWorldPropsService>();
        await service.StartAsync();
        service.Set("event.day", 12L);

        await host.Owner.SaveAllAsync();
        service.Set("event.day", 13L);
        await host.Owner.SaveAllAsync();

        var again = new WorldPropsService(data);
        await again.StartAsync();
        Assert.Equal(13L, again.Get("event.day"));
        Assert.Single(await data.GetAllAsync());
    }

    public async Task DisposeAsync()
    {
        await _host.DisposeAsync();
    }
}
