using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types.Templates;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Extensions;

/// <summary>
///     The value of an <see cref="ItemTemplate" /> field once unset fields fall back to the client's tiledata or to
///     their defaults, as in POL and ModernUO. Call them on a template whose <see cref="ItemTemplate.BaseId" /> chain
///     is already resolved.
/// </summary>
public static class ItemTemplateExtensions
{
    private const byte TiledataWeightCannotLift = 255;
    private const int DefaultDecayMinutes = 60;

    /// <summary>
    ///     Gets the weight in stones: the template's, or the tiledata weight of its graphic.
    /// </summary>
    public static decimal EffectiveWeight(this ItemTemplate template, ITileDataService tiles)
    {
        return template.Weight ?? Tile(template, tiles).Weight;
    }

    /// <summary>
    ///     Gets whether items stack: the template's, or the tiledata <see cref="TileFlagType.Generic" /> flag.
    /// </summary>
    public static bool EffectiveStackable(this ItemTemplate template, ITileDataService tiles)
    {
        return template.Stackable ?? (Tile(template, tiles).Flags & TileFlagType.Generic) != 0;
    }

    /// <summary>
    ///     Gets the layer the item is worn on: the template's, or the tiledata layer; null when neither has one.
    /// </summary>
    public static LayerType? EffectiveLayer(this ItemTemplate template, ITileDataService tiles)
    {
        if (template.Layer is { } layer)
        {
            return layer;
        }

        var tiledataLayer = (LayerType)Tile(template, tiles).Layer;

        return tiledataLayer == LayerType.None ? null : tiledataLayer;
    }

    /// <summary>
    ///     Gets whether the item can be picked up: the template's, or true unless the tiledata weight is 255.
    /// </summary>
    public static bool EffectiveMovable(this ItemTemplate template, ITileDataService tiles)
    {
        return template.Movable ?? Tile(template, tiles).Weight != TiledataWeightCannotLift;
    }

    /// <summary>
    ///     Gets whether the item decays: the template's, or whether it is movable.
    /// </summary>
    public static bool EffectiveDecays(this ItemTemplate template, ITileDataService tiles)
    {
        return template.Decays ?? template.EffectiveMovable(tiles);
    }

    /// <summary>
    ///     Gets how long a decaying item lasts: the template's minutes, or one hour.
    /// </summary>
    public static TimeSpan EffectiveDecayTime(this ItemTemplate template)
    {
        return TimeSpan.FromMinutes(template.DecayMinutes ?? DefaultDecayMinutes);
    }

    /// <summary>
    ///     Gets what happens to the item when its owner dies: the template's, or <see cref="LootType.Regular" />.
    /// </summary>
    public static LootType EffectiveLootType(this ItemTemplate template)
    {
        return template.LootType ?? LootType.Regular;
    }

    private static Data.Tiles.ItemTile Tile(ItemTemplate template, ITileDataService tiles)
    {
        return tiles.GetItem((int)template.ItemId.Value);
    }
}
