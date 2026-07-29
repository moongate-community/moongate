using Moongate.Ultima.Tiles;
using Moongate.Ultima.Types;
using Moongate.UO.Data.Items;

namespace Moongate.Server.Services.Items;

/// <summary>
/// Fills an item template's layer and weight from the client's own tiledata, so the shard agrees with
/// what the client draws. The template keeps whatever it states outright; a layer of
/// <see cref="LayerType.None" /> and a weight of zero are read as "the file did not say".
/// <para>
/// Static, like <c>ItemTemplateYamlDeserializer</c> and <c>ItemTemplateValidator</c> beside it in the
/// loader: this runs once per template at load, not per call, and has no dependencies of its own.
/// </para>
/// </summary>
public static class TileDataTemplateResolver
{
    /// <summary>What a tile reporting no usable weight is worth instead.</summary>
    private const double NoWeightData = 1.0;

    /// <summary>
    /// The weight a tile implies. Both 0 and 255 mean "no usable data" — 255 is the sentinel UO uses
    /// for things that cannot be carried — and ModernUO's <c>Item.DefaultWeight</c> substitutes 1 for
    /// each rather than reporting a 255-stone item.
    /// </summary>
    public static double WeightFor(int tileWeight)
        => tileWeight is 0 or 255 ? NoWeightData : tileWeight;

    /// <summary>
    /// The layer a tile implies, or null when it implies none. The byte is only meaningful on a tile
    /// the client marks wearable, and a value outside <see cref="LayerType" /> is not a layer.
    /// </summary>
    public static LayerType? LayerFor(bool wearable, int layerByte)
    {
        if (!wearable || layerByte == 0 || !Enum.IsDefined((LayerType)layerByte))
        {
            return null;
        }

        return (LayerType)layerByte;
    }

    /// <summary>
    /// Fills <paramref name="template" />'s layer and weight from the tile its <c>ItemId</c> names, and
    /// reports whether anything changed. A template whose tile the client files do not describe — or a
    /// shard with no client files at all — is left exactly as the YAML wrote it.
    /// </summary>
    public static bool Resolve(ItemTemplate template)
    {
        if (TileData.ItemTable is not { Length: > 0 } tiles ||
            template.ItemId < 0 ||
            template.ItemId >= tiles.Length)
        {
            return false;
        }

        var tile = tiles[template.ItemId];
        var changed = false;

        if (LayerFor(tile.Wearable, tile.Quality) is { } layer)
        {
            if (template.Equip is null)
            {
                template.Equip = new() { Layer = layer };
                changed = true;
            }
            else if (template.Equip.Layer == LayerType.None)
            {
                template.Equip.Layer = layer;
                changed = true;
            }
        }

        if (template.Weight == 0)
        {
            template.Weight = WeightFor(tile.Weight);
            changed = true;
        }

        return changed;
    }
}
