namespace Moongate.UO.Data.Utils;

/// <summary>
/// Represents MapSectorConsts.
/// </summary>
public class MapSectorConsts
{
    /// <summary>
    /// Size of each sector in tiles.
    /// </summary>
    public const int SectorSize = 16;

    /// <summary>
    /// Bit shift for fast division/multiplication by SectorSize
    /// 16 = 2^4, so shift by 4 bits.
    /// </summary>
    public const int SectorShift = 4;

    /// <summary>
    /// Maximum view range for players (used for nearby queries)
    /// </summary>
    public const int MaxViewRange = 24;
}
