using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.World;

namespace Moongate.Server.Abstractions.Interfaces.World;

/// <summary>Tracks player-driven sector activity and its grace-period expiry on the game loop.</summary>
public interface ISectorActivityService
{
    /// <summary>Gets the current counts of reference-positive and grace-period sectors.</summary>
    SectorActivitySnapshot Current { get; }

    /// <summary>Returns whether a sector is reference-positive or still within its idle grace period.</summary>
    bool IsActive(int mapId, int sectorX, int sectorY);

    /// <summary>Moves an already tracked player to a spatial-index sector; unknown serials are ignored.</summary>
    void MovePlayer(Serial playerId, int mapId, int sectorX, int sectorY);

    /// <summary>Expires zero-reference sectors whose idle grace period has elapsed.</summary>
    void Tick();

    /// <summary>Tracks a player at its current map and spatial-index sector.</summary>
    void TrackPlayer(MobileEntity player);

    /// <summary>Stops tracking a player; unknown serials are ignored.</summary>
    void UntrackPlayer(Serial playerId);
}
