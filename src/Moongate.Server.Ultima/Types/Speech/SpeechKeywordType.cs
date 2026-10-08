namespace Moongate.Server.Ultima.Types.Speech;

/// <summary>
///     Speech keywords the client finds in what a player says (ids of the client's speech.mul), in any language of the
///     client; <c>on_speech</c> gets them as numbers. The bank's, the guards' and the vendors', as ModernUO reads them.
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
    Guards = 0x0007,

    /// <summary>
    ///     "train", as ModernUO's trainers read it.
    /// </summary>
    Train = 0x006C,

    /// <summary>
    ///     "vendor buy", as ModernUO's vendors read it.
    /// </summary>
    VendorBuy = 0x003C,

    /// <summary>
    ///     "vendor sell", as ModernUO's vendors read it.
    /// </summary>
    VendorSell = 0x014D
}
