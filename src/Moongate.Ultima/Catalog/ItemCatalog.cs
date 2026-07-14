using Moongate.Ultima.Data;
using Moongate.Ultima.Graphics;
using Moongate.Ultima.Imaging;
using Moongate.Ultima.Interfaces;
using Moongate.Ultima.Tiles;
using Moongate.Ultima.Types;
using SixLabors.ImageSharp;

namespace Moongate.Ultima.Catalog;

/// <summary>
/// Stateless facade over <see cref="TileData" />, <see cref="Art" /> and <see cref="Hues" />.
/// </summary>
public sealed class ItemCatalog : IItemCatalog
{
    public UoItemInfo? GetItem(uint itemId)
    {
        var table = TileData.ItemTable;

        if (table is null || itemId >= table.Length)
        {
            return null;
        }

        var data = table[itemId];

        // cache-owned bitmap: never dispose (the LRU may hand it to other callers)
        var art = Art.GetStatic((int)itemId, false);

        return new()
        {
            ItemId = itemId,
            Name = data.Name ?? string.Empty,
            Flags = data.Flags,
            Weight = data.Weight,
            Quality = data.Quality,
            Quantity = data.Quantity,
            Value = data.Value,
            Hue = data.Hue,
            StackingOffset = data.StackingOffset,
            Height = data.Height,
            MiscData = data.MiscData,
            Animation = data.Animation,
            Layer = data.Wearable ? (LayerType)data.Quality : LayerType.None,
            HasArt = art is not null,
            ArtWidth = art?.Width ?? 0,
            ArtHeight = art?.Height ?? 0
        };
    }

    public Stream? GetItemImage(uint itemId, ushort hue = 0)
    {
        var cached = Art.GetStatic((int)itemId, false);

        if (cached is null)
        {
            return null;
        }

        UltimaBitmap? owned = null;

        try
        {
            var bitmap = cached;

            if (hue > 0)
            {
                owned = cached.Clone();

                var partialHue = false;
                var table = TileData.ItemTable;

                if (table is not null && itemId < table.Length)
                {
                    partialHue = (table[itemId].Flags & TileFlagType.PartialHue) != 0;
                }

                Hues.GetHue(hue - 1).ApplyTo(owned, partialHue);
                bitmap = owned;
            }

            return EncodePng(bitmap);
        }
        finally
        {
            owned?.Dispose();
        }
    }

    internal static Stream EncodePng(UltimaBitmap bitmap)
    {
        using var image = bitmap.ToImage();
        var stream = new MemoryStream();
        image.SaveAsPng(stream);
        stream.Position = 0;

        return stream;
    }
}
