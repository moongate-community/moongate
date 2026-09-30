using Moongate.Server.Ultima.Data.Regions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Packets.World;

namespace Moongate.Server.Ultima.Data.Internal.Weather;

/// <summary>
///     A player the weather follows: its region, and once its login completed, its session and the last weather sent.
/// </summary>
public sealed class WeatherViewer
{
    public required MobileEntity Player { get; init; }

    public RegionContent? Region { get; set; }

    public long? SessionId { get; set; }

    public WeatherPacket? LastSent { get; set; }
}
