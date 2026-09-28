namespace Moongate.Server.Ultima.Types.Mobiles;

/// <summary>
///     The state bits a mobile carries in 0x20 and 0x78: how the client draws it and its health bar.
/// </summary>
[Flags]
public enum MobileFlagsType : byte
{
    None = 0x00,

    /// <summary>
    ///     Paralyzed or frozen: the client does not animate it.
    /// </summary>
    Frozen = 0x01,

    Female = 0x02,

    /// <summary>
    ///     Flying, for gargoyles (Stygian Abyss and later clients).
    /// </summary>
    Flying = 0x04,

    /// <summary>
    ///     A yellow health bar, as for a blessed mobile.
    /// </summary>
    YellowHealthBar = 0x08,

    /// <summary>
    ///     Walks through other mobiles.
    /// </summary>
    IgnoreMobiles = 0x10,

    WarMode = 0x40,

    Hidden = 0x80
}
