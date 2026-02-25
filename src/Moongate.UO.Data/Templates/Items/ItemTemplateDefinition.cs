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

    [JsonPropertyName("weightmax")]
    public int WeightMax { get; set; }

    [JsonPropertyName("maxitems")]
    public int MaxItems { get; set; }

    [JsonPropertyName("lodamage")]
    public int LowDamage { get; set; }

    [JsonPropertyName("hidamage")]
    public int HighDamage { get; set; }

    [JsonPropertyName("def")]
    public int Defense { get; set; }

    [JsonPropertyName("hp")]
    public int HitPoints { get; set; }

    [JsonPropertyName("spd")]
    public int Speed { get; set; }

    [JsonPropertyName("str")]
    public int Strength { get; set; }

    [JsonPropertyName("stradd")]
    public int StrengthAdd { get; set; }

    [JsonPropertyName("dex")]
    public int Dexterity { get; set; }

    [JsonPropertyName("dexadd")]
    public int DexterityAdd { get; set; }

    [JsonPropertyName("int")]
    public int Intelligence { get; set; }

    [JsonPropertyName("intadd")]
    public int IntelligenceAdd { get; set; }

    [JsonPropertyName("ammo"), JsonConverter(typeof(Int32FlexibleJsonConverter))]
    public int Ammo { get; set; }

    [JsonPropertyName("ammofx"), JsonConverter(typeof(Int32FlexibleJsonConverter))]
    public int AmmoFx { get; set; }

    [JsonPropertyName("maxrange")]
    public int MaxRange { get; set; }

    [JsonPropertyName("baserange")]
    public int BaseRange { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter<LootType>))]
    public LootType LootType { get; set; }

    public string ScriptId { get; set; }

    public bool Stackable { get; set; }

    public List<string> Tags { get; set; } = [];

    public decimal Weight { get; set; }
}
