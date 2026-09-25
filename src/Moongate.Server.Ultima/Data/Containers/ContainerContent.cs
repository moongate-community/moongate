using Moongate.Core.Geometry;

namespace Moongate.Server.Ultima.Data.Containers;

/// <summary>
///     One entry of <c>containers.toml</c>: how the client shows the containers whose graphic is in
///     <see cref="Items" />.
/// </summary>
public class ContainerContent
{
    /// <summary>
    ///     The id of the gump the client opens.
    /// </summary>
    public int Gump { get; set; }

    /// <summary>
    ///     The area of the gump where items can be placed.
    /// </summary>
    public Rectangle2D Bounds { get; set; }

    /// <summary>
    ///     The sound played when an item is dropped in; <see langword="null" /> for none.
    /// </summary>
    public int? DropSound { get; set; }

    /// <summary>
    ///     The item ids (graphics) of the containers that use this entry.
    /// </summary>
    public List<int> Items { get; set; } = [];

    /// <summary>
    ///     Whether this entry is used for containers whose graphic is not listed anywhere.
    /// </summary>
    public bool Default { get; set; }
}
