using System.Diagnostics.CodeAnalysis;
using Moongate.Core.Primitives;
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

    /// <summary>
    ///     Builds the tooltip of <paramref name="target" /> when <paramref name="viewer" />, a mobile in the world, can
    ///     see it: an item it carries or wears, an item worn by a mobile in view range, an item on the ground in view
    ///     range, or a mobile in view range on its map. False for anything else, so a client cannot read what it does
    ///     not see.
    /// </summary>
    bool TryBuildFor(Serial viewer, Serial target, [NotNullWhen(true)] out PropertyList? list);
}
