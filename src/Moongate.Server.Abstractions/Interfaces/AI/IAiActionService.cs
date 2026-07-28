using Moongate.Core.Primitives;
using Moongate.Core.Types;
using Moongate.Server.Abstractions.Data.AI;

namespace Moongate.Server.Abstractions.Interfaces.AI;

/// <summary>Applies the bounded world effects of NPC brain actions against the mobile of the active tick.</summary>
public interface IAiActionService
{
    /// <summary>Sets the ambient brain context for the current tick; disposing restores the previous one.</summary>
    IDisposable Begin(BrainContext context);

    /// <summary>Clears combatant and warmode on the active mobile.</summary>
    bool ClearTarget();

    /// <summary>Sets combatant and warmode on the perceived target; false when the target is not perceivable.</summary>
    bool Engage(Serial targetId);

    /// <summary>Steps one tile away from the perceived target; false when the target is not perceivable.</summary>
    bool MoveAway(Serial targetId);

    /// <summary>Steps one tile toward (x, y) at the owner's current z; false when the step is blocked.</summary>
    bool MoveTo(int x, int y);

    /// <summary>Steps one tile toward the perceived target; false when the target is not perceivable.</summary>
    bool MoveToward(Serial targetId);

    /// <summary>Steps randomly within the home leash, or toward home when outside it; false when off the home map.</summary>
    bool Patrol();

    /// <summary>Steps one tile toward the home position; false when off the home map.</summary>
    bool ReturnHome();

    /// <summary>Speaks as the active mobile; false when the text is blank or over the length limit.</summary>
    bool Say(string text);

    /// <summary>Steps one tile in the given compass direction; false when the step is blocked.</summary>
    bool Step(DirectionType direction);
}
