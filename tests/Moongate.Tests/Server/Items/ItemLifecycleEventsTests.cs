using Moongate.Core.Extensions;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Events;
using Moongate.Server.Services.Items;
using Moongate.Tests.Support;
using Moongate.Ultima.Types;
using SquidStd.Services.Core.Services;

namespace Moongate.Tests.Server.Items;

public class ItemLifecycleEventsTests
{
    [Fact]
    public void Equip_PublishesItemEquipped()
    {
        var persistence = new FakePersistenceService();
        var bus = new EventBusService();
        ItemEquippedEvent? published = null;
        bus.Subscribe<ItemEquippedEvent>(
            (evt, _) =>
            {
                published = evt;

                return Task.CompletedTask;
            }
        );

        var service = new ItemService(persistence, eventBus: bus);
        var mobile = new MobileEntity { MapId = 1, Position = new(100, 100, 0) };
        persistence.Store<MobileEntity>().UpsertAsync(mobile).WaitSync();

        var shirt = new ItemEntity { TemplateId = "shirt", ItemId = 5399 };
        service.Create(shirt);
        service.Equip(mobile, shirt, LayerType.Shirt);

        Assert.NotNull(published);
        Assert.Equal(shirt.Id, published!.Item);
        Assert.Equal(mobile.Id, published.Mobile);
        Assert.Equal(LayerType.Shirt, published.Layer);
    }

    [Fact]
    public void Unequip_PublishesItemUnequipped()
    {
        var persistence = new FakePersistenceService();
        var bus = new EventBusService();
        ItemUnequippedEvent? published = null;
        bus.Subscribe<ItemUnequippedEvent>(
            (evt, _) =>
            {
                published = evt;

                return Task.CompletedTask;
            }
        );

        var service = new ItemService(persistence, eventBus: bus);
        var mobile = new MobileEntity { MapId = 1, Position = new(100, 100, 0) };
        persistence.Store<MobileEntity>().UpsertAsync(mobile).WaitSync();

        var shirt = new ItemEntity { TemplateId = "shirt", ItemId = 5399 };
        service.Create(shirt);
        service.Equip(mobile, shirt, LayerType.Shirt);
        service.Unequip(mobile, LayerType.Shirt);

        Assert.NotNull(published);
        Assert.Equal(shirt.Id, published!.Item);
        Assert.Equal(LayerType.Shirt, published.Layer);
    }

    [Fact]
    public void Unequip_OfAnEmptyLayer_PublishesNothing()
    {
        var persistence = new FakePersistenceService();
        var bus = new EventBusService();
        var published = 0;
        bus.Subscribe<ItemUnequippedEvent>(
            (_, _) =>
            {
                published++;

                return Task.CompletedTask;
            }
        );

        var service = new ItemService(persistence, eventBus: bus);
        var mobile = new MobileEntity { MapId = 1, Position = new(100, 100, 0) };
        persistence.Store<MobileEntity>().UpsertAsync(mobile).WaitSync();

        service.Unequip(mobile, LayerType.Shirt);

        Assert.Equal(0, published);
    }
}
