using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Interfaces.Services.Movement;

/// <summary>
/// Computes walkable paths for mobiles using server-authoritative movement rules.
/// </summary>
public interface IPathfindingService
{
    /// <summary>
    /// Attempts to find a path from the mobile current location to the target location.
    /// </summary>
    /// <param name="mobile">Moving mobile.</param>
    /// <param name="targetLocation">Desired target location.</param>
    /// <param name="path">Resolved path as movement directions when successful.</param>
    /// <param name="maxVisitedNodes">Maximum graph nodes explored before aborting.</param>
    /// <returns><c>true</c> when a path was found; otherwise <c>false</c>.</returns>
    bool TryFindPath(
        UOMobileEntity mobile,
        Point3D targetLocation,
        out IReadOnlyList<DirectionType> path,
        int maxVisitedNodes = 1024
    );
}
