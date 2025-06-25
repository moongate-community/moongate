using Moongate.Core.Server.Instances;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Interfaces.Services.Systems;

namespace Moongate.Server.Services;

public class PlayerNotificationSystem : IPlayerNotificationSystem
{
    public void TrackMobile(UOMobileEntity mobile)
    {
        mobile.OtherMobileMoved += MobileOnOtherMobileMoved;
    }

    public void UntrackMobile(UOMobileEntity mobile)
    {
        mobile.OtherMobileMoved -= MobileOnOtherMobileMoved;
    }

    private void MobileOnOtherMobileMoved(UOMobileEntity mobile)
    {
        MoongateContext.EnqueueAction("PlayerNotificationSystem.MobileOnOtherMobileMoved", () => { });
    }

    private void NewPlayerJoined(UOMobileEntity mobile)
    {
        MoongateContext.EnqueueAction("PlayerNotificationSystem.NewPlayerJoined", () => { });
    }
}
