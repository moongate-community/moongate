using Moongate.UO.Data.Contexts;

namespace Moongate.UO.Data.Interfaces.Entities;

public interface IItemAction
{
    void OnUseItem(ItemUseContext context);
}
