namespace Moongate.Server.Abstractions.Types;

public enum NpcBrainEventType
{
    Activate,
    Deactivate,
    SpeechHeard,
    MobileEnteredRange,
    MobileLeftRange,
    MobileMoved,
    Attacked,
    Damage,
    Death
}
