namespace Moongate.Server.Abstractions.Data.Config;

public sealed class NpcAiAdvancedConfig
{
    public int SectorIdleGraceSeconds { get; set; } = 60;

    public int MinTickMilliseconds { get; set; } = 100;

    public int MaxTickMilliseconds { get; set; } = 60_000;

    public int MaxBrainsPerLoop { get; set; } = 100;

    public int MaxEventsPerBrainWake { get; set; } = 16;

    public int MaxMailboxEvents { get; set; } = 64;

    public int MaxIntentsPerDecision { get; set; } = 8;

    public long InstructionBudget { get; set; } = 50_000;
}
