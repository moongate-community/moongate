using Moongate.Core.Geometry;
using Moongate.Server.Ultima.Data.Movement;
using Moongate.Server.Ultima.Types.Movement;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Finds the shortest way a mover can walk between two places of a map, with A*: every step is one
///     <see cref="IMovementService.CheckMovement" /> allows, so a path is made of steps the mover can really take.
/// </summary>
/// <remarks>
///     Game loop only. The search looks at a square of <c>ultima.world.pathfinding_range</c> plus one tiles a side,
///     placed
///     so that start and goal lie in it, and at <c>ultima.world.pathfinding_max_nodes</c> places at most. A path sees
///     what
///     the movement sees: an impassable item on the ground, a closed door among them, blocks it. Other mobiles do not.
///     A tile on the way has one height in a search, so a path cannot pass both over and under the same tile; the goal's
///     own tile is only entered at the goal's height.
/// </remarks>
public interface IPathfindingService
{
    /// <summary>
    ///     Finds the steps from <paramref name="from" /> to <paramref name="to" />, reached when a step lands on its
    ///     tile within a mover's height (16) of it. With <paramref name="allowPartial" />, a goal that cannot be
    ///     reached gives the steps to the closest place found instead.
    /// </summary>
    PathResult FindPath(
        MapType map,
        Point3D from,
        Point3D to,
        MovementAbilityType ability = MovementAbilityType.Walk,
        bool allowPartial = false
    );
}
