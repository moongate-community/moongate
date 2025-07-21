using Moongate.UO.Data.Contexts;

namespace Moongate.UO.Data.Interfaces.Actions;

public interface IItemAction
{
    void OnUseItem(ItemUseContext context);
}
