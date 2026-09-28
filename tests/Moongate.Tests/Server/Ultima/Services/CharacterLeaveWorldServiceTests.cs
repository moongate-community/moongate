using DryIoc;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Data.Events;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Persistence;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class CharacterLeaveWorldServiceTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly Container _events = new();
    private readonly MobileService _mobiles = new(new StubMovementService());
    private readonly RecordingDataAccess<MobileEntity> _data = new();
    private readonly ItemService _items = new();
    private readonly RecordingDataAccess<ItemEntity> _itemData = new();
    private readonly List<Serial> _saveOrder = [];
    private readonly List<CharacterLeftWorldEvent> _left = [];
    private readonly MobileEntity _aria = new()
    {
        Id = new(2), AccountId = new Serial(42), Name = "Aria", Map = MapType.Trammel,
        Location = new Point3D(1497, 1628, 12)
    };

    public void Dispose()
    {
        _events.Dispose();
    }

    [Fact]
    public async Task OnSessionClosed_SavesTheCharacterRemovesItAndPublishesTheEvent()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var session = await SessionWithCharacterAsync(fixture);
        var service = Service();

        await fixture.ExecuteOnLoopAsync(() => service.OnSessionClosed(session));
        await service.StopAsync().WaitAsync(Timeout);

        Assert.False(_mobiles.IsInWorld(_aria.Id));
        var saved = Assert.Single(_data.Upserted);
        Assert.NotSame(_aria, saved);
        Assert.Equal((_aria.Id, new Point3D(1497, 1628, 12)), (saved.Id, saved.Location));
        Assert.Same(saved, Assert.Single(_left).Character);
    }

    [Fact]
    public async Task OnSessionClosed_TheCharacterLeavesTheWorldBeforeTheSaveFinishes()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var session = await SessionWithCharacterAsync(fixture);
        var hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _data.HoldUpserts = hold.Task;
        var service = Service();

        await fixture.ExecuteOnLoopAsync(() => service.OnSessionClosed(session));

        Assert.False(_mobiles.IsInWorld(_aria.Id));
        var stopping = service.StopAsync();
        Assert.False(stopping.IsCompleted);
        hold.SetResult();
        await stopping.WaitAsync(Timeout);
        Assert.Single(_data.Upserted);
    }

    [Fact]
    public async Task OnSessionClosed_AFailedSave_StillRemovesTheCharacterAndPublishesTheEvent()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var session = await SessionWithCharacterAsync(fixture);
        _data.FailUpserts = new InvalidOperationException("database down");
        var service = Service();

        await fixture.ExecuteOnLoopAsync(() => service.OnSessionClosed(session));
        await service.StopAsync().WaitAsync(Timeout);

        Assert.False(_mobiles.IsInWorld(_aria.Id));
        Assert.Empty(_data.Upserted);
        Assert.Single(_left);
    }

    [Fact]
    public async Task OnSessionClosed_RemovesTheCharactersItemsAndSavesThemAfterTheCharacter()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var session = await SessionWithCharacterAsync(fixture);
        var (backpack, coin, ground) = CarriedItems();
        _data.OnUpsert = mobile => _saveOrder.Add(mobile.Id);
        _itemData.OnUpsert = item => _saveOrder.Add(item.Id);
        var service = Service();

        await fixture.ExecuteOnLoopAsync(() => service.OnSessionClosed(session));
        await service.StopAsync().WaitAsync(Timeout);

        Assert.Equal([ground], _items.Items);
        Assert.Equal(_aria.Id, _saveOrder[0]);
        Assert.Equal([backpack.Id, coin.Id], _saveOrder.Skip(1).Order());
        Assert.All(_itemData.Upserted, saved => Assert.NotSame(backpack, saved));
    }

    [Fact]
    public async Task OnSessionClosed_AFailedItemSave_StillSavesTheOthersAndPublishes()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var session = await SessionWithCharacterAsync(fixture);
        var (backpack, coin, _) = CarriedItems();
        _itemData.OnUpsert = item =>
        {
            if (item.Id == backpack.Id)
            {
                throw new InvalidOperationException("database down");
            }
        };
        var service = Service();

        await fixture.ExecuteOnLoopAsync(() => service.OnSessionClosed(session));
        await service.StopAsync().WaitAsync(Timeout);

        Assert.Equal([coin.Id], _itemData.Upserted.Select(item => item.Id));
        Assert.Single(_left);
    }

    [Fact]
    public async Task OnSessionClosed_WithoutACharacter_DoesNothing()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var session = new SessionService(fixture.Loop).GetOrCreate(fixture.Client);
        _mobiles.EnterWorld(_aria);
        var service = Service();

        await fixture.ExecuteOnLoopAsync(() => service.OnSessionClosed(session));
        await service.StopAsync().WaitAsync(Timeout);

        Assert.True(_mobiles.IsInWorld(_aria.Id));
        Assert.Empty(_data.Upserted);
        Assert.Empty(_left);
    }

    [Fact]
    public async Task OnSessionClosed_ACharacterNoLongerLive_DoesNothing()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var session = await SessionWithCharacterAsync(fixture);
        _mobiles.LeaveWorld(_aria.Id);
        var service = Service();

        await fixture.ExecuteOnLoopAsync(() => service.OnSessionClosed(session));
        await service.StopAsync().WaitAsync(Timeout);

        Assert.Empty(_data.Upserted);
        Assert.Empty(_left);
    }

    private async Task<GameSession> SessionWithCharacterAsync(SessionFixture fixture)
    {
        var session = new SessionService(fixture.Loop).GetOrCreate(fixture.Client);
        await fixture.ExecuteOnLoopAsync(() => session.Set(SessionKeys.CharacterId, _aria.Id));
        _mobiles.EnterWorld(_aria);

        return session;
    }

    private (ItemEntity Backpack, ItemEntity Coin, ItemEntity Ground) CarriedItems()
    {
        var backpack = new ItemEntity { Id = new(0x40000001), TemplateId = "backpack", ItemId = 0x0E75, Amount = 1 };
        backpack.Equip(_aria.Id, LayerType.Backpack);
        var coin = new ItemEntity { Id = new(0x40000002), TemplateId = "gold", ItemId = 0x0EED, Amount = 5 };
        coin.PutInContainer(backpack.Id, new Point2D(44, 65));
        var ground = new ItemEntity { Id = new(0x40000003), TemplateId = "gold", ItemId = 0x0EED, Amount = 5 };
        ground.PlaceOnGround(MapType.Trammel, new Point3D(1, 1, 0));
        _items.Add([backpack, coin, ground]);

        return (backpack, coin, ground);
    }

    private CharacterLeaveWorldService Service()
    {
        _events.RegisterMoongateEventBus();
        var bus = _events.Resolve<IMoongateEventBus>();
        bus.Subscribe<CharacterLeftWorldEvent>((evt, _) =>
            {
                lock (_left)
                {
                    _left.Add(evt);
                }

                return Task.CompletedTask;
            }
        );

        return new(_mobiles, _data, _items, _itemData, bus);
    }
}
