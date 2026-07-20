namespace Moongate.Http.Plugin.Data.Mobiles;

/// <summary>The inputs needed to render a dressed mobile figure. Hues are resolved values, not specs.</summary>
public sealed record MobileFigureRequest(
    int Body,
    int SkinHue,
    int HairStyle,
    int HairHue,
    int FacialHairStyle,
    int FacialHairHue,
    IReadOnlyList<MobileFigureEquipment> Equipment
);
