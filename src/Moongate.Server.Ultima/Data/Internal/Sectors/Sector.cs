using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Data.Internal.Sectors;

/// <summary>
///     A 16×16 block of one map, the mobiles standing in it and the items lying on its ground.
/// </summary>
public sealed class Sector
{
    public List<MobileEntity> Mobiles { get; } = [];

    public List<ItemEntity> Items { get; } = [];
}
