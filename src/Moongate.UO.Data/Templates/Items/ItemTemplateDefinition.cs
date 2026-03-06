using System.Text.Json.Serialization;
using Moongate.UO.Data.Json.Converters;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Templates.Items;

/// <summary>
/// Represents ItemTemplateDefinition.
/// </summary>
public class ItemTemplateDefinition : ItemTemplateDefinitionBase
{
    [JsonPropertyName("base_item")]
    public string? BaseItem { get; set; }

    public string? ContainerLayoutId { get; set; }

    public List<string> Container { get; set; } = [];

    public string Description { get; set; }

    public bool Dyeable { get; set; }

    [JsonConverter(typeof(GoldValueSpecJsonConverter))]
    public GoldValueSpec GoldValue { get; set; }

    public string? GumpId { get; set; }

    [JsonConverter(typeof(HueSpecJsonConverter))]
    public HueSpec Hue { get; set; }

    public bool IsMovable { get; set; }

    public string ItemId { get; set; }

    public int WeightMax { get; set; }

    public int MaxItems { get; set; }

    public int LowDamage { get; set; }

    public int HighDamage { get; set; }

    public int Defense { get; set; }

    public int HitPoints { get; set; }

    public int Speed { get; set; }

    public int Strength { get; set; }

    public int StrengthAdd { get; set; }

    public int Dexterity { get; set; }

    public int DexterityAdd { get; set; }

    public int Intelligence { get; set; }

    public int IntelligenceAdd { get; set; }

    [JsonConverter(typeof(Int32FlexibleJsonConverter))]
    public int Ammo { get; set; }

    [JsonConverter(typeof(Int32FlexibleJsonConverter))]
    public int AmmoFx { get; set; }

    public int MaxRange { get; set; }

    public int BaseRange { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter<LootType>))]
    public LootType LootType { get; set; }

    public string ScriptId { get; set; }

    public List<string> Tags { get; set; } = [];

    public decimal Weight { get; set; }

    public ItemRarity Rarity { get; set; } = ItemRarity.None;

    public Dictionary<string, ItemTemplateParamDefinition> Params { get; set; } = [];
}
