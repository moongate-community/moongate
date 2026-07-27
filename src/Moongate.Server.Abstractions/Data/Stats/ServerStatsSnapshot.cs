namespace Moongate.Server.Abstractions.Data.Stats;

/// <summary>
/// A point-in-time count of what the shard holds. Computed on the game loop and published for readers
/// on any thread, so it is immutable by construction.
/// </summary>
public sealed record ServerStatsSnapshot(
    DateTimeOffset GeneratedAt,
    TimeSpan Uptime,
    int OnlinePlayers,
    int Connections,
    int Accounts,
    int ActiveAccounts,
    int Characters,
    int Npcs,
    int WorldItems,
    int ItemTemplates,
    int MobileTemplates
)
{
    /// <summary>
    /// What readers see between startup and the first refresh: every count zero, with
    /// <see cref="GeneratedAt" /> at <see cref="DateTimeOffset.MinValue" /> so the gap is
    /// distinguishable from a genuinely empty shard.
    /// </summary>
    public static ServerStatsSnapshot Empty { get; } =
        new(DateTimeOffset.MinValue, TimeSpan.Zero, 0, 0, 0, 0, 0, 0, 0, 0, 0);
}
