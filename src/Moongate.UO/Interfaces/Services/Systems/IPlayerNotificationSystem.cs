using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.UO.Interfaces.Services.Systems;

public interface IPlayerNotificationSystem
{
    void TrackMobile(UOMobileEntity mobile);

    void UntrackMobile(UOMobileEntity mobile);

}
