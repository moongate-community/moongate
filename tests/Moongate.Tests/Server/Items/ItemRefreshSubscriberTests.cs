using Moongate.Core.Extensions;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Outgoing;
using Moongate.Persistence.Entities;
using Moongate.Server.Services.Items;
using Moongate.Server.Subscribers;
using Moongate.Tests.Support;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Items;

public class ItemRefreshSubscriberTests
{
    [Fact]
    public async Task OnItemChanged_ContainedItem_SendsToTheOwnerAndTheOpeners()
    {
        var (subscriber, world, items, persistence) = Build(out var openers);
        var mobile = new MobileEntity { MapId = 1, Position = new(10, 20, 0) };
        persistence.Store<MobileEntity>().UpsertAsync(mobile).WaitSync();

        var backpack = new ItemEntity { ItemId = 3701 };
        items.Create(backpack);
        items.Equip(mobile, backpack, LayerType.Backpack);

        var coin = new ItemEntity { ItemId = 3821 };
        items.Create(coin);
        items.AddToContainer(backpack, coin, new(10, 10));

        var opener = new MobileEntity { MapId = 1, Position = new(11, 20, 0) };
        persistence.Store<MobileEntity>().UpsertAsync(opener).WaitSync();
        openers.Opened(backpack.Id, opener.Id);

        await subscriber.OnItemChanged(new(coin.Id), CancellationToken.None);

        // The wearer and the registered opener, one packet each.
        Assert.Equal(2, world.ToPlayer.Count);
        Assert.All(world.ToPlayer, sent => Assert.IsType<AddItemToContainerPacket>(sent.Packet));
        Assert.Contains(world.ToPlayer, sent => sent.MobileId == mobile.Id);
        Assert.Contains(world.ToPlayer, sent => sent.MobileId == opener.Id);
    }

    [Fact]
    public async Task OnItemChanged_OpenerOutOfRange_IsDroppedAndNotSentTo()
    {
        var (subscriber, world, items, persistence) = Build(out var openers);
        var mobile = new MobileEntity { MapId = 1, Position = new(10, 20, 0) };
        persistence.Store<MobileEntity>().UpsertAsync(mobile).WaitSync();

        var backpack = new ItemEntity { ItemId = 3701 };
        items.Create(backpack);
        items.Equip(mobile, backpack, LayerType.Backpack);

        var coin = new ItemEntity { ItemId = 3821 };
        items.Create(coin);
        items.AddToContainer(backpack, coin, new(10, 10));

        // Walked far away with the gump still notionally open.
        var wanderer = new MobileEntity { MapId = 1, Position = new(500, 500, 0) };
        persistence.Store<MobileEntity>().UpsertAsync(wanderer).WaitSync();
        openers.Opened(backpack.Id, wanderer.Id);

        await subscriber.OnItemChanged(new(coin.Id), CancellationToken.None);

        Assert.DoesNotContain(world.ToPlayer, sent => sent.MobileId == wanderer.Id);
        Assert.Empty(openers.OpenersOf(backpack.Id));
    }

    [Fact]
    public async Task OnItemChanged_UnknownItem_SendsNothing()
    {
        var (subscriber, world, _, _) = Build();

        await subscriber.OnItemChanged(new((Serial)0xDEAD), CancellationToken.None);

        Assert.Empty(world.InRange);
        Assert.Empty(world.ToPlayer);
    }

    [Fact]
    public async Task OnItemChanged_WornItem_SendsWornItemToPlayersInRange()
    {
        var (subscriber, world, items, persistence) = Build();
        var mobile = new MobileEntity { MapId = 1, Position = new(10, 20, 0) };
        persistence.Store<MobileEntity>().UpsertAsync(mobile).WaitSync();

        var shirt = new ItemEntity { ItemId = 5399 };
        items.Create(shirt);
        items.Equip(mobile, shirt, LayerType.Shirt);

        await subscriber.OnItemChanged(new(shirt.Id), CancellationToken.None);

        var sent = Assert.Single(world.InRange);
        Assert.IsType<WornItemPacket>(sent.Packet);
    }

    private static (ItemRefreshSubscriber Subscriber, RecordingWorldService World, ItemService Items,
        FakePersistenceService Persistence) Build()
        => Build(out _);

    private static (ItemRefreshSubscriber Subscriber, RecordingWorldService World, ItemService Items,
        FakePersistenceService Persistence) Build(out ContainerOpenerRegistry openers)
    {
        var persistence = new FakePersistenceService();
        var items = new ItemService(persistence);
        var world = new RecordingWorldService();
        openers = new();

        return (new(items, persistence, openers, world), world, items, persistence);
    }
}
