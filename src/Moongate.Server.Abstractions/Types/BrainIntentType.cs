namespace Moongate.Server.Abstractions.Types;

public enum BrainIntentType
{
    Unknown,
    Idle,
    Say,
    Patrol,
    MoveToward,
    MoveAway,
    Engage,
    ClearTarget,
    ReturnHome
}
