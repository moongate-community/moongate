using System.Text.Json.Serialization;
using Moongate.UO.Data.Json.Converters;
using Moongate.UO.Data.Templates.Items;

namespace Moongate.UO.Data.Templates.Mobiles;

/// <summary>
/// Weighted item option used by equipment entries.
/// </summary>
public class MobileWeightedEquipmentItemTemplate
{
    public string ItemTemplateId { get; set; }

    public int Weight { get; set; } = 1;

    [JsonConverter(typeof(HueSpecJsonConverter))]
    public HueSpec? Hue { get; set; }

    public Dictionary<string, ItemTemplateParamDefinition> Params { get; set; } = [];
}
