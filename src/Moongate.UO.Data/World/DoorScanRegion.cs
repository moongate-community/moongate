namespace Moongate.UO.Data.World;

/// <summary>
/// A rectangle of a map to look for doorways in, end exclusive.
/// <para>
/// Doorways are only searched for where buildings are. The alternative — every tile of every facet —
/// is tens of millions of lookups to find a few thousand doors, nearly all of it over open country.
/// </para>
/// </summary>
/// <param name="StartX">Left edge, included.</param>
/// <param name="StartY">Top edge, included.</param>
/// <param name="EndX">Right edge, excluded.</param>
/// <param name="EndY">Bottom edge, excluded.</param>
public readonly record struct DoorScanRegion(int StartX, int StartY, int EndX, int EndY);
