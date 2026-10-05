namespace Moongate.Server.Ultima.Data.Death;

/// <summary>
///     What a corpse keeps in its props, and its graphic. A corpse is an item like any other: its amount stays 1, and
///     the body it shows travels to the client in the place of the amount.
/// </summary>
public static class CorpseProps
{
    /// <summary>
    ///     The graphic of every corpse; the client draws the body it is told in the place of the amount.
    /// </summary>
    public const int Graphic = 0x2006;

    /// <summary>
    ///     The id of the item template a corpse is made from.
    /// </summary>
    public const string Template = "corpse";

    /// <summary>
    ///     The body of who died, a whole number: the client draws the corpse with it.
    /// </summary>
    public const string Body = "corpse.body";

    /// <summary>
    ///     The way who died was facing, a DirectionType number: the corpse lies that way.
    /// </summary>
    public const string Direction = "corpse.direction";

    /// <summary>
    ///     The id of the mobile template of who died, when it had one.
    /// </summary>
    public const string MobileTemplate = "corpse.template";

    /// <summary>
    ///     The serial of who killed it, when someone did.
    /// </summary>
    public const string Killer = "corpse.killer";
}
