using Moongate.UO.Data.Types;

namespace Moongate.Server.Data.Interaction;

/// <summary>
/// Describes the outcome of a stat gain attempt triggered by skill gain.
/// </summary>
public sealed record StatGainResult(
    bool StatIncreased,
    Stat? GainedStat,
    Stat? LoweredStat
);
