using Moongate.Core.Persistence.Interfaces.Services;
using Moongate.Core.Server.Interfaces.Services.Base;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.UO.Interfaces.Services;

public interface IItemService : IMoongateAutostartService, IPersistenceLoadSave
{
    delegate void ItemEventHandler(UOItemEntity item);

    event ItemEventHandler? ItemCreated;
    UOItemEntity CreateItem();
}
