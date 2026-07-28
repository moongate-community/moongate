using Moongate.Core.Primitives;
using Moongate.Network.Types;
using Moongate.Tests.Support;

namespace Moongate.Tests.Server.Items;

public class DragDropServiceLiftTests
{
    [Fact]
    public void Lift_Accepted_DetachesTheItemAndRecordsWhereItCameFrom()
    {
        var fixture = DragDropFixture.WithGoldInBackpack(amount: 1);

        fixture.Service.Lift(fixture.Actor, fixture.Gold.Id, 1, Serial.Zero, out var heldId, out var origin);

        Assert.Equal(fixture.Gold.Id, heldId);
        Assert.Equal(Serial.Zero, fixture.Gold.ParentContainerId);
        Assert.NotNull(origin);
        Assert.Equal(fixture.Backpack.Id, origin!.ContainerId);
    }

    [Fact]
    public void Lift_AlreadyHolding_RefusesAndLeavesTheItemAlone()
    {
        var fixture = DragDropFixture.WithGoldInBackpack(amount: 1);

        var decision = fixture.Service.Lift(
            fixture.Actor,
            fixture.Gold.Id,
            1,
            heldItemId: (Serial)999,
            out var heldId,
            out _
        );

        Assert.False(decision.Accepted);
        Assert.Equal(LiftRejectReasonType.AreHolding, decision.Reason);
        Assert.Equal(Serial.Zero, heldId);
        Assert.Equal(fixture.Backpack.Id, fixture.Gold.ParentContainerId);
    }

    [Fact]
    public void Lift_PartialStack_KeepsTheOriginalOnTheCursorAndLeavesTheRemainder()
    {
        var fixture = DragDropFixture.WithGoldInBackpack(amount: 100);

        var decision = fixture.Service.Lift(
            fixture.Actor,
            fixture.Gold.Id,
            amount: 5,
            heldItemId: Serial.Zero,
            out var heldId,
            out _
        );

        Assert.True(decision.Accepted);

        // ModernUO's inversion: the serial the client dragged stays the lifted portion.
        Assert.Equal(fixture.Gold.Id, heldId);
        Assert.Equal(5, fixture.Gold.Amount);

        var remainder = Assert.Single(fixture.BackpackContents());
        Assert.Equal(95, remainder.Amount);
        Assert.Equal("gold", remainder.TemplateId);
    }

    [Fact]
    public void Lift_UnknownSerial_RefusesAsInspecific()
    {
        var fixture = DragDropFixture.WithGoldInBackpack(amount: 1);

        var decision = fixture.Service.Lift(fixture.Actor, (Serial)0xDEAD, 1, Serial.Zero, out _, out _);

        Assert.False(decision.Accepted);
        Assert.Equal(LiftRejectReasonType.Inspecific, decision.Reason);
    }

    [Fact]
    public void Lift_WholeStack_CreatesNoRemainder()
    {
        var fixture = DragDropFixture.WithGoldInBackpack(amount: 100);

        fixture.Service.Lift(fixture.Actor, fixture.Gold.Id, 100, Serial.Zero, out _, out _);

        Assert.Empty(fixture.BackpackContents());
        Assert.Equal(100, fixture.Gold.Amount);
    }
}
