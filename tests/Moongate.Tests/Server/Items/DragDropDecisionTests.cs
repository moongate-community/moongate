using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Network.Types;
using Moongate.Persistence.Entities;
using Moongate.Server.Services.Items;
using Moongate.UO.Data.Items;

namespace Moongate.Tests.Server.Items;

public class DragDropDecisionTests
{
    [Fact]
    public void Evaluate_AlreadyHolding_RefusesBeforeAnythingElse()
    {
        // Out of range and unmovable too: already-holding still wins, as in ModernUO's Mobile.Lift.
        var decision = DragDropService.Evaluate(
            Actor(),
            itemMapId: 1,
            itemWorldPosition: new(500, 500, 0),
            template: Template(movable: false),
            heldItemId: (Serial)42,
            reachable: false
        );

        Assert.False(decision.Accepted);
        Assert.Equal(LiftRejectReasonType.AreHolding, decision.Reason);
    }

    [Fact]
    public void Evaluate_InRangeMovableAndReachable_Accepts()
    {
        var decision = DragDropService.Evaluate(
            Actor(),
            itemMapId: 1,
            itemWorldPosition: new(101, 100, 0),
            template: Template(),
            heldItemId: Serial.Zero,
            reachable: true
        );

        Assert.True(decision.Accepted);
    }

    [Fact]
    public void Evaluate_MissingTemplate_RefusesAsUnliftable()
    {
        var decision = DragDropService.Evaluate(
            Actor(),
            itemMapId: 1,
            itemWorldPosition: new(100, 100, 0),
            template: null,
            heldItemId: Serial.Zero,
            reachable: true
        );

        Assert.False(decision.Accepted);
        Assert.Equal(LiftRejectReasonType.CannotLift, decision.Reason);
    }

    [Fact]
    public void Evaluate_OnAnotherMap_IsOutOfRange()
    {
        var decision = DragDropService.Evaluate(
            Actor(),
            itemMapId: 2,
            itemWorldPosition: new(100, 100, 0),
            template: Template(),
            heldItemId: Serial.Zero,
            reachable: true
        );

        Assert.False(decision.Accepted);
        Assert.Equal(LiftRejectReasonType.OutOfRange, decision.Reason);
    }

    [Fact]
    public void Evaluate_ThreeTilesAway_IsOutOfRange()
    {
        var decision = DragDropService.Evaluate(
            Actor(),
            itemMapId: 1,
            itemWorldPosition: new(103, 100, 0),
            template: Template(),
            heldItemId: Serial.Zero,
            reachable: true
        );

        Assert.False(decision.Accepted);
        Assert.Equal(LiftRejectReasonType.OutOfRange, decision.Reason);
    }

    [Fact]
    public void Evaluate_TwoTilesAway_IsInRange()
    {
        var decision = DragDropService.Evaluate(
            Actor(),
            itemMapId: 1,
            itemWorldPosition: new(102, 100, 0),
            template: Template(),
            heldItemId: Serial.Zero,
            reachable: true
        );

        Assert.True(decision.Accepted);
    }

    [Fact]
    public void Evaluate_UnmovableTemplate_RefusesAsUnliftable()
    {
        var decision = DragDropService.Evaluate(
            Actor(),
            itemMapId: 1,
            itemWorldPosition: new(100, 100, 0),
            template: Template(movable: false),
            heldItemId: Serial.Zero,
            reachable: true
        );

        Assert.False(decision.Accepted);
        Assert.Equal(LiftRejectReasonType.CannotLift, decision.Reason);
    }

    [Fact]
    public void Evaluate_UnreachableItem_RefusesAsUnliftable()
    {
        var decision = DragDropService.Evaluate(
            Actor(),
            itemMapId: 1,
            itemWorldPosition: new(100, 100, 0),
            template: Template(),
            heldItemId: Serial.Zero,
            reachable: false
        );

        Assert.False(decision.Accepted);
        Assert.Equal(LiftRejectReasonType.CannotLift, decision.Reason);
    }

    private static MobileEntity Actor()
        => new() { Id = (Serial)7, MapId = 1, Position = new(100, 100, 0) };

    private static ItemTemplate Template(bool movable = true)
        => new() { Id = "dagger", Name = "Dagger", Category = "Weapon", ItemId = 3921, IsMovable = movable };
}
