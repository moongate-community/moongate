namespace Moongate.Server.Ultima.Data.Spawns;

/// <summary>
///     A spawn region as it is now: its live NPCs against its max, how long until its next spawn (zero when due), and
///     whether its last check found no spot, so it is retrying every minute.
/// </summary>
public sealed record SpawnRegionStatus(string Id, string? Name, int Live, int Max, TimeSpan NextSpawnIn, bool Retrying);
