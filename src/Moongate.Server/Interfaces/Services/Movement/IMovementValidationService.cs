using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Interfaces.Services.Movement;

/// <summary>
/// Validates requested movement against map bounds and tile collisions.
/// </summary>
public interface IMovementValidationService
{
    /// <summary>
    /// Attempts to validate and resolve the destination location for a movement request.
    /// </summary>
    /// <param name="mobile">Mobile requesting movement.</param>
    /// <param name="direction">Requested direction.</param>
    /// <param name="newLocation">Resolved destination location when valid.</param>
    /// <returns><c>true</c> when movement is valid; otherwise <c>false</c>.</returns>
    bool TryResolveMove(UOMobileEntity mobile, DirectionType direction, out Point3D newLocation);
}
