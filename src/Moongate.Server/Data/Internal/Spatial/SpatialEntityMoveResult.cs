namespace Moongate.Server.Data.Internal.Spatial;

/// <summary>
/// Represents the result of moving an entity in the spatial index.
/// </summary>
internal readonly record struct SpatialEntityMoveResult(
    bool SectorChanged,
    int MapId,
    int OldSectorX,
    int OldSectorY,
    int NewSectorX,
    int NewSectorY
);
