namespace Moongate.Server.Ultima.Types.Speech;

/// <summary>
///     Speech keywords the client finds in what a player says (ids of the client's speech.mul), in any language of the
///     client; <c>on_speech</c> gets them as numbers. The bank's, the guards', the vendors' and the pets', as ModernUO reads them.
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
    ///     "come", said to a pet by its name.
    /// </summary>
    PetCome = 0x0155,

    /// <summary>
    ///     "drop", said to a pet by its name.
    /// </summary>
    PetDrop = 0x0156,

    /// <summary>
    ///     "follow", said to a pet by its name.
    /// </summary>
    PetFollow = 0x015A,

    /// <summary>
    ///     "friend", said to a pet by its name.
    /// </summary>
    PetFriend = 0x015B,

    /// <summary>
    ///     "guard", said to a pet by its name.
    /// </summary>
    PetGuard = 0x015C,

    /// <summary>
    ///     "kill", said to a pet by its name.
    /// </summary>
    PetKill = 0x015D,

    /// <summary>
    ///     "attack", said to a pet by its name.
    /// </summary>
    PetAttack = 0x015E,

    /// <summary>
    ///     "stop", said to a pet by its name.
    /// </summary>
    PetStop = 0x0161,

    /// <summary>
    ///     "follow me", said to a pet by its name.
    /// </summary>
    PetFollowMe = 0x0163,

    /// <summary>
    ///     "release", said to a pet by its name.
    /// </summary>
    PetRelease = 0x016D,

    /// <summary>
    ///     "transfer", said to a pet by its name.
    /// </summary>
    PetTransfer = 0x016E,

    /// <summary>
    ///     "stay", said to a pet by its name.
    /// </summary>
    PetStay = 0x016F,

    /// <summary>
    ///     "all come", said to every pet.
    /// </summary>
    AllCome = 0x0164,

    /// <summary>
    ///     "all follow", said to every pet.
    /// </summary>
    AllFollow = 0x0165,

    /// <summary>
    ///     "all guard", said to every pet.
    /// </summary>
    AllGuard = 0x0166,

    /// <summary>
    ///     "all stop", said to every pet.
    /// </summary>
    AllStop = 0x0167,

    /// <summary>
    ///     "all kill", said to every pet.
    /// </summary>
    AllKill = 0x0168,

    /// <summary>
    ///     "all attack", said to every pet.
    /// </summary>
    AllAttack = 0x0169,

    /// <summary>
    ///     "all guard me", said to every pet.
    /// </summary>
    AllGuardMe = 0x016B,

    /// <summary>
    ///     "all follow me", said to every pet.
    /// </summary>
    AllFollowMe = 0x016C,

    /// <summary>
    ///     "all stay", said to every pet.
    /// </summary>
    AllStay = 0x0170,

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
