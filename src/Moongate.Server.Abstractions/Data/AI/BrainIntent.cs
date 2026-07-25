using Moongate.Core.Primitives;
using Moongate.Server.Abstractions.Types;

namespace Moongate.Server.Abstractions.Data.AI;

public sealed record BrainIntent(
    BrainIntentType Type,
    string RawType,
    Serial TargetId,
    string? Text
);
