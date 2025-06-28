using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Extensions;

public static class UOMobileExtensions
{
    public static void AddToBackpack(this UOMobileEntity mobile, UOItemEntity item)
    {
        var haveBackpack = mobile.Equipment.TryGetValue(ItemLayerType.Backpack, out var backpackRef);

        if (haveBackpack)
        {
            backpackRef.ToEntity().ContainedItems.Add(new Point2D(0, 0), item.ToItemReference());
        }
    }
}
