namespace Moongate.UO.Data.Mobiles.Templates;

/// <summary>Appearance data for a mobile template or variant. Hues are unresolved specs.</summary>
public sealed class MobileAppearance
{
    public int Body { get; set; }

    public string? SkinHue { get; set; }

    public int HairStyle { get; set; }

    public string? HairHue { get; set; }

    public int FacialHairStyle { get; set; }

    public string? FacialHairHue { get; set; }
}
