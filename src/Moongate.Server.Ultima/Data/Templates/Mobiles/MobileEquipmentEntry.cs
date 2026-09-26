using Moongate.Core.Primitives;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Data.Templates.Mobiles;

/// <summary>
///     One piece of equipment a mobile template puts on every mobile made from it.
/// </summary>
public class MobileEquipmentEntry
{
    /// <summary>
    ///     Item template ids; one is picked at random, so
    ///     <c>
    ///         ["leather_skirt", "leather_shorts"]
    ///     </c>
    ///     gives either.
    /// </summary>
    public List<string> Items { get; set; } = [];

    /// <summary>
    ///     The hue to give the item, or a range to pick one from; unset keeps the item's own hue.
    /// </summary>
    public HueSpec? Hue { get; set; }

    /// <summary>
    ///     Equips only mobiles of this gender, such as a skirt for female guards; unset equips both.
    /// </summary>
    public GenderType? Gender { get; set; }
}
