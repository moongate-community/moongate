using Moongate.Core.Persistence.Interfaces.Services;
using Moongate.Core.Server.Interfaces.Services.Base;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Interfaces.Actions;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.UO.Data.Interfaces.Services;

public interface IItemService : IMoongateAutostartService, IPersistenceLoadSave
{
    delegate void ItemEventHandler(UOItemEntity item);

    delegate void ItemMovedEventHandler(UOItemEntity item, Point3D oldLocation, Point3D newLocation, bool isOnGround);

    event ItemEventHandler? ItemCreated;
    event ItemEventHandler? ItemAdded;

    event ItemMovedEventHandler? ItemMoved;

    void AddItem(UOItemEntity item);

    void AddItemActionScript(string itemId, IItemAction itemAction);

    UOItemEntity CreateItem();
    UOItemEntity CreateItemAndAdd();

    UOItemEntity? GetItem(Serial id);

    void RemoveItemFromWorld(UOItemEntity item);

    void UseItem(UOItemEntity item, UOMobileEntity? user);
}
