namespace Moongate.Server.Ultima.Types.Speech;

/// <summary>
///     Speech keywords the client finds in what a player says (ids of the client's speech.mul), in any language of the
///     client; <c>on_speech</c> gets them as numbers. The bank's, as ModernUO's banker reads them.
/// </summary>
public enum SpeechKeywordType
{
    Withdraw = 0x0000,
    Balance = 0x0001,
    Bank = 0x0002,
    Check = 0x0003,

    /// <summary>
    ///     "guards", as ModernUO's guarded regions read it.
    /// </summary>
    Guards = 0x0007
}
