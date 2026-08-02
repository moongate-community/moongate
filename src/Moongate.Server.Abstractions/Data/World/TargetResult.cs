using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Abstractions.Types.World;

namespace Moongate.Server.Abstractions.Data.World;

/// <summary>
/// What a target cursor came back with, already interpreted — the caller is handed a decision
/// rather than a packet to work out for itself.
/// </summary>
/// <param name="Type">Object, location, or cancelled.</param>
/// <param name="Serial">The clicked entity when <paramref name="Type" /> is Object, otherwise zero.</param>
/// <param name="Location">Where it was, for both Object and Location.</param>
/// <param name="Graphic">The clicked graphic, when the client reported one.</param>
public readonly record struct TargetResult(
    TargetResultType Type,
    Serial Serial,
    Point3D Location,
    int Graphic
)
{
    /// <summary>The player did not pick anything.</summary>
    public bool IsCancelled => Type == TargetResultType.Cancelled;

    /// <summary>A cancelled answer, carrying nothing else worth reading.</summary>
    public static TargetResult Cancelled => new(TargetResultType.Cancelled, Serial.Zero, default, 0);
}
