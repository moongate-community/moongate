namespace Moongate.UO.Data.Factory;

public class WeightedEquipmentItem
{
    /// <summary>
    /// Item template ID
    /// </summary>
    public string ItemTemplateId { get; set; }

    /// <summary>
    /// Weight for random selection (higher = more likely)
    /// </summary>
    public int Weight { get; set; } = 1;
}
