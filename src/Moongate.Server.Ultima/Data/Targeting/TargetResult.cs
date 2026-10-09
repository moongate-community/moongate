using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Types.Targeting;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Data.Targeting;

/// <summary>
///     What the player picked with the target cursor: an object (a live item or mobile), a location with its height
///     already resolved and the graphic of the static picked there, or nothing because the target was cancelled.
/// </summary>
public sealed record TargetResult(
    TargetResultType Kind,
    Serial Serial,
    MapType Map,
    Point3D Location,
    TargetCancelType CancelReason
)
{
    public static TargetResult ForObject(Serial serial)
    {
        return new(TargetResultType.Object, serial, default, default, default);
    }

    /// <summary>
    ///     Gets the graphic of the static that was picked; 0 for the land, an object or a cancel.
    /// </summary>
    public int Graphic { get; init; }

    public static TargetResult ForLocation(MapType map, Point3D location, int graphic = 0)
    {
        return new(TargetResultType.Location, Serial.Zero, map, location, default) { Graphic = graphic };
    }

    public static TargetResult Canceled(TargetCancelType reason)
    {
        return new(TargetResultType.Canceled, Serial.Zero, default, default, reason);
    }
}
