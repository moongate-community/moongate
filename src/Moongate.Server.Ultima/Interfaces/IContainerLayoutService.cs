using Moongate.Core.Geometry;
using Moongate.Server.Ultima.Data.Containers;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Tells how the client shows a container, from <c>data/containers.toml</c>.
/// </summary>
public interface IContainerLayoutService
{
    /// <summary>
    ///     Gets the entry whose item ids include <paramref name="itemId" />, else the default entry.
    /// </summary>
    ContainerContent GetLayout(int itemId);

    /// <summary>
    ///     Picks a random point inside the bounds of the container <paramref name="itemId" />, for an item put in it.
    /// </summary>
    Point2D RandomGridPosition(int itemId);
}
