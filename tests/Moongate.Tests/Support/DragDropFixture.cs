using Moongate.Core.Extensions;
using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Services.Items;
using Moongate.Ultima.Types;
using Moongate.UO.Data.Hues;
using SquidStd.Services.Core.Services;

namespace Moongate.Tests.Support;

/// <summary>
/// A live <see cref="DragDropService" /> over fake persistence, with the least world a drag needs: an
/// actor standing at (100, 100, 0) on map 1 wearing a backpack, and templates for the three things the
/// tests move around. <see cref="StubWorldService" /> swallows the visibility packets — this fixture
/// is about where items end up, not what the client is told.
/// </summary>
public sealed class DragDropFixture
{
    public FakePersistenceService Persistence { get; }

    public EventBusService EventBus { get; } = new();

    public ItemService Items { get; }

    public DragDropService Service { get; }

    public MobileEntity Actor { get; }

    public ItemEntity Backpack { get; }

    public ItemEntity Gold { get; private set; } = null!;

    public ItemEntity Bag { get; private set; } = null!;

    private DragDropFixture()
    {
        Persistence = new();
        Items = new(Persistence);

        var templates = new ItemTemplateService();
        templates.Register(
            new() { Id = "backpack", Name = "Backpack", Category = "Container", ItemId = 3701, IsMovable = true }
        );
        templates.Register(
            new()
            {
                Id = "gold", Name = "Gold", Category = "Currency", ItemId = 3821,
                IsMovable = true, Stackable = true
            }
        );
        templates.Register(
            new() { Id = "bag", Name = "Bag", Category = "Container", ItemId = 3702, IsMovable = true }
        );

        Service = new(
            Items,
            new ItemFactoryService(templates, new Random(1)),
            templates,
            new StubWorldService(),
            eventBus: EventBus
        );

        Actor = new() { MapId = 1, Position = new(100, 100, 0) };
        Persistence.Store<MobileEntity>().UpsertAsync(Actor).WaitSync();

        Backpack = new() { TemplateId = "backpack", ItemId = 3701 };
        Items.Create(Backpack);
        Items.Equip(Actor, Backpack, LayerType.Backpack);
    }

    /// <summary>An actor carrying a single stack of <paramref name="amount" /> gold in the backpack.</summary>
    public static DragDropFixture WithGoldInBackpack(int amount)
    {
        var fixture = new DragDropFixture();

        fixture.Gold = new() { TemplateId = "gold", ItemId = 3821, Amount = amount, Hue = new Hue(0) };
        fixture.Items.Create(fixture.Gold);
        fixture.Items.AddToContainer(fixture.Backpack, fixture.Gold, new(50, 70));

        return fixture;
    }

    /// <summary>An actor carrying an empty bag inside the backpack, for the nesting cases.</summary>
    public static DragDropFixture WithBagInBackpack()
    {
        var fixture = new DragDropFixture();

        fixture.Bag = new() { TemplateId = "bag", ItemId = 3702, Amount = 1 };
        fixture.Items.Create(fixture.Bag);
        fixture.Items.AddToContainer(fixture.Backpack, fixture.Bag, new(50, 70));

        return fixture;
    }

    public IReadOnlyList<ItemEntity> BackpackContents()
        => Items.GetContents(Backpack.Id);

    /// <summary>Drops the bag out of the world, to exercise the bounce falling past a dead origin.</summary>
    public void DeleteTheBag()
        => Items.Delete(Bag.Id);

    /// <summary>Takes the backpack off the actor, to exercise the bounce falling all the way to the feet.</summary>
    public void RemoveTheBackpack()
        => Items.Unequip(Actor, LayerType.Backpack);

    /// <summary>Puts a stack of gold inside the bag, for the nested-origin bounce case.</summary>
    public ItemEntity PutGoldInTheBag(int amount)
    {
        var gold = new ItemEntity { TemplateId = "gold", ItemId = 3821, Amount = amount, Hue = new Hue(0) };
        Items.Create(gold);
        Items.AddToContainer(Bag, gold, new(20, 30));

        return gold;
    }
}
