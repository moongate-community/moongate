using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Types.Spells;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Data.Spells;

/// <summary>
///     What a spell was aimed at, as its script gets it: a mobile or an item by serial, or a place.
/// </summary>
public sealed record SpellTargetInfo(SpellTargetType Kind, Serial Serial, MapType Map, Point3D Location)
{
    /// <summary>
    ///     The target of a spell that asks for none.
    /// </summary>
    public static SpellTargetInfo None { get; } = new(SpellTargetType.None, Serial.Zero, default, default);
}
