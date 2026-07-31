using Moongate.Http.Plugin.Services.Mobiles;
using Moongate.Persistence.Entities;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Http.Mobiles;

/// <summary>
/// The hash is the cache file's name and the ETag, so everything visible has to be in it and
/// nothing invisible may be. It is also why nothing needs invalidating: a new look is a new key.
/// </summary>
public class MobileAppearanceHashTests
{
    [Fact]
    public void TheSameAppearance_HashesTheSame()
        => Assert.Equal(MobileAppearanceHash.Of(Mobile(), []), MobileAppearanceHash.Of(Mobile(), []));

    [Fact]
    public void ADifferentBody_HashesDifferently()
    {
        var other = Mobile();

        other.Body = 401;

        Assert.NotEqual(MobileAppearanceHash.Of(Mobile(), []), MobileAppearanceHash.Of(other, []));
    }

    [Fact]
    public void ADifferentSkinHue_HashesDifferently()
    {
        var other = Mobile();

        other.SkinHue = new(1002);

        Assert.NotEqual(MobileAppearanceHash.Of(Mobile(), []), MobileAppearanceHash.Of(other, []));
    }

    [Fact]
    public void ADifferentHairStyle_HashesDifferently()
    {
        var other = Mobile();

        other.HairStyle = 0x203C;

        Assert.NotEqual(MobileAppearanceHash.Of(Mobile(), []), MobileAppearanceHash.Of(other, []));
    }

    [Fact]
    public void AWornItem_ChangesTheHash()
        => Assert.NotEqual(
            MobileAppearanceHash.Of(Mobile(), []),
            MobileAppearanceHash.Of(Mobile(), [Worn("plate_helm", LayerType.Helm, 0)])
        );

    [Fact]
    public void ADifferentItemHue_ChangesTheHash()
        => Assert.NotEqual(
            MobileAppearanceHash.Of(Mobile(), [Worn("plate_helm", LayerType.Helm, 0)]),
            MobileAppearanceHash.Of(Mobile(), [Worn("plate_helm", LayerType.Helm, 33)])
        );

    // Ordered by layer before hashing, so the store handing them back in a different order does
    // not produce a second file for the same outfit.
    [Fact]
    public void TheOrderItemsComeBackIn_DoesNotMatter()
    {
        ItemEntity[] helmFirst = [Worn("plate_helm", LayerType.Helm, 0), Worn("plate_gloves", LayerType.Gloves, 0)];
        ItemEntity[] glovesFirst = [Worn("plate_gloves", LayerType.Gloves, 0), Worn("plate_helm", LayerType.Helm, 0)];

        Assert.Equal(MobileAppearanceHash.Of(Mobile(), helmFirst), MobileAppearanceHash.Of(Mobile(), glovesFirst));
    }

    // The whole point of content addressing: two characters dressed alike share one file.
    [Fact]
    public void TwoIdenticallyDressedMobiles_ShareAHash()
    {
        var one = Mobile();
        var two = Mobile();

        two.Name = "Someone Else";

        Assert.Equal(
            MobileAppearanceHash.Of(one, [Worn("plate_helm", LayerType.Helm, 0)]),
            MobileAppearanceHash.Of(two, [Worn("plate_helm", LayerType.Helm, 0)])
        );
    }

    // An item held or carried is not worn, and must not move the fingerprint.
    [Fact]
    public void AnItemOnNoLayer_IsIgnored()
        => Assert.Equal(
            MobileAppearanceHash.Of(Mobile(), []),
            MobileAppearanceHash.Of(Mobile(), [new() { TemplateId = "plate_helm" }])
        );

    private static MobileEntity Mobile()
        => new()
        {
            Name = "Squid",
            Body = 400,
            SkinHue = new(1001),
            HairStyle = 0x203B,
            HairHue = new(1102)
        };

    // Fingerprinted by template id, which is what the renderer is handed -- not by art id.
    private static ItemEntity Worn(string templateId, LayerType layer, ushort hue)
        => new() { TemplateId = templateId, EquippedLayer = layer, Hue = new(hue) };
}
