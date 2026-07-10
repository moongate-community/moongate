using Moongate.Ultima.Types;

namespace Moongate.UO.Data.Items;

/// <summary>Wearable attributes: paperdoll layer, durability and the stat requirements to equip.</summary>
public sealed class EquipSpec
{
    public LayerType Layer { get; set; }
    public int? HitPoints { get; set; }
    public int? StrengthReq { get; set; }
    public int? DexterityReq { get; set; }
    public int? IntelligenceReq { get; set; }
}
