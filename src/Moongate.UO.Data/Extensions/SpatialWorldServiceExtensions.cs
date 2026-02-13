using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Interfaces.Services;
using Moongate.UO.Data.Session;

namespace Moongate.UO.Data.Extensions;

/// <summary>
/// Extension methods for easy usage
/// </summary>
public static class SpatialWorldServiceExtensions
{
    /// <summary>
    /// Gets all nearby players for packet broadcasting
    /// </summary>
    public static async Task BroadcastToNearbyPlayers(
        this ISpatialWorldService spatialService,
        Point3D location,
        int mapIndex,
        object packet,
        GameSession? excludeSession = null,
        int range = 24
    )
    {
        var nearbySessions = spatialService.GetPlayersInRange(location, range, mapIndex, excludeSession);

        foreach (var session in nearbySessions)
        {
            /// TODO: Send packet to session
            /// await session.SendPacketAsync(packet);
        }
    }
}
