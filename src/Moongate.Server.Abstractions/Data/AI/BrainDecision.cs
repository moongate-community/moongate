namespace Moongate.Server.Abstractions.Data.AI;

public sealed record BrainDecision(int? NextTickMilliseconds, IReadOnlyList<BrainIntent> Intents)
{
    public static BrainDecision Empty { get; } = new(null, []);
}
