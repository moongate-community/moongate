namespace Moongate.Server.Abstractions.Data.AI;

public sealed record NpcBrainInvocationResult(
    bool Success,
    int? NextTickMs,
    string? Error,
    bool InstructionBudgetExceeded
)
{
    public static NpcBrainInvocationResult Succeeded(int? nextTickMs) => new(true, nextTickMs, null, false);

    public static NpcBrainInvocationResult Failed(string error, bool budgetExceeded = false)
        => new(false, null, error, budgetExceeded);
}
