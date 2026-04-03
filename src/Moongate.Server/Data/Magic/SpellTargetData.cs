using Moongate.Server.Types.Magic;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Data.Magic;

/// <summary>
/// Stores the raw target payload captured for a spell cast.
/// </summary>
public sealed record SpellTargetData
{
    private SpellTargetData(
        SpellTargetKind kind,
        Serial targetId,
        int mapId,
        Point3D location,
        ushort graphic
    )
    {
        Kind = kind;
        TargetId = targetId;
        MapId = mapId;
        Location = location;
        Graphic = graphic;
    }

    public SpellTargetKind Kind { get; init; }

    public Serial TargetId { get; init; }

    public int MapId { get; init; }

    public Point3D Location { get; init; }

    public ushort Graphic { get; init; }

    public static SpellTargetData None()
        => new(SpellTargetKind.None, Serial.Zero, 0, Point3D.Zero, 0);

    public static SpellTargetData Mobile(Serial targetId)
        => new(SpellTargetKind.Mobile, targetId, 0, Point3D.Zero, 0);

    public static SpellTargetData Item(Serial targetId, Point3D location = default, ushort graphic = 0)
        => new(SpellTargetKind.Item, targetId, 0, location, graphic);

    public static SpellTargetData FromLocation(int mapId, Point3D location, ushort graphic = 0)
        => new(SpellTargetKind.Location, Serial.Zero, mapId, location, graphic);
}
