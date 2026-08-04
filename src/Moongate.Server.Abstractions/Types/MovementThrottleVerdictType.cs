namespace Moongate.Server.Abstractions.Types;

/// <summary>What to do with a step the moment it arrives.</summary>
public enum MovementThrottleVerdictType
{
    /// <summary>Take it now — either it was due, or the slack covered how early it was.</summary>
    Run,

    /// <summary>Too early for the slack left. It waits its turn rather than being refused.</summary>
    Queue
}
