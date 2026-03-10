using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Modules.Internal;

/// <summary>
/// Resolves runtime mobile entities for Lua modules from spatial active sectors.
/// </summary>
internal static class MobileScriptResolver
{
    public static bool TryResolveMobile(
        ISpatialWorldService spatialWorldService,
        uint mobileSerial,
        out UOMobileEntity? mobile
    )
    {
        mobile = null;

        if (mobileSerial == 0)
        {
            return false;
        }

        var serial = (Serial)mobileSerial;

        if (!serial.IsMobile)
        {
            return false;
        }

        foreach (var sector in spatialWorldService.GetActiveSectors())
        {
            var resolved = sector.GetEntity<UOMobileEntity>(serial);

            if (resolved is null)
            {
                continue;
            }

            mobile = resolved;

            return true;
        }

        return false;
    }
}
