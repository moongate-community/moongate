using Moongate.Ultima.Types;

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
}
