using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Items;

/// <summary>
/// A declarative item definition: base attributes plus optional typed specs for the item's family
/// (equippable, weapon, container, book). Inheritance (base_item) is resolved offline; templates are flat.
/// </summary>
public sealed class ItemTemplate
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int ItemId { get; set; }
    public int Hue { get; set; }
    public int GoldValue { get; set; }
    public double Weight { get; set; }
    public string ScriptId { get; set; } = string.Empty;
    public bool IsMovable { get; set; }
    public ItemRarityType Rarity { get; set; }
    public List<string> Tags { get; set; } = [];
    public bool? Stackable { get; set; }
    public bool? Dyeable { get; set; }
    public string? Visibility { get; set; }
    public string? LootType { get; set; }
    public List<int>? FlippableItemIds { get; set; }
    public List<string>? LootTables { get; set; }
    public Dictionary<string, ItemParam>? Params { get; set; }
    public EquipSpec? Equip { get; set; }
    public WeaponSpec? Weapon { get; set; }
    public ContainerSpec? Container { get; set; }
    public BookSpec? Book { get; set; }
}
