namespace Moongate.Server.Abstractions.Data.AI;

public sealed record NpcBrainInvocationResult(
    bool Success,
    BrainDecision Decision,
    string? Error,
    bool InstructionBudgetExceeded
)
{
    public static NpcBrainInvocationResult Succeeded(BrainDecision decision) => new(true, decision, null, false);

    public static NpcBrainInvocationResult Failed(string error, bool budgetExceeded = false)
        => new(false, BrainDecision.Empty, error, budgetExceeded);
}
