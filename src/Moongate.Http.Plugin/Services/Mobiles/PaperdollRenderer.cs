using Moongate.Http.Plugin.Data.Mobiles;
using Moongate.Http.Plugin.Interfaces.Mobiles;
using Moongate.Server.Abstractions.Interfaces.Items;
using Moongate.Ultima.Imaging;
using Moongate.Ultima.Types;
using Moongate.UO.Data.Types;

namespace Moongate.Http.Plugin.Services.Mobiles;

/// <summary>
/// Port of Moongate_Old's paperdoll compositor: gump layers drawn at (0,0) in
/// <see cref="PaperdollDrawOrder" /> onto a canvas sized to the largest layer — gump art is
/// pre-aligned, unlike the animated figure's anim.mul-anchored frames.
/// </summary>
public sealed class PaperdollRenderer : IPaperdollRenderer
{
    private const int MaleOffset = 50000;
    private const int FemaleOffset = 60000;
    private const int BackgroundMale = 0x07D0;
    private const int BackgroundFemale = 0x07D1;
    private const int BodyMale = 0x000C;
    private const int BodyFemale = 0x000D;

    private readonly IGumpCatalog _gumps;
    private readonly IAnimationCatalog _animations;
    private readonly IItemTemplateService _itemTemplates;

    public PaperdollRenderer(IGumpCatalog gumps, IAnimationCatalog animations, IItemTemplateService itemTemplates)
    {
        _gumps = gumps;
        _animations = animations;
        _itemTemplates = itemTemplates;
    }

    public UltimaBitmap? Render(PaperdollRenderRequest request)
    {
        var female = request.Gender == GenderType.Female;
        var layers = new List<(int Priority, UltimaBitmap Bitmap)>();

        try
        {
            if (request.IncludeBackground)
            {
                var background = _gumps.GetGump(female ? BackgroundFemale : BackgroundMale, 0, false);

                if (background is not null)
                {
                    layers.Add((PaperdollDrawOrder.BackgroundPriority, background));
                }
            }

            var body = _gumps.GetGump(female ? BodyFemale : BodyMale, request.SkinHue, false);

            if (body is null)
            {
                return null;
            }

            layers.Add((PaperdollDrawOrder.BodyPriority, body));

            AddStyleLayer(layers, request.HairStyle, request.HairHue, PaperdollDrawOrder.Priority(LayerType.Hair), female);
            AddStyleLayer(
                layers,
                request.FacialHairStyle,
                request.FacialHairHue,
                PaperdollDrawOrder.Priority(LayerType.FacialHair),
                female
            );
            AddEquipmentLayers(layers, request.Equipment, female);

            return Compose([.. layers.OrderBy(layer => layer.Priority).Select(layer => layer.Bitmap)]);
        }
        finally
        {
            foreach (var layer in layers)
            {
                layer.Bitmap.Dispose();
            }
        }
    }

    private void AddEquipmentLayers(
        List<(int Priority, UltimaBitmap Bitmap)> layers,
        IReadOnlyList<MobileFigureEquipment> equipment,
        bool female
    )
    {
        foreach (var worn in equipment)
        {
            var template = _itemTemplates.GetById(worn.ItemTemplateId);

            if (template?.Equip is null)
            {
                continue;
            }

            var priority = PaperdollDrawOrder.Priority(template.Equip.Layer);

            if (priority == PaperdollDrawOrder.Skip)
            {
                continue;
            }

            var animationId = _animations.GetItemAnimation(template.ItemId);

            if (animationId is null)
            {
                continue;
            }

            var hue = worn.Hue != 0 ? worn.Hue : template.Hue;
            var gump = LoadGenderGump(animationId.Value, hue, female);

            if (gump is not null)
            {
                layers.Add((priority, gump));
            }
        }
    }

    private void AddStyleLayer(
        List<(int Priority, UltimaBitmap Bitmap)> layers,
        int style,
        int hue,
        int priority,
        bool female
    )
    {
        if (style <= 0)
        {
            return;
        }

        // Hair/beard paperdoll gumps are derived from the style item's animation id, like equipment,
        // not from the raw style id.
        var animationId = _animations.GetItemAnimation(style);

        if (animationId is null)
        {
            return;
        }

        var gump = LoadGenderGump(animationId.Value, hue, female);

        if (gump is not null)
        {
            layers.Add((priority, gump));
        }
    }

    private UltimaBitmap? LoadGenderGump(int artId, int hue, bool female)
    {
        var gump = _gumps.GetGump(artId + (female ? FemaleOffset : MaleOffset), hue, false);

        // A number of items (and every hair style) never shipped a female paperdoll variant; the male
        // gump is the client's own fallback.
        return gump ?? (female ? _gumps.GetGump(artId + MaleOffset, hue, false) : null);
    }

    private static UltimaBitmap Compose(IReadOnlyList<UltimaBitmap> layers)
    {
        var width = layers.Max(layer => layer.Width);
        var height = layers.Max(layer => layer.Height);
        var canvas = new UltimaBitmap(width, height);

        foreach (var layer in layers)
        {
            layer.DrawInto(canvas, 0, 0);
        }

        return canvas;
    }
}
