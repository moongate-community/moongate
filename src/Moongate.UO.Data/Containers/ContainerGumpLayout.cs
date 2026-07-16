namespace Moongate.UO.Data.Containers;

/// <summary>
/// The client gump layout for a container: the gump id, the content rectangle items can be dropped
/// into, the drop sound, and the item ids that render with this gump. A drop sound of -1 means none.
/// </summary>
public sealed class ContainerGumpLayout
{
    /// <summary>
    /// The gump a container falls back to when its own is not stated — the plain bag. It is the entry
    /// ModernUO's container table keeps as its default, and the reason the backpack is listed nowhere:
    /// it simply lands here.
    /// </summary>
    public const int DefaultGumpId = 60;

    public int GumpId { get; set; }
    public int RectX { get; set; }
    public int RectY { get; set; }
    public int RectWidth { get; set; }
    public int RectHeight { get; set; }
    public int DropSound { get; set; }
    public List<int> ItemIds { get; set; } = [];
}
