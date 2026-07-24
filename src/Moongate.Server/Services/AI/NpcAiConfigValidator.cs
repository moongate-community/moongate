using Moongate.Server.Abstractions.Data.Config;

namespace Moongate.Server.Services.AI;

public static class NpcAiConfigValidator
{
    public static void Validate(NpcAiConfig config)
    {
        var advanced = config.Advanced;

        ValidatePositive(advanced.SectorIdleGraceSeconds, nameof(NpcAiAdvancedConfig.SectorIdleGraceSeconds));
        ValidatePositive(advanced.MinTickMilliseconds, nameof(NpcAiAdvancedConfig.MinTickMilliseconds));
        ValidatePositive(advanced.MaxTickMilliseconds, nameof(NpcAiAdvancedConfig.MaxTickMilliseconds));
        ValidatePositive(advanced.MaxBrainsPerLoop, nameof(NpcAiAdvancedConfig.MaxBrainsPerLoop));
        ValidatePositive(advanced.MaxEventsPerBrainWake, nameof(NpcAiAdvancedConfig.MaxEventsPerBrainWake));
        ValidatePositive(advanced.MaxMailboxEvents, nameof(NpcAiAdvancedConfig.MaxMailboxEvents));
        ValidatePositive(advanced.MaxIntentsPerDecision, nameof(NpcAiAdvancedConfig.MaxIntentsPerDecision));
        ValidatePositive(advanced.InstructionBudget, nameof(NpcAiAdvancedConfig.InstructionBudget));

        if (advanced.MinTickMilliseconds > advanced.MaxTickMilliseconds)
        {
            throw new InvalidOperationException(
                $"{nameof(NpcAiAdvancedConfig.MinTickMilliseconds)} cannot exceed {nameof(NpcAiAdvancedConfig.MaxTickMilliseconds)}."
            );
        }

        if (advanced.MaxMailboxEvents < advanced.MaxEventsPerBrainWake)
        {
            throw new InvalidOperationException(
                $"{nameof(NpcAiAdvancedConfig.MaxMailboxEvents)} cannot be smaller than {nameof(NpcAiAdvancedConfig.MaxEventsPerBrainWake)}."
            );
        }
    }

    private static void ValidatePositive(long value, string propertyName)
    {
        if (value <= 0)
        {
            throw new InvalidOperationException($"{propertyName} must be positive.");
        }
    }
}
