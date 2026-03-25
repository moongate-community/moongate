using System.Text.Json.Serialization;
using Moongate.UO.Data.Json.Converters;
using Moongate.UO.Data.Templates.Items;

namespace Moongate.UO.Data.Templates.Mobiles;

/// <summary>
/// Defines the appearance data for a mobile variant.
/// </summary>
public class MobileAppearanceTemplate
{
    [JsonConverter(typeof(Int32FlexibleJsonConverter))]
    public int Body { get; set; }

    [JsonConverter(typeof(HueSpecJsonConverter))]
    public HueSpec? SkinHue { get; set; }

    [JsonConverter(typeof(HueSpecJsonConverter))]
    public HueSpec? HairHue { get; set; }

    public int HairStyle { get; set; }

    [JsonConverter(typeof(HueSpecJsonConverter))]
    public HueSpec? FacialHairHue { get; set; }

    public int FacialHairStyle { get; set; }
}
