using Moongate.Abstractions.Interfaces.Services.Base;

namespace Moongate.Server.Interfaces.Services.World;

/// <summary>
/// Drives runtime NPC spawning from active spawner items.
/// </summary>
public interface ISpawnService : IMoongateService
{
    /// <summary>
    /// Returns the number of tracked runtime spawner states.
    /// </summary>
    int GetTrackedSpawnerCount();
}
