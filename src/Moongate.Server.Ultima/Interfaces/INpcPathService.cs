using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Movement;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Types.Movement;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Keeps the path each NPC is walking, so a script can ask for the next step on every tick without searching every
///     time: a path is searched with <see cref="IPathfindingService" /> when the NPC has none, when its goal changed or
///     when a step was blocked, and never more than once every two seconds for an NPC, as ModernUO's
///     <c>PathFollower</c>; after a search that found nothing, the costly kind, only ten seconds later.
/// </summary>
/// <remarks>
///     Game loop only. The caller takes the step and reports it with <see cref="Stepped" />.
/// </remarks>
public interface INpcPathService
{
    /// <summary>
    ///     Gets what the NPC does now on its way to <paramref name="goal" />: nothing when it stands within
    ///     <paramref name="range" /> tiles of it, a step, or a wait. A goal that cannot be reached is walked towards as
    ///     far as a path leads.
    /// </summary>
    NpcPathStep Next(MobileEntity npc, Point3D goal, int range, MovementAbilityType ability);

    /// <summary>
    ///     Records the step <see cref="Next" /> gave: taken, the path goes on; refused, the path is dropped and searched
    ///     again when two seconds have passed since the last search.
    /// </summary>
    void Stepped(MobileEntity npc, bool moved);

    /// <summary>
    ///     Forgets the path of an NPC, such as one that was deleted.
    /// </summary>
    void Forget(Serial npc);
}
