using Moongate.Http.Plugin.Data.Mobiles;
using Moongate.Http.Plugin.Services.Mobiles;
using Moongate.Server.Services.Items;
using Moongate.Tests.Support;
using Moongate.Ultima.Types;
using Moongate.UO.Data.Items;

namespace Moongate.Tests.Http.Services.Mobiles;

public class MobileFigureRendererTests
{
    private const int Body = 400;
    private const int HairStyle = 0x203B;
    private const int HairAnim = 0x2FBF;

    private static (MobileFigureRenderer Renderer, FakeAnimationCatalog Catalog, ItemTemplateService Templates) Build()
    {
        var catalog = new FakeAnimationCatalog();
        catalog.Frames[(Body, 0)] = (W: 30, H: 50, Cx: 15, Cy: 0);
        var templates = new ItemTemplateService();

        return (new MobileFigureRenderer(catalog, templates), catalog, templates);
    }

    [Fact]
    public void Render_BodyWithoutAnimation_ReturnsNull()
    {
        var (renderer, _, _) = Build();

        Assert.Null(renderer.Render(new(999, 0, 0, 0, 0, 0, [])));
    }

    [Fact]
    public void Render_BareBody_ReturnsTheBodyCanvas()
    {
        var (renderer, _, _) = Build();

        using var figure = renderer.Render(new(Body, 0, 0, 0, 0, 0, []));

        Assert.NotNull(figure);
        Assert.Equal(30, figure!.Width);
        Assert.Equal(50, figure.Height);
    }

    [Fact]
    public void Render_HairLayer_GrowsTheCanvasToTheUnion()
    {
        var (renderer, catalog, _) = Build();
        catalog.ItemAnimations[HairStyle] = HairAnim;
        // Hair frame is taller than the body and anchored higher: the canvas must cover both.
        catalog.Frames[(HairAnim, 0)] = (W: 30, H: 60, Cx: 15, Cy: 0);

        using var figure = renderer.Render(new(Body, 0, HairStyle, 0, 0, 0, []));

        Assert.Equal(60, figure!.Height);
    }

    [Fact]
    public void Render_EquipmentOnASkipLayer_IsIgnored()
    {
        var (renderer, catalog, templates) = Build();
        templates.Register(
            new ItemTemplate { Id = "pack", ItemId = 0x0E75, Equip = new() { Layer = LayerType.Backpack } }
        );
        catalog.ItemAnimations[0x0E75] = 999;
        catalog.Frames[(999, 0)] = (W: 200, H: 200, Cx: 0, Cy: 0);

        using var figure = renderer.Render(new(Body, 0, 0, 0, 0, 0, [new("pack", 0)]));

        // The oversized backpack frame must not have grown the canvas.
        Assert.Equal(30, figure!.Width);
    }

    [Fact]
    public void Render_EquipConv_ReroutesTheAnimationAndHue()
    {
        var (renderer, catalog, templates) = Build();
        templates.Register(
            new ItemTemplate { Id = "sword", ItemId = 0x13FF, Equip = new() { Layer = LayerType.OneHanded } }
        );
        catalog.ItemAnimations[0x13FF] = 475;
        catalog.Conversions[(Body, 475)] = (267, 1153);
        // Only the converted (anim, hue) pair decodes: if the renderer skipped equipconv, the layer
        // would silently drop and the canvas would stay at body size.
        catalog.Frames[(267, 1153)] = (W: 40, H: 50, Cx: 20, Cy: 0);

        using var figure = renderer.Render(new(Body, 0, 0, 0, 0, 0, [new("sword", 0)]));

        Assert.Equal(40, figure!.Width);
    }
}
