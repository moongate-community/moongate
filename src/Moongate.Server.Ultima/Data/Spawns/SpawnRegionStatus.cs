namespace Moongate.Server.Ultima.Data.Spawns;

/// <summary>
///     A spawn region as it is now: its live NPCs against its max, and how long until its next spawn (zero when due).
/// </summary>
public sealed record SpawnRegionStatus(string Id, string? Name, int Live, int Max, TimeSpan NextSpawnIn);
