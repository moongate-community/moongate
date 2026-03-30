using Moongate.Server.Data.Session;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Utils;

internal static class MobileVisibilityHelper
{
    public static bool CanSessionSeeMobile(GameSession session, UOMobileEntity mobile)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(mobile);

        if (mobile.IsPlayer && !mobile.IsAlive)
        {
            return session.AccountType >= AccountType.GameMaster;
        }

        return true;
    }
}
