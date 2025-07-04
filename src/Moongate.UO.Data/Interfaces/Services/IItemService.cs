using Moongate.Core.Persistence.Interfaces.Services;
using Moongate.Core.Server.Interfaces.Services.Base;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.UO.Data.Interfaces.Services;

public interface IItemService : IMoongateAutostartService, IPersistenceLoadSave
{
    delegate void ItemEventHandler(UOItemEntity item);

    UOItemEntity? GetItem(Serial id);
    event ItemEventHandler? ItemCreated;
    event ItemEventHandler? ItemAdded;

    UOItemEntity CreateItem();
    UOItemEntity CreateItemAndAdd();

    void AddItem(UOItemEntity item);

    void UseItem(UOItemEntity item, UOMobileEntity? user);
}
