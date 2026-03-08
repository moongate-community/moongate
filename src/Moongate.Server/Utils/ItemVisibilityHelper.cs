using Moongate.Server.Data.Session;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Utils;

internal static class ItemVisibilityHelper
{
    public static bool CanSessionSeeItem(GameSession session, UOItemEntity item)
        => session.AccountType >= item.Visibility;
}
