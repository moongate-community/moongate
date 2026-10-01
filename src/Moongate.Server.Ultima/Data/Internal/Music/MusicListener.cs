using Moongate.Server.Ultima.Data.Regions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Data.Internal.Music;

/// <summary>
///     A player the music follows: its region, and once its login completed, its session and the last track sent.
/// </summary>
public sealed class MusicListener
{
    public required MobileEntity Player { get; init; }

    public RegionContent? Region { get; set; }

    public long? SessionId { get; set; }

    public MusicType? LastSent { get; set; }
}
