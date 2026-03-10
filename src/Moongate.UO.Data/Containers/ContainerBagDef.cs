using Moongate.UO.Data.Geometry;

namespace Moongate.UO.Data.Containers;

/// <summary>
/// Describes a container profile merged from JSON definitions and containers.cfg metadata.
/// </summary>
public sealed class ContainerBagDef
{
    public string Id { get; set; } = string.Empty;

    public int ItemId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Width { get; set; }

    public int Height { get; set; }

    public int? GumpId { get; set; }

    public int? DropSound { get; set; }

    public Rectangle2D Bounds { get; set; } = new(0, 0, 0, 0);
}
