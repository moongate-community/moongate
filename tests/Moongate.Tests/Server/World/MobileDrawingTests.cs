using Moongate.Core.Extensions;
using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Services.Items;
using Moongate.Server.Services.World;
using Moongate.Tests.Support;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.World;

/// <summary>
/// The equipment list a mobile is drawn with. Its two rules are easy to lose in a move, and were
/// untested while the method had a single private caller.
/// </summary>
public class MobileDrawingTests
{
    // One item per layer reaches the client, because it cannot render two things on one slot.
    // Which one survives is settled earlier, by IItemService.Equip replacing what was there —
    // the guard here is the second line of defence, for a store that somehow holds both.
    [Fact]
    public void Equipment_DrawsOneItemPerLayer()
    {
        var (items, mobile) = World();

        Equip(items, mobile, 0x1410, LayerType.Helm);
        Equip(items, mobile, 0x1411, LayerType.Helm);

        var drawn = MobileDrawing.BuildEquipment(mobile, items, new VirtualSerialService());

        Assert.Equal(LayerType.Helm, Assert.Single(drawn).Layer);
    }

    [Fact]
    public void Equipment_AddsHairAsAPseudoItem()
    {
        var (items, mobile) = World();

        mobile.HairStyle = 0x203B;

        var drawn = MobileDrawing.BuildEquipment(mobile, items, new VirtualSerialService());

        Assert.Equal(LayerType.Hair, Assert.Single(drawn).Layer);
        Assert.Equal(0x203B, Assert.Single(drawn).ItemId);
    }

    // Hair is drawn only if nothing real claimed its layer: a helm hides it.
    [Fact]
    public void Equipment_SkipsHairWhenSomethingRealAlreadyHoldsItsLayer()
    {
        var (items, mobile) = World();

        mobile.HairStyle = 0x203B;
        Equip(items, mobile, 0x1410, LayerType.Hair);

        var drawn = MobileDrawing.BuildEquipment(mobile, items, new VirtualSerialService());

        Assert.Equal(0x1410, Assert.Single(drawn).ItemId);
    }

    private static (ItemService Items, MobileEntity Mobile) World()
    {
        var persistence = new FakePersistenceService();
        var mobile = new MobileEntity { Name = "Squid", MapId = 1, Position = new(1, 1, 0) };

        persistence.Store<MobileEntity>().UpsertAsync(mobile).WaitSync();

        return (new(persistence), mobile);
    }

    private static void Equip(ItemService items, MobileEntity mobile, int itemId, LayerType layer)
    {
        var item = new ItemEntity { ItemId = itemId };

        items.Save(item);
        items.Equip(mobile, item, layer);
    }
}
