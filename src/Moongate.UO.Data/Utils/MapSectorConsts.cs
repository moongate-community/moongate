using System.Numerics;

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
    /// Bit shift for fast division/multiplication by <see cref="SectorSize" />.
    /// Derived automatically from <see cref="SectorSize" /> to keep a single source of truth.
    /// </summary>
    public static readonly int SectorShift = ComputeSectorShift();

    /// <summary>
    /// Maximum view range for players (used for nearby queries)
    /// </summary>
    public const int MaxViewRange = 24;

    private static int ComputeSectorShift()
    {
        if (SectorSize <= 0 || !BitOperations.IsPow2((uint)SectorSize))
        {
            throw new InvalidOperationException(
                $"Invalid {nameof(SectorSize)}={SectorSize}. Sector size must be a positive power of two."
            );
        }

        return BitOperations.TrailingZeroCount((uint)SectorSize);
    }
}
