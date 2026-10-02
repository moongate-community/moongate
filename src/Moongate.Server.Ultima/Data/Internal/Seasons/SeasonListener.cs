using Moongate.Server.Ultima.Data.Regions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Data.Internal.Seasons;

/// <summary>
///     A player the seasons follow: its region, and once its login completed, its session; the last season sent.
/// </summary>
public sealed class SeasonListener
{
    public required MobileEntity Player { get; init; }

    public RegionContent? Region { get; set; }

    public long? SessionId { get; set; }

    public SeasonType? LastSent { get; set; }
}
