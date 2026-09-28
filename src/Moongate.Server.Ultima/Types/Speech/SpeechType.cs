namespace Moongate.Server.Ultima.Types.Speech;

/// <summary>
///     Message kinds carried by Ultima Online speech packets.
/// </summary>
public enum SpeechType : byte
{
    Regular = 0,
    System = 1,
    Emote = 2,
    Whisper = 8,
    Yell = 9
}
