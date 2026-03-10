using System.Text.Json.Serialization;
using Moongate.UO.Data.Json.Converters;
using Moongate.UO.Data.Templates.Items;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Templates.Mobiles;

/// <summary>
/// Serializable definition of a mobile spawn template.
/// </summary>
public class MobileTemplateDefinition : MobileTemplateDefinitionBase
{
    [JsonPropertyName("base_mobile")]
    public string? BaseMobile { get; set; }

    [JsonConverter(typeof(Int32FlexibleJsonConverter))]
    public int Body { get; set; }

    [JsonConverter(typeof(HueSpecJsonConverter))]
    public HueSpec SkinHue { get; set; }

    [JsonConverter(typeof(HueSpecJsonConverter))]
    public HueSpec HairHue { get; set; }

    public int HairStyle { get; set; }

    public int Strength { get; set; } = 50;

    public int Dexterity { get; set; } = 50;

    public int Intelligence { get; set; } = 50;

    public int Hits { get; set; } = 100;

    public int MaxHits { get; set; }

    public int Mana { get; set; } = 100;

    public int Stamina { get; set; } = 100;

    public int MinDamage { get; set; }

    public int MaxDamage { get; set; }

    public int ArmorRating { get; set; }

    public int Fame { get; set; }

    public int Karma { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter<Notoriety>))]
    public Notoriety Notoriety { get; set; } = Notoriety.Innocent;

    public string Brain { get; set; } = "None";

    public string? SellProfileId { get; set; }

    public Dictionary<MobileSoundType, int> Sounds { get; set; } = new();

    [JsonConverter(typeof(GoldValueSpecJsonConverter))]
    public GoldValueSpec GoldDrop { get; set; }

    public List<string> LootTables { get; set; } = [];

    public Dictionary<string, int> Skills { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public int TamingDifficulty { get; set; }

    public int ProvocationDifficulty { get; set; }

    public int PacificationDifficulty { get; set; }

    public int ControlSlots { get; set; }

    public bool CanRun { get; set; }

    public int FleesAtHitsPercent { get; set; } = -1;

    public int SpellAttackType { get; set; }

    public int SpellAttackDelay { get; set; }

    public List<MobileEquipmentItemTemplate> FixedEquipment { get; set; } = [];

    public List<MobileRandomEquipmentPoolTemplate> RandomEquipment { get; set; } = [];

    public Dictionary<string, ItemTemplateParamDefinition> Params { get; set; } = [];
}
