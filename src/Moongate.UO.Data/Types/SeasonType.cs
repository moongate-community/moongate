namespace Moongate.UO.Data.Types;

/// <summary>The season the client renders, as sent in the seasonal information (0xBC) packet.</summary>
public enum SeasonType : byte
{
    Spring = 0,
    Summer = 1,
    Fall = 2,
    Winter = 3,
    Desolation = 4
}
