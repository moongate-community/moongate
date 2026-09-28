using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Data.Internal.Sectors;

/// <summary>
///     A 16×16 block of one map and the mobiles standing in it.
/// </summary>
public sealed class Sector
{
    public List<MobileEntity> Mobiles { get; } = [];
}
