using Moongate.Server.Abstractions.Data.Stats;

namespace Moongate.Server.Abstractions.Interfaces.Server;

/// <summary>Publishes the shard's live figures as a snapshot recomputed on the game loop.</summary>
public interface IServerStatsService
{
    /// <summary>The most recent snapshot. Never null, and safe to read from any thread.</summary>
    ServerStatsSnapshot Current { get; }

    /// <summary>
    /// Recomputes the snapshot and publishes it. Must run on the game loop: it reads the world stores,
    /// which are single-writer there.
    /// </summary>
    void Refresh();
}
