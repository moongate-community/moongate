using MessagePack;

namespace Moongate.Persistence.Data.Persistence;

/// <summary>
/// Serialized item state used inside world snapshots and journal payloads.
/// </summary>
[MessagePackObject(true)]
public sealed class ItemSnapshot
{
    public uint Id { get; set; }

    public int X { get; set; }

    public int Y { get; set; }

    public int Z { get; set; }

    public int MapId { get; set; }

    public string? Name { get; set; }

    public int Weight { get; set; }

    public int Amount { get; set; }

    public bool IsStackable { get; set; }

    public byte Rarity { get; set; }

    public int ItemId { get; set; }

    public int Hue { get; set; }

    public int? GumpId { get; set; }

    public byte Direction { get; set; }

    public string ScriptId { get; set; }

    public uint ParentContainerId { get; set; }

    public int ContainerX { get; set; }

    public int ContainerY { get; set; }

    public uint EquippedMobileId { get; set; }

    public byte? EquippedLayer { get; set; }

    public uint[] ContainedItemIds { get; set; } = [];

    public ItemCustomPropertySnapshot[] CustomProperties { get; set; } = [];
}
