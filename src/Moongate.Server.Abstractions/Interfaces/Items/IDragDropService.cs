using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Internal;

namespace Moongate.Server.Abstractions.Interfaces.Items;

/// <summary>
/// The drag-and-drop rules: lifting an item onto a player's cursor, dropping it on the ground or into
/// a container, and putting it back when either fails.
/// </summary>
public interface IDragDropService
{
    /// <summary>
    /// Puts <paramref name="itemId" /> back where <paramref name="origin" /> says it came from,
    /// falling back to <paramref name="actor" />'s backpack and then to the ground at their feet.
    /// </summary>
    void Bounce(MobileEntity actor, Serial itemId, HeldItemOrigin? origin);

    /// <summary>
    /// Drops the held item into <paramref name="containerId" /> (or on the ground when it is
    /// <see cref="Serial.Zero" />) and reports whether it went through.
    /// </summary>
    LiftDecision Drop(
        MobileEntity actor,
        Serial heldItemId,
        Serial containerId,
        Point3D groundPosition,
        Point2D containerPosition
    );

    /// <summary>
    /// Validates and performs a lift. On success <paramref name="heldId" /> is the item now on the
    /// cursor and <paramref name="origin" /> is where it was; on refusal both are empty and nothing
    /// moved.
    /// </summary>
    LiftDecision Lift(
        MobileEntity actor,
        Serial itemId,
        int amount,
        Serial heldItemId,
        out Serial heldId,
        out HeldItemOrigin? origin
    );
}
