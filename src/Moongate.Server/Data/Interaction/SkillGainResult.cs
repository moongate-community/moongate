using Moongate.UO.Data.Types;

namespace Moongate.Server.Data.Interaction;

/// <summary>
/// Describes the outcome of a single skill gain attempt.
/// </summary>
public sealed record SkillGainResult(
    UOSkillName SkillName,
    bool SkillIncreased,
    UOSkillName? LoweredSkillName
);
