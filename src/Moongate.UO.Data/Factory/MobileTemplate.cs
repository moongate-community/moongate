using System.Text.Json.Serialization;
using Moongate.Core.Server.Json.Converters;

namespace Moongate.UO.Data.Factory;

public class MobileTemplate : BaseTemplate
{
    [JsonConverter(typeof(HexValueConverter<int>))]
    public int Body { get; set; }

    public int SkinHue { get; set; }

    public int HairHue { get; set; }

    public int HairStyle { get; set; }

    public int Strength { get; set; } = 50;

    public int Dexterity { get; set; } = 50;

    public int Intelligence { get; set; } = 50;

    public int Hits { get; set; } = 100;

    public int Mana { get; set; } = 100;

    public int Stamina { get; set; } = 100;

    public string Brain { get; set; } = "None";


    /// <summary>
    /// Fixed equipment that always spawns on this mobile
    /// </summary>
    public List<EquipmentItemTemplate> FixedEquipment { get; set; } = new();

    /// <summary>
    /// Random equipment pools with probability
    /// </summary>
    public List<RandomEquipmentPoolTemplate> RandomEquipment { get; set; } = new();
}
