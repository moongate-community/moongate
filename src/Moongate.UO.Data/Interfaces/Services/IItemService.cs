using Moongate.Core.Persistence.Interfaces.Services;
using Moongate.Core.Server.Interfaces.Services.Base;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Interfaces.Actions;
using Moongate.UO.Data.Interfaces.Entities;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.UO.Data.Interfaces.Services;

public interface IItemService : IMoongateAutostartService, IPersistenceLoadSave
{
    delegate void ItemEventHandler(UOItemEntity item);

    delegate void ItemMovedEventHandler(
        UOItemEntity item, Point3D oldLocation, Point3D newLocation, bool isOnGround
    );

    UOItemEntity? GetItem(Serial id);
    event ItemEventHandler? ItemCreated;
    event ItemEventHandler? ItemAdded;

    event ItemMovedEventHandler? ItemMoved;

    UOItemEntity CreateItem();
    UOItemEntity CreateItemAndAdd();

    void AddItem(UOItemEntity item);

    void UseItem(UOItemEntity item, UOMobileEntity? user);

    void AddItemActionScript(string itemId, IItemAction itemAction);

    void RemoveItemFromWorld(UOItemEntity item);
}
