using Moongate.Server.Ultima.Data.Maps;

namespace Moongate.Server.Ultima.Services.Internal;

/// <summary>
///     The terrain range and statics of the cell the line of sight walk is on, kept while the walk only changes Z.
/// </summary>
internal struct LineOfSightCell
{
    public int X;
    public int Y;
    public int LandLowest;
    public int LandHighest;
    public bool LandIgnored;
    public IReadOnlyList<MapStaticTile> Statics;
}
