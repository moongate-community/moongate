using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Services.Items;
using Moongate.Ultima.Types;
using Moongate.UO.Data.Items;

namespace Moongate.Tests.Server.Items;

/// <summary>
/// Dropping a held item onto a paperdoll. The chain is POL's equip_item, minus the parts this shard has
/// no concept for — strength requirements, dressing others, death. The one that matters for safety is
/// the layer: it comes from the item, never from the request.
/// </summary>
public class WearItemTests
{
    private const int RobeItemId = 7939;

    [Fact]
    public void WearingSomethingNobodyIsHolding_IsRefused()
    {
        var actor = Actor();

        Assert.False(DragDropService.EvaluateWear(actor, null, new(0x4000000A), actor.Id, Wearable()).Accepted);
    }

    // The held item is the authority. A packet naming anything else is a desynced or hostile client,
    // and honouring it would let one wear what it never lifted.
    [Fact]
    public void WearingAnItemOtherThanTheHeldOne_IsRefused()
    {
        var actor = Actor();
        var held = Robe(0x4000000A);

        Assert.False(DragDropService.EvaluateWear(actor, held, new(0x4000000B), actor.Id, Wearable()).Accepted);
    }

    [Fact]
    public void WearingSomethingWithNoLayer_IsRefused()
    {
        var actor = Actor();
        var held = Robe(0x4000000A);

        Assert.False(DragDropService.EvaluateWear(actor, held, held.Id, actor.Id, NotWearable()).Accepted);
    }

    [Fact]
    public void WearingOntoAnOccupiedLayer_IsRefused()
    {
        var actor = Actor();

        actor.EquippedItemIds[LayerType.OuterTorso] = new(0x4000000F);

        var held = Robe(0x4000000A);

        Assert.False(DragDropService.EvaluateWear(actor, held, held.Id, actor.Id, Wearable()).Accepted);
    }

    // POL gates dressing someone else behind can_clothe; there is no permission concept here yet, so
    // the answer is no rather than a guess at one.
    [Fact]
    public void WearingOntoSomebodyElse_IsRefused()
    {
        var actor = Actor();
        var held = Robe(0x4000000A);

        Assert.False(DragDropService.EvaluateWear(actor, held, held.Id, new(0x00000009), Wearable()).Accepted);
    }

    [Fact]
    public void WearingAFreeLayerOnYourself_IsAccepted()
    {
        var actor = Actor();
        var held = Robe(0x4000000A);

        Assert.True(DragDropService.EvaluateWear(actor, held, held.Id, actor.Id, Wearable()).Accepted);
    }

    // A template with no Equip block at all is the same answer as one whose layer is None: not wearable.
    [Fact]
    public void WearingATemplateWithNoEquipBlock_IsRefused()
    {
        var actor = Actor();
        var held = Robe(0x4000000A);

        Assert.False(DragDropService.EvaluateWear(actor, held, held.Id, actor.Id, null).Accepted);
    }

    private static MobileEntity Actor()
        => new() { Id = new(0x00000001), MapId = 1, Position = new(100, 100, 0) };

    private static ItemEntity Robe(uint serial)
        => new() { Id = new(serial), TemplateId = "robe", ItemId = RobeItemId };

    private static ItemTemplate Wearable()
        => new() { Id = "robe", ItemId = RobeItemId, Equip = new() { Layer = LayerType.OuterTorso } };

    private static ItemTemplate NotWearable()
        => new() { Id = "robe", ItemId = RobeItemId, Equip = new() { Layer = LayerType.None } };
}
