namespace Moongate.Server.Abstractions.Data.AI;

public sealed record BrainDescriptor(
    string BrainId,
    int DefaultTickMilliseconds,
    int PerceptionRange,
    int HearingRange
);
