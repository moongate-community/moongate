using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.UO.Data.Interfaces.Entities;

public interface IItemAction
{
    void OnUseItem(UOItemEntity item, UOMobileEntity user);
}
