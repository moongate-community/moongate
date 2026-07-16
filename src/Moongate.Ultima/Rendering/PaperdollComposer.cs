using Moongate.Ultima.Catalog;
using Moongate.Ultima.Data;
using Moongate.Ultima.Graphics;
using Moongate.Ultima.Imaging;
using Moongate.Ultima.Interfaces;
using Moongate.Ultima.Tiles;
using Moongate.Ultima.Types;

namespace Moongate.Ultima.Rendering;

/// <summary>
/// Stateless paperdoll compositor over <see cref="Gumps" />, <see cref="Hues" /> and
/// <see cref="TileData" />: background, hued body, hair/beard and worn equipment,
/// blitted in <see cref="PaperdollDrawOrder" /> priority.
/// </summary>
public sealed class PaperdollComposer : IPaperdollComposer
{
    private const int CanvasWidth = 260;
    private const int CanvasHeight = 237;
    private const int MaleGumpOffset = 50000;
    private const int FemaleGumpOffset = 60000;
    private const int BackgroundMale = 0x07D0;
    private const int BackgroundFemale = 0x07D1;
    private const int BodyMale = 0x000C;
    private const int BodyFemale = 0x000D;

    public Stream? Compose(PaperdollRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var layers = new List<(int Priority, UltimaBitmap Bitmap)>();
        var owned = new List<UltimaBitmap>();

        try
        {
            if (request.IncludeBackground)
            {
                var background = Gumps.GetGump(request.Female ? BackgroundFemale : BackgroundMale);

                if (background is not null)
                {
                    layers.Add((PaperdollDrawOrder.BackgroundPriority, background));
                }
            }

            var body = LoadHued(request.Female ? BodyFemale : BodyMale, request.SkinHue, true, owned);

            if (body is null)
            {
                return null;
            }

            layers.Add((PaperdollDrawOrder.BodyPriority, body));

            AddGenderGump(layers, owned, request.HairStyle, request.HairHue, LayerType.Hair, request.Female);
            AddGenderGump(
                layers,
                owned,
                request.FacialHairStyle,
                request.FacialHairHue,
                LayerType.FacialHair,
                request.Female
            );
            AddEquipment(layers, owned, request);

            using var canvas = new UltimaBitmap(CanvasWidth, CanvasHeight);

            foreach (var (_, bitmap) in layers.OrderBy(l => l.Priority))
            {
                bitmap.DrawInto(canvas, 0, 0);
            }

            return ItemCatalog.EncodePng(canvas);
        }
        finally
        {
            foreach (var bitmap in owned)
            {
                bitmap.Dispose();
            }
        }
    }

    private static void AddEquipment(
        List<(int Priority, UltimaBitmap Bitmap)> layers,
        List<UltimaBitmap> owned,
        PaperdollRequest request
    )
    {
        var table = TileData.ItemTable;

        if (table is null)
        {
            return;
        }

        foreach (var entry in request.Equipment)
        {
            if (entry.ItemId >= table.Length)
            {
                continue;
            }

            var data = table[entry.ItemId];
            var gumpId = data.Animation + (request.Female ? FemaleGumpOffset : MaleGumpOffset);
            var bitmap = Gumps.GetGump(gumpId);

            if (bitmap is null && request.Female)
            {
                bitmap = Gumps.GetGump(data.Animation + MaleGumpOffset);
            }

            if (bitmap is null)
            {
                continue;
            }

            if (entry.Hue > 0)
            {
                var partialHue = entry.PartialHueOverride ?? (data.Flags & TileFlagType.PartialHue) != 0;
                var clone = bitmap.Clone();
                owned.Add(clone);
                Hues.GetHue(entry.Hue - 1).ApplyTo(clone, partialHue);
                bitmap = clone;
            }

            var layer = data.Wearable ? (LayerType)data.Quality : LayerType.None;
            layers.Add((PaperdollDrawOrder.Priority(layer), bitmap));
        }
    }

    private static void AddGenderGump(
        List<(int Priority, UltimaBitmap Bitmap)> layers,
        List<UltimaBitmap> owned,
        int style,
        ushort hue,
        LayerType layer,
        bool female
    )
    {
        if (style <= 0)
        {
            return;
        }

        var bitmap = Gumps.GetGump(style + (female ? FemaleGumpOffset : MaleGumpOffset));

        if (bitmap is null && female)
        {
            bitmap = Gumps.GetGump(style + MaleGumpOffset);
        }

        if (bitmap is null)
        {
            return;
        }

        if (hue > 0)
        {
            var clone = bitmap.Clone();
            owned.Add(clone);
            Hues.GetHue(hue - 1).ApplyTo(clone, false);
            bitmap = clone;
        }

        layers.Add((PaperdollDrawOrder.Priority(layer), bitmap));
    }

    private static UltimaBitmap? LoadHued(int gumpId, ushort hue, bool onlyGray, List<UltimaBitmap> owned)
    {
        var bitmap = Gumps.GetGump(gumpId);

        if (bitmap is null)
        {
            return null;
        }

        if (hue == 0)
        {
            return bitmap;
        }

        var clone = bitmap.Clone();
        owned.Add(clone);
        Hues.GetHue(hue - 1).ApplyTo(clone, onlyGray);

        return clone;
    }
}
