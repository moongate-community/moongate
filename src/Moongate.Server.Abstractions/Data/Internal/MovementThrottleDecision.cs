using Moongate.Server.Abstractions.Types;

namespace Moongate.Server.Abstractions.Data.Internal;

/// <summary>
/// What to do with a step, and the slack the client is left with afterwards. Separate from the
/// legality decision on purpose: whether a step is *allowed* and whether it is *due* are different
/// questions, and only the second one has any business being forgiving.
/// </summary>
/// <param name="Verdict">Run it now, or hold it until it comes due.</param>
/// <param name="Credit">The slack after this step is accounted for.</param>
public readonly record struct MovementThrottleDecision(MovementThrottleVerdictType Verdict, TimeSpan Credit);
