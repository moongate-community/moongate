using Moongate.Http.Plugin.Data.Mobiles;
using Moongate.Http.Plugin.Interfaces.Mobiles;
using Moongate.Server.Abstractions.Interfaces.Items;
using Moongate.Ultima.Imaging;
using Moongate.Ultima.Types;

namespace Moongate.Http.Plugin.Services.Mobiles;

/// <summary>
/// Port of Moongate_Old's figure renderer on the new primitives: layers decode through the catalog,
/// order by <see cref="EquipmentDrawOrder" />, and compose anchored at each frame's anim.mul center.
/// Callers hold the Ultima read gate; this class only computes.
/// </summary>
public sealed class MobileFigureRenderer : IMobileFigureRenderer
{
    // anim.mul anchors are relative to a virtual 0x200-square origin; compositing in that space and
    // trimming to the union reproduces the client's alignment without knowing any frame in advance.
    private const int Anchor = 0x200;

    private const int DirectionCount = 5;

    private readonly IAnimationCatalog _catalog;
    private readonly IItemTemplateService _itemTemplates;

    public MobileFigureRenderer(IAnimationCatalog catalog, IItemTemplateService itemTemplates)
    {
        _catalog = catalog;
        _itemTemplates = itemTemplates;
    }

    public UltimaBitmap? Render(MobileFigureRequest request)
    {
        MobileFrame? body = null;
        var chosenDirection = 0;

        for (var direction = 0; direction < DirectionCount; direction++)
        {
            body = _catalog.GetFrame(request.Body, 0, direction, 0, request.SkinHue);

            if (body is not null)
            {
                chosenDirection = direction;

                break;
            }
        }

        if (body is null)
        {
            return null;
        }

        var layers = new List<(int Priority, MobileFrame Frame)> { (EquipmentDrawOrder.BodyPriority, body) };

        try
        {
            AddHairLayer(layers, request.HairStyle, request.HairHue, chosenDirection, LayerType.Hair);
            AddHairLayer(layers, request.FacialHairStyle, request.FacialHairHue, chosenDirection, LayerType.FacialHair);
            AddEquipmentLayers(layers, request, chosenDirection);

            return Compose([.. layers.OrderBy(layer => layer.Priority).Select(layer => layer.Frame)]);
        }
        finally
        {
            foreach (var layer in layers)
            {
                layer.Frame.Dispose();
            }
        }
    }

    private void AddEquipmentLayers(
        List<(int Priority, MobileFrame Frame)> layers,
        MobileFigureRequest request,
        int direction
    )
    {
        foreach (var worn in request.Equipment)
        {
            var template = _itemTemplates.GetById(worn.ItemTemplateId);

            if (template?.Equip is null)
            {
                continue;
            }

            var priority = EquipmentDrawOrder.Priority(template.Equip.Layer);

            if (priority == EquipmentDrawOrder.Skip)
            {
                continue;
            }

            var equipmentAnim = _catalog.GetItemAnimation(template.ItemId);

            if (equipmentAnim is null)
            {
                continue;
            }

            int finalAnim;
            int hue;

            if (_catalog.TryConvertEquipment(request.Body, equipmentAnim.Value, out var conversion))
            {
                finalAnim = conversion.AnimId;
                hue = conversion.Hue != 0 ? conversion.Hue : ResolveWornHue(worn, template.Hue);
            }
            else
            {
                finalAnim = equipmentAnim.Value;
                hue = ResolveWornHue(worn, template.Hue);
            }

            var frame = _catalog.GetFrame(finalAnim, 0, direction, 0, hue);

            if (frame is not null)
            {
                layers.Add((priority, frame));
            }
        }
    }

    private void AddHairLayer(
        List<(int Priority, MobileFrame Frame)> layers,
        int style,
        int hue,
        int direction,
        LayerType layer
    )
    {
        if (style == 0)
        {
            return;
        }

        var animationId = _catalog.GetItemAnimation(style);

        if (animationId is null)
        {
            return;
        }

        var frame = _catalog.GetFrame(animationId.Value, 0, direction, 0, hue);

        if (frame is not null)
        {
            layers.Add((EquipmentDrawOrder.Priority(layer), frame));
        }
    }

    private static UltimaBitmap Compose(IReadOnlyList<MobileFrame> layers)
    {
        var topLeftX = new int[layers.Count];
        var topLeftY = new int[layers.Count];

        for (var i = 0; i < layers.Count; i++)
        {
            topLeftX[i] = Anchor - layers[i].CenterX;
            topLeftY[i] = Anchor - layers[i].CenterY - layers[i].Bitmap.Height;
        }

        var minX = topLeftX.Min();
        var minY = topLeftY.Min();
        var maxX = int.MinValue;
        var maxY = int.MinValue;

        for (var i = 0; i < layers.Count; i++)
        {
            maxX = Math.Max(maxX, topLeftX[i] + layers[i].Bitmap.Width);
            maxY = Math.Max(maxY, topLeftY[i] + layers[i].Bitmap.Height);
        }

        var canvas = new UltimaBitmap(maxX - minX, maxY - minY);

        for (var i = 0; i < layers.Count; i++)
        {
            layers[i].Bitmap.DrawInto(canvas, topLeftX[i] - minX, topLeftY[i] - minY);
        }

        return canvas;
    }

    private static int ResolveWornHue(MobileFigureEquipment worn, int templateHue)
        => worn.Hue != 0 ? worn.Hue : templateHue;
}
