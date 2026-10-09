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
    ///     "join" or "member", said to a guildmaster.
    /// </summary>
    Join = 0x0004,

    /// <summary>
    ///     "resign" or "quit", said to a guildmaster.
    /// </summary>
    Resign = 0x0005,

    /// <summary>
    ///     "stable", said to an animal trainer to leave a pet with it.
    /// </summary>
    Stable = 0x0008,

    /// <summary>
    ///     "claim", said to an animal trainer to take a pet back.
    /// </summary>
    Claim = 0x0009,

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
