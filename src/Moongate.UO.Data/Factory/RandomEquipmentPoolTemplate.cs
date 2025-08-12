using System.Text.Json.Serialization;
using Moongate.Core.Server.Json.Converters;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Factory;

public class RandomEquipmentPoolTemplate
{
    /// <summary>
    /// Name of this equipment pool for debugging
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Layer this pool affects
    /// </summary>
    public ItemLayerType Layer { get; set; }

    /// <summary>
    /// Chance that ANY item from this pool will spawn (0.0 to 1.0)
    /// </summary>
    [JsonConverter(typeof(RandomValueConverter<float>))]
    public float SpawnChance { get; set; } = 1.0f;

    /// <summary>
    /// Possible items to choose from
    /// </summary>
    public List<WeightedEquipmentItem> Items { get; set; } = new();
}
