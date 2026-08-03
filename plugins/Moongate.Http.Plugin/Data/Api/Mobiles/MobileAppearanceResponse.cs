using Moongate.UO.Data.Mobiles.Templates;

namespace Moongate.Http.Plugin.Data.Api.Mobiles;

/// <summary>How a template says a mobile looks, shared by the template and each of its variants.</summary>
/// <param name="Body">The animation body id.</param>
/// <param name="SkinHue">
/// A hue <em>spec</em>, not a number: a single value or a range like <c>0x455-0x45a</c> resolved once
/// per spawn. Reported as written, because picking one end of a range would lie about the other.
/// </param>
/// <param name="HairHue">A hue spec, like <paramref name="SkinHue" />.</param>
/// <param name="FacialHairHue">A hue spec, like <paramref name="SkinHue" />.</param>
public sealed record MobileAppearanceResponse(
    int Body,
    string? SkinHue,
    int HairStyle,
    string? HairHue,
    int FacialHairStyle,
    string? FacialHairHue
)
{
    /// <summary>Projects a template's appearance.</summary>
    public static MobileAppearanceResponse From(MobileAppearance appearance)
        => new(
            appearance.Body,
            appearance.SkinHue,
            appearance.HairStyle,
            appearance.HairHue,
            appearance.FacialHairStyle,
            appearance.FacialHairHue
        );
}
