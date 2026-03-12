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

    public string? BookId { get; set; }

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

    public int PhysicalResist { get; set; }

    public int FireResist { get; set; }

    public int ColdResist { get; set; }

    public int PoisonResist { get; set; }

    public int EnergyResist { get; set; }

    public int HitChanceIncrease { get; set; }

    public int DefenseChanceIncrease { get; set; }

    public int DamageIncrease { get; set; }

    public int SwingSpeedIncrease { get; set; }

    public int SpellDamageIncrease { get; set; }

    public int FasterCasting { get; set; }

    public int FasterCastRecovery { get; set; }

    public int LowerManaCost { get; set; }

    public int LowerReagentCost { get; set; }

    public int Luck { get; set; }

    public bool SpellChanneling { get; set; }

    public int UsesRemaining { get; set; }

    [JsonConverter(typeof(Int32FlexibleJsonConverter))]
    public int Ammo { get; set; }

    [JsonConverter(typeof(Int32FlexibleJsonConverter))]
    public int AmmoFx { get; set; }

    public int MaxRange { get; set; }

    public int BaseRange { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter<LootType>))]
    public LootType LootType { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter<AccountType>))]
    public AccountType Visibility { get; set; } = AccountType.Regular;

    public string ScriptId { get; set; }

    public List<string> FlippableItemIds { get; set; } = [];

    public List<string> Tags { get; set; } = [];

    public decimal Weight { get; set; }

    public ItemRarity Rarity { get; set; } = ItemRarity.None;

    public Dictionary<string, ItemTemplateParamDefinition> Params { get; set; } = [];
}
