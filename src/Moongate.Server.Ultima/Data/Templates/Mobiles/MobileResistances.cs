using Moongate.Core.Primitives;

namespace Moongate.Server.Ultima.Data.Templates.Mobiles;

/// <summary>
///     The resistances of a mobile template, in percent, rolled for every mobile; an unset one is 0.
/// </summary>
public class MobileResistances
{
    /// <summary>
    ///     Resistance to physical damage.
    /// </summary>
    public DiceSpec? Physical { get; set; }

    /// <summary>
    ///     Resistance to fire damage.
    /// </summary>
    public DiceSpec? Fire { get; set; }

    /// <summary>
    ///     Resistance to cold damage.
    /// </summary>
    public DiceSpec? Cold { get; set; }

    /// <summary>
    ///     Resistance to poison damage.
    /// </summary>
    public DiceSpec? Poison { get; set; }

    /// <summary>
    ///     Resistance to energy damage.
    /// </summary>
    public DiceSpec? Energy { get; set; }
}
