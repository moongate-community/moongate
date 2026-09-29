using Moongate.Server.Ultima.Data.Tooltips;
using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Builds the AOS tooltip (object property list) of an item or a mobile, as the 0xD6 packet carries it.
/// </summary>
public interface ITooltipService
{
    /// <summary>
    ///     The item's lines: its name (the client's cliloc for its graphic when it has no name of its own or from its
    ///     template, with the amount for a stack), blessed or cursed, its weight and, above common, its rarity in the
    ///     server language.
    /// </summary>
    PropertyList Build(ItemEntity item);

    /// <summary>
    ///     The mobile's line: its name and title.
    /// </summary>
    PropertyList Build(MobileEntity mobile);
}
