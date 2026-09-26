namespace Moongate.Ultima.Types;

/// <summary>
///     Client season, sent as the season byte of packet 0xBC.
/// </summary>
public enum SeasonType : byte
{
    Spring = 0,
    Summer = 1,
    Fall = 2,
    Winter = 3,
    Desolation = 4
}
