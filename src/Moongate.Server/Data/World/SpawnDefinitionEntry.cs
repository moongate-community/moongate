using Moongate.UO.Data.Geometry;

namespace Moongate.Server.Data.World;

/// <summary>
/// Represents one spawn definition loaded from ModernUO spawn JSON files.
/// </summary>
public readonly record struct SpawnDefinitionEntry(
    int MapId,
    string Map,
    string SourceGroup,
    string SourceFile,
    Guid Guid,
    SpawnDefinitionKind Kind,
    string Name,
    Point3D Location,
    int Count,
    TimeSpan MinDelay,
    TimeSpan MaxDelay,
    int Team,
    int HomeRange,
    int WalkingRange,
    IReadOnlyList<SpawnEntryDefinition> Entries
);
