namespace Moongate.Ultima.Data;

/// <summary>
/// One worn item for paperdoll composition. The gump is derived from the item's
/// tiledata Animation (+50000 male / +60000 female).
/// </summary>
public sealed record PaperdollEquipEntry
{
    public uint ItemId { get; init; }

    public ushort Hue { get; init; }

    /// <summary>null = follow the tiledata PartialHue flag; true/false forces gray-only / full hue.</summary>
    public bool? PartialHueOverride { get; init; }
}
