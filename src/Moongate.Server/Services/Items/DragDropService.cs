using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Network.Types;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Internal;
using Moongate.UO.Data.Items;

namespace Moongate.Server.Services.Items;

/// <summary>
/// Owns the drag-and-drop game rules. <see cref="Evaluate" /> is the pure decision core — public and
/// static so the rules are unit-testable without a live session, mirroring
/// <c>MovementService.Evaluate</c>. The rest of the service is orchestration: detach, hold, place,
/// bounce, and the packets that follow.
/// </summary>
public sealed class DragDropService
{
    /// <summary>How close the player must be to lift something, in tiles. ModernUO uses the same 2.</summary>
    public const int LiftRange = 2;

    /// <summary>
    /// Decides whether <paramref name="actor" /> may lift an item, in ModernUO's order: already
    /// holding, then range, then whether the item can be moved at all, then whether the actor can
    /// reach it. <paramref name="reachable" /> is computed by the caller, which needs the store to
    /// walk the container chain; everything else here is a plain value.
    /// </summary>
    public static LiftDecision Evaluate(
        MobileEntity actor,
        int itemMapId,
        Point3D itemWorldPosition,
        ItemTemplate? template,
        Serial heldItemId,
        bool reachable
    )
    {
        if (heldItemId != Serial.Zero)
        {
            return new(false, LiftRejectReasonType.AreHolding);
        }

        if (actor.MapId != itemMapId || !actor.Position.InRange(itemWorldPosition, LiftRange))
        {
            return new(false, LiftRejectReasonType.OutOfRange);
        }

        // A template that cannot be resolved is treated as unmovable: better a refused lift than an
        // item whose rules are unknown.
        if (template is null || !template.IsMovable)
        {
            return new(false, LiftRejectReasonType.CannotLift);
        }

        if (!reachable)
        {
            return new(false, LiftRejectReasonType.CannotLift);
        }

        return new(true, LiftRejectReasonType.Inspecific);
    }
}
