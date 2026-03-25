using System.Text.Json.Serialization;
using Moongate.UO.Data.Json.Converters;
using Moongate.UO.Data.Templates.Items;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Templates.Mobiles;

/// <summary>
/// Defines a mobile equipment slot with a direct item or a weighted item pool.
/// </summary>
public class MobileEquipmentEntryTemplate
{
    public ItemLayerType Layer { get; set; }

    public string? ItemTemplateId { get; set; }

    public double Chance { get; set; } = 1.0;

    [JsonConverter(typeof(HueSpecJsonConverter))]
    public HueSpec? Hue { get; set; }

    public Dictionary<string, ItemTemplateParamDefinition> Params { get; set; } = [];

    public List<MobileWeightedEquipmentItemTemplate> Items { get; set; } = [];
}
