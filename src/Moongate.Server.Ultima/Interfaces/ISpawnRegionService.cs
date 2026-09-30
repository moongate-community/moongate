using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     UOX3's region spawns from <c>templates/spawns</c>: every region keeps up to its max NPCs alive, spawning a few at a
///     time between its min and max minutes, and tells the game masters and administrators what it spawned.
/// </summary>
public interface ISpawnRegionService : IMoongateStartupService
{
}
