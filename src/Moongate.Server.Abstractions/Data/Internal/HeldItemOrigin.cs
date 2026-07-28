using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Ultima.Types;

namespace Moongate.Server.Abstractions.Data.Internal;

/// <summary>
/// Where an item was before it was lifted, so a failed drop or a disconnect can put it back exactly
/// there. Exactly one of the three origins is meaningful: a container slot, a worn layer, or a spot
/// on a map. ModernUO calls the same idea BounceInfo.
/// </summary>
public sealed record HeldItemOrigin(
    Serial ContainerId,
    Point2D ContainerPosition,
    Serial EquippedMobileId,
    LayerType? EquippedLayer,
    int MapId,
    Point3D WorldPosition
)
{
    /// <summary>Snapshots where <paramref name="item" /> currently is, before it is detached.</summary>
    public static HeldItemOrigin From(ItemEntity item)
        => new(
            item.ParentContainerId,
            item.ContainerPosition,
            item.EquippedMobileId,
            item.EquippedLayer,
            item.MapId,
            item.Position
        );
}
