using Moongate.UO.Data.Types;

namespace Moongate.Http.Plugin.Data.Mobiles;

/// <summary>The inputs needed to render a paperdoll. Hues are resolved values, not specs.</summary>
public sealed record PaperdollRenderRequest(
    GenderType Gender,
    bool IncludeBackground,
    int SkinHue,
    int HairStyle,
    int HairHue,
    int FacialHairStyle,
    int FacialHairHue,
    IReadOnlyList<MobileFigureEquipment> Equipment
);
