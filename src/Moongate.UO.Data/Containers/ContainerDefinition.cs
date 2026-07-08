namespace Moongate.UO.Data.Containers;

/// <summary>
/// A container type: its gump item id and the width/height of its item grid (e.g. a backpack is 7x4).
/// </summary>
public sealed class ContainerDefinition
{
    public string Id { get; set; } = string.Empty;
    public int ItemId { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string Name { get; set; } = string.Empty;
}
