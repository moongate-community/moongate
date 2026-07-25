namespace Moongate.Server.Abstractions.Types;

public enum NpcBrainHookType
{
    Activate,
    Deactivate,
    SpeechHeard,
    MobileEnteredRange,
    MobileLeftRange,
    MobileMoved,
    Attacked,
    Damage,
    Death,
    Think
}
