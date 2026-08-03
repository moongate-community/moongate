using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Network.Types;
using Moongate.Server.Abstractions.Data.Events;
using Moongate.Tests.Support;

namespace Moongate.Tests.Server.Items;

public class DragDropServiceDropTests
{
    [Fact]
    public void Drop_IntoAContainer_StoresItAtTheGivenSlot()
    {
        var fixture = DragDropFixture.WithGoldInBackpack(1);
        fixture.Service.Lift(fixture.Actor, fixture.Gold.Id, 1, Serial.Zero, out var heldId, out _);

        var decision = fixture.Service.Drop(
            fixture.Actor,
            heldId,
            fixture.Backpack.Id,
            Point3D.Zero,
            new(60, 80)
        );

        Assert.True(decision.Accepted);
        Assert.Equal(fixture.Backpack.Id, fixture.Gold.ParentContainerId);
        Assert.Equal(new(60, 80), fixture.Gold.ContainerPosition);
    }

    [Fact]
    public void Drop_IntoTheHeldContainerItself_IsRefused()
    {
        var fixture = DragDropFixture.WithBagInBackpack();
        fixture.Service.Lift(fixture.Actor, fixture.Bag.Id, 1, Serial.Zero, out var heldId, out _);

        var decision = fixture.Service.Drop(fixture.Actor, heldId, heldId, Point3D.Zero, new(60, 80));

        Assert.False(decision.Accepted);
        Assert.Equal(LiftRejectReasonType.CannotLift, decision.Reason);
    }

    [Fact]
    public void Drop_OfSomethingNotHeld_IsRefused()
    {
        var fixture = DragDropFixture.WithGoldInBackpack(1);

        var decision = fixture.Service.Drop(
            fixture.Actor,
            (Serial)0xDEAD,
            Serial.Zero,
            new(101, 100, 0),
            Point2D.Zero
        );

        Assert.False(decision.Accepted);
        Assert.Equal(LiftRejectReasonType.Inspecific, decision.Reason);
    }

    [Fact]
    public void Drop_OnTheGround_PlacesTheItemAtThePosition()
    {
        var fixture = DragDropFixture.WithGoldInBackpack(1);
        fixture.Service.Lift(fixture.Actor, fixture.Gold.Id, 1, Serial.Zero, out var heldId, out _);

        var decision = fixture.Service.Drop(
            fixture.Actor,
            heldId,
            Serial.Zero,
            new(101, 100, 0),
            Point2D.Zero
        );

        Assert.True(decision.Accepted);
        Assert.Equal(1, fixture.Gold.MapId);
        Assert.Equal(new(101, 100, 0), fixture.Gold.Position);
        Assert.Equal(Serial.Zero, fixture.Gold.ParentContainerId);
    }

    [Fact]
    public void Drop_OnTheGround_PublishesItemDropped()
    {
        var fixture = DragDropFixture.WithGoldInBackpack(1);
        ItemDroppedEvent? published = null;
        fixture.EventBus.Subscribe<ItemDroppedEvent>(
            (evt, _) =>
            {
                published = evt;

                return Task.CompletedTask;
            }
        );

        fixture.Service.Lift(fixture.Actor, fixture.Gold.Id, 1, Serial.Zero, out var heldId, out _);
        fixture.Service.Drop(fixture.Actor, heldId, Serial.Zero, new(101, 100, 0), Point2D.Zero);

        Assert.NotNull(published);
        Assert.Equal(fixture.Gold.Id, published!.Item);
        Assert.Equal(Serial.Zero, published.ContainerId);
    }

    [Fact]
    public void Drop_OntoACompatibleStack_MergesAndDeletesTheDroppedEntity()
    {
        // 100 gold in the backpack; lift 40, then drop the 40 back onto the 60 -- which the client
        // reports by naming the 60, not the backpack. Named with a spot inside the backpack instead,
        // the two halves would stay apart, and SplitStackPlacementTests covers that.
        var fixture = DragDropFixture.WithGoldInBackpack(100);
        fixture.Service.Lift(fixture.Actor, fixture.Gold.Id, 40, Serial.Zero, out var heldId, out _);

        var remainder = fixture.BackpackContents().Single(item => item.Id != heldId);

        fixture.Service.Drop(fixture.Actor, heldId, remainder.Id, Point3D.Zero, new(60, 80));

        var stack = Assert.Single(fixture.BackpackContents());
        Assert.Equal(100, stack.Amount);
    }
}
