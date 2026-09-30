namespace Moongate.Server.Ultima.Types.Mobiles;

/// <summary>
///     Where a mobile template's mobiles move, as UOX3's <c>MOVEMENT</c> of <c>creatures.dfn</c>.
/// </summary>
public enum MobileMovementType : byte
{
    /// <summary>
    ///     On land only.
    /// </summary>
    Land = 0,

    /// <summary>
    ///     In water only, such as a dolphin.
    /// </summary>
    Water = 1,

    /// <summary>
    ///     On land and in water, such as a walrus.
    /// </summary>
    Both = 2
}
