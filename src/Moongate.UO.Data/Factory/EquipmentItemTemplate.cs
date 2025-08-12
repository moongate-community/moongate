using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Factory;

public class EquipmentItemTemplate
{
    /// <summary>
    /// Item template ID to equip
    /// </summary>
    public string ItemTemplateId { get; set; }

    /// <summary>
    /// Layer where this item should be equipped
    /// </summary>
    public ItemLayerType Layer { get; set; }

}
