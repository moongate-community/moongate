namespace Moongate.UO.Data.Data.Reputation;

/// <summary>
/// Maps an upper fame bound to an ordered set of karma title buckets.
/// </summary>
public sealed record ReputationFameBucket(int MaxFame, IReadOnlyList<ReputationKarmaBucket> KarmaBuckets);
