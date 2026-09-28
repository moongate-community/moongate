using Moongate.Persistence.Interfaces;
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
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class CharacterLeaveWorldServiceTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly Container _events = new();
    private readonly MobileService _mobiles = new(new StubMovementService(), TestSectors.Create());
    private readonly RecordingWorldTransactionService _world = new();
    private readonly ItemService _items = TestItems.Create();
    private readonly RecordingWorldViewService _view = new();
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
        var saved = Assert.Single(_world.Mobiles.Upserted);
        Assert.NotSame(_aria, saved);
        Assert.Equal((_aria.Id, new Point3D(1497, 1628, 12)), (saved.Id, saved.Location));
        Assert.Same(saved, Assert.Single(_left).Character);
    }

    [Fact]
    public async Task OnSessionClosed_TellsTheWorldViewWhileTheCharacterIsStillInTheWorld()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var session = await SessionWithCharacterAsync(fixture);
        var service = Service();
        bool? inWorld = null;
        _view.OnCall = _ => inWorld = _mobiles.IsInWorld(_aria.Id);

        await fixture.ExecuteOnLoopAsync(() => service.OnSessionClosed(session));
        await service.StopAsync().WaitAsync(Timeout);

        Assert.Equal(["Left 2"], _view.Calls);
        Assert.True(inWorld);
        Assert.False(_mobiles.IsInWorld(_aria.Id));
    }

    [Fact]
    public async Task OnSessionClosed_TheCharacterLeavesTheWorldBeforeTheSaveFinishes()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var session = await SessionWithCharacterAsync(fixture);
        var hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _world.Hold = hold.Task;
        var service = Service();

        await fixture.ExecuteOnLoopAsync(() => service.OnSessionClosed(session));

        Assert.False(_mobiles.IsInWorld(_aria.Id));
        var stopping = service.StopAsync();
        Assert.False(stopping.IsCompleted);
        hold.SetResult();
        await stopping.WaitAsync(Timeout);
        Assert.Single(_world.Mobiles.Upserted);
    }

    [Fact]
    public async Task WaitForAccountAsync_CompletesOnlyAfterThatAccountsLeaveSave()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var session = await SessionWithCharacterAsync(fixture);
        var hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _world.Hold = hold.Task;
        var service = Service();
        await fixture.ExecuteOnLoopAsync(() => service.OnSessionClosed(session));

        var waiting = service.WaitForAccountAsync(new Serial(42));

        Assert.False(waiting.IsCompleted);
        Assert.True(service.WaitForAccountAsync(new Serial(99)).IsCompleted);
        hold.SetResult();
        await waiting.WaitAsync(Timeout);
        Assert.Single(_world.Mobiles.Upserted);
    }

    [Fact]
    public async Task OnSessionClosed_AFailedSave_StillRemovesTheCharacterAndPublishesTheEvent()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var session = await SessionWithCharacterAsync(fixture);
        _world.Mobiles.FailUpserts = new InvalidOperationException("database down");
        var service = Service();

        await fixture.ExecuteOnLoopAsync(() => service.OnSessionClosed(session));
        await service.StopAsync().WaitAsync(Timeout);

        Assert.False(_mobiles.IsInWorld(_aria.Id));
        Assert.Empty(_world.Mobiles.Upserted);
        Assert.Single(_left);
    }

    [Fact]
    public async Task OnSessionClosed_RemovesTheCharactersItemsAndSavesThemAfterTheCharacter()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var session = await SessionWithCharacterAsync(fixture);
        var (backpack, coin, ground) = CarriedItems();
        _world.Mobiles.OnUpsert = mobile => _saveOrder.Add(mobile.Id);
        _world.Items.OnUpsert = item => _saveOrder.Add(item.Id);
        var service = Service();

        await fixture.ExecuteOnLoopAsync(() => service.OnSessionClosed(session));
        await service.StopAsync().WaitAsync(Timeout);

        Assert.Equal([ground], _items.Items);
        Assert.Equal(_aria.Id, _saveOrder[0]);
        Assert.Equal([backpack.Id, coin.Id], _saveOrder.Skip(1).Order());
        Assert.All(_world.Items.Upserted, saved => Assert.NotSame(backpack, saved));
    }

    [Fact]
    public async Task OnSessionClosed_SavesTheCharacterItsItemsAndDeletesItsMergedStacksInOneTransaction()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var session = await SessionWithCharacterAsync(fixture);
        var (_, coin, _) = CarriedItems();
        _items.Absorb(coin);
        var service = Service();

        await fixture.ExecuteOnLoopAsync(() => service.OnSessionClosed(session));
        await service.StopAsync().WaitAsync(Timeout);

        Assert.Equal(1, _world.Transactions);
        Assert.Equal([coin.Id], _world.Deleted);
        Assert.Empty(_items.TombstonesOf(_aria.Id));
    }

    [Fact]
    public async Task OnSessionClosed_AFailedItemSave_SavesNothing_DropsTheDeletionsAndPublishes()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var session = await SessionWithCharacterAsync(fixture);
        var (backpack, coin, _) = CarriedItems();
        var absorbed = new ItemEntity { Id = new(0x40000009), TemplateId = "gold", ItemId = 0x0EED, Amount = 5 };
        absorbed.PutInContainer(backpack.Id, new Point2D(1, 1));
        _items.Add([absorbed]);
        _items.Absorb(absorbed);
        _world.Items.OnUpsert = item =>
        {
            if (item.Id == coin.Id)
            {
                throw new InvalidOperationException("database down");
            }
        };
        var service = Service();

        await fixture.ExecuteOnLoopAsync(() => service.OnSessionClosed(session));
        await service.StopAsync().WaitAsync(Timeout);

        Assert.Empty(_world.Mobiles.Upserted);
        Assert.Empty(_world.Items.Upserted);
        // The database still holds both stacks as before the merge: deleting the absorbed one now would lose it.
        Assert.Empty(_items.TombstonesOf(_aria.Id));
        Assert.Empty(((IPersistenceDeletionSource)_items).Capture());
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
        Assert.Empty(_world.Mobiles.Upserted);
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

        Assert.Empty(_world.Mobiles.Upserted);
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

        return new(_mobiles, _items, _view, _world, bus);
    }
}
