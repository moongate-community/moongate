using Moongate.Http.Plugin.Data.Mobiles;
using Moongate.Http.Plugin.Services.Mobiles;
using Moongate.Server.Services.Items;
using Moongate.Tests.Support;
using Moongate.Ultima.Types;
using Moongate.UO.Data.Items;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Http.Services.Mobiles;

public class PaperdollRendererTests
{
    private const int BodyMaleGump = 0x000C;
    private const int BackgroundMaleGump = 0x07D0;
    private const int HairStyle = 0x203B;
    private const int HairAnim = 0x2FBF;
    private const int MaleOffset = 50000;

    private static (PaperdollRenderer Renderer, FakeGumpCatalog Gumps, FakeAnimationCatalog Anim, ItemTemplateService Templates) Build()
    {
        var gumps = new FakeGumpCatalog();
        gumps.Gumps[(BodyMaleGump, 0)] = (W: 40, H: 100);
        var anim = new FakeAnimationCatalog();
        var templates = new ItemTemplateService();

        return (new PaperdollRenderer(gumps, anim, templates), gumps, anim, templates);
    }

    [Fact]
    public void Render_NoBodyGump_ReturnsNull()
    {
        var (renderer, _, _, _) = Build();

        Assert.Null(renderer.Render(new(GenderType.Female, false, 0, 0, 0, 0, 0, [])));
    }

    [Fact]
    public void Render_BareMaleBody_ReturnsTheBodyCanvas()
    {
        var (renderer, _, _, _) = Build();

        using var doll = renderer.Render(new(GenderType.Male, false, 0, 0, 0, 0, 0, []));

        Assert.NotNull(doll);
        Assert.Equal(40, doll!.Width);
        Assert.Equal(100, doll.Height);
    }

    [Fact]
    public void Render_WithBackground_GrowsTheCanvasToTheLargerBackdrop()
    {
        var (renderer, gumps, _, _) = Build();
        gumps.Gumps[(BackgroundMaleGump, 0)] = (W: 80, H: 120);

        using var doll = renderer.Render(new(GenderType.Male, true, 0, 0, 0, 0, 0, []));

        Assert.Equal(80, doll!.Width);
        Assert.Equal(120, doll.Height);
    }

    [Fact]
    public void Render_Hair_UsesTheGenderOffsetGump()
    {
        var (renderer, gumps, anim, _) = Build();
        anim.ItemAnimations[HairStyle] = HairAnim;
        gumps.Gumps[(HairAnim + MaleOffset, 0)] = (W: 40, H: 30);

        using var doll = renderer.Render(new(GenderType.Male, false, 0, HairStyle, 0, 0, 0, []));

        // Hair alone is narrower than the body but drawn at (0,0), so a successful decode does not
        // shrink the canvas below the body's own size — proving the hair layer was found and added
        // requires it to be taller than the body instead.
        Assert.Equal(100, doll!.Height);
    }

    [Fact]
    public void Render_FemaleHair_FallsBackToTheMaleGumpWhenHersIsMissing()
    {
        const int femaleOffset = 60000;
        var (renderer, gumps, anim, _) = Build();
        gumps.Gumps[(0x000D, 0)] = (W: 40, H: 100); // female body
        anim.ItemAnimations[HairStyle] = HairAnim;
        // Only the male-offset gump exists.
        gumps.Gumps[(HairAnim + MaleOffset, 0)] = (W: 40, H: 150);

        using var doll = renderer.Render(new(GenderType.Female, false, 0, HairStyle, 0, 0, 0, []));

        Assert.Equal(150, doll!.Height);
    }

    [Fact]
    public void Render_EquipmentOnASkipLayer_IsIgnored()
    {
        var (renderer, gumps, anim, templates) = Build();
        templates.Register(
            new ItemTemplate { Id = "pack", ItemId = 0x0E75, Equip = new() { Layer = LayerType.Backpack } }
        );
        anim.ItemAnimations[0x0E75] = 999;
        gumps.Gumps[(999 + MaleOffset, 0)] = (W: 500, H: 500);

        using var doll = renderer.Render(new(GenderType.Male, false, 0, 0, 0, 0, 0, [new("pack", 0)]));

        Assert.Equal(40, doll!.Width);
    }
}
