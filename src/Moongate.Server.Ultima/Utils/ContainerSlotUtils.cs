using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Utils;

/// <summary>
///     Picks the grid slot of an item entering a container. The Enhanced Client lays a container out as a grid and
///     places each item by its slot; the classic client ignores it and uses the gump position.
/// </summary>
public static class ContainerSlotUtils
{
    /// <summary>
    ///     How many slots the grid of the Enhanced Client has: indices 0 to 124, as in ServUO.
    /// </summary>
    public const int SlotCount = 125;

    /// <summary>
    ///     Returns <paramref name="wanted" /> when no item of <paramref name="contents" /> has it, otherwise the next
    ///     free slot after it, wrapping to the first. A slot beyond the grid starts from the first. When every slot
    ///     is taken the wanted one is shared.
    /// </summary>
    public static byte FirstFree(IEnumerable<ItemEntity> contents, int wanted = 0)
    {
        var start = wanted is >= 0 and < SlotCount ? wanted : 0;
        var taken = new bool[SlotCount];

        foreach (var item in contents)
        {
            if (item.GridIndex is { } slot and >= 0 and < SlotCount)
            {
                taken[slot] = true;
            }
        }

        for (var offset = 0; offset < SlotCount; offset++)
        {
            var slot = (start + offset) % SlotCount;

            if (!taken[slot])
            {
                return (byte)slot;
            }
        }

        return (byte)start;
    }
}
