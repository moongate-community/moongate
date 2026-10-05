using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Tests.TestSupport.Ultima.Containers;

/// <summary>
///     Containers with room for everything, or for nothing when a test says so; it records what was asked.
/// </summary>
public sealed class StubContainerCapacityService : IContainerCapacityService
{
    public bool HasRoomResult { get; set; } = true;

    public List<(ItemEntity Container, ItemEntity Item)> Asked { get; } = [];

    public int? MaximumOf(ItemEntity container)
    {
        return null;
    }

    public int CountIn(ItemEntity container)
    {
        return 0;
    }

    public bool HasRoom(ItemEntity container, ItemEntity item)
    {
        Asked.Add((container, item));

        return HasRoomResult;
    }

    public bool HasRoomFor(ItemEntity container, int items)
    {
        return HasRoomResult;
    }
}
