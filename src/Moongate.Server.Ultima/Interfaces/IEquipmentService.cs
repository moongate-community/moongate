using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Decides where an item is worn and whether a mobile can put it on.
/// </summary>
public interface IEquipmentService
{
    /// <summary>
    ///     Gets the layer the item is worn on: its template's, else the tiledata layer of a wearable graphic; false when
    ///     it has none.
    /// </summary>
    bool TryGetLayer(ItemEntity item, out LayerType layer);

    /// <summary>
    ///     Whether <paramref name="mobile" /> can wear <paramref name="item" /> on <paramref name="layer" />: a layer worn
    ///     from the paperdoll (not the backpack, hair, beard, mount or the shop and bank layers), free, and as ModernUO
    ///     and POL, a two-handed weapon only with both hands free and nothing else in hand while one is worn.
    /// </summary>
    bool CanWear(Serial mobile, ItemEntity item, LayerType layer);
}
