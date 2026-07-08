namespace Moongate.UO.Data.Containers;

/// <summary>
/// The client gump layout for a container: the gump id, the content rectangle items can be dropped
/// into, the drop sound, and the item ids that render with this gump. A drop sound of -1 means none.
/// </summary>
public sealed class ContainerGumpLayout
{
    public int GumpId { get; set; }
    public int RectX { get; set; }
    public int RectY { get; set; }
    public int RectWidth { get; set; }
    public int RectHeight { get; set; }
    public int DropSound { get; set; }
    public List<int> ItemIds { get; set; } = [];
}
