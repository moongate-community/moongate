using DryIoc;
using Moongate.Core.Server.Instances;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Interfaces.Services;

namespace Moongate.UO.Extensions;

public static class ItemReferenceExtensions
{

    public static UOItemEntity ToEntity(this ItemReference itemReference)
    {
        var itemService = MoongateContext.Container.Resolve<IItemService>();

        var item = itemService.GetItem(itemReference.Id);

        if (item == null)
        {
            throw new InvalidOperationException($"Item with ID {itemReference.ItemId} not found.");
        }

        return item;
    }

}
