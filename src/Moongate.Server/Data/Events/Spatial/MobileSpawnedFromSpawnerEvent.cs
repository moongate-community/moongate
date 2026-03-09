using Moongate.Server.Data.Events.Base;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Data.Events.Spatial;

/// <summary>
/// Event emitted when a mobile is spawned by a runtime spawner definition.
/// </summary>
public readonly record struct MobileSpawnedFromSpawnerEvent(
    GameEventBase BaseEvent,
    UOMobileEntity Mobile,
    Guid SpawnerGuid,
    string SpawnerName,
    string SourceGroup,
    string SourceFile,
    Point3D SpawnerLocation,
    int SpawnCount,
    TimeSpan MinDelay,
    TimeSpan MaxDelay,
    int Team,
    int HomeRange,
    int WalkingRange,
    string EntryName,
    int EntryMaxCount,
    int EntryProbability
) : IGameEvent
{
    /// <summary>
    /// Creates a spawn event with current timestamp.
    /// </summary>
    public MobileSpawnedFromSpawnerEvent(
        UOMobileEntity mobile,
        Guid spawnerGuid,
        string spawnerName,
        string sourceGroup,
        string sourceFile,
        Point3D spawnerLocation,
        int spawnCount,
        TimeSpan minDelay,
        TimeSpan maxDelay,
        int team,
        int homeRange,
        int walkingRange,
        string entryName,
        int entryMaxCount,
        int entryProbability
    )
        : this(
            GameEventBase.CreateNow(),
            mobile,
            spawnerGuid,
            spawnerName,
            sourceGroup,
            sourceFile,
            spawnerLocation,
            spawnCount,
            minDelay,
            maxDelay,
            team,
            homeRange,
            walkingRange,
            entryName,
            entryMaxCount,
            entryProbability
        ) { }
}
